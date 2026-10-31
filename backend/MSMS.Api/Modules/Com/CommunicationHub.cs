using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Data;

namespace MSMS.Api.Modules.Com;

/// <summary>
/// SignalR Hub cho thông báo (NotificationCreated) và tin nhắn (MessageReceived, MessagesRead, UserTyping)
/// Xác thực bằng access token qua query string; tài khoản bị khóa hoặc phiên bị thu hồi thì ngắt kết nối.
/// BR-25, COM-4.8.6, COM-4.8.7, COM-4.8.8
/// </summary>
[Authorize]
public class CommunicationHub(MsmsDbContext db, CurrentUser currentUser) : Hub
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

    /// <summary>
    /// Nhóm cho từng người dùng (PersonId) để đẩy thông báo cá nhân.
    /// Format: "user_{personId}"
    /// </summary>
    private string UserGroup => $"user_{PersonId}";

    private string AccountType => Context.User?.FindFirst(CurrentUser.TypeClaim)?.Value ?? currentUser.AccountType;

    /// <summary>
    /// Nhóm cho từng hội thoại, để broadcast sự kiện sang bên kia trong cùng conversation.
    /// Format: "conversation_{conversationId}"
    /// </summary>
    private static string ConversationGroup(int conversationId) => $"conversation_{conversationId}";

    /// <summary>
    /// OnConnected: Thêm người dùng vào nhóm cá nhân để nhận thông báo.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        // Người dùng hiện tại tự động vào nhóm cá nhân
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Client gọi khi bắt đầu soạn hoặc ngừng soạn tin nhắn.
    /// Broadcast sự kiện `UserTyping` tới hội thoại để bên kia nhận được.
    /// Không lưu vào CSDL, chỉ realtime broadcast.
    /// 
    /// COM-4.8.8: "Đang nhập không lưu CSDL, tự tắt sau vài giây"
    /// </summary>
    /// <param name="conversationId">ID hội thoại</param>
    /// <param name="isTyping">true = đang soạn, false = ngừng soạn</param>
    [Authorize(Policy = Policies.ConversationParticipant)]
    public async Task SendTyping(int conversationId, bool isTyping)
    {
        // Kiểm tra người gọi có trong hội thoại này không
        if (!await IsUserInConversationAsync(conversationId))
        {
            throw new HubException("Bạn không thuộc hội thoại này.");
        }

        // Broadcast sự kiện tới nhóm hội thoại (sẽ được bên kia nhận)
        // Không gửi lại cho người gửi (broadcast tới nhóm trừ chính mình)
        await Clients
            .Group(ConversationGroup(conversationId))
            .SendAsync("UserTyping", conversationId, PersonId, isTyping);
    }

    /// <summary>
    /// Client gọi khi vào hội thoại (JoinConversation).
    /// Thêm connection vào nhóm hội thoại để nhận các sự kiện liên quan.
    /// Có thể dùng để báo online/offline cho bên kia.
    /// 
    /// COM-4.8.8: "mỗi lệnh kiểm người gọi thuộc hội thoại rồi chuyển cho bên kia"
    /// </summary>
    /// <param name="conversationId">ID hội thoại</param>
    [Authorize(Policy = Policies.ConversationParticipant)]
    public async Task JoinConversation(int conversationId)
    {
        // Kiểm tra người gọi có trong hội thoại này không
        if (!await IsUserInConversationAsync(conversationId))
        {
            throw new HubException("Bạn không thuộc hội thoại này.");
        }

        // Thêm connection vào nhóm hội thoại
        await Groups.AddToGroupAsync(Context.ConnectionId, ConversationGroup(conversationId));

        // Thông báo cho bên kia biết person này đã online trong hội thoại này
        // (hoặc thực hiện đánh dấu tin nhắn là đã đọc tự động tại đây)
    }

    /// <summary>
    /// Kiểm tra người dùng hiện tại có thuộc hội thoại (conversationId) không.
    /// </summary>
    private async Task<bool> IsUserInConversationAsync(int conversationId)
    {
        var conversation = await db.Conversation.FindAsync(conversationId);
        if (conversation == null)
            return false;

        // Người dùng là ParentId hoặc StaffId của hội thoại
        var pid = PersonId;
        return ConversationAccess.IsParticipant(AccountType, pid, conversation.ParentId, conversation.StaffId);
    }
}
