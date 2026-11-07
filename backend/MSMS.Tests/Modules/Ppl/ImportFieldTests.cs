using ClosedXML.Excel;
using MSMS.Api.Common.Excel;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Ppl.Services;

namespace MSMS.Tests.Modules.Ppl;

/// <summary>Kết quả chạy một lần import trong test: qua validate (dừng ở bước ghi vì cần SQL Server) hoặc bị từ chối kèm danh sách lỗi.</summary>
internal sealed record ImportOutcome(bool Passed, bool Accepted, IReadOnlyList<ImportError> Errors, int TotalErrors, int Skipped, string? SkippedCode)
{
    public bool HasError(string column, string reasonPart) => Errors.Any(e => e.Column == column && e.Reason.Contains(reasonPart, StringComparison.OrdinalIgnoreCase));
}

internal static class ImportRun
{
    public static async Task<ImportOutcome> Staff(PplEnv e, Action<IXLWorksheet>? tamper = null, params string[][] rows)
    {
        var svc = new StaffImportService(e.Db, e.Clock);
        using var wb = new XLWorkbook(new MemoryStream(svc.BuildTemplate().FileContents));
        var ws = wb.Worksheet(1);
        Fill(ws, 5, rows);
        tamper?.Invoke(ws);
        var ms = new MemoryStream(); wb.SaveAs(ms); ms.Position = 0;
        try
        {
            var r = await svc.ImportAsync(ms);
            return new(r.Accepted, r.Accepted, r.Errors, r.TotalErrors, r.Skipped.Count, r.Skipped.FirstOrDefault()?.PersonCode);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Relational")) { return new(true, false, [], 0, 0, null); }
    }

    public static async Task<ImportOutcome> Student(PplEnv e, Action<IXLWorksheet>? tamper = null, params string[][] rows)
    {
        var svc = new StudentImportService(e.Db, e.Clock);
        using var wb = new XLWorkbook(new MemoryStream((await svc.BuildTemplateAsync(null, null)).FileContents));
        var ws = wb.Worksheet(1);
        ws.Cell(3, 2).Value = "2026-2027";
        ws.Cell(4, 2).Value = "Phân hiệu 1";
        Fill(ws, 7, rows);
        tamper?.Invoke(ws);
        var ms = new MemoryStream(); wb.SaveAs(ms); ms.Position = 0;
        try
        {
            var r = await svc.ImportAsync(ms);
            return new(r.Accepted, r.Accepted, r.Errors, r.TotalErrors, r.Skipped.Count, r.Skipped.FirstOrDefault()?.PersonCode);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Relational")) { return new(true, false, [], 0, 0, null); }
    }

    private static void Fill(IXLWorksheet ws, int firstRow, string[][] rows)
    {
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                if (rows[r][c].Length > 0) ws.Cell(firstRow + r, c + 1).Value = rows[r][c];
    }

    /// <summary>Thêm cột lớp ngừng hoạt động để thử "lớp đã ngừng hoạt động".</summary>
    public static void AddInactiveClass(PplEnv e)
    {
        e.Db.Class.Add(new Class { ClassId = 60, CampusId = PplEnv.Campus1, AcademicYearId = PplEnv.Year1, Name = "Cũ", GradeLevel = 6, IsActive = false, CreatedAt = e.Clock.Now });
        e.Db.SaveChanges();
    }
}

/// <summary>Import cán bộ từ Excel, kiểm từng cột của file mẫu (PPL-01, 4.3.8-4.3.11, BR-10, BR-11).</summary>
public class StaffImportFieldTests
{
    // Cột: 1 Họ tên | 2 Ngày sinh | 3 Giới tính | 4 SĐT | 5 Email | 6 Tổ | 7 Phân hiệu chính | 8 Viết tắt | 9 Số định danh
    private static string[] Row(params (int Col, string Value)[] change)
    {
        var row = new[] { "Nguyễn Văn A", "01/01/1990", "Nam", "0912345678", "a@school.vn", "Tổ Toán", "Phân hiệu 1", "NVA", "222222222222" };
        foreach (var (col, value) in change) row[col - 1] = value;
        return row;
    }

    [Fact(DisplayName = "PPL | Import cán bộ | Dòng hợp lệ | mọi cột hợp lệ qua được validate")]
    public async Task Valid_Row_Passes()
    {
        using var e = new PplEnv();
        Assert.True((await ImportRun.Staff(e, null, Row())).Passed);
    }

    [Fact(DisplayName = "PPL | Import cán bộ | Dòng hợp lệ | chỉ điền 4 cột bắt buộc (họ tên, ngày sinh, phân hiệu, số định danh) vẫn qua")]
    public async Task Only_Required_Columns_Pass()
    {
        using var e = new PplEnv();
        var row = new[] { "Trần Thị B", "05/05/1985", "", "", "", "", "Phân hiệu 1", "", "333333333333" };
        Assert.True((await ImportRun.Staff(e, null, row)).Passed);
    }

    [Theory(DisplayName = "PPL | Import cán bộ | Từng cột | giá trị sai bị từ chối kèm đúng cột và nguyên nhân")]
    [InlineData(1, "", "Họ và tên", "Thiếu họ và tên")]
    [InlineData(2, "", "Ngày sinh", "Thiếu ngày sinh")]
    [InlineData(2, "abc", "Ngày sinh", "sai định dạng")]
    [InlineData(2, "1990-01-01", "Ngày sinh", "sai định dạng")]
    [InlineData(2, "31/12/2099", "Ngày sinh", "tương lai")]
    [InlineData(2, "01/01/2020", "Ngày sinh", "18 đến 75")]
    [InlineData(2, "01/01/1900", "Ngày sinh", "18 đến 75")]
    [InlineData(3, "robot", "Giới tính", "Nam, Nữ hoặc Khác")]
    [InlineData(5, "abc", "Email", "chưa đúng định dạng")]
    [InlineData(5, "a@b", "Email", "chưa đúng định dạng")]
    [InlineData(6, "Tổ không có", "Tổ chuyên môn", "chưa khai")]
    [InlineData(7, "", "Phân hiệu chính", "Thiếu phân hiệu")]
    [InlineData(7, "Phân hiệu X", "Phân hiệu chính", "chưa khai")]
    [InlineData(7, "Phân hiệu cũ", "Phân hiệu chính", "ngừng hoạt động")]
    [InlineData(8, "N-V", "Viết tắt GV", "chỉ gồm chữ và số")]
    [InlineData(8, "N V", "Viết tắt GV", "chỉ gồm chữ và số")]
    [InlineData(8, "ABC", "Viết tắt GV", "đã được cán bộ khác dùng")]
    [InlineData(9, "", "Số định danh", "Thiếu số định danh")]
    [InlineData(9, "12345", "Số định danh", "12 chữ số")]
    [InlineData(9, "12345678901a", "Số định danh", "12 chữ số")]
    public async Task Column_Invalid(int col, string value, string column, string reason)
    {
        using var e = new PplEnv();
        var o = await ImportRun.Staff(e, null, Row((col, value)));
        Assert.False(o.Passed);
        Assert.True(o.HasError(column, reason), string.Join(" | ", o.Errors.Select(x => $"{x.Column}: {x.Reason}")));
    }

    [Fact(DisplayName = "PPL | Import cán bộ | Độ dài | họ tên 150/151, SĐT 20/21, viết tắt 10/11, email 150/151 ký tự")]
    public async Task Length_Boundaries()
    {
        using var e = new PplEnv();
        Assert.True((await ImportRun.Staff(e, null, Row((1, PplEnv.Text(150)), (4, new string('1', 20)), (8, new string('A', 10)), (5, new string('a', 140) + "@school.vn")))).Passed);
        var o = await ImportRun.Staff(e, null, Row((1, PplEnv.Text(151)), (4, new string('1', 21)), (8, new string('A', 11)), (5, new string('a', 141) + "@school.vn")));
        Assert.True(o.HasError("Họ và tên", "150")); Assert.True(o.HasError("Số điện thoại", "20"));
        Assert.True(o.HasError("Viết tắt GV", "10")); Assert.True(o.HasError("Email", "định dạng"));
    }

    [Fact(DisplayName = "PPL | Import cán bộ | Tên tổ và phân hiệu | không phân biệt hoa thường và khoảng trắng thừa")]
    public async Task Names_CaseInsensitive()
    {
        using var e = new PplEnv();
        Assert.True((await ImportRun.Staff(e, null, Row((6, "  tổ   TOÁN "), (7, "phân HIỆU 1")))).Passed);
    }

    [Fact(DisplayName = "PPL | Import cán bộ | Tổ chuyên môn | tổ đã ngừng hoạt động bị báo lỗi ở đúng ô")]
    public async Task Department_Inactive_Rejected()
    {
        using var e = new PplEnv();
        var o = await ImportRun.Staff(e, null, Row((6, "Tổ cũ")));
        Assert.False(o.Passed);
        Assert.True(o.HasError("Tổ chuyên môn", "ngừng hoạt động"));
    }

    [Fact(DisplayName = "PPL | Import cán bộ | Số định danh | người đã có (trùng số) bị bỏ qua cả dòng kèm mã hồ sơ, không báo lỗi")]
    public async Task ExistingNid_Skipped()
    {
        using var e = new PplEnv();
        var o = await ImportRun.Staff(e, null, Row((9, PplEnv.ExistingNid), (1, "Tên khác hoàn toàn"), (7, "Phân hiệu không có")));
        Assert.True(o.Accepted);
        Assert.Equal(1, o.Skipped);
        Assert.Equal("CB000010", o.SkippedCode);   // không kiểm các cột còn lại vì hồ sơ cũ không bao giờ bị cập nhật
    }

    [Fact(DisplayName = "PPL | Import cán bộ | Trùng trong file | hai dòng cùng số định danh hoặc cùng viết tắt bị báo lỗi ở dòng sau")]
    public async Task Duplicates_InsideFile()
    {
        using var e = new PplEnv();
        var o = await ImportRun.Staff(e, null, Row(), Row((1, "Người khác")));
        Assert.True(o.HasError("Số định danh", "dòng 5"));
        Assert.True(o.HasError("Viết tắt GV", "dòng 5"));
        Assert.All(o.Errors, er => Assert.Equal("Dòng 6", er.Location));
    }

    [Fact(DisplayName = "PPL | Import cán bộ | Trùng họ tên và ngày sinh | hai người cùng họ tên, ngày sinh nhưng khác số định danh là hai hồ sơ")]
    public async Task SameNameAndDob_DifferentNid_Allowed()
    {
        using var e = new PplEnv();
        Assert.True((await ImportRun.Staff(e, null, Row(), Row((9, "333333333333"), (8, "NVB")))).Passed);
    }

    [Fact(DisplayName = "PPL | Import cán bộ | Nhiều lỗi | một dòng nhiều cột sai thì báo đủ từng cột, một ô sai là không ghi gì")]
    public async Task MultipleErrors_AllReported()
    {
        using var e = new PplEnv();
        var o = await ImportRun.Staff(e, null, Row((1, ""), (2, "abc"), (9, "1")));
        Assert.False(o.Passed);
        Assert.True(o.Errors.Count >= 3);
        Assert.Empty(e.Db.Person.Where(p => p.NationalId == "222222222222"));
    }

    [Fact(DisplayName = "PPL | Import cán bộ | Nhiều lỗi | bảng lỗi chỉ hiện tối đa 300 dòng nhưng tổng số lỗi vẫn đúng")]
    public async Task ErrorReport_CappedAt300()
    {
        using var e = new PplEnv();
        var rows = Enumerable.Range(0, 320).Select(i => new[] { "", "", "", "", "", "", "", "", $"{i:D12}" }).ToArray();
        var o = await ImportRun.Staff(e, null, rows);
        Assert.False(o.Passed);
        Assert.Equal(300, o.Errors.Count);
        Assert.True(o.TotalErrors > 300);
    }

    [Fact(DisplayName = "PPL | Import cán bộ | Hàng tiêu đề | sửa tiêu đề cột bị từ chối cả file")]
    public async Task Header_Tampered_Rejected()
    {
        using var e = new PplEnv();
        var o = await ImportRun.Staff(e, ws => ws.Cell(4, 3).Value = "Phái", Row());
        Assert.False(o.Passed);
        Assert.True(o.HasError("Giới tính", "Hàng tiêu đề bị sửa"));
    }

    [Fact(DisplayName = "PPL | Import cán bộ | File | file không phải .xlsx bị từ chối với thông báo rõ")]
    public async Task NotAnXlsx_Rejected()
    {
        using var e = new PplEnv();
        var ex = await Assert.ThrowsAsync<MSMS.Api.Common.Errors.ApiException>(() => new StaffImportService(e.Db, e.Clock).ImportAsync(new MemoryStream("không phải excel"u8.ToArray())));
        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("Không đọc được file", ex.Message);
    }

    [Fact(DisplayName = "PPL | Import cán bộ | File | file mẫu chưa điền dòng nào được chấp nhận, tạo 0 hồ sơ")]
    public async Task EmptyTemplate_CreatesNothing()
    {
        using var e = new PplEnv();
        var o = await ImportRun.Staff(e);
        Assert.True(o.Accepted);
    }

    [Fact(DisplayName = "PPL | Import cán bộ | File mẫu | mẫu có đúng 9 cột cố định, cột bắt buộc tô cam, cột số định danh là Text")]
    public void Template_Layout()
    {
        using var e = new PplEnv();
        using var wb = new XLWorkbook(new MemoryStream(new StaffImportService(e.Db, e.Clock).BuildTemplate().FileContents));
        var ws = wb.Worksheet("02_Can_bo");
        Assert.Equal(["Họ và tên", "Ngày sinh", "Giới tính", "Số điện thoại", "Email", "Tổ chuyên môn", "Phân hiệu chính", "Viết tắt GV", "Số định danh"],
            Enumerable.Range(1, 9).Select(c => ws.Cell(4, c).GetString()));
        Assert.Equal("@", ws.Cell(5, 9).Style.NumberFormat.Format);
    }
}

/// <summary>Import học sinh kèm ghi danh và phụ huynh, kiểm từng cột và hai ô meta (PPL-02, 4.3.16-4.3.18, BR-10, BR-11).</summary>
public class StudentImportFieldTests
{
    // Cột: 1 Họ tên | 2 Ngày sinh | 3 Giới tính | 4 Địa chỉ | 5 Tên lớp | 6 PH họ tên | 7 PH quan hệ | 8 PH SĐT | 9 PH email | 10 Số định danh
    private static string[] Row(params (int Col, string Value)[] change)
    {
        var row = new[] { "Trần Văn B", "20/05/2014", "Nam", "12 Lạch Tray", "6A", "Trần Văn C", "Bố", "0900000099", "c@gmail.com", "444444444444" };
        foreach (var (col, value) in change) row[col - 1] = value;
        return row;
    }

    [Fact(DisplayName = "PPL | Import học sinh | Dòng hợp lệ | mọi cột hợp lệ qua được validate")]
    public async Task Valid_Row_Passes()
    {
        using var e = new PplEnv();
        Assert.True((await ImportRun.Student(e, null, Row())).Passed);
    }

    [Fact(DisplayName = "PPL | Import học sinh | Tên lớp | để trống thì chỉ tạo hồ sơ, không ghi danh")]
    public async Task Class_Blank_Passes()
    {
        using var e = new PplEnv();
        Assert.True((await ImportRun.Student(e, null, Row((5, "")))).Passed);
    }

    [Theory(DisplayName = "PPL | Import học sinh | Từng cột | giá trị sai bị từ chối kèm đúng cột và nguyên nhân")]
    [InlineData(1, "", "Họ và tên", "Thiếu họ và tên")]
    [InlineData(2, "", "Ngày sinh", "Thiếu ngày sinh")]
    [InlineData(2, "abc", "Ngày sinh", "sai định dạng")]
    [InlineData(2, "31/12/2099", "Ngày sinh", "tương lai")]
    [InlineData(2, "01/01/1990", "Ngày sinh", "5 đến 25")]
    [InlineData(2, "01/01/2025", "Ngày sinh", "5 đến 25")]
    [InlineData(3, "robot", "Giới tính", "Nam, Nữ hoặc Khác")]
    [InlineData(5, "9Z", "Tên lớp", "không có ở")]
    [InlineData(5, "Cũ", "Tên lớp", "ngừng hoạt động")]
    [InlineData(6, "", "Phụ huynh — Họ và tên", "Thiếu họ và tên phụ huynh")]
    [InlineData(7, "", "Phụ huynh — Quan hệ", "Thiếu quan hệ")]
    [InlineData(8, "", "Phụ huynh — Số điện thoại", "Thiếu số điện thoại")]
    [InlineData(8, "12345678", "Phụ huynh — Số điện thoại", "9 đến 15")]
    [InlineData(8, "abc", "Phụ huynh — Số điện thoại", "Thiếu số điện thoại")]
    [InlineData(9, "abc", "Phụ huynh — Email", "chưa đúng định dạng")]
    [InlineData(10, "", "Số định danh", "Thiếu số định danh")]
    [InlineData(10, "12345", "Số định danh", "12 chữ số")]
    [InlineData(10, "12345678901a", "Số định danh", "12 chữ số")]
    public async Task Column_Invalid(int col, string value, string column, string reason)
    {
        using var e = new PplEnv();
        ImportRun.AddInactiveClass(e);
        var o = await ImportRun.Student(e, null, Row((col, value)));
        Assert.False(o.Passed);
        Assert.True(o.HasError(column, reason), string.Join(" | ", o.Errors.Select(x => $"{x.Column}: {x.Reason}")));
    }

    [Fact(DisplayName = "PPL | Import học sinh | Độ dài | họ tên 150/151, địa chỉ 255/256, họ tên PH 150/151, quan hệ 30/31 ký tự")]
    public async Task Length_Boundaries()
    {
        using var e = new PplEnv();
        Assert.True((await ImportRun.Student(e, null, Row((1, PplEnv.Text(150)), (4, PplEnv.Text(255)), (6, PplEnv.Text(150)), (7, PplEnv.Text(30))))).Passed);
        var o = await ImportRun.Student(e, null, Row((1, PplEnv.Text(151)), (4, PplEnv.Text(256)), (6, PplEnv.Text(151)), (7, PplEnv.Text(31))));
        Assert.True(o.HasError("Họ và tên", "150")); Assert.True(o.HasError("Địa chỉ", "255"));
        Assert.True(o.HasError("Phụ huynh — Họ và tên", "150")); Assert.True(o.HasError("Phụ huynh — Quan hệ", "30"));
    }

    [Fact(DisplayName = "PPL | Import học sinh | Số điện thoại phụ huynh | ô số mất số 0 đầu (912345678) được khôi phục thành 0912345678")]
    public async Task Phone_NumericCell_LeadingZeroRestored()
    {
        using var e = new PplEnv();
        var o = await ImportRun.Student(e, ws => ws.Cell(7, 8).Value = 912345678, Row());
        Assert.True(o.Passed);
    }

    [Theory(DisplayName = "PPL | Import học sinh | Số điện thoại phụ huynh | viết +84 hoặc có dấu cách vẫn nhận ra phụ huynh đã có")]
    [InlineData("+84912345678")] [InlineData("091 234 5678")]
    public async Task Phone_Formats_Accepted(string phone)
    {
        using var e = new PplEnv();
        Assert.True((await ImportRun.Student(e, null, Row((8, phone)))).Passed);
    }

    [Fact(DisplayName = "PPL | Import học sinh | Phụ huynh dùng chung | hai con cùng số điện thoại và cùng họ tên phụ huynh là một phụ huynh (hợp lệ)")]
    public async Task SiblingsShareGuardian_Passes()
    {
        using var e = new PplEnv();
        Assert.True((await ImportRun.Student(e, null, Row(), Row((1, "Trần Thị D"), (10, "555555555555")))).Passed);
    }

    [Fact(DisplayName = "PPL | Import học sinh | Phụ huynh dùng chung | cùng số điện thoại nhưng khác họ tên phụ huynh trong cùng file bị báo lỗi")]
    public async Task SamePhone_DifferentName_InFile_Error()
    {
        using var e = new PplEnv();
        var o = await ImportRun.Student(e, null, Row(), Row((1, "Trần Thị D"), (10, "555555555555"), (6, "Người khác hẳn")));
        Assert.True(o.HasError("Phụ huynh — Số điện thoại", "họ tên khác"));
    }

    [Fact(DisplayName = "PPL | Import học sinh | Phụ huynh đã có | số điện thoại đã có nhưng ghi họ tên khác vẫn được chấp nhận và dùng lại hồ sơ cũ (CHƯA cảnh báo lệch họ tên)")]
    public async Task ExistingGuardian_DifferentName_CurrentlyAccepted()
    {
        using var e = new PplEnv();
        Assert.True((await ImportRun.Student(e, null, Row((8, "0912345678"), (6, "Tên hoàn toàn khác")))).Passed);
    }

    [Fact(DisplayName = "PPL | Import học sinh | Số định danh | học sinh đã có bị bỏ qua cả dòng kể cả khi đổi lớp, nêu mã hồ sơ")]
    public async Task ExistingNid_Skipped_EvenIfClassChanges()
    {
        using var e = new PplEnv();
        e.Db.Person.First(p => p.PersonId == PplEnv.StudentA).NationalId = "666666666666"; e.Db.SaveChanges();
        var o = await ImportRun.Student(e, null, Row((10, "666666666666"), (5, "9A")));
        Assert.True(o.Accepted);
        Assert.Equal(1, o.Skipped);
        Assert.Equal("HS000101", o.SkippedCode);
    }

    [Fact(DisplayName = "PPL | Import học sinh | Số định danh | trùng số trong cùng file bị báo lỗi, hai em cùng họ tên và ngày sinh khác số định danh là hai hồ sơ")]
    public async Task Nid_DuplicateInFile_And_SameNameDiffNid()
    {
        using var e = new PplEnv();
        var dup = await ImportRun.Student(e, null, Row(), Row());
        Assert.True(dup.HasError("Số định danh", "dòng 7"));
        Assert.True((await ImportRun.Student(e, null, Row(), Row((10, "555555555555")))).Passed);
    }

    // ---------- Hai ô meta ----------
    [Fact(DisplayName = "PPL | Import học sinh | Ô meta Năm học | để trống hoặc năm chưa khai bị từ chối")]
    public async Task Meta_Year_Invalid()
    {
        using var e = new PplEnv();
        var blank = await ImportRun.Student(e, ws => ws.Cell(3, 2).Clear(), Row());
        Assert.True(blank.HasError("Năm học", "Thiếu năm học"));
        var unknown = await ImportRun.Student(e, ws => ws.Cell(3, 2).Value = "1999-2000", Row());
        Assert.True(unknown.HasError("Năm học", "chưa khai"));
    }

    [Fact(DisplayName = "PPL | Import học sinh | Ô meta Phân hiệu | để trống, chưa khai hoặc đã ngừng hoạt động bị từ chối")]
    public async Task Meta_Campus_Invalid()
    {
        using var e = new PplEnv();
        Assert.True((await ImportRun.Student(e, ws => ws.Cell(4, 2).Clear(), Row())).HasError("Phân hiệu", "Thiếu phân hiệu"));
        Assert.True((await ImportRun.Student(e, ws => ws.Cell(4, 2).Value = "Phân hiệu X", Row())).HasError("Phân hiệu", "chưa khai"));
        Assert.True((await ImportRun.Student(e, ws => ws.Cell(4, 2).Value = "Phân hiệu cũ", Row())).HasError("Phân hiệu", "ngừng hoạt động"));
    }

    [Fact(DisplayName = "PPL | Import học sinh | Lớp theo phân hiệu | tên lớp tra theo phân hiệu và năm học của ô meta, lớp phân hiệu khác không nhận")]
    public async Task Class_IsScopedToMetaCampusAndYear()
    {
        using var e = new PplEnv();
        SchTestSupportAddOtherCampus(e);
        var o = await ImportRun.Student(e, null, Row((5, "7X")));   // lớp 7X chỉ có ở phân hiệu 3
        Assert.True(o.HasError("Tên lớp", "không có ở"));
    }

    private static void SchTestSupportAddOtherCampus(PplEnv e)
    {
        e.Db.Campus.Add(new Campus { CampusId = 3, Code = "PH3", Name = "Phân hiệu 3", IsActive = true, CreatedAt = e.Clock.Now });
        e.Db.Class.Add(new Class { ClassId = 70, CampusId = 3, AcademicYearId = PplEnv.Year1, Name = "7X", GradeLevel = 7, IsActive = true, CreatedAt = e.Clock.Now });
        e.Db.SaveChanges();
    }

    [Fact(DisplayName = "PPL | Import học sinh | Hàng tiêu đề | sửa tiêu đề cột bị từ chối cả file")]
    public async Task Header_Tampered_Rejected()
    {
        using var e = new PplEnv();
        var o = await ImportRun.Student(e, ws => ws.Cell(6, 5).Value = "Lớp", Row());
        Assert.False(o.Passed);
        Assert.True(o.HasError("Tên lớp", "Hàng tiêu đề bị sửa"));
    }

    [Fact(DisplayName = "PPL | Import học sinh | File | file không phải .xlsx bị từ chối với thông báo rõ")]
    public async Task NotAnXlsx_Rejected()
    {
        using var e = new PplEnv();
        var ex = await Assert.ThrowsAsync<MSMS.Api.Common.Errors.ApiException>(() => new StudentImportService(e.Db, e.Clock).ImportAsync(new MemoryStream("không phải excel"u8.ToArray())));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact(DisplayName = "PPL | Import học sinh | File mẫu | mẫu có đúng 10 cột cố định, điền sẵn năm học và phân hiệu khi mở từ màn Lớp & Ghi danh")]
    public async Task Template_Layout_AndPrefilledMeta()
    {
        using var e = new PplEnv();
        using var wb = new XLWorkbook(new MemoryStream((await new StudentImportService(e.Db, e.Clock).BuildTemplateAsync(PplEnv.Year1, PplEnv.Campus1)).FileContents));
        var ws = wb.Worksheet("03_Hoc_sinh");
        Assert.Equal("2026-2027", ws.Cell(3, 2).GetString());
        Assert.Equal("Phân hiệu 1", ws.Cell(4, 2).GetString());
        Assert.Equal(10, Enumerable.Range(1, 10).Count(c => ws.Cell(6, c).GetString().Length > 0));
    }
}