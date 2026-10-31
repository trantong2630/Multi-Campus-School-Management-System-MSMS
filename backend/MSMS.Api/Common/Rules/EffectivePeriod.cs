using System.Linq.Expressions;
using MSMS.Api.Common.Errors;

namespace MSMS.Api.Data.Entities
{
    /// <summary>Bản ghi có khoảng hiệu lực (BR-09): trách nhiệm, phụ trách chuyên môn, ghi danh.</summary>
    public interface IEffectivePeriod
    {
        DateOnly StartDate { get; set; }
        DateOnly? EndDate { get; set; }
    }

    /// <summary>Ba loại trách nhiệm: phụ trách phân hiệu, tổ trưởng, chủ nhiệm. Mỗi đích tối đa một bản đang hiệu lực.</summary>
    public interface IResponsibilityRecord : IEffectivePeriod
    {
        int StaffId { get; set; }
        int? AssignedByPersonId { get; set; }
    }

    public partial class CampusLeadership : IResponsibilityRecord { }
    public partial class DepartmentHead : IResponsibilityRecord { }
    public partial class HomeroomAssignment : IResponsibilityRecord { }
    public partial class Enrollment : IEffectivePeriod { }
}

namespace MSMS.Api.Common.Rules
{
    using MSMS.Api.Data.Entities;

    /// <summary>
    /// Quy tắc khoảng hiệu lực dùng chung: ngày bắt đầu ≤ D và (ngày kết thúc trống hoặc ≥ D).
    /// Mọi nơi cần "đang hiệu lực" phải gọi qua đây, không tự viết lại điều kiện.
    /// </summary>
    public static class EffectivePeriod
    {
        public static bool IsEffectiveAt(this IEffectivePeriod p, DateOnly date) =>
            p.StartDate <= date && (p.EndDate == null || p.EndDate >= date);

        /// <summary>Biểu thức cho EF để lọc bản ghi hiệu lực tại một ngày.</summary>
        public static Expression<Func<T, bool>> EffectiveAt<T>(DateOnly date) where T : class, IEffectivePeriod =>
            x => x.StartDate <= date && (x.EndDate == null || x.EndDate >= date);

        /// <summary>
        /// Kiểm quyền hiện tại của trách nhiệm (BR-09): kết thúc hôm nay thì quyền đã mất ngay,
        /// khác tra cứu lịch sử theo ngày dùng đầu mút kết thúc bao gồm ngày đó.
        /// </summary>
        public static Expression<Func<T, bool>> CurrentResponsibility<T>(DateOnly today) where T : class, IResponsibilityRecord =>
            x => x.StartDate <= today && (x.EndDate == null || x.EndDate > today);

        /// <summary>
        /// Thay người trong một transaction: bản cũ kết thúc vào NGÀY LIỀN TRƯỚC ngày hiệu lực của bản mới,
        /// nên hai khoảng không chạm nhau ngày nào (BR-09).
        /// </summary>
        public static void CloseBefore(IEffectivePeriod current, DateOnly newStart)
        {
            if (newStart <= current.StartDate)
                throw ApiException.BadRequest(
                    $"Ngày hiệu lực phải sau ngày bắt đầu của người đang đảm nhiệm ({current.StartDate:dd/MM/yyyy}). Chọn ngày muộn hơn.");
            current.EndDate = newStart.AddDays(-1);
        }
    }
}
