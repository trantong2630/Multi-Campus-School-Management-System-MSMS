using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Scd.Dtos;

namespace MSMS.Api.Modules.Scd.Services;

/// <summary>
/// SCD-01: sửa bản Nháp trước khi Phát hành (BR-12) — chuyển hoặc đổi chỗ tiết, thêm tiết, đổi giáo viên, xóa tiết,
/// khôi phục về file import. Chỉ sửa phiên bản đang Nháp và chỉ trong các lớp của phiên bản. Thao tác gây xung đột
/// (BR-13) vẫn được lưu; xung đột chỉ chặn Phát hành. Mỗi thao tác lưu ngay, không đụng lịch dạy, không thông báo, không Audit.
/// </summary>
public sealed class TimetableDraftService(MsmsDbContext db, ScopeResolver scope, ScheduleQueryService query)
{
    public async Task<DraftOptionsDto> OptionsAsync(int campusId)
    {
        await scope.EnsureCanManageCampusAsync(campusId);
        var subjects = await db.Subject.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Abbreviation)
            .Select(s => new SubjectOptionDto(s.SubjectId, s.Abbreviation, s.Name)).ToListAsync();
        var teachers = await db.StaffProfile.AsNoTracking()
            .Where(t => t.IsActive && t.TeacherAbbr != null && t.Staff.Account != null
                        && t.Staff.Account.AccountType == AccountTypes.Teacher && t.Staff.Account.Status != AccountStatuses.Locked)
            .OrderBy(t => t.Staff.FullName).Select(t => new TeacherOptionDto(t.StaffId, t.Staff.FullName, t.TeacherAbbr)).ToListAsync();
        return new DraftOptionsDto(subjects, teachers);
    }

    /// <summary>Thả vào ô trống thì chuyển tiết, thả vào ô có tiết thì đổi chỗ hai tiết. Chỉ trong cột của cùng một lớp.</summary>
    public async Task<VersionGridDto> MoveAsync(int versionId, MoveEntryRequest r)
    {
        var version = await LoadDraftAsync(versionId, r.ClassId);
        await EnsureSlotAsync(r.ToDayOfWeek, r.ToPeriodSlotId);
        var entries = await db.TimetableEntry.Where(e => e.VersionId == versionId && e.ClassId == r.ClassId).ToListAsync();

        var from = Find(entries, r.FromDayOfWeek, r.FromPeriodSlotId) ?? throw ApiException.BadRequest("Ô nguồn không có tiết.");
        if (r.FromDayOfWeek != r.ToDayOfWeek || r.FromPeriodSlotId != r.ToPeriodSlotId)
        {
            var to = Find(entries, r.ToDayOfWeek, r.ToPeriodSlotId);
            if (to is null)
            {
                from.DayOfWeek = (byte)r.ToDayOfWeek;
                from.PeriodSlotId = r.ToPeriodSlotId;
            }
            else
            {
                // Đổi môn và giáo viên giữa hai ô thay vì đổi vị trí, để không vướng chỉ mục duy nhất (phiên bản, lớp, thứ, khung giờ).
                (from.SubjectId, to.SubjectId) = (to.SubjectId, from.SubjectId);
                (from.TeacherId, to.TeacherId) = (to.TeacherId, from.TeacherId);
            }
            await db.SaveChangesAsync();
        }
        return await query.GetVersionGridAsync(version.VersionId);
    }

    public async Task<VersionGridDto> AddAsync(int versionId, AddEntryRequest r)
    {
        await LoadDraftAsync(versionId, r.ClassId);
        await EnsureSlotAsync(r.DayOfWeek, r.PeriodSlotId);
        if (await db.TimetableEntry.AnyAsync(e => e.VersionId == versionId && e.ClassId == r.ClassId
                                                  && e.DayOfWeek == r.DayOfWeek && e.PeriodSlotId == r.PeriodSlotId))
            throw ApiException.BadRequest("Ô này đã có tiết. Đổi giáo viên hoặc xóa tiết trước.");
        if (!await db.Subject.AnyAsync(s => s.SubjectId == r.SubjectId && s.IsActive))
            throw ApiException.BadRequest("Môn không tồn tại hoặc đã ngừng dùng.");
        await EnsureTeacherAsync(r.TeacherId);

        db.TimetableEntry.Add(new TimetableEntry
        {
            VersionId = versionId, ClassId = r.ClassId, DayOfWeek = (byte)r.DayOfWeek, PeriodSlotId = r.PeriodSlotId,
            SubjectId = r.SubjectId, TeacherId = r.TeacherId,
        });
        await db.SaveChangesAsync();
        return await query.GetVersionGridAsync(versionId);
    }

    public async Task<VersionGridDto> ChangeTeacherAsync(int versionId, ChangeTeacherRequest r)
    {
        await LoadDraftAsync(versionId, r.ClassId);
        var entry = await db.TimetableEntry.FirstOrDefaultAsync(e => e.VersionId == versionId && e.ClassId == r.ClassId
                                                                     && e.DayOfWeek == r.DayOfWeek && e.PeriodSlotId == r.PeriodSlotId)
                    ?? throw ApiException.BadRequest("Ô này không có tiết.");
        await EnsureTeacherAsync(r.TeacherId);
        entry.TeacherId = r.TeacherId;
        await db.SaveChangesAsync();
        return await query.GetVersionGridAsync(versionId);
    }

    public async Task<VersionGridDto> RemoveAsync(int versionId, int classId, int dayOfWeek, int periodSlotId)
    {
        await LoadDraftAsync(versionId, classId);
        var entry = await db.TimetableEntry.FirstOrDefaultAsync(e => e.VersionId == versionId && e.ClassId == classId
                                                                     && e.DayOfWeek == dayOfWeek && e.PeriodSlotId == periodSlotId)
                    ?? throw ApiException.BadRequest("Ô này không có tiết.");
        db.TimetableEntry.Remove(entry);
        await db.SaveChangesAsync();
        return await query.GetVersionGridAsync(versionId);
    }

    /// <summary>Đưa cả bản Nháp về đúng nội dung file import.</summary>
    public async Task<VersionGridDto> ResetAsync(int versionId)
    {
        var version = await db.TimetableVersion.FirstOrDefaultAsync(v => v.VersionId == versionId)
                      ?? throw ApiException.NotFound("Không tìm thấy phiên bản thời khóa biểu.");
        await scope.EnsureCanManageCampusAsync(version.CampusId);
        EnsureDraft(version);
        var snapshot = DraftSnapshot.Parse(version.ImportSnapshot)
                       ?? throw ApiException.BadRequest("Phiên bản này không lưu nội dung file import nên không khôi phục được. Import lại file.");

        await using var tx = await db.Database.BeginTransactionAsync();
        await db.TimetableEntry.Where(e => e.VersionId == versionId).ExecuteDeleteAsync();
        db.TimetableEntry.AddRange(snapshot.Select(x => new TimetableEntry
        {
            VersionId = versionId, ClassId = x.ClassId, DayOfWeek = (byte)x.DayOfWeek, PeriodSlotId = x.PeriodSlotId,
            SubjectId = x.SubjectId, TeacherId = x.TeacherId,
        }));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return await query.GetVersionGridAsync(versionId);
    }

    // ---------------------------------------------------------------- Kiểm tra dùng chung
    private static TimetableEntry? Find(List<TimetableEntry> entries, int day, int slotId) =>
        entries.FirstOrDefault(e => e.DayOfWeek == day && e.PeriodSlotId == slotId);

    private static void EnsureDraft(TimetableVersion version)
    {
        if (version.Status != "Draft")
            throw ApiException.BadRequest("Phiên bản này đã phát hành nên không sửa được. Muốn đổi lịch đang chạy thì dùng điều chỉnh lịch dạy, hoặc import file mới.");
    }

    private async Task<TimetableVersion> LoadDraftAsync(int versionId, int classId)
    {
        var version = await db.TimetableVersion.AsNoTracking().FirstOrDefaultAsync(v => v.VersionId == versionId)
                      ?? throw ApiException.NotFound("Không tìm thấy phiên bản thời khóa biểu.");
        await scope.EnsureCanManageCampusAsync(version.CampusId);
        EnsureDraft(version);
        if (!await db.TimetableVersionClass.AnyAsync(c => c.VersionId == versionId && c.ClassId == classId))
            throw ApiException.BadRequest("Lớp này không có trong phiên bản. Chỉ sửa được các lớp có trong file đã import.");
        return version;
    }

    private async Task EnsureSlotAsync(int dayOfWeek, int periodSlotId)
    {
        if (dayOfWeek is < 2 or > 7) throw ApiException.BadRequest("Thứ không hợp lệ.");
        if (!await db.PeriodSlot.AnyAsync(p => p.PeriodSlotId == periodSlotId && p.IsActive))
            throw ApiException.BadRequest("Khung giờ tiết không tồn tại hoặc đã ngừng dùng.");
    }

    /// <summary>Giáo viên được xếp phải đủ điều kiện như lúc import: có viết tắt, tài khoản Teacher không bị khóa (BR-06).</summary>
    private async Task EnsureTeacherAsync(int teacherId)
    {
        var teacher = await db.StaffProfile.AsNoTracking().Include(t => t.Staff).ThenInclude(p => p.Account)
                          .FirstOrDefaultAsync(t => t.StaffId == teacherId)
                      ?? throw ApiException.BadRequest("Không tìm thấy giáo viên.");
        if (teacher.TeacherAbbr is null) throw ApiException.BadRequest("Giáo viên chưa có viết tắt. Gán viết tắt ở Hồ sơ → Cán bộ.");
        if (TimetableImportService.TeacherProblem(teacher) is { } problem)
            throw ApiException.BadRequest($"Giáo viên {teacher.TeacherAbbr} không đủ điều kiện dạy: {problem}");
    }
}
