using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Excel;
using MSMS.Api.Common.Text;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Ppl.Dtos;

namespace MSMS.Api.Modules.Ppl.Services;

/// <summary>
/// BR-11 — import một bước: validate TOÀN BỘ file rồi ghi trong một transaction; có một lỗi là không ghi gì.
/// Người đã có (trùng Họ tên + Ngày sinh) bị bỏ qua cả dòng, import không bao giờ cập nhật hồ sơ cũ.
/// </summary>
public sealed class StaffImportService(MsmsDbContext db, IClock clock)
{
    private const int HeaderRow = 4;
    private static readonly string[] Headers =
    [
        "Họ và tên", "Ngày sinh", "Giới tính", "Số điện thoại", "Email",
        "Tổ chuyên môn", "Phân hiệu chính", "Viết tắt GV",
    ];
    private static readonly int[] RequiredColumns = [1, 2, 7];

    private const string SheetName = "02_Can_bo";
    private const int InputRows = 200;
    private static readonly double[] ColumnWidths = [26, 13, 10, 15, 26, 20, 18, 12];

    /// <summary>Mẫu cố định, bố cục và kiểu dáng khớp sheet 02_Can_bo của MSMS_Excel_Import_Templates.xlsx.</summary>
    public FileContentResult BuildTemplate()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet(SheetName);
        ws.ShowGridLines = false;
        var last = Headers.Length;

        ws.WriteTitle("MSMS — MẪU IMPORT CÁN BỘ", last);
        ws.WriteNote(2,
            "Bắt buộc (ô tô cam): Họ và tên, Ngày sinh, Phân hiệu chính. Tùy chọn: Giới tính, Số điện thoại, Email, Tổ chuyên môn, Viết tắt GV. "
            + "KHÔNG có cột mã hồ sơ — hệ thống tự sinh mã CB và tên đăng nhập. Viết tắt GV chỉ điền cho giáo viên, duy nhất toàn trường, chỉ chữ và số. "
            + "Người đã có (trùng Họ và tên + Ngày sinh) thì bỏ qua cả dòng. Ngày sinh dd/mm/yyyy. Import chỉ tạo hồ sơ, không tạo tài khoản.",
            last, 40);

        for (var i = 0; i < Headers.Length; i++)
        {
            ws.Cell(HeaderRow, i + 1).Value = Headers[i];
            ws.Column(i + 1).Width = ColumnWidths[i];
        }
        ws.StyleHeaderRow(HeaderRow, last, 26);

        // Vùng nhập 200 dòng: viền mảnh, ô cam là cột bắt buộc.
        var input = ws.Range(HeaderRow + 1, 1, HeaderRow + InputRows, last);
        input.Style.Font.FontSize = 10;
        input.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        input.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        foreach (var c in RequiredColumns)
            ws.Range(HeaderRow + 1, c, HeaderRow + InputRows, c).Style.Fill.BackgroundColor = ExcelHelpers.RequiredOrange;

        ws.SheetView.FreezeRows(HeaderRow);
        return wb.ToFile("MSMS_Mau_Import_Can_bo.xlsx");
    }

    public async Task<StaffImportResult> ImportAsync(Stream stream)
    {
        XLWorkbook wb;
        try { wb = new XLWorkbook(stream); }
        catch { throw ApiException.BadRequest("Không đọc được file. Dùng file .xlsx tải từ nút \"Tải file mẫu\"."); }

        using (wb)
        {
            var ws = wb.FindSheet(SheetName) ?? throw ApiException.BadRequest("File không có sheet nào.");
            var errors = new List<ImportError>();

            // Hàng tiêu đề là chữ ký nhận diện mẫu: sửa một chữ là từ chối cả file.
            for (var i = 0; i < Headers.Length; i++)
            {
                var actual = ws.Cell(HeaderRow, i + 1).Text();
                if (!string.Equals(actual, Headers[i], StringComparison.OrdinalIgnoreCase))
                    errors.Add(new ImportError(ws.Cell(HeaderRow, i + 1).Address(), Headers[i], actual,
                        $"Hàng tiêu đề bị sửa. Cột này phải là \"{Headers[i]}\". Tải lại file mẫu, không sửa hàng tiêu đề."));
            }
            if (errors.Count > 0) return Rejected(errors, []);

            var campuses = await db.Campus.AsNoTracking().ToDictionaryAsync(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);
            var departments = await db.SubjectDepartment.AsNoTracking().ToDictionaryAsync(d => d.Name, d => d.DepartmentId, StringComparer.OrdinalIgnoreCase);
            var existingAbbrs = (await db.StaffProfile.Where(s => s.TeacherAbbr != null).Select(s => s.TeacherAbbr!).ToListAsync())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var existingKeys = (await db.Person.Where(p => p.PersonType == "Staff")
                    .Select(p => new { p.FullName, p.DateOfBirth }).ToListAsync())
                .Select(p => Key(p.FullName, p.DateOfBirth)).ToHashSet();

            var skipped = new List<StaffImportSkipped>();
            var valid = new List<(int Row, string Name, DateOnly Dob, string? Gender, string? Phone, string? Email, int? DeptId, int CampusId, string? Abbr)>();
            var fileKeys = new Dictionary<string, int>();
            var fileAbbrs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            var lastRow = ws.LastRowUsed()?.RowNumber() ?? HeaderRow;
            for (var row = HeaderRow + 1; row <= lastRow; row++)
            {
                if (Enumerable.Range(1, Headers.Length).All(c => ws.Cell(row, c).IsEmpty())) continue;

                var rowErrors = errors.Count;
                void Err(int col, string reason) =>
                    errors.Add(new ImportError($"Dòng {row}", Headers[col - 1], ws.Cell(row, col).Text(), reason));

                var name = ws.Cell(row, 1).Text();
                if (name.Length == 0) Err(1, "Thiếu họ và tên. Điền họ và tên đầy đủ.");
                else if (name.Length > 150) Err(1, "Họ và tên tối đa 150 ký tự.");

                DateOnly dob = default;
                if (!ws.Cell(row, 2).TryReadDate(out dob))
                    Err(2, ws.Cell(row, 2).IsEmpty() ? "Thiếu ngày sinh. Điền theo dạng dd/mm/yyyy." : "Ngày sinh sai định dạng. Điền theo dạng dd/mm/yyyy.");
                else if (dob > clock.Today) Err(2, "Ngày sinh ở tương lai. Kiểm tra lại năm sinh.");

                // Khóa nhận diện hợp lệ thì mới tra người đã có; người đã có thì bỏ qua cả dòng, không kiểm các cột còn lại.
                if (errors.Count == rowErrors)
                {
                    var key = Key(name, dob);
                    if (existingKeys.Contains(key))
                    {
                        skipped.Add(new StaffImportSkipped(row, name, "Đã có hồ sơ cùng họ tên và ngày sinh, bỏ qua cả dòng."));
                        continue;
                    }
                    if (fileKeys.TryGetValue(key, out var firstRow))
                        Err(1, $"Trùng họ tên và ngày sinh với dòng {firstRow} trong cùng file. Xóa hoặc sửa một trong hai dòng.");
                    else fileKeys[key] = row;
                }

                var genderText = ws.Cell(row, 3).Text();
                var gender = Genders.Parse(genderText);
                if (gender == "?") { Err(3, "Giới tính chỉ gồm Nam, Nữ hoặc Khác."); gender = null; }

                var phone = ws.Cell(row, 4).Text();
                if (phone.Length > 20) Err(4, "Số điện thoại tối đa 20 ký tự.");

                var email = ws.Cell(row, 5).Text();
                if (email.Length > 0 && (!email.Contains('@') || email.Length > 150)) Err(5, "Email chưa đúng định dạng.");

                int? deptId = null;
                var deptName = ws.Cell(row, 6).Text();
                if (deptName.Length > 0)
                {
                    if (departments.TryGetValue(deptName, out var d)) deptId = d;
                    else Err(6, $"Tổ chuyên môn \"{deptName}\" chưa khai trong hệ thống. Khai ở Thiết lập hoặc sửa lại tên cho đúng.");
                }

                var campusId = 0;
                var campusName = ws.Cell(row, 7).Text();
                if (campusName.Length == 0) Err(7, "Thiếu phân hiệu chính. Điền đúng tên phân hiệu đã khai trong hệ thống.");
                else if (!campuses.TryGetValue(campusName, out var campus)) Err(7, $"Phân hiệu \"{campusName}\" chưa khai trong hệ thống. Sửa lại cho đúng tên đã khai.");
                else if (!campus.IsActive) Err(7, $"Phân hiệu \"{campusName}\" đã ngừng hoạt động.");
                else campusId = campus.CampusId;

                string? abbr = null;
                var abbrText = ws.Cell(row, 8).Text();
                if (abbrText.Length > 0)
                {
                    if (abbrText.Length > 10 || !Guard.IsAlnum(abbrText)) Err(8, "Viết tắt GV chỉ gồm chữ và số, tối đa 10 ký tự.");
                    else if (existingAbbrs.Contains(abbrText)) Err(8, $"Viết tắt \"{abbrText}\" đã được cán bộ khác dùng. Viết tắt phải duy nhất toàn trường.");
                    else if (fileAbbrs.TryGetValue(abbrText, out var abbrRow)) Err(8, $"Viết tắt \"{abbrText}\" trùng với dòng {abbrRow} trong cùng file.");
                    else { abbr = abbrText; fileAbbrs[abbrText] = row; }
                }

                if (errors.Count == rowErrors)
                    valid.Add((row, name, dob, gender, phone.Length > 0 ? phone : null, email.Length > 0 ? email : null, deptId, campusId, abbr));
            }

            if (errors.Count > 0) return Rejected(errors, skipped);

            await using var tx = await db.Database.BeginTransactionAsync();
            foreach (var v in valid)
            {
                var person = new Person
                {
                    PersonType = "Staff", PersonCode = await PersonCodes.NextAsync(db, PersonCodes.Staff),
                    FullName = v.Name, DateOfBirth = v.Dob, Gender = v.Gender, PhoneNumber = v.Phone, Email = v.Email,
                    CreatedAt = clock.Now,
                };
                db.Person.Add(person);
                db.StaffProfile.Add(new StaffProfile
                {
                    Staff = person, PrimaryCampusId = v.CampusId, DepartmentId = v.DeptId, TeacherAbbr = v.Abbr, IsActive = true,
                });
                await db.SaveChangesAsync();   // mỗi người một lần lưu để mã hồ sơ tăng đúng thứ tự dòng
            }
            await tx.CommitAsync();
            return new StaffImportResult(true, valid.Count, skipped, [], 0);
        }
    }

    private static string Key(string name, DateOnly? dob) =>
        $"{TextNormalizer.Normalize(name).ToUpperInvariant()}|{dob:yyyy-MM-dd}";

    private static StaffImportResult Rejected(List<ImportError> errors, List<StaffImportSkipped> skipped) =>
        new(false, 0, skipped, errors.Take(ExcelHelpers.MaxReportedErrors).ToList(), errors.Count);
}
