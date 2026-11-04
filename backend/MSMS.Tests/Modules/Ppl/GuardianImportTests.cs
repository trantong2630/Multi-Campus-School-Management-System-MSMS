using ClosedXML.Excel;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Ppl.Services;
using MSMS.Tests.Modules.Iam;

namespace MSMS.Tests.Modules.Ppl;

/// <summary>Import phụ huynh (BR-11): một lỗi là không ghi gì, số điện thoại đã có thì bỏ qua cả dòng, tiêu đề mẫu không được sửa.</summary>
public class GuardianImportTests
{
    private static (GuardianImportService svc, AccountTestFixture f) Setup()
    {
        var f = new AccountTestFixture();
        f.Db.Person.Add(new Person { PersonId = 50, PersonType = "Parent", PersonCode = "PH000050", FullName = "Nguyễn Thị Lan",
            PhoneNumber = "0912345678", CreatedAt = f.Clock.Now });
        f.Db.SaveChanges();
        return (new GuardianImportService(f.Db, f.Clock), f);
    }

    /// <summary>Mở file mẫu thật, điền các dòng (họ tên, SĐT, giới tính, email, địa chỉ) từ hàng 5 rồi trả về luồng để import.</summary>
    private static MemoryStream Workbook(GuardianImportService svc, Action<IXLWorksheet>? tamper = null, params string[][] rows)
    {
        using var wb = new XLWorkbook(new MemoryStream(svc.BuildTemplate().FileContents));
        var ws = wb.Worksheet(1);
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                ws.Cell(5 + r, c + 1).Value = rows[r][c];
        tamper?.Invoke(ws);
        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public void Template_has_the_fixed_header_row_and_phone_column_as_text()
    {
        var (svc, _) = Setup();
        using var wb = new XLWorkbook(new MemoryStream(svc.BuildTemplate().FileContents));
        var ws = wb.Worksheet("04_Phu_huynh");
        Assert.Equal(["Họ và tên", "Số điện thoại", "Giới tính", "Email", "Địa chỉ"], Enumerable.Range(1, 5).Select(c => ws.Cell(4, c).GetString()));
        Assert.Equal("@", ws.Cell(5, 2).Style.NumberFormat.Format);
    }

    [Fact]
    public async Task One_bad_row_rejects_the_whole_file_and_reports_the_cell()
    {
        var (svc, f) = Setup();
        var file = Workbook(svc, null,
            ["Trần Văn Bình", "0987654321", "Nam", "binh@school.vn", "Hà Nội"],
            ["", "12", "robot", "khong-phai-email", ""]);

        var result = await svc.ImportAsync(file);

        Assert.False(result.Accepted);
        Assert.Equal(0, result.Created);
        Assert.Contains(result.Errors, e => e.Location == "Dòng 6" && e.Column == "Họ và tên");
        Assert.Contains(result.Errors, e => e.Location == "Dòng 6" && e.Column == "Số điện thoại");
        Assert.DoesNotContain(result.Errors, e => e.Location == "Dòng 5");
        Assert.Single(f.Db.Person);   // không ghi gì
    }

    [Fact]
    public async Task Gender_and_email_errors_are_reported_when_the_phone_is_valid()
    {
        var (svc, _) = Setup();
        var file = Workbook(svc, null, ["Lê Văn C", "0911111111", "robot", "khong-phai-email", ""]);

        var result = await svc.ImportAsync(file);

        Assert.False(result.Accepted);
        Assert.Contains(result.Errors, e => e.Column == "Giới tính");
        Assert.Contains(result.Errors, e => e.Column == "Email");
    }

    [Fact]
    public async Task Duplicate_phone_inside_the_file_is_an_error()
    {
        var (svc, _) = Setup();
        var file = Workbook(svc, null,
            ["Phạm A", "0900000001", "", "", ""],
            ["Phạm B", "0900000001", "", "", ""]);

        var result = await svc.ImportAsync(file);

        Assert.False(result.Accepted);
        Assert.Contains(result.Errors, e => e.Location == "Dòng 6" && e.Column == "Số điện thoại" && e.Reason.Contains("dòng 5"));
    }

    [Fact]
    public async Task An_existing_phone_is_skipped_with_its_code_and_nothing_is_updated()
    {
        var (svc, f) = Setup();
        // Cùng số nhưng viết khác kiểu (+84) và tên khác: vẫn là hồ sơ cũ, bỏ qua cả dòng.
        var file = Workbook(svc, null, ["Tên Khác Hẳn", "+84912345678", "Nam", "x@school.vn", "Địa chỉ mới"]);

        var result = await svc.ImportAsync(file);

        Assert.True(result.Accepted);
        Assert.Equal(0, result.Created);
        var skipped = Assert.Single(result.Skipped);
        Assert.Equal("PH000050", skipped.PersonCode);
        var parent = f.Db.Person.Single();
        Assert.Equal("Nguyễn Thị Lan", parent.FullName);   // hồ sơ cũ không bị cập nhật
        Assert.Null(parent.Address);
    }

    [Fact]
    public async Task Editing_the_header_row_rejects_the_file()
    {
        var (svc, _) = Setup();
        var file = Workbook(svc, ws => ws.Cell(4, 2).Value = "SĐT", ["Trần Văn Bình", "0987654321", "", "", ""]);

        var result = await svc.ImportAsync(file);

        Assert.False(result.Accepted);
        Assert.Contains(result.Errors, e => e.Reason.Contains("Hàng tiêu đề bị sửa"));
    }
}