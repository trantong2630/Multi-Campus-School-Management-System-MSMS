using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;

namespace MSMS.Api.Common.Auth;

/// <summary>
/// IAM 4.1.3: tính lại phạm vi từ trách nhiệm, thời khóa biểu đã phát hành, ghi danh và liên kết.
/// Không lưu phạm vi vào token để thay đổi trách nhiệm có hiệu lực ngay ở request kế tiếp.
/// </summary>
public sealed class AccountScopeResolver(MsmsDbContext db, IClock clock)
{
    public async Task<AccountScope> ResolveAsync(int personId, string accountType)
    {
        var today = clock.Today;
        IReadOnlyList<int> ledCampuses = [], headDepartments = [], homeroomClasses = [], linkedStudents = [];
        IReadOnlyList<SubjectScope> subjects = [];
        IReadOnlyList<EnrollmentScope> enrollments = [];

        switch (accountType)
        {
            case AccountTypes.VicePrincipal:
                ledCampuses = await db.CampusLeadership.AsNoTracking()
                    .Where(EffectivePeriod.CurrentResponsibility<CampusLeadership>(today))
                    .Where(x => x.StaffId == personId)
                    .Select(x => x.CampusId).Distinct().OrderBy(x => x).ToListAsync();
                break;
            case AccountTypes.Teacher:
                headDepartments = await db.DepartmentHead.AsNoTracking()
                    .Where(EffectivePeriod.CurrentResponsibility<DepartmentHead>(today))
                    .Where(x => x.StaffId == personId)
                    .Select(x => x.DepartmentId).Distinct().OrderBy(x => x).ToListAsync();
                homeroomClasses = await db.HomeroomAssignment.AsNoTracking()
                    .Where(EffectivePeriod.CurrentResponsibility<HomeroomAssignment>(today))
                    .Where(x => x.StaffId == personId)
                    .Select(x => x.ClassId).Distinct().OrderBy(x => x).ToListAsync();
                subjects = await SubjectResponsibilitiesAsync(personId, today);
                break;
            case AccountTypes.Student:
                enrollments = await EnrollmentsAsync([personId], today);
                break;
            case AccountTypes.Parent:
                linkedStudents = await db.ParentStudentLink.AsNoTracking()
                    .Where(x => x.ParentId == personId)
                    .Select(x => x.StudentId).Distinct().OrderBy(x => x).ToListAsync();
                enrollments = await EnrollmentsAsync(linkedStudents, today);
                break;
        }

        return new AccountScope(today, accountType is AccountTypes.SchoolAdmin or AccountTypes.Principal,
            ledCampuses, headDepartments, homeroomClasses, subjects, enrollments, linkedStudents);
    }

    private async Task<List<EnrollmentScope>> EnrollmentsAsync(IReadOnlyList<int> studentIds, DateOnly today)
    {
        if (studentIds.Count == 0) return [];
        return await db.Enrollment.AsNoTracking()
            .Where(EnrollmentRules.ActiveAt(today))
            .Where(e => studentIds.Contains(e.StudentId))
            .OrderBy(e => e.StudentId).ThenBy(e => e.EnrollmentId)
            .Select(e => new EnrollmentScope(e.StudentId, e.EnrollmentId, e.ClassId, e.Class.CampusId, e.Class.AcademicYearId))
            .ToListAsync();
    }

    private async Task<List<SubjectScope>> SubjectResponsibilitiesAsync(int teacherId, DateOnly today)
    {
        // Ngoài học kỳ hiện hành thì không lấy kỳ đã qua hay kỳ sắp tới để cấp quyền.
        var semesterId = await db.Semester.AsNoTracking()
            .Where(s => s.AcademicYear.IsCurrent && s.StartDate <= today && s.EndDate >= today)
            .Select(s => (int?)s.SemesterId).FirstOrDefaultAsync();
        if (semesterId is null) return [];

        var entries = await PublishedTimetable.LatestEntries(db, semesterId.Value)
            .Where(e => e.TeacherId == teacherId)
            .Select(e => new { e.ClassId, e.SubjectId }).Distinct()
            .OrderBy(e => e.ClassId).ThenBy(e => e.SubjectId).ToListAsync();

        return entries
            .Select(e => new SubjectScope(e.ClassId, e.SubjectId, semesterId.Value))
            .ToList();
    }
}
