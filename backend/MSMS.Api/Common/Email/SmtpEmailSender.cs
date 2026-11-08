using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Utils;

namespace MSMS.Api.Common.Email;

/// <summary>Cấu hình hộp thư chung của trường (mục "Smtp" trong appsettings). Dùng cho OTP (BR-04) và email phân công dạy thay/tiết bù.</summary>
public sealed class SmtpOptions
{
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = "";
    public string AppPassword { get; set; } = "";
    public string SenderName { get; set; } = "MSMS";
}

/// <summary>Gửi một email văn bản thuần. Trả false khi chưa cấu hình hoặc SMTP lỗi; không ném lỗi, không thử lại.</summary>
public interface IEmailSender
{
    Task<bool> SendAsync(string recipient, string subject, string body, CancellationToken ct = default);
}

/// <summary>
/// LỆCH SPEC BR-26 (đã được người dùng đồng ý, chờ nhóm cập nhật spec): ngoài OTP, email còn dùng để báo giáo viên được phân công
/// dạy thay hoặc dạy tiết bù. Không hàng đợi, không gửi lại; lỗi chỉ ghi loại lỗi, không ghi địa chỉ hay nội dung thư.
/// </summary>
public sealed class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task<bool> SendAsync(string recipient, string subject, string body, CancellationToken ct = default)
    {
        var config = options.Value;
        if (string.IsNullOrWhiteSpace(config.Username) || string.IsNullOrWhiteSpace(config.AppPassword))
        {
            logger.LogWarning("Chưa cấu hình Smtp:Username hoặc Smtp:AppPassword. Không gửi email.");
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
            mail.Subject = subject;
            mail.Body = new TextPart("plain") { Text = body };

            using var client = new SmtpClient();
            await client.ConnectAsync(config.Host, config.Port, SecureSocketOptions.StartTls, timeout.Token);
            await client.AuthenticateAsync(config.Username, config.AppPassword.Replace(" ", ""), timeout.Token);
            await client.SendAsync(mail, timeout.Token);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning("Không gửi được email qua SMTP ({FailureType}).", ex.GetType().Name);
            return false;
        }
    }
}
