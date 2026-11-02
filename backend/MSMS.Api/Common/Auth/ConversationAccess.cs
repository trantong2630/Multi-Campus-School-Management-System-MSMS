namespace MSMS.Api.Common.Auth;

/// <summary>BR-25: hộp thư riêng tư thuộc đúng vai trò và đúng hai người tham gia, kể cả khi đọc lịch sử.</summary>
public static class ConversationAccess
{
    public static bool IsParticipant(string accountType, int personId, int parentId, int staffId) =>
        (accountType == AccountTypes.Parent && personId == parentId)
        || (accountType == AccountTypes.Teacher && personId == staffId);
}
