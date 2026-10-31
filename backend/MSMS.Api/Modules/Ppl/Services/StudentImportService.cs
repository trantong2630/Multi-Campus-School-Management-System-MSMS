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
/// BR-11 — import học sinh một bước: validate TOÀN BỘ file rồi ghi trong một transaction; có một lỗi là không ghi gì.
/// Một dòng = một học sinh kèm phụ huynh đại diện, tạo tối đa bốn thứ: hồ sơ học sinh, ghi danh (chỉ khi cột Tên lớp có giá trị,
/// hiệu lực từ ngày bắt đầu năm học), hồ sơ phụ huynh và liên kết. Học sinh nhận diện theo Số định danh, phụ huynh theo số điện thoại.
/// Người đã có bị bỏ qua cả dòng, import không bao giờ cập nhật hồ sơ cũ và không tự tạo lớp.
/// </summary>
public sealed class StudentImportService(MsmsDbContext db, IClock clock)
{
    private const string SheetName = "03_Hoc_sinh";
    private const int YearRow = 3, CampusRow = 4, HeaderRow = 6, InputRows = 200;
    private static readonly string[] Headers =
    [
        "Họ và tên", "Ngày sinh", "Giới tính", "Địa chỉ", "Tên lớp",
        "Phụ huynh — Họ và tên", "Phụ huynh — Quan hệ", "Phụ huynh — Số điện thoại", "Phụ huynh — Email", "Số định danh",
    ];
    private static readonly int[] RequiredColumns = [1, 2, 6, 7, 8, 10];
    private static readonly double[] ColumnWidths = [26, 13, 10, 34, 12, 26, 14, 18, 26, 16];

    private const int ColName = 1, ColDob = 2, ColGender = 3, ColAddress = 4, ColClass = 5,
        ColParentName = 6, ColRelation = 7, ColPhone = 8, ColParentEmail = 9, ColNationalId = 10;

    /// <summary>Mẫu cố định, bố cục và kiểu dáng khớp sheet 03_Hoc_sinh của MSMS_Excel_Import_Templates.xlsx. Có thể điền sẵn hai ô meta.</summary>
    public async Task<FileContentResult> BuildTemplateAsync(int? yearId, int? campusId)
    {
        var yearName = yearId is null ? null : await db.AcademicYear.AsNoTracking().Where(y => y.AcademicYearId == yearId).Select(y => y.Name).FirstOrDefaultAsync();
        var campusName = campusId is null ? null : await db.Campus.AsNoTracking().Where(c => c.CampusId == campusId).Select(c => c.Name).FirstOrDefaultAsync();

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet(SheetName);
        ws.ShowGridLines = false;
        var last = Headers.Length;

        ws.WriteTitle("MSMS — MẪU IMPORT HỌC SINH (GỒM GHI DANH VÀ PHỤ HUYNH)", last);
        ws.WriteNote(2,
            "Mỗi dòng là một học sinh MỚI kèm phụ huynh đại diện. Khai Năm học và Phân hiệu ở hai ô meta bên dưới — mỗi file một phân hiệu. "
            + "KHÔNG có cột mã: hệ thống tự sinh mã HS, mã PH và tên đăng nhập. Cột Tên lớp có giá trị thì ghi danh luôn, hiệu lực từ ngày bắt đầu năm học; "
            + "lớp phải được tạo trước. Phụ huynh nhận diện theo số điện thoại — cùng số thì dùng chung một hồ sơ và liên kết nhiều con. "
            + "Học sinh nhận diện theo Số định danh (12 chữ số, duy nhất toàn trường, cột định dạng Text để giữ số 0 đầu); đã có thì bỏ qua cả dòng. Ô tô cam là cột bắt buộc.",
            last, 40);

        WriteMeta(ws, YearRow, "Năm học", yearName, "Ví dụ: 2026-2027");
        WriteMeta(ws, CampusRow, "Phân hiệu", campusName, "Tên phân hiệu đúng như đã khai trong hệ thống");

        for (var i = 0; i < Headers.Length; i++)
        {
            ws.Cell(HeaderRow, i + 1).Value = Headers[i];
            ws.Column(i + 1).Width = ColumnWidths[i];
        }
        ws.StyleHeaderRow(HeaderRow, last, 26);

        var input = ws.Range(HeaderRow + 1, 1, HeaderRow + InputRows, last);
        input.Style.Font.FontSize = 10;
        input.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        input.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        foreach (var c in RequiredColumns)
            ws.Range(HeaderRow + 1, c, HeaderRow + InputRows, c).Style.Fill.BackgroundColor = ExcelHelpers.RequiredOrange;

        // Số định danh và số điện thoại giữ dạng Text để không mất số 0 đầu.
        ws.Range(HeaderRow + 1, ColPhone, HeaderRow + InputRows, ColPhone).Style.NumberFormat.Format = "@";
        ws.Range(HeaderRow + 1, ColNationalId, HeaderRow + InputRows, ColNationalId).Style.NumberFormat.Format = "@";
        ws.Range(HeaderRow + 1, ColDob, HeaderRow + InputRows, ColDob).Style.NumberFormat.Format = "dd/mm/yyyy";
        ws.SheetView.FreezeRows(HeaderRow);
        return wb.ToFile("MSMS_Mau_Import_Hoc_sinh.xlsx");
    }

    private static void WriteMeta(IXLWorksheet ws, int row, string label, string? value, string hint)
    {
        ws.Cell(row, 1).Value = label;
        ws.Cell(row, 1).Style.Font.Bold = true;
        var input = ws.Cell(row, 2);
        if (!string.IsNullOrEmpty(value)) input.Value = value;
        input.Style.Fill.BackgroundColor = ExcelHelpers.MetaYellow;
        input.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ws.Cell(row, 3).Value = hint;
        ws.Cell(row, 3).Style.Font.Italic = true;
        ws.Cell(row, 3).Style.Font.FontColor = XLColor.Gray;
    }

    private sealed record ValidRow(int Row, string Name, DateOnly Dob, string NationalId, string? Gender, string? Address, int? ClassId,
        string ParentName, string Relationship, string Phone, string? ParentEmail, int? ExistingParentId);

    public async Task<StudentImportResult> ImportAsync(Stream stream)
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

            // Hai ô meta bắt buộc, áp dụng cho mọi dòng.
            var year = await ReadYearAsync(ws, errors);
            var campus = await ReadCampusAsync(ws, errors);
            var classes = year is not null && campus is not null
                ? (await db.Class.AsNoTracking().Where(c => c.CampusId == campus.CampusId && c.AcademicYearId == year.AcademicYearId).ToListAsync())
                    .ToDictionary(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase)
                : [];

            var existingIds = await db.Person.Where(p => p.NationalId != null).Select(p => new { p.NationalId, p.PersonCode })
                .ToDictionaryAsync(p => p.NationalId!, p => p.PersonCode);
            var existingParents = new Dictionary<string, (int Id, string Name)>();
            foreach (var p in await db.Person.AsNoTracking().Where(p => p.PersonType == "Parent" && p.PhoneNumber != null)
                         .Select(p => new { p.PersonId, p.FullName, p.PhoneNumber }).ToListAsync())
                existingParents.TryAdd(NormalizePhone(p.PhoneNumber!), (p.PersonId, p.FullName));

            var skipped = new List<StudentImportSkipped>();
            var valid = new List<ValidRow>();
            var fileIds = new Dictionary<string, int>();
            var fileParents = new Dictionary<string, (string Name, int Row)>();

            var lastRow = ws.LastRowUsed()?.RowNumber() ?? HeaderRow;
            for (var row = HeaderRow + 1; row <= lastRow; row++)
            {
                if (Enumerable.Range(1, Headers.Length).All(c => ws.Cell(row, c).IsEmpty())) continue;

                var rowErrors = errors.Count;
                void Err(int col, string reason) =>
                    errors.Add(new ImportError($"Dòng {row}", Headers[col - 1], ws.Cell(row, col).Text(), reason));

                var name = ws.Cell(row, ColName).Text();
                if (name.Length == 0) Err(ColName, "Thiếu họ và tên học sinh. Điền họ và tên đầy đủ.");
                else if (name.Length > 150) Err(ColName, "Họ và tên tối đa 150 ký tự.");

                DateOnly dob = default;
                if (!ws.Cell(row, ColDob).TryReadDate(out dob))
                    Err(ColDob, ws.Cell(row, ColDob).IsEmpty() ? "Thiếu ngày sinh. Điền theo dạng dd/mm/yyyy." : "Ngày sinh sai định dạng. Điền theo dạng dd/mm/yyyy.");
                else if (dob > clock.Today) Err(ColDob, "Ngày sinh ở tương lai. Kiểm tra lại năm sinh.");

                var nationalId = ws.Cell(row, ColNationalId).NationalIdText();
                if (nationalId.Length == 0) Err(ColNationalId, "Thiếu số định danh. Điền đủ 12 chữ số.");
                else if (!Guard.IsNationalId(nationalId)) Err(ColNationalId, "Số định danh phải gồm đúng 12 chữ số.");

                // Khóa nhận diện hợp lệ thì mới tra học sinh đã có; đã có thì bỏ qua cả dòng, không kiểm các cột còn lại.
                if (errors.Count == rowErrors)
                {
                    if (existingIds.TryGetValue(nationalId, out var existingCode))
                    {
                        skipped.Add(new StudentImportSkipped(row, name, $"Đã có hồ sơ {existingCode} cùng số định danh, bỏ qua cả dòng.", existingCode));
                        continue;
                    }
                    if (fileIds.TryGetValue(nationalId, out var firstRow))
                        Err(ColNationalId, $"Trùng số định danh với dòng {firstRow} trong cùng file. Xóa hoặc sửa một trong hai dòng.");
                    else fileIds[nationalId] = row;
                }

                var gender = Genders.Parse(ws.Cell(row, ColGender).Text());
                if (gender == "?") { Err(ColGender, "Giới tính chỉ gồm Nam, Nữ hoặc Khác."); gender = null; }

                var address = ws.Cell(row, ColAddress).Text();
                if (address.Length > 255) Err(ColAddress, "Địa chỉ tối đa 255 ký tự.");

                // Tên lớp để trống thì chỉ tạo hồ sơ, không ghi danh. Hệ thống không tự tạo lớp.
                int? classId = null;
                var className = ws.Cell(row, ColClass).Text();
                if (className.Length > 0 && year is not null && campus is not null)
                {
                    if (!classes.TryGetValue(className, out var cls))
                        Err(ColClass, $"Lớp \"{className}\" không có ở {campus.Name}, năm học {year.Name}. Tạo lớp ở Lớp & Ghi danh trước, hoặc sửa lại tên lớp cho đúng.");
                    else if (!cls.IsActive) Err(ColClass, $"Lớp \"{className}\" đã ngừng hoạt động nên không ghi danh được.");
                    else classId = cls.ClassId;
                }

                var parentName = ws.Cell(row, ColParentName).Text();
                if (parentName.Length == 0) Err(ColParentName, "Thiếu họ và tên phụ huynh. Mỗi học sinh có đúng một phụ huynh đại diện.");
                else if (parentName.Length > 150) Err(ColParentName, "Họ và tên phụ huynh tối đa 150 ký tự.");

                var relationship = ws.Cell(row, ColRelation).Text();
                if (relationship.Length == 0) Err(ColRelation, "Thiếu quan hệ với học sinh. Ví dụ: Bố, Mẹ, Ông, Bà.");
                else if (relationship.Length > 30) Err(ColRelation, "Quan hệ tối đa 30 ký tự.");

                var phone = NormalizePhone(ReadPhone(ws.Cell(row, ColPhone)));
                int? existingParentId = null;
                if (phone.Length == 0) Err(ColPhone, "Thiếu số điện thoại phụ huynh. Đây là khóa nhận diện phụ huynh.");
                else if (!IsPhone(phone)) Err(ColPhone, "Số điện thoại phụ huynh chỉ gồm 9 đến 15 chữ số.");
                else
                {
                    // Cùng số điện thoại thì cùng một phụ huynh: khác họ tên trong cùng file là lỗi (BR-11).
                    if (fileParents.TryGetValue(phone, out var seen))
                    {
                        if (!string.Equals(seen.Name, parentName, StringComparison.OrdinalIgnoreCase))
                            Err(ColPhone, $"Số điện thoại này đã dùng cho phụ huynh \"{seen.Name}\" ở dòng {seen.Row} nhưng dòng này ghi họ tên khác. Cùng số điện thoại phải cùng một phụ huynh.");
                    }
                    else fileParents[phone] = (parentName, row);

                    if (existingParents.TryGetValue(phone, out var existing)) existingParentId = existing.Id;
                }

                var parentEmail = ws.Cell(row, ColParentEmail).Text();
                if (parentEmail.Length > 0 && (!Guard.IsEmail(parentEmail) || parentEmail.Length > 150)) Err(ColParentEmail, "Email chưa đúng định dạng.");

                if (errors.Count == rowErrors)
                    valid.Add(new ValidRow(row, name, dob, nationalId, gender, address.Length > 0 ? address : null, classId,
                        parentName, relationship, phone, parentEmail.Length > 0 ? parentEmail : null, existingParentId));
            }

            if (errors.Count > 0) return Rejected(errors, skipped);

            var students = 0;
            var enrollments = 0;
            var parentsCreated = 0;
            await using var tx = await db.Database.BeginTransactionAsync();
            var newParents = new Dictionary<string, Person>();   // phụ huynh vừa tạo trong file này, dùng lại cho các con cùng số điện thoại
            foreach (var v in valid)
            {
                var student = new Person
                {
                    PersonType = "Student", PersonCode = await PersonCodes.NextAsync(db, PersonCodes.Student),
                    FullName = v.Name, DateOfBirth = v.Dob, NationalId = v.NationalId, Gender = v.Gender, Address = v.Address, CreatedAt = clock.Now,
                };
                var profile = new StudentProfile { Student = student, Status = "Studying", AdmissionDate = year!.StartDate };
                db.Person.Add(student);
                db.StudentProfile.Add(profile);
                students++;

                if (v.ClassId is int classId)
                {
                    db.Enrollment.Add(new Enrollment { Student = profile, ClassId = classId, StartDate = year.StartDate, CreatedAt = clock.Now });
                    enrollments++;
                }

                var link = new ParentStudentLink { Student = profile, Relationship = v.Relationship, IsPrimary = true, CreatedAt = clock.Now };
                if (v.ExistingParentId is int existingParentId) link.ParentId = existingParentId;
                else if (newParents.TryGetValue(v.Phone, out var sharedParent)) link.Parent = sharedParent;
                else
                {
                    var parent = new Person
                    {
                        PersonType = "Parent", PersonCode = await PersonCodes.NextAsync(db, PersonCodes.Parent),
                        FullName = v.ParentName, PhoneNumber = v.Phone, Email = v.ParentEmail, CreatedAt = clock.Now,
                    };
                    db.Person.Add(parent);
                    newParents[v.Phone] = parent;
                    link.Parent = parent;
                    parentsCreated++;
                }
                db.ParentStudentLink.Add(link);
                await db.SaveChangesAsync();   // mỗi dòng một lần lưu để mã hồ sơ tăng đúng thứ tự dòng
            }
            await tx.CommitAsync();
            return new StudentImportResult(true, students, enrollments, parentsCreated, skipped, [], 0);
        }
    }

    private async Task<AcademicYear?> ReadYearAsync(IXLWorksheet ws, List<ImportError> errors)
    {
        var text = ws.Cell(YearRow, 2).Text();
        if (text.Length == 0)
        {
            errors.Add(new ImportError("B3", "Năm học", "", "Thiếu năm học. Điền tên năm học đã khai, ví dụ 2026-2027."));
            return null;
        }
        var year = (await db.AcademicYear.AsNoTracking().ToListAsync()).FirstOrDefault(y => string.Equals(y.Name, text, StringComparison.OrdinalIgnoreCase));
        if (year is null) errors.Add(new ImportError("B3", "Năm học", text, $"Năm học \"{text}\" chưa khai trong hệ thống. Khai ở Thiết lập → Năm học hoặc sửa lại tên cho đúng."));
        return year;
    }

    private async Task<Campus?> ReadCampusAsync(IXLWorksheet ws, List<ImportError> errors)
    {
        var text = ws.Cell(CampusRow, 2).Text();
        if (text.Length == 0)
        {
            errors.Add(new ImportError("B4", "Phân hiệu", "", "Thiếu phân hiệu. Điền đúng tên phân hiệu đã khai trong hệ thống."));
            return null;
        }
        var campus = (await db.Campus.AsNoTracking().ToListAsync()).FirstOrDefault(c => string.Equals(c.Name, text, StringComparison.OrdinalIgnoreCase));
        if (campus is null) errors.Add(new ImportError("B4", "Phân hiệu", text, $"Phân hiệu \"{text}\" chưa khai trong hệ thống. Sửa lại cho đúng tên đã khai."));
        else if (!campus.IsActive) { errors.Add(new ImportError("B4", "Phân hiệu", text, $"Phân hiệu \"{text}\" đã ngừng hoạt động.")); return null; }
        return campus;
    }

    /// <summary>Ô số điện thoại: Excel hay chuyển số có số 0 đầu thành số và làm mất số 0, nên bù lại cho số di động 10 chữ số.</summary>
    private static string ReadPhone(IXLCell cell)
    {
        if (cell.IsEmpty()) return string.Empty;
        if (cell.DataType == XLDataType.Number)
        {
            var d = cell.GetDouble();
            if (d >= 0 && d == Math.Floor(d))
            {
                var digits = ((long)d).ToString();
                return digits.Length == 9 ? "0" + digits : digits;
            }
        }
        return cell.Text();
    }

    /// <summary>Chỉ giữ chữ số (và dấu + đầu), đổi +84 thành 0 để cùng một số không bị hiểu thành hai phụ huynh.</summary>
    private static string NormalizePhone(string raw)
    {
        var s = new string(raw.Where(ch => char.IsAsciiDigit(ch) || ch == '+').ToArray());
        if (s.StartsWith("+84")) s = "0" + s[3..];
        return s;
    }

    private static bool IsPhone(string phone) => phone.Length is >= 9 and <= 15 && phone.All(char.IsAsciiDigit);

    private static StudentImportResult Rejected(List<ImportError> errors, List<StudentImportSkipped> skipped) =>
        new(false, 0, 0, 0, skipped, errors.Take(ExcelHelpers.MaxReportedErrors).ToList(), errors.Count);
}
