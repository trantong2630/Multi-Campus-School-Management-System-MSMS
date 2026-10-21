namespace MSMS.Api.Modules.Iam.Dtos;

public record AccountDto(
    int AccountId, int PersonId, string PersonCode, string FullName, string PersonType, string Username,
    string AccountType, string Status, DateTime? LastLoginAt, string? CampusName);

public record CreateAccountRequest(int PersonId, string AccountType);
public record BulkCreateAccountsRequest(IReadOnlyList<int> PersonIds, string? AccountType);
public record ChangeAccountTypeRequest(string AccountType);

/// <summary>Mật khẩu tạm chỉ hiện một lần trong kết quả tạo; không lưu dạng thô ở đâu cả (BR-04).</summary>
public record TempPasswordItem(int PersonId, string PersonCode, string FullName, string Username, string AccountType, string TempPassword);
public record SkippedAccount(int PersonId, string FullName, string Reason);
public record BulkCreateResult(IReadOnlyList<TempPasswordItem> Created, IReadOnlyList<SkippedAccount> Skipped);
public record TempPasswordSheetRequest(IReadOnlyList<TempPasswordItem> Items);
public record ResetPasswordResult(string Username, string TempPassword);
