using System.Data.Common;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MSMS.Api.Common.Notifications;
using MSMS.Api.Data.Entities;

namespace MSMS.Api.Modules.Com;

/// <summary>
/// COM-4.8.7: đẩy <c>NotificationCreated</c> chỉ sau khi dữ liệu đã được lưu thật (BR-26), để giao dịch bị hoàn tác
/// không sinh thông báo ảo. Không có transaction ngoài: đẩy ngay sau SaveChanges. Có transaction ngoài:
/// giữ lại đến khi commit, rollback thì bỏ.
/// Lỗi khi đẩy chỉ ghi log, không làm hỏng giao dịch nghiệp vụ.
/// </summary>
public sealed class NotificationPushInterceptor(IServiceProvider services, ILogger<NotificationPushInterceptor> logger)
    : SaveChangesInterceptor, IDbTransactionInterceptor
{
    /// <summary>
    /// Tin nhắn mới do ConversationService tự đẩy (kèm MessageReceived theo hội thoại), không đẩy lại ở đây để tránh trùng.
    /// </summary>
    private static readonly string[] PushedElsewhere = [NotificationTypes.MessageReceived];

    // Lấy NotificationSender lười để tránh vòng phụ thuộc DbContext -> interceptor -> NotificationSender -> DbContext.
    private NotificationSender Sender => services.GetRequiredService<NotificationSender>();

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context is { } ctx && ctx.Database.CurrentTransaction is null) _ = PushPendingAsync();
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } ctx && ctx.Database.CurrentTransaction is null) _ = PushPendingAsync();
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => Discard(eventData.Context);

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Discard(eventData.Context);
        return Task.CompletedTask;
    }

    // Transaction ngoài: commit mới đẩy, rollback thì bỏ.
    public void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) => _ = PushPendingAsync();

    public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        _ = PushPendingAsync();
        return Task.CompletedTask;
    }

    public void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) => Sender.DiscardPending();

    public Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Sender.DiscardPending();
        return Task.CompletedTask;
    }

    private void Discard(DbContext? ctx)
    {
        // Lưu lỗi ngoài transaction: các dòng chờ đẩy không bao giờ được lưu. Trong transaction thì để quyết định ở commit/rollback.
        if (ctx?.Database.CurrentTransaction is null) Sender.DiscardPending();
    }

    private async Task PushPendingAsync()
    {
        try
        {
            var sender = Sender;
            var saved = sender.TakePending().Where(n => n.NotificationId > 0).ToList();
            if (saved.Count == 0) return;
            var hub = services.GetRequiredService<IHubContext<CommunicationHub>>();
            foreach (var n in saved.Where(n => !PushedElsewhere.Contains(n.NotificationType)))
                await hub.Clients.Group($"user_{n.RecipientPersonId}").SendAsync("NotificationCreated", ToPayload(n));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không đẩy được NotificationCreated qua SignalR");
        }
    }

    private static object ToPayload(Notification n) => new
    {
        notificationId = n.NotificationId,
        type = n.NotificationType,
        title = n.Title,
        preview = n.Body,
        refEntityType = n.RefEntityType,
        refEntityId = n.RefEntityId,
        createdAt = n.CreatedAt,
    };
}
