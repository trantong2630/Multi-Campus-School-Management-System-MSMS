using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Utils;
using MSMS.Api.Common.Email;

namespace MSMS.Api.Modules.Iam.Services;

public interface IOtpEmailSender
{
    Task<bool> SendAsync(string recipient, string code, DateTime expiresAt, CancellationToken ct = default);
}

/// <summary>4.1.22: chỉ gửi OTP trực tiếp; không hàng đợi, không tự gửi lại, không log mã.</summary>
public sealed class GmailOtpSender(IOptions<SmtpOptions> options, ILogger<GmailOtpSender> logger) : IOtpEmailSender
{
    public async Task<bool> SendAsync(string recipient, string code, DateTime expiresAt, CancellationToken ct = default)
    {
        var config = options.Value;
        if (string.IsNullOrWhiteSpace(config.Username) || string.IsNullOrWhiteSpace(config.AppPassword))
        {
            logger.LogWarning("Chưa cấu hình Smtp:Username hoặc Smtp:AppPassword. Không gửi OTP.");
            return false;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            var mail = new MimeMessage();
            mail.MessageId = MimeUtils.GenerateMessageId();
            mail.From.Add(new MailboxAddress(config.SenderName, config.Username));
            mail.To.Add(MailboxAddress.Parse(recipient));
            // Tiêu đề riêng cho mỗi lần gửi để Gmail không gom thư mới vào hội thoại chứa OTP cũ.
            mail.Subject = $"Mã đặt lại mật khẩu MSMS ({expiresAt.AddMinutes(-PasswordResetState.LifetimeMinutes):dd/MM/yyyy HH:mm:ss})";
            mail.Body = new TextPart("plain")
            {
                Text = $"Mã OTP: {code}\nHết hạn lúc {expiresAt:HH:mm:ss dd/MM/yyyy} (giờ Việt Nam, {PasswordResetState.LifetimeMinutes} phút).",
            };

            using var client = new SmtpClient();
            await client.ConnectAsync(config.Host, config.Port, SecureSocketOptions.StartTls, timeout.Token);
            await client.AuthenticateAsync(config.Username, config.AppPassword.Replace(" ", ""), timeout.Token);
            await client.SendAsync(mail, timeout.Token);
            // Đã được SMTP chấp nhận; lỗi đóng kết nối không làm mã vừa gửi mất hiệu lực.
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            // Exception SMTP có thể chứa địa chỉ/nội dung thư: chỉ ghi loại lỗi, không ghi exception hoặc payload.
            logger.LogWarning("Không gửi được OTP qua Gmail SMTP ({FailureType}). Người dùng có thể gửi lại mã.", ex.GetType().Name);
            return false;
        }
    }
}
