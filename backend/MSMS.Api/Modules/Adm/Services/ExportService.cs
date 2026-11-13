using System.Text;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Excel;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Text;
using MSMS.Api.Modules.Att.Services;
using MSMS.Api.Modules.Scd.Services;

namespace MSMS.Api.Modules.Adm.Services;

/// <summary>
/// ADM-03 Xuất dữ liệu (spec 5.2): dựng file Excel từ dữ liệu do module nguồn trả về; phạm vi người gọi do module nguồn kiểm.
/// File sinh tức thời, không lưu, không ghi Audit Log (ngoài ba nhóm của BR-28). Mỗi dòng một bản ghi, có bộ lọc và cố định tiêu đề.
/// </summary>
public sealed class ExportService(AttendanceService attendance, ScheduleQueryService schedule)
{
    private const int HeaderRow = 2;

    /// <summary>4.9.12: điểm danh của một lớp trong khoảng ngày, kèm nhãn cuối suy ra (BR-18).</summary>
    public async Task<FileContentResult> AttendanceAsync(int classId, DateOnly from, DateOnly to)
    {
        var data = await attendance.ExportAsync(classId, from, to);
        string[] headers = ["Ngày", "Thứ", "Buổi", "Tiết", "Môn học", "Giáo viên", "Mã học sinh", "Họ và tên", "Điểm danh", "Ghi chú"];

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Điểm danh");
        ws.WriteTitle($"Điểm danh lớp {data.ClassName} · {data.CampusName} · {Range(from, to)}", headers.Length);
        var r = WriteHeader(ws, headers);
        foreach (var row in data.Rows)
        {
            WriteDate(ws.Cell(r, 1), row.Date);
            ws.Cell(r, 2).Value = Weekday(row.Date);
            ws.Cell(r, 3).Value = SessionOfDay(row.SessionOfDay);
            ws.Cell(r, 4).Value = row.PeriodNo;
            ws.Cell(r, 5).Value = row.SubjectName;
            ws.Cell(r, 6).Value = row.TeacherName ?? "";
            ws.Cell(r, 7).Value = row.StudentCode;
            ws.Cell(r, 8).Value = row.FullName;
            ws.Cell(r, 9).Value = AttendanceLabel(row.Label);
            ws.Cell(r, 10).Value = row.Note ?? "";
            r++;
        }
        Finish(ws, headers.Length, r, "Không có tiết Đã xếp lịch nào của lớp trong khoảng ngày đã chọn (chỉ tính tới hôm nay).");
        return wb.ToFile(FileName("Diem_danh", data.ClassName, from, to));
    }

    /// <summary>4.9.13: thời khóa biểu hiện hành của phân hiệu, đọc từ lịch dạy (BR-14).</summary>
    public async Task<FileContentResult> TimetableAsync(int campusId, DateOnly from, DateOnly to)
    {
        var (campusName, sessions) = await schedule.GetCampusSessionsForExportAsync(campusId, from, to);
        string[] headers = ["Ngày", "Thứ", "Buổi", "Tiết", "Giờ", "Lớp", "Môn học", "Giáo viên", "Trạng thái", "Nguồn tiết"];

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Lịch dạy");
        ws.WriteTitle($"Thời khóa biểu · {campusName} · {Range(from, to)}", headers.Length);
        var r = WriteHeader(ws, headers);
        foreach (var s in sessions)
        {
            WriteDate(ws.Cell(r, 1), s.Date);
            ws.Cell(r, 2).Value = Weekday(s.Date);
            ws.Cell(r, 3).Value = SessionOfDay(s.SessionOfDay);
            ws.Cell(r, 4).Value = s.PeriodNo;
            ws.Cell(r, 5).Value = $"{s.StartTime:HH\\:mm}–{s.EndTime:HH\\:mm}";
            ws.Cell(r, 6).Value = s.ClassName;
            ws.Cell(r, 7).Value = s.SubjectName;
            ws.Cell(r, 8).Value = s.TeacherName ?? "";
            ws.Cell(r, 9).Value = SessionStatus(s.Status);
            ws.Cell(r, 10).Value = SessionSource(s.Source);
            r++;
        }
        Finish(ws, headers.Length, r, "Phân hiệu không có tiết nào trong khoảng ngày đã chọn.");
        return wb.ToFile(FileName("Thoi_khoa_bieu", campusName, from, to));
    }

    // ---------------------------------------------------------------- Nhãn hiển thị (Conventions §4.4)

    public static string AttendanceLabel(string label) => label switch
    {
        AttendanceLabels.Present => "Có mặt",
        AttendanceLabels.Late => "Muộn",
        AttendanceLabels.ExcusedAbsent => "Vắng có phép",
        AttendanceLabels.UnexcusedAbsent => "Vắng không phép",
        _ => "Chưa điểm danh",
    };

    public static string SessionStatus(string status) => status switch
    {
        "Scheduled" => "Đã xếp lịch",
        "Pending" => "Chờ xử lý",
        "Cancelled" => "Đã hủy",
        _ => status,
    };

    public static string SessionSource(string source) => source == "Manual" ? "Chỉnh tay" : "Sinh tự động";

    private static string SessionOfDay(string value) => value == "Morning" ? "Sáng" : "Chiều";

    private static string Weekday(DateOnly d) => d.DayOfWeek == DayOfWeek.Sunday ? "CN" : $"Thứ {(int)d.DayOfWeek + 1}";

    private static string Range(DateOnly from, DateOnly to) => $"{from:dd/MM/yyyy} – {to:dd/MM/yyyy}";

    // ---------------------------------------------------------------- Bố cục chung

    private static int WriteHeader(IXLWorksheet ws, string[] headers)
    {
        for (var c = 0; c < headers.Length; c++) ws.Cell(HeaderRow, c + 1).Value = headers[c];
        ws.StyleHeaderRow(HeaderRow, headers.Length, 22);
        return HeaderRow + 1;
    }

    /// <summary>Ngày ghi dạng ngày thật của Excel để sắp xếp và lọc được, hiển thị dd/MM/yyyy (Conventions §3).</summary>
    private static void WriteDate(IXLCell cell, DateOnly date)
    {
        cell.Value = date.ToDateTime(TimeOnly.MinValue);
        cell.Style.DateFormat.Format = "dd/MM/yyyy";
    }

    private static void Finish(IXLWorksheet ws, int lastColumn, int nextRow, string emptyNote)
    {
        if (nextRow == HeaderRow + 1)
        {
            ws.WriteNote(nextRow, emptyNote, lastColumn, 22);
        }
        else
        {
            ws.Range(HeaderRow, 1, nextRow - 1, lastColumn).SetAutoFilter();
        }
        ws.SheetView.FreezeRows(HeaderRow);
        ws.Columns(1, lastColumn).AdjustToContents(HeaderRow, nextRow - 1);   // bỏ qua dòng tiêu đề và dòng ghi chú gộp ô
    }

    /// <summary>Tên file bỏ dấu (Conventions §3); tên lớp, phân hiệu là chữ tự do nên chỉ giữ chữ, số và dấu gạch.</summary>
    public static string FileName(string kind, string subject, DateOnly from, DateOnly to)
    {
        var sb = new StringBuilder();
        foreach (var c in TextNormalizer.RemoveDiacritics(subject))
            sb.Append(char.IsAsciiLetterOrDigit(c) || c == '-' ? c : '_');
        var safe = sb.ToString().Trim('_');
        return $"MSMS_{kind}_{(safe.Length == 0 ? "file" : safe)}_{from:yyyyMMdd}_{to:yyyyMMdd}.xlsx";
    }
}
