using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;

namespace MSMS.Api.Common.Excel;

/// <summary>Một dòng của bảng lỗi import: Ô/Dòng · Cột · Giá trị gốc · Nguyên nhân (Design_Web §9.2).</summary>
public record ImportError(string Location, string Column, string Value, string Reason);

public static class ExcelHelpers
{
    public const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public const int MaxReportedErrors = 300;

    /// <summary>Giá trị ô dạng chuỗi đã bỏ khoảng trắng thừa; ô trống trả về chuỗi rỗng.</summary>
    public static string Text(this IXLCell cell)
    {
        if (cell.IsEmpty()) return string.Empty;
        var raw = cell.DataType == XLDataType.DateTime ? cell.GetDateTime().ToString("dd/MM/yyyy") : cell.GetFormattedString();
        return Common.Text.TextNormalizer.Normalize(raw);
    }

    /// <summary>
    /// Đọc ô số định danh: ô số (Excel tự chuyển 12 chữ số thành số) được đưa về chuỗi 12 chữ số, bù số 0 đầu đã mất.
    /// </summary>
    public static string NationalIdText(this IXLCell cell)
    {
        if (cell.IsEmpty()) return string.Empty;
        if (cell.DataType == XLDataType.Number)
        {
            var d = cell.GetDouble();
            return d >= 0 && d == Math.Floor(d) ? ((long)d).ToString().PadLeft(12, '0') : cell.Text();
        }
        return cell.Text();
    }

    public static string Address(this IXLCell cell) => cell.Address.ToStringRelative();

    public static FileContentResult ToFile(this XLWorkbook workbook, string fileName)
    {
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return new FileContentResult(ms.ToArray(), XlsxContentType) { FileDownloadName = fileName };
    }

    public static void StyleHeader(this IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Fill.BackgroundColor = HeaderBlue;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    // ---- Kiểu dáng chung của các file mẫu (khớp MSMS_Excel_Import_Templates.xlsx) ----
    public static readonly XLColor TitleBlue = XLColor.FromHtml("#1F4E78");
    public static readonly XLColor HeaderBlue = XLColor.FromHtml("#2F75B5");
    public static readonly XLColor NoteBlue = XLColor.FromHtml("#D9EAF7");
    public static readonly XLColor RequiredOrange = XLColor.FromHtml("#FCE4D6");
    public static readonly XLColor MetaYellow = XLColor.FromHtml("#FFF2CC");

    /// <summary>Dải tiêu đề đậm xanh ở hàng 1, gộp ngang hết bảng.</summary>
    public static void WriteTitle(this IXLWorksheet ws, string text, int lastColumn)
    {
        var range = ws.Range(1, 1, 1, lastColumn).Merge();
        ws.Cell(1, 1).Value = text;
        range.Style.Font.Bold = true;
        range.Style.Font.FontSize = 15;
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Fill.BackgroundColor = TitleBlue;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ws.Row(1).Height = 26;
    }

    /// <summary>Dải hướng dẫn xanh nhạt, gộp ngang, tự xuống dòng.</summary>
    public static void WriteNote(this IXLWorksheet ws, int row, string text, int lastColumn, double height)
    {
        var range = ws.Range(row, 1, row, lastColumn).Merge();
        ws.Cell(row, 1).Value = text;
        range.Style.Font.FontSize = 10;
        range.Style.Fill.BackgroundColor = NoteBlue;
        range.Style.Alignment.WrapText = true;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ws.Row(row).Height = height;
    }

    /// <summary>Hàng tiêu đề cột: nền xanh, chữ trắng đậm, viền dưới đậm.</summary>
    public static void StyleHeaderRow(this IXLWorksheet ws, int row, int lastColumn, double height)
    {
        var range = ws.Range(row, 1, row, lastColumn);
        range.Style.Font.Bold = true;
        range.Style.Font.FontSize = 11;
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Fill.BackgroundColor = HeaderBlue;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        range.Style.Alignment.WrapText = true;
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
        ws.Row(row).Height = height;
    }

    /// <summary>Chọn sheet theo tên mẫu; không có thì lấy sheet đầu tiên. Người dùng có thể tải lên cả bộ mẫu nhiều sheet.</summary>
    public static IXLWorksheet? FindSheet(this XLWorkbook workbook, string preferredName) =>
        workbook.Worksheets.FirstOrDefault(w => string.Equals(w.Name, preferredName, StringComparison.OrdinalIgnoreCase))
        ?? workbook.Worksheets.FirstOrDefault();


    /// <summary>Đọc ngày dd/MM/yyyy từ ô kiểu Date hoặc Text (quy tắc chung của file import).</summary>
    public static bool TryReadDate(this IXLCell cell, out DateOnly date)
    {
        date = default;
        if (cell.IsEmpty()) return false;
        if (cell.DataType == XLDataType.DateTime)
        {
            date = DateOnly.FromDateTime(cell.GetDateTime());
            return true;
        }
        return DateOnly.TryParseExact(cell.Text(), ["dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy"],
            System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out date);
    }
}
