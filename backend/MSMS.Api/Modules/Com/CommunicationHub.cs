using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Data;

namespace MSMS.Api.Modules.Com;

/// <summary>
/// SignalR Hub cho thông báo (NotificationCreated) và tin nhắn (MessageReceived, MessagesRead, UserTyping, PresenceChanged).
/// Xác thực bằng access token qua query string; tài khoản bị khóa hoặc phiên bị thu hồi thì ngắt kết nối (<see cref="HubGuardFilter"/>).
/// Hub chỉ nhận SendTyping và JoinConversation; gửi tin vẫn qua REST (BR-25).
/// BR-25, COM-4.8.6, COM-4.8.7, COM-4.8.8
/// </summary>
[Authorize]
public class CommunicationHub(MsmsDbContext db, CurrentUser currentUser, PresenceTracker presence) : Hub
{
    /// <summary>
    /// Lấy PersonId an toàn từ Context.User (SignalR connection ClaimsPrincipal), fallback sang currentUser.
    /// </summary>
    private int PersonId
    {
        get
        {
            var claimVal = Context.User?.FindFirst(CurrentUser.PersonIdClaim)?.Value;
            if (int.TryParse(claimVal, out var pid))
                return pid;

            return currentUser.PersonId;
        }
    }

    /// <summary>School Admin không tham gia hội thoại: không hiện online, không gửi đang nhập (COM-4.8.8).</summary>
    private bool IsSchoolAdmin => Context.User?.FindFirst(CurrentUser.TypeClaim)?.Value == AccountTypes.SchoolAdmin;

    /// <summary>Nhóm cá nhân để đẩy thông báo: "user_{personId}".</summary>
    private string UserGroup => $"user_{PersonId}";

    private string AccountType => Context.User?.FindFirst(CurrentUser.TypeClaim)?.Value ?? currentUser.AccountType;

    /// <summary>Nhóm hội thoại để chuyển sự kiện sang bên kia: "conversation_{conversationId}".</summary>
    private static string ConversationGroup(int conversationId) => $"conversation_{conversationId}";

    /// <summary>
    /// Vào nhóm cá nhân để nhận thông báo; kết nối đầu tiên của tài khoản thì báo online cho bên kia.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup);
        if (!IsSchoolAdmin && presence.Connected(PersonId, Context.ConnectionId))
            await NotifyPresenceAsync(true);
        await base.OnConnectedAsync();
    }

    /// <summary>Mất kết nối cuối cùng của tài khoản thì báo offline (nhiều tab/thiết bị vẫn tính online).</summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (!IsSchoolAdmin && presence.Disconnected(PersonId, Context.ConnectionId))
            await NotifyPresenceAsync(false);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Chỉ báo cho người ở bên kia của các hội thoại chung với mình (COM-4.8.8).</summary>
    private async Task NotifyPresenceAsync(bool isOnline)
    {
        var me = PersonId;
        var others = await db.Conversation.AsNoTracking()
            .Where(c => c.ParentId == me || c.StaffId == me)
            .Select(c => c.ParentId == me ? c.StaffId : c.ParentId)
            .Distinct().ToListAsync();
        if (others.Count == 0) return;
        await Clients.Groups(others.Select(o => $"user_{o}").ToList()).SendAsync("PresenceChanged", me, isOnline);
    }

    /// <summary>
    /// Client gọi khi bắt đầu hoặc ngừng soạn tin. Chuyển `UserTyping` cho bên kia, không lưu CSDL;
    /// client tự tắt chỉ báo sau vài giây (COM-4.8.8).
    /// </summary>
    /// <param name="conversationId">ID hội thoại</param>
    /// <param name="isTyping">true = đang soạn, false = ngừng soạn</param>
    [Authorize(Policy = Policies.ConversationParticipant)]
    public async Task SendTyping(int conversationId, bool isTyping)
    {
        if (IsSchoolAdmin || !await IsUserInConversationAsync(conversationId))
            throw new HubException("Bạn không thuộc hội thoại này.");

        await Clients
            .OthersInGroup(ConversationGroup(conversationId))
            .SendAsync("UserTyping", conversationId, PersonId, isTyping);
    }

    /// <summary>
    /// Client gọi khi mở hội thoại: kiểm người gọi thuộc hội thoại rồi thêm connection vào nhóm hội thoại.
    /// Trả trạng thái online hiện tại của bên kia để hiện chấm online ngay khi mở.
    /// </summary>
    /// <param name="conversationId">ID hội thoại</param>
    [Authorize(Policy = Policies.ConversationParticipant)]
    public async Task<bool> JoinConversation(int conversationId)
    {
        if (IsSchoolAdmin)
            throw new HubException("Bạn không thuộc hội thoại này.");

        var c = await db.Conversation.AsNoTracking().Where(x => x.ConversationId == conversationId)
            .Select(x => new { x.ParentId, x.StaffId }).FirstOrDefaultAsync();
        var me = PersonId;
        if (c is null || !ConversationAccess.IsParticipant(AccountType, me, c.ParentId, c.StaffId))
            throw new HubException("Bạn không thuộc hội thoại này.");

        await Groups.AddToGroupAsync(Context.ConnectionId, ConversationGroup(conversationId));
        return presence.IsOnline(c.ParentId == me ? c.StaffId : c.ParentId);
    }

    private async Task<bool> IsUserInConversationAsync(int conversationId)
    {
        var conversation = await db.Conversation.AsNoTracking().FirstOrDefaultAsync(c => c.ConversationId == conversationId);
        if (conversation == null)
            return false;

        // Người dùng là ParentId hoặc StaffId của hội thoại
        var pid = PersonId;
        return ConversationAccess.IsParticipant(AccountType, pid, conversation.ParentId, conversation.StaffId);
    }
}
