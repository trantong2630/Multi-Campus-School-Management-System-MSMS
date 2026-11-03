namespace MSMS.Api.Modules.Iam.Dtos;

/// <summary>
/// IAM-04: hồ sơ cá nhân của người dùng đang đăng nhập (4.1.27, 4.1.30).
/// AvatarVersion đổi mỗi lần hồ sơ cập nhật, giao diện gắn vào đường dẫn ảnh để không dùng ảnh cũ trong cache.
/// </summary>
public record OwnProfileDto(
    int PersonId,
    string PersonCode,
    string PersonType,
    string FullName,
    string? DateOfBirth,
    string? Gender,
    string? PhoneNumber,
    string? Email,
    string? Address,
    bool HasAvatar,
    long? AvatarVersion,
    // Tài khoản (4.1.30)
    int AccountId,
    string Username,
    string AccountType,
    string Status,
    DateTime? LastLoginAt,
    // Trách nhiệm đang hiệu lực tại ngày hiện tại (4.1.27, BR-09)
    IReadOnlyList<ResponsibilityDto> ActiveResponsibilities,
    // Phụ huynh không tự sửa số điện thoại vì đó là khóa nhận diện (BR-10)
    bool CanEditPhone
);

/// <summary>Một trách nhiệm đang hiệu lực: loại (VD: Chủ nhiệm), đối tượng (VD: 8A1) và ngày bắt đầu.</summary>
public record ResponsibilityDto(string Kind, string TargetName, string StartDate);

/// <summary>
/// Các trường cá nhân được phép tự sửa (4.1.28). Email dùng để nhận OTP, không có luồng xác nhận riêng (BR-04).
/// Bỏ trống một trường là xóa giá trị của trường đó.
/// </summary>
public record UpdateProfileRequest(
    string? PhoneNumber,
    string? Email,
    string? Address
);
