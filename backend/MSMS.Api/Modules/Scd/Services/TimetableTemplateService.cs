using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Excel;
using MSMS.Api.Common.Text;
using MSMS.Api.Data;
using MSMS.Api.Modules.Sch.Services;

namespace MSMS.Api.Modules.Scd.Services;

/// <summary>Bố cục file thời khóa biểu (BR-12). Một nơi duy nhất định nghĩa vị trí ô, dùng chung cho sinh mẫu và đọc file.</summary>
public static class TimetableLayout
{
    public const int CampusRow = 2, YearRow = 3, SemesterRow = 4, MetaValueColumn = 2;
    public const int HeaderRow = 7, FirstDataRow = 8, FirstClassColumn = 4;
    public const string DayHeader = "Thứ", SessionHeader = "Buổi", PeriodHeader = "Tiết";
    public const string SheetName = "01_TKB";

    public static string SessionLabel(string sessionOfDay) => sessionOfDay == SessionOfDay.Morning ? "Sáng" : "Chiều";
}

/// <summary>
/// File mẫu do hệ thống sinh, không phải file tĩnh: số hàng theo khung giờ đã khai, số cột theo lớp của
/// (phân hiệu, năm học), ba ô meta điền sẵn, nội dung điền sẵn theo phiên bản Đã phát hành gần nhất (BR-12).
/// </summary>
public sealed class TimetableTemplateService(MsmsDbContext db, ScopeResolver scope)
{
    public async Task<FileContentResult> BuildAsync(int campusId, int semesterId)
    {
        await scope.EnsureCanManageCampusAsync(campusId);
        var campus = await db.Campus.FindAsync(campusId) ?? throw ApiException.NotFound("Không tìm thấy phân hiệu.");
        var semester = await db.Semester.Include(s => s.AcademicYear).FirstOrDefaultAsync(s => s.SemesterId == semesterId)
            ?? throw ApiException.NotFound("Không tìm thấy học kỳ.");

        var classes = await db.Class.AsNoTracking()
            .Where(c => c.CampusId == campusId && c.AcademicYearId == semester.AcademicYearId && c.IsActive)
            .OrderBy(c => c.GradeLevel).ThenBy(c => c.Name).ToListAsync();
        if (classes.Count == 0)
            throw ApiException.BadRequest($"{campus.Name} chưa có lớp nào trong năm học {semester.AcademicYear.Name}. Tạo lớp ở Thiết lập trước khi tải mẫu.");

        var slots = await db.PeriodSlot.AsNoTracking().Where(p => p.IsActive)
            .OrderBy(p => p.SessionOfDay == SessionOfDay.Morning ? 0 : 1).ThenBy(p => p.PeriodNo).ToListAsync();
        if (slots.Count == 0) throw ApiException.BadRequest("Chưa khai khung giờ tiết nào. Khai ở Thiết lập → Khung giờ tiết trước khi tải mẫu.");

        // Nội dung điền sẵn: phiên bản Đã phát hành gần nhất của (phân hiệu, học kỳ).
        var latest = await db.TimetableVersion.AsNoTracking()
            .Where(v => v.CampusId == campusId && v.SemesterId == semesterId && v.Status == "Published")
            .OrderByDescending(v => v.PublishedAt).ThenByDescending(v => v.VersionId).FirstOrDefaultAsync();
        var filled = new Dictionary<(int ClassId, int Day, int SlotId), string>();
        if (latest is not null)
        {
            var entries = await db.TimetableEntry.AsNoTracking().Where(e => e.VersionId == latest.VersionId)
                .Select(e => new { e.ClassId, e.DayOfWeek, e.PeriodSlotId, Subject = e.Subject.Abbreviation, Teacher = e.Teacher.TeacherAbbr })
                .ToListAsync();
            foreach (var e in entries) filled[(e.ClassId, e.DayOfWeek, e.PeriodSlotId)] = $"{e.Subject}-{e.Teacher}";
        }

        // Bố cục và kiểu dáng khớp sheet 01_TKB của MSMS_Excel_Import_Templates.xlsx.
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet(TimetableLayout.SheetName);
        ws.ShowGridLines = false;
        var lastClassColumn = TimetableLayout.FirstClassColumn + classes.Count - 1;
        var wide = Math.Max(lastClassColumn, 8);   // dải tiêu đề và ghi chú đủ rộng để đọc được khi ít lớp

        ws.WriteTitle("MSMS — THỜI KHÓA BIỂU", wide);

        (int Row, string Label, string Value, string Hint)[] meta =
        [
            (TimetableLayout.CampusRow, "Phân hiệu", campus.Name, "Tên phân hiệu đúng như đã khai trong hệ thống"),
            (TimetableLayout.YearRow, "Năm học", semester.AcademicYear.Name, "Ví dụ: 2026-2027"),
            (TimetableLayout.SemesterRow, "Học kỳ", semester.SemesterNo.ToString(), "1 hoặc 2"),
        ];
        foreach (var m in meta)
        {
            var label = ws.Cell(m.Row, 1);
            label.Value = m.Label;
            label.Style.Font.Bold = true;
            label.Style.Font.FontSize = 10;
            label.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            label.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            label.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            var value = ws.Range(m.Row, TimetableLayout.MetaValueColumn, m.Row, TimetableLayout.MetaValueColumn + 1).Merge();
            ws.Cell(m.Row, TimetableLayout.MetaValueColumn).SetValue(m.Value);   // luôn là chuỗi, "2026-2027" không bị hiểu thành ngày
            value.Style.Fill.BackgroundColor = ExcelHelpers.MetaYellow;
            value.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            value.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            value.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            ws.Cell(m.Row, 4).Value = m.Hint;
            ws.Cell(m.Row, 4).Style.Font.FontSize = 9;
            ws.Cell(m.Row, 4).Style.Font.FontColor = XLColor.Gray;
        }

        ws.WriteNote(5,
            "Mỗi ô ghi dạng  MÔN-GV  (viết tắt môn, gạch ngang, viết tắt giáo viên), ví dụ TOAN-NVA; tiết trống để ô trống. "
            + "File chỉ cần gồm các lớp muốn đổi: xóa các cột lớp không đổi; lớp có trong file được ghi đè, lớp không có trong file giữ nguyên (BR-12). Không sửa hàng tiêu đề, ba ô meta, cột Thứ, Buổi và Tiết.",
            wide, 32);

        ws.Cell(TimetableLayout.HeaderRow, 1).Value = TimetableLayout.DayHeader;
        ws.Cell(TimetableLayout.HeaderRow, 2).Value = TimetableLayout.SessionHeader;
        ws.Cell(TimetableLayout.HeaderRow, 3).Value = TimetableLayout.PeriodHeader;
        for (var i = 0; i < classes.Count; i++)
            ws.Cell(TimetableLayout.HeaderRow, TimetableLayout.FirstClassColumn + i).Value = classes[i].Name;
        ws.StyleHeaderRow(TimetableLayout.HeaderRow, lastClassColumn, 22);

        var row = TimetableLayout.FirstDataRow;
        for (var day = 2; day <= 7; day++)
        {
            var dayStart = row;
            var sessionStart = row;
            for (var s = 0; s < slots.Count; s++)
            {
                var slot = slots[s];
                ws.Cell(row, 3).Value = slot.PeriodNo;
                for (var i = 0; i < classes.Count; i++)
                {
                    if (filled.TryGetValue((classes[i].ClassId, day, slot.PeriodSlotId), out var text))
                        ws.Cell(row, TimetableLayout.FirstClassColumn + i).Value = text;
                }
                row++;

                var endOfSession = s == slots.Count - 1 || slots[s + 1].SessionOfDay != slot.SessionOfDay;
                if (endOfSession)
                {
                    // Cột Buổi gộp ô theo từng buổi, cột Thứ gộp ô theo từng ngày (file mẫu đã gộp sẵn).
                    ws.Cell(sessionStart, 2).Value = TimetableLayout.SessionLabel(slot.SessionOfDay);
                    ws.Range(sessionStart, 2, row - 1, 2).Merge();
                    sessionStart = row;
                }
            }
            ws.Cell(dayStart, 1).Value = day;
            ws.Range(dayStart, 1, row - 1, 1).Merge();
        }
        var lastRow = row - 1;

        var body = ws.Range(TimetableLayout.FirstDataRow, 1, lastRow, lastClassColumn);
        body.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        body.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        body.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        body.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        body.Style.Font.FontSize = 11;
        ws.Range(TimetableLayout.FirstDataRow, 1, lastRow, 1).Style.Font.Bold = true;
        ws.Range(TimetableLayout.FirstDataRow, 2, lastRow, 3).Style.Font.FontSize = 10;
        ws.Rows(TimetableLayout.FirstDataRow, lastRow).Height = 18;

        ws.Column(1).Width = 7;
        ws.Column(2).Width = 9;
        ws.Column(3).Width = 7;
        ws.Columns(TimetableLayout.FirstClassColumn, lastClassColumn).Width = 15;
        ws.SheetView.Freeze(TimetableLayout.HeaderRow, 3);

        var fileName = $"MSMS_TKB_{TextNormalizer.RemoveDiacritics(campus.Name).Replace(' ', '_')}_HK{semester.SemesterNo}.xlsx";
        return wb.ToFile(fileName);
    }
}
