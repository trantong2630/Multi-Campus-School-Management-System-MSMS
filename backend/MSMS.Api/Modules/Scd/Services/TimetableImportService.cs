using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Excel;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Sch.Services;
using MSMS.Api.Modules.Scd.Dtos;

namespace MSMS.Api.Modules.Scd.Services;

/// <summary>
/// BR-12: đọc file lưới, đối chiếu viết tắt, báo lỗi theo địa chỉ ô. Có một lỗi là từ chối cả file,
/// không import một phần. File chỉ cần gồm các lớp muốn đổi; file hợp lệ được lưu thành một phiên bản Nháp
/// chỉ chứa các lớp có trong file.
/// </summary>
public sealed partial class TimetableImportService(MsmsDbContext db, CurrentUser user, ScopeResolver scope, IClock clock)
{
    [GeneratedRegex(@"(\d+)\s*$")]
    private static partial Regex TrailingNumber();

    public async Task<TimetableImportResult> ImportAsync(Stream stream, string fileName)
    {
        XLWorkbook wb;
        try { wb = new XLWorkbook(stream); }
        catch { throw ApiException.BadRequest("Không đọc được file. Dùng file .xlsx tải từ nút \"Tải file mẫu\"."); }

        using (wb)
        {
            var ws = wb.FindSheet(TimetableLayout.SheetName) ?? throw ApiException.BadRequest("File không có sheet nào.");
            var errors = new List<ImportError>();
            void Err(IXLCell cell, string column, string reason) => errors.Add(new ImportError(cell.Address(), column, cell.Text(), reason));

            // ---- Ba ô meta ----
            var campusCell = ws.Cell(TimetableLayout.CampusRow, TimetableLayout.MetaValueColumn);
            var yearCell = ws.Cell(TimetableLayout.YearRow, TimetableLayout.MetaValueColumn);
            var semesterCell = ws.Cell(TimetableLayout.SemesterRow, TimetableLayout.MetaValueColumn);

            var campusName = campusCell.Text();
            var campus = campusName.Length == 0 ? null : await db.Campus.FirstOrDefaultAsync(c => c.Name == campusName);
            if (campus is null) Err(campusCell, "Phân hiệu", "Phân hiệu chưa khai trong hệ thống hoặc để trống. Điền đúng tên phân hiệu đã khai.");
            var yearName = yearCell.Text();
            var year = yearName.Length == 0 ? null : await db.AcademicYear.FirstOrDefaultAsync(y => y.Name == yearName);
            if (year is null) Err(yearCell, "Năm học", "Năm học chưa khai trong hệ thống hoặc để trống. Điền đúng tên năm học, ví dụ 2026-2027.");

            Semester? semester = null;
            if (year is not null)
            {
                if (!byte.TryParse(semesterCell.Text(), out var semNo) || semNo is not (1 or 2))
                    Err(semesterCell, "Học kỳ", "Học kỳ chỉ nhận 1 hoặc 2.");
                else
                {
                    semester = await db.Semester.FirstOrDefaultAsync(s => s.AcademicYearId == year.AcademicYearId && s.SemesterNo == semNo);
                    if (semester is null) Err(semesterCell, "Học kỳ", $"Năm học {year.Name} chưa khai học kỳ {semNo}. Khai ở Thiết lập → Năm học & Học kỳ.");
                }
            }
            if (campus is null || year is null || semester is null) return Rejected(errors);
            if (!campus.IsActive) { Err(campusCell, "Phân hiệu", "Phân hiệu đã ngừng hoạt động."); return Rejected(errors); }
            await scope.EnsureCanManageCampusAsync(campus.CampusId);

            // ---- Hàng tiêu đề: sửa một chữ là từ chối cả file ----
            string[] expected = [TimetableLayout.DayHeader, TimetableLayout.SessionHeader, TimetableLayout.PeriodHeader];
            for (var i = 0; i < expected.Length; i++)
            {
                var cell = ws.Cell(TimetableLayout.HeaderRow, i + 1);
                if (!string.Equals(cell.Text(), expected[i], StringComparison.OrdinalIgnoreCase))
                    Err(cell, expected[i], $"Hàng tiêu đề bị sửa. Ô này phải là \"{expected[i]}\". Tải lại file mẫu, không sửa hàng tiêu đề.");
            }
            if (errors.Count > 0) return Rejected(errors);

            // ---- Cột lớp: từ cột D sang phải tới ô tiêu đề trống đầu tiên ----
            var classes = await db.Class.AsNoTracking()
                .Where(c => c.CampusId == campus.CampusId && c.AcademicYearId == year.AcademicYearId && c.IsActive).ToListAsync();
            var classByName = classes.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
            var classColumns = new List<(int Column, Class Class)>();
            var seenClassCols = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var col = TimetableLayout.FirstClassColumn; ; col++)
            {
                var header = ws.Cell(TimetableLayout.HeaderRow, col);
                var name = header.Text();
                if (name.Length == 0) break;
                if (seenClassCols.TryGetValue(name, out var firstAddr))
                {
                    Err(header, "Lớp", $"Hai cột cùng một tên lớp (trùng với ô {firstAddr}). Mỗi lớp chỉ một cột.");
                    continue;
                }
                seenClassCols[name] = header.Address();
                if (!classByName.TryGetValue(name, out var cls))
                    Err(header, "Lớp", $"Lớp \"{name}\" không thuộc {campus.Name}, năm học {year.Name}. Sửa tên lớp hoặc tạo lớp ở Thiết lập.");
                else classColumns.Add((col, cls));
            }

            // ---- Danh mục tra cứu ----
            var slots = await db.PeriodSlot.AsNoTracking().Where(p => p.IsActive).ToListAsync();
            var slotByKey = slots.ToDictionary(p => (p.SessionOfDay, (int)p.PeriodNo));
            var subjects = await db.Subject.AsNoTracking().Where(s => s.IsActive).ToDictionaryAsync(s => s.Abbreviation, StringComparer.OrdinalIgnoreCase);
            var teachers = await db.StaffProfile.AsNoTracking().Include(s => s.Staff).ThenInclude(p => p.Account)
                .Where(s => s.TeacherAbbr != null).ToDictionaryAsync(s => s.TeacherAbbr!, StringComparer.OrdinalIgnoreCase);

            // ---- Các hàng Thứ × Buổi × Tiết ----
            var entries = new List<TimetableEntry>();
            var teacherSlots = new Dictionary<(int TeacherId, int Day, int SlotId), string>();
            var rowKeys = new Dictionary<(int Day, int SlotId), int>();
            int? day = null;
            string? sessionOfDay = null;
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? TimetableLayout.HeaderRow;

            for (var row = TimetableLayout.FirstDataRow; row <= lastRow; row++)
            {
                var a = ws.Cell(row, 1);
                var b = ws.Cell(row, 2);
                var c = ws.Cell(row, 3);
                var hasCells = classColumns.Any(cc => !ws.Cell(row, cc.Column).IsEmpty());
                if (a.IsEmpty() && b.IsEmpty() && c.IsEmpty() && !hasCells) continue;

                // Cột Thứ và Buổi trong file mẫu có thể được gộp ô: ô trống thì dùng giá trị của hàng trước.
                if (!a.IsEmpty()) day = ParseDay(a.Text());
                if (!b.IsEmpty()) sessionOfDay = ParseSession(b.Text());

                var rowOk = true;
                if (day is null) { Err(a, "Thứ", "Thứ không hợp lệ. Điền số từ 2 đến 7 (Thứ 2 đến Thứ 7)."); rowOk = false; }
                if (sessionOfDay is null) { Err(b, "Buổi", "Buổi chỉ nhận Sáng hoặc Chiều."); rowOk = false; }
                if (!int.TryParse(c.Text(), out var period) || period < 1) { Err(c, "Tiết", "Tiết phải là số từ 1 trở lên."); rowOk = false; }
                if (!rowOk) continue;

                if (!slotByKey.TryGetValue((sessionOfDay!, period), out var slot))
                {
                    Err(c, "Tiết", $"Khung giờ {TimetableLayout.SessionLabel(sessionOfDay!)} tiết {period} chưa khai hoặc đã ngừng dùng. Khai ở Thiết lập → Khung giờ tiết.");
                    continue;
                }
                if (rowKeys.TryGetValue((day!.Value, slot.PeriodSlotId), out var firstRow))
                {
                    Err(c, "Tiết", $"Trùng hàng: Thứ {day}, {TimetableLayout.SessionLabel(sessionOfDay!)} tiết {period} đã có ở dòng {firstRow}.");
                    continue;
                }
                rowKeys[(day.Value, slot.PeriodSlotId)] = row;

                foreach (var (col, cls) in classColumns)
                {
                    var cell = ws.Cell(row, col);
                    if (cell.IsEmpty()) continue;
                    var text = cell.Text();

                    var dash = text.IndexOf('-');   // dấu "-" ĐẦU TIÊN là dấu tách
                    if (dash <= 0 || dash == text.Length - 1)
                    {
                        Err(cell, $"Lớp {cls.Name}", "Ô phải ghi dạng MÔN-GV (viết tắt môn, gạch ngang, viết tắt giáo viên), ví dụ TOAN-NVA.");
                        continue;
                    }
                    var subjectAbbr = text[..dash].Trim();
                    var teacherAbbr = text[(dash + 1)..].Trim();

                    var cellOk = true;
                    if (!subjects.TryGetValue(subjectAbbr, out var subject))
                    {
                        Err(cell, $"Lớp {cls.Name}", $"Viết tắt môn \"{subjectAbbr}\" không khớp môn nào đang hoạt động. Kiểm tra ở Thiết lập → Tổ chuyên môn & Môn học.");
                        cellOk = false;
                    }
                    if (!teachers.TryGetValue(teacherAbbr, out var teacher))
                    {
                        Err(cell, $"Lớp {cls.Name}", $"Viết tắt giáo viên \"{teacherAbbr}\" không khớp cán bộ nào. Kiểm tra ở Hồ sơ → Cán bộ.");
                        cellOk = false;
                    }
                    else if (TeacherProblem(teacher) is { } problem)
                    {
                        Err(cell, $"Lớp {cls.Name}", $"Giáo viên {teacherAbbr} không đủ điều kiện dạy: {problem}");
                        cellOk = false;
                    }
                    if (!cellOk) continue;

                    var tKey = (teacher!.StaffId, day.Value, slot.PeriodSlotId);
                    if (teacherSlots.TryGetValue(tKey, out var firstCell))
                    {
                        Err(cell, $"Lớp {cls.Name}", $"Giáo viên {teacherAbbr} đã được xếp ở ô {firstCell} cùng Thứ {day}, {TimetableLayout.SessionLabel(sessionOfDay!)} tiết {period}. Một giáo viên chỉ dạy một lớp mỗi tiết.");
                        continue;
                    }
                    teacherSlots[tKey] = cell.Address();
                    entries.Add(new TimetableEntry
                    {
                        ClassId = cls.ClassId, DayOfWeek = (byte)day.Value, PeriodSlotId = slot.PeriodSlotId,
                        SubjectId = subject!.SubjectId, TeacherId = teacher.StaffId,
                    });
                }
            }

            if (errors.Count == 0 && classColumns.Count == 0)
                errors.Add(new ImportError("Cả file", "Nội dung", "", "File chưa có cột lớp nào. Giữ ít nhất một cột lớp từ tệp mẫu rồi tải lên."));
            if (errors.Count > 0) return Rejected(errors);

            var version = new TimetableVersion
            {
                CampusId = campus.CampusId, SemesterId = semester.SemesterId, Status = "Draft",
                SourceFileName = fileName.Length > 255 ? fileName[..255] : fileName,
                ImportedByPersonId = user.PersonId, ImportedAt = clock.Now,
                TimetableEntry = entries,
                TimetableVersionClass = classColumns.Select(cc => new TimetableVersionClass { ClassId = cc.Class.ClassId }).ToList(),
                ImportSnapshot = DraftSnapshot.Serialize(entries),
            };
            db.TimetableVersion.Add(version);
            await db.SaveChangesAsync();
            return new TimetableImportResult(true, version.VersionId, entries.Count, [], 0);
        }
    }

    /// <summary>BR-06: người được xếp dạy phải có tài khoản Teacher không ở trạng thái Đã khóa (Chờ đổi mật khẩu vẫn hợp lệ).</summary>
    public static string? TeacherProblem(StaffProfile teacher)
    {
        if (!teacher.IsActive) return "hồ sơ cán bộ đã ngừng hoạt động.";
        var account = teacher.Staff.Account;
        if (account is null || account.AccountType != AccountTypes.Teacher) return "chưa có tài khoản loại Giáo viên. Tạo ở mục Tài khoản.";
        if (account.Status == AccountStatuses.Locked) return "tài khoản đang bị khóa. Mở khóa trước.";
        return null;
    }

    private static int? ParseDay(string text)
    {
        var m = TrailingNumber().Match(text);
        return m.Success && int.TryParse(m.Groups[1].Value, out var d) && d is >= 2 and <= 7 ? d : null;
    }

    private static string? ParseSession(string text) => Common.Text.TextNormalizer.RemoveDiacritics(text).ToLowerInvariant() switch
    {
        "sang" or "morning" => SessionOfDay.Morning,
        "chieu" or "afternoon" => SessionOfDay.Afternoon,
        _ => null,
    };

    private static TimetableImportResult Rejected(List<ImportError> errors) =>
        new(false, null, 0, errors.Take(ExcelHelpers.MaxReportedErrors).ToList(), errors.Count);
}
