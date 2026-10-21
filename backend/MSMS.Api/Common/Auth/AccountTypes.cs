namespace MSMS.Api.Common.Auth;

public static class AccountTypes
{
    public const string SchoolAdmin = "SchoolAdmin";
    public const string Principal = "Principal";
    public const string VicePrincipal = "VicePrincipal";
    public const string Teacher = "Teacher";
    public const string Student = "Student";
    public const string Parent = "Parent";
}

public static class AccountStatuses
{
    public const string PendingPasswordChange = "PendingPasswordChange";
    public const string Active = "Active";
    public const string Locked = "Locked";
}
