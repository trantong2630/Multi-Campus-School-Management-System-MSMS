using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Excel;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Ppl.Dtos;

namespace MSMS.Api.Modules.Ppl.Services;

/// <summary>
/// Import phụ huynh (BR-11): validate TOÀN BỘ file rồi ghi trong một transaction; có một lỗi là không ghi gì.
/// Số điện thoại là khóa nhận diện: số đã có thì bỏ qua cả dòng, import không bao giờ cập nhật hồ sơ cũ. Chỉ tạo hồ sơ, không tạo tài khoản
/// và không liên kết học sinh (liên kết làm ở import học sinh hoặc "Đổi phụ huynh đại diện").
/// </summary>
public sealed class GuardianImportService(MsmsDbContext db, IClock clock)
{
    private const int HeaderRow = 4;
    private static readonly string[] Headers = ["Họ và tên", "Số điện thoại", "Giới tính", "Email", "Địa chỉ"];
    private static readonly int[] RequiredColumns = [1, 2];
    private const string SheetName = "04_Phu_huynh";
    private const int InputRows = 200;
    private static readonly double[] ColumnWidths = [26, 16, 10, 28, 40];

    public FileContentResult BuildTemplate()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet(SheetName);
        ws.ShowGridLines = false;
        var last = Headers.Length;

        ws.WriteTitle("MSMS — MẪU IMPORT PHỤ HUYNH", last);
        ws.WriteNote(2,
            "Bắt buộc (ô tô cam): Họ và tên, Số điện thoại. Tùy chọn: Giới tính, Email, Địa chỉ. "
            + "KHÔNG có cột mã hồ sơ — hệ thống tự sinh mã PH. Số điện thoại gồm 9 đến 15 chữ số, duy nhất giữa các phụ huynh, dùng để nhận diện người đã có "
            + "(nên định dạng cột là Text để giữ số 0 đầu). Số đã có thì bỏ qua cả dòng. Import chỉ tạo hồ sơ, không tạo tài khoản và không liên kết học sinh.",
            last, 40);

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

        ws.Range(HeaderRow + 1, 2, HeaderRow + InputRows, 2).Style.NumberFormat.Format = "@";
        ws.SheetView.FreezeRows(HeaderRow);
        return wb.ToFile("MSMS_Mau_Import_Phu_huynh.xlsx");
    }

    public async Task<GuardianImportResult> ImportAsync(Stream stream)
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

            var existing = new Dictionary<string, string>();
            foreach (var p in await db.Person.AsNoTracking().Where(p => p.PersonType == "Parent" && p.PhoneNumber != null)
                         .Select(p => new { p.PersonCode, p.PhoneNumber }).ToListAsync())
                existing.TryAdd(StudentImportService.NormalizePhone(p.PhoneNumber!), p.PersonCode);

            var skipped = new List<GuardianImportSkipped>();
            var valid = new List<(string Name, string Phone, string? Gender, string? Email, string? Address)>();
            var filePhones = new Dictionary<string, int>();

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

                var phone = StudentImportService.NormalizePhone(StudentImportService.ReadPhone(ws.Cell(row, 2)));
                if (phone.Length == 0) Err(2, "Thiếu số điện thoại. Đây là khóa nhận diện phụ huynh.");
                else if (!StudentImportService.IsPhone(phone)) Err(2, "Số điện thoại chỉ gồm 9 đến 15 chữ số.");

                // Khóa nhận diện hợp lệ thì mới tra người đã có; người đã có thì bỏ qua cả dòng, không kiểm các cột còn lại.
                if (errors.Count == rowErrors)
                {
                    if (existing.TryGetValue(phone, out var existingCode))
                    {
                        skipped.Add(new GuardianImportSkipped(row, name, $"Đã có hồ sơ {existingCode} cùng số điện thoại, bỏ qua cả dòng.", existingCode));
                        continue;
                    }
                    if (filePhones.TryGetValue(phone, out var firstRow))
                        Err(2, $"Trùng số điện thoại với dòng {firstRow} trong cùng file. Xóa hoặc sửa một trong hai dòng.");
                    else filePhones[phone] = row;
                }

                var gender = Genders.Parse(ws.Cell(row, 3).Text());
                if (gender == "?") { Err(3, "Giới tính chỉ gồm Nam, Nữ hoặc Khác."); gender = null; }

                var email = ws.Cell(row, 4).Text();
                if (email.Length > 0 && (!Guard.IsEmail(email) || email.Length > 150)) Err(4, "Email chưa đúng định dạng.");

                var address = ws.Cell(row, 5).Text();
                if (address.Length > 255) Err(5, "Địa chỉ tối đa 255 ký tự.");

                if (errors.Count == rowErrors)
                    valid.Add((name, phone, gender, email.Length > 0 ? email : null, address.Length > 0 ? address : null));
            }

            if (errors.Count > 0) return Rejected(errors, skipped);
            if (valid.Count == 0) return new GuardianImportResult(true, 0, skipped, [], 0);

            await using var tx = await db.Database.BeginTransactionAsync();
            foreach (var v in valid)
            {
                db.Person.Add(new Person
                {
                    PersonType = "Parent", PersonCode = await PersonCodes.NextAsync(db, PersonCodes.Parent),
                    FullName = v.Name, PhoneNumber = v.Phone, Gender = v.Gender, Email = v.Email, Address = v.Address, CreatedAt = clock.Now,
                });
                await db.SaveChangesAsync();   // mỗi người một lần lưu để mã hồ sơ tăng đúng thứ tự dòng
            }
            await tx.CommitAsync();
            return new GuardianImportResult(true, valid.Count, skipped, [], 0);
        }
    }

    private static GuardianImportResult Rejected(List<ImportError> errors, List<GuardianImportSkipped> skipped) =>
        new(false, 0, skipped, errors.Take(ExcelHelpers.MaxReportedErrors).ToList(), errors.Count);
}