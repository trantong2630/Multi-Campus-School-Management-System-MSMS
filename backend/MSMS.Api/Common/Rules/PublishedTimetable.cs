using Microsoft.EntityFrameworkCore;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;

namespace MSMS.Api.Common.Rules;

/// <summary>BR-14: phụ trách chuyên môn lấy từ phiên bản Đã phát hành mới nhất của từng lớp.</summary>
public static class PublishedTimetable
{
    /// <summary>
    /// Chọn phiên bản trước khi lọc giáo viên, môn hoặc phân hiệu. Lần phát hành chỉ có một số lớp
    /// không thay thế thời khóa biểu của các lớp khác; bản Nháp không tạo phụ trách chuyên môn.
    /// </summary>
    public static IQueryable<TimetableEntry> LatestEntries(MsmsDbContext db, int semesterId) =>
        db.TimetableEntry.AsNoTracking().Where(e => e.VersionId == db.TimetableVersionClass
            .Where(vc => vc.ClassId == e.ClassId && vc.Version.Status == "Published" && vc.Version.SemesterId == semesterId)
            .OrderByDescending(vc => vc.Version.PublishedAt).ThenByDescending(vc => vc.VersionId)
            .Select(vc => (int?)vc.VersionId).FirstOrDefault());
}
