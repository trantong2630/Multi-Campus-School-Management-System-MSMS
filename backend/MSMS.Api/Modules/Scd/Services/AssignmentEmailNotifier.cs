using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Email;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;

namespace MSMS.Api.Modules.Scd.Services;

/// <summary>
/// Email báo giáo viên được phân công dạy thay (đổi giáo viên, xử lý tiết Chờ xử lý) hoặc dạy tiết bù, và báo giáo viên mất tiết
/// (giáo viên cũ khi đổi giáo viên, giáo viên của tiết bị hủy), gửi kèm thông báo trong ứng dụng.
/// Spec mục 3.2 và BR-26 cho phép email này, ngoài email OTP; bốn trường hợp, không email nào khác cho thông báo lịch dạy.
/// Thư được giữ lại trong request; người gọi chỉ gọi <see cref="Dispatch"/> sau khi SaveChanges/commit thành công.
/// Gửi chạy nền nên request không chờ SMTP; gửi lỗi hoặc hồ sơ không có email thì bỏ qua, thao tác xếp lịch vẫn thành công.
/// </summary>
public sealed class AssignmentEmailNotifier(MsmsDbContext db, IEmailSender sender, ILogger<AssignmentEmailNotifier> logger)
{
    private readonly List<(string To, string Subject, string Body)> _queued = [];

    /// <summary>Giữ một thư cho giáo viên; gửi theo email ghi trong hồ sơ (Person.Email). Không có email thì không giữ gì.</summary>
    public async Task QueueAsync(int teacherId, string subject, string body)
    {
        var email = (await db.Person.AsNoTracking().Where(p => p.PersonId == teacherId).Select(p => p.Email).FirstOrDefaultAsync())?.Trim();
        if (string.IsNullOrEmpty(email)) return;
        _queued.Add((email, subject, body));
    }

    /// <summary>Gửi các thư đang giữ rồi xóa danh sách. Trả về tác vụ gửi để test chờ được; code nghiệp vụ không cần chờ.</summary>
    public Task Dispatch()
    {
        if (_queued.Count == 0) return Task.CompletedTask;
        var mails = _queued.ToArray();
        _queued.Clear();
        // Không dùng DbContext hay service scoped bên trong: request có thể đã kết thúc khi thư được gửi.
        return Task.Run(async () =>
        {
            foreach (var (to, subject, body) in mails)
            {
                try { await sender.SendAsync(to, subject, body); }
                catch (Exception ex) { logger.LogWarning("Không gửi được email phân công dạy ({FailureType}).", ex.GetType().Name); }
            }
        });
    }

    /// <summary>Nội dung thư: câu chính, mô tả tiết kèm giờ học, lời nhắc xem trên MSMS.</summary>
    public static string Body(string headline, string what, PeriodSlot slot) =>
        $"{headline}\n\n{what} ({slot.StartTime:HH\\:mm}–{slot.EndTime:HH\\:mm}).\n\n"
        + "Xem chi tiết trong lịch dạy trên MSMS. Đây là email tự động, vui lòng không trả lời.";
}
