using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Scd.Services;

namespace MSMS.Api.Modules.Lrn.Services;

/// <summary>
/// Tầng 2 của kiểm quyền LRN (BR-01): người gọi có đang phụ trách chuyên môn ở (lớp, môn) không.
/// Phạm vi luôn tính tại thời điểm gọi, không lưu: giáo viên bị đổi phụ trách thì mất quyền ngay lần gọi kế tiếp.
/// Sau khi giao, bài chỉ còn đường đọc nên không có quyền "ghi" ở đây. Tổ trưởng không xem bài làm hay điểm từng học sinh;
/// việc giao bài của tổ chỉ có số tổng hợp ở Tình hình tổ chuyên môn (ADM-01).
/// </summary>
public sealed class LrnAccess(MsmsDbContext db, CurrentUser user, SubjectResponsibilityService responsibility)
{
    public Task<bool> IsResponsibleAsync(int classId, int subjectId) =>
        responsibility.IsResponsibleAsync(user.PersonId, classId, subjectId);

    /// <summary>
    /// Tải bài để xem kết quả: chỉ giáo viên đang phụ trách chuyên môn ở (lớp, môn) của bài.
    /// Không có quyền xem thì báo không tìm thấy để không lộ bài của lớp khác.
    /// </summary>
    public async Task<Assignment> LoadForViewAsync(int assignmentId)
    {
        var a = await db.Assignment.AsNoTracking().Include(x => x.Class).Include(x => x.Subject)
            .FirstOrDefaultAsync(x => x.AssignmentId == assignmentId)
            ?? throw ApiException.NotFound("Không tìm thấy bài tập.");
        if (await IsResponsibleAsync(a.ClassId, a.SubjectId)) return a;
        throw ApiException.NotFound("Không tìm thấy bài tập.");
    }

    public Task<bool> IsLinkedParentAsync(int studentId) =>
        db.ParentStudentLink.AsNoTracking().AnyAsync(l => l.ParentId == user.PersonId && l.StudentId == studentId);

    /// <summary>Học sinh mà người gọi xem kết quả: học sinh xem của chính mình, phụ huynh xem của con được liên kết.</summary>
    public async Task<int> ResolveStudentAsync(int? studentId)
    {
        if (user.AccountType == AccountTypes.Student) return user.PersonId;
        if (user.AccountType == AccountTypes.Parent)
        {
            if (studentId is null) throw ApiException.BadRequest("Chọn học sinh cần xem.");
            if (!await IsLinkedParentAsync(studentId.Value))
                throw ApiException.Forbidden("Chỉ xem được kết quả của con đã liên kết với tài khoản.");
            return studentId.Value;
        }
        throw ApiException.Forbidden("Không có quyền xem kết quả bài tập.");
    }
}
