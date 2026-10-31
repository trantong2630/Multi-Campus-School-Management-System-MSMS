using MSMS.Api.Common.Errors;

namespace MSMS.Tests.Rules;

/// <summary>Email là nơi nhận mã OTP lấy lại mật khẩu (BR-04), nên chỉ nhận địa chỉ có thể gửi tới được: tên@miền.đuôi.</summary>
public class GuardEmailTests
{
    [Theory]
    [InlineData("an@school.vn")]
    [InlineData("nguyen.van.a@gmail.com")]
    [InlineData("a+tag@mail.example.org")]
    [InlineData("ten_dai@sub.domain.edu.vn")]
    public void WellFormedAddresses_AreAccepted(string email) => Assert.True(Guard.IsEmail(email));

    [Theory]
    [InlineData("nguoi dung@")]               // có dấu cách, thiếu tên miền
    [InlineData("nguyenvana@")]               // chỉ có @
    [InlineData("an@@school.vn")]             // hai dấu @
    [InlineData("an dang@gmail.com")]         // dấu cách trong tên
    [InlineData("an@gmail")]                  // tên miền không có đuôi
    [InlineData("@school.vn")]                // thiếu tên
    [InlineData("an.school.vn")]              // không có @
    [InlineData("Nguyễn Văn An <an@school.vn>")]   // có tên hiển thị, không phải một địa chỉ thuần
    [InlineData("an@school.vn, binh@school.vn")]   // hai địa chỉ
    [InlineData("")]
    public void MalformedAddresses_AreRejected(string email) => Assert.False(Guard.IsEmail(email));

    [Fact]
    public void AnAddressLongerThan254Characters_IsRejected() =>
        Assert.False(Guard.IsEmail(new string('a', 250) + "@x.vn"));
}
