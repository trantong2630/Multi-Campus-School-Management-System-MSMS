using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Sch.Dtos;

namespace MSMS.Api.Modules.Sch.Services;

/// <summary>
/// SCH-06: kết chuyển năm học (BR-09). Gợi ý và tạo lớp năm mới từ lớp năm cũ, rồi chuyển nguyên học sinh của lớp.
/// Không tự xét lên lớp hay lưu ban; không sao chép thời khóa biểu, phân công hay trách nhiệm; không sửa dữ liệu năm cũ.
/// Kết chuyển không vào Audit (người thực hiện và thời điểm đã nằm trên bản ghi nghiệp vụ).
/// </summary>
public sealed class RolloverService(MsmsDbContext db, IClock clock)
{
    private const string Studying = "Studying";

    /// <summary>
    /// 4.2.35: mỗi lớp đang hoạt động của năm cũ kèm lớp tương ứng ở năm mới (nếu có), tên và khối gợi ý, sĩ số cuối năm,
    /// và số học sinh đã có ghi danh năm mới. Lớp tương ứng là lớp được tạo từ nó, hoặc lớp cùng phân hiệu có đúng tên gợi ý và khối mới.
    /// </summary>
    public async Task<List<RolloverRowDto>> OverviewAsync(int fromYearId, int toYearId, int? campusId)
    {
        var (from, to) = await LoadYearsAsync(fromYearId, toYearId);
        var sources = await db.Class.AsNoTracking().Include(c => c.Campus)
            .Where(c => c.AcademicYearId == from.AcademicYearId && c.IsActive && (campusId == null || c.CampusId == campusId))
            .OrderBy(c => c.Campus.Name).ThenBy(c => c.GradeLevel).ThenBy(c => c.Name)
            .ToListAsync();
        var targets = await db.Class.AsNoTracking()
            .Where(c => c.AcademicYearId == to.AcademicYearId && (campusId == null || c.CampusId == campusId))
            .ToListAsync();

        // Học sinh còn học ở ngày cuối năm cũ: ghi danh còn hiệu lực ngày đó (BR-09) và hồ sơ đang ở trạng thái Đang học.
        var onLastDay = db.Enrollment.AsNoTracking().Where(EnrollmentRules.ActiveAt(from.EndDate))
            .Where(e => e.Class.AcademicYearId == from.AcademicYearId && e.Student.Status == Studying);
        var eligible = await onLastDay.GroupBy(e => e.ClassId).Select(g => new { ClassId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClassId, x => x.Count);
        var already = await onLastDay
            .Where(e => db.Enrollment.Any(n => n.StudentId == e.StudentId && n.Class.AcademicYearId == to.AcademicYearId))
            .GroupBy(e => e.ClassId).Select(g => new { ClassId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClassId, x => x.Count);

        return sources.Select(s =>
        {
            var nextGrade = RolloverRules.NextGrade(s.GradeLevel);
            var suggestedName = nextGrade is byte g ? RolloverRules.SuggestName(s.Name, s.GradeLevel, g) : s.Name;
            var target = targets.FirstOrDefault(t => t.CopiedFromClassId == s.ClassId)
                         ?? (nextGrade is byte ng
                             ? targets.FirstOrDefault(t => t.CampusId == s.CampusId && t.GradeLevel == ng
                                                           && string.Equals(t.Name, suggestedName, StringComparison.OrdinalIgnoreCase))
                             : null);
            return new RolloverRowDto(s.ClassId, s.Name, s.CampusId, s.Campus.Name, s.GradeLevel,
                eligible.GetValueOrDefault(s.ClassId), already.GetValueOrDefault(s.ClassId), suggestedName, nextGrade,
                target is null ? null : new RolloverTargetDto(target.ClassId, target.Name, target.GradeLevel, target.IsActive));
        }).ToList();
    }

    /// <summary>
    /// 4.2.36: tạo lớp đã chốt cho năm mới trong một lần lưu (có lỗi là không tạo lớp nào). Mỗi lớp mới thuộc cùng phân hiệu với lớp gốc
    /// và ghi nhớ lớp gốc. KHÔNG sao chép thời khóa biểu, chủ nhiệm hay trách nhiệm.
    /// </summary>
    public async Task<RolloverCreateResult> CreateClassesAsync(RolloverCreateRequest r)
    {
        var (from, to) = await LoadYearsAsync(r.FromYearId, r.ToYearId);
        if (r.Items is not { Count: > 0 }) throw ApiException.BadRequest("Chọn ít nhất một lớp để tạo.");

        var sourceIds = r.Items.Select(i => i.SourceClassId).ToList();
        if (sourceIds.Distinct().Count() != sourceIds.Count)
            throw ApiException.BadRequest("Một lớp năm cũ xuất hiện hai lần trong danh sách. Mỗi lớp cũ chỉ tạo một lớp mới.");
        var sources = await db.Class.Include(c => c.Campus).Where(c => sourceIds.Contains(c.ClassId)).ToDictionaryAsync(c => c.ClassId);

        var existing = await db.Class.AsNoTracking().Where(c => c.AcademicYearId == to.AcademicYearId)
            .Select(c => new { c.CampusId, c.Name, c.CopiedFromClassId }).ToListAsync();
        var taken = existing.Select(e => $"{e.CampusId}|{e.Name}").ToHashSet(StringComparer.OrdinalIgnoreCase);
        var alreadyCopied = existing.Where(e => e.CopiedFromClassId != null).Select(e => e.CopiedFromClassId!.Value).ToHashSet();

        var created = new List<Class>();
        foreach (var item in r.Items)
        {
            if (!sources.TryGetValue(item.SourceClassId, out var source) || source.AcademicYearId != from.AcademicYearId)
                throw ApiException.BadRequest($"Có lớp không thuộc năm học {from.Name}. Tải lại danh sách rồi chọn lại.");
            if (alreadyCopied.Contains(source.ClassId))
                throw ApiException.BadRequest($"Lớp {source.Name} đã có lớp tương ứng ở năm học {to.Name}. Bỏ lớp này khỏi danh sách.");
            if (!source.Campus.IsActive)
                throw ApiException.BadRequest($"{source.Campus.Name} đã ngừng hoạt động nên không tạo lớp mới được.");

            var name = Guard.Required(item.Name, "Tên lớp", 50);
            if (item.GradeLevel is < 6 or > 9) throw ApiException.BadRequest($"Lớp \"{name}\": khối chỉ từ 6 đến 9.");
            if (!taken.Add($"{source.CampusId}|{name}"))
                throw ApiException.BadRequest(
                    $"Lớp \"{name}\" đã có hoặc bị trùng trong danh sách ở {source.Campus.Name}, năm học {to.Name}. Tên lớp phải duy nhất trong phạm vi đó, đổi tên khác.");

            created.Add(new Class
            {
                CampusId = source.CampusId, AcademicYearId = to.AcademicYearId, Name = name, GradeLevel = item.GradeLevel,
                IsActive = true, CopiedFromClassId = source.ClassId, CreatedAt = clock.Now,
            });
        }

        db.Class.AddRange(created);
        await db.SaveChangesAsync();   // một lần lưu = một transaction
        return new RolloverCreateResult(created.Count, created.Select(c =>
            new ClassDto(c.ClassId, c.CampusId, sources[c.CopiedFromClassId!.Value].Campus.Name, to.AcademicYearId, to.Name, c.Name, c.GradeLevel, true, 0, null)).ToList());
    }

    /// <summary>
    /// 4.2.37: chuyển nguyên học sinh của lớp năm cũ sang lớp năm mới, tất cả các lớp trong một transaction. Chỉ chuyển học sinh còn học ở ngày
    /// cuối năm cũ; ghi danh mới có hiệu lực từ ngày bắt đầu năm mới. Học sinh đã có ghi danh ở năm mới thì bỏ qua nên chạy lại không tạo trùng.
    /// Ghi danh năm cũ không bị đụng tới (đã tự hết hiệu lực ở cuối năm học). Hệ thống không tự xét lên lớp hay lưu ban.
    /// </summary>
    public async Task<RolloverMoveResult> MoveStudentsAsync(RolloverMoveRequest r)
    {
        if (r.Pairs is not { Count: > 0 }) throw ApiException.BadRequest("Chọn ít nhất một lớp để chuyển học sinh.");
        if (r.Pairs.Select(p => p.SourceClassId).Distinct().Count() != r.Pairs.Count)
            throw ApiException.BadRequest("Một lớp năm cũ xuất hiện hai lần trong danh sách chuyển.");

        var ids = r.Pairs.SelectMany(p => new[] { p.SourceClassId, p.TargetClassId }).Distinct().ToList();
        var classes = await db.Class.Include(c => c.AcademicYear).Where(c => ids.Contains(c.ClassId)).ToDictionaryAsync(c => c.ClassId);
        foreach (var p in r.Pairs)
        {
            if (!classes.TryGetValue(p.SourceClassId, out var s) || !classes.TryGetValue(p.TargetClassId, out var t))
                throw ApiException.BadRequest("Có lớp không tồn tại. Tải lại danh sách rồi chọn lại.");
            if (s.ClassId == t.ClassId) throw ApiException.BadRequest($"Lớp {s.Name}: lớp chuyển đến trùng lớp nguồn.");
            if (!t.IsActive) throw ApiException.BadRequest($"Lớp {t.Name} đã ngừng hoạt động nên không chuyển học sinh vào được.");
            if (t.AcademicYear.StartDate <= s.AcademicYear.StartDate)
                throw ApiException.BadRequest($"Lớp {t.Name} (năm học {t.AcademicYear.Name}) không thuộc năm học sau năm học {s.AcademicYear.Name} của lớp {s.Name}.");
        }

        var results = new List<RolloverMoveItemResult>();
        await using var tx = await db.Database.BeginTransactionAsync();
        foreach (var p in r.Pairs)
        {
            var source = classes[p.SourceClassId];
            var target = classes[p.TargetClassId];
            var toYear = target.AcademicYear;

            var students = await db.Enrollment.Where(EnrollmentRules.ActiveAt(source.AcademicYear.EndDate))
                .Where(e => e.ClassId == source.ClassId && e.Student.Status == Studying)
                .Select(e => e.StudentId).Distinct().ToListAsync();
            var alreadyEnrolled = await db.Enrollment
                .Where(e => students.Contains(e.StudentId) && e.Class.AcademicYearId == toYear.AcademicYearId)
                .Select(e => e.StudentId).Distinct().ToListAsync();
            var toMove = students.Except(alreadyEnrolled).ToList();

            db.Enrollment.AddRange(toMove.Select(id => new Enrollment
            {
                StudentId = id, ClassId = target.ClassId, StartDate = toYear.StartDate, CreatedAt = clock.Now,
            }));
            await db.SaveChangesAsync();   // lưu từng lớp để lớp sau thấy ghi danh vừa tạo
            results.Add(new RolloverMoveItemResult(source.ClassId, target.ClassId, toMove.Count, alreadyEnrolled.Count));
        }
        await tx.CommitAsync();
        return new RolloverMoveResult(results.Sum(x => x.Moved), results.Sum(x => x.Skipped), results);
    }

    /// <summary>
    /// Học sinh còn học của một lớp năm cũ tại ngày cuối năm cũ, kèm lớp năm mới em đã được xếp (để hiện ở cột giữa
    /// và biết em nào chưa xếp lớp).
    /// </summary>
    public async Task<List<RolloverStudentDto>> StudentsAsync(int fromClassId, int toYearId)
    {
        var source = await db.Class.AsNoTracking().Include(c => c.AcademicYear).FirstOrDefaultAsync(c => c.ClassId == fromClassId)
                     ?? throw ApiException.NotFound("Không tìm thấy lớp.");
        var (_, to) = await LoadYearsAsync(source.AcademicYearId, toYearId);

        var students = await db.Enrollment.AsNoTracking().Where(EnrollmentRules.ActiveAt(source.AcademicYear.EndDate))
            .Where(e => e.ClassId == fromClassId && (e.Student.Status == Studying || (e.Student.Status == "Graduated" && e.Reason == EnrollmentCloseReasons.Graduated)))
            .Select(e => new { e.StudentId, e.Student.Student.PersonCode, e.Student.Student.FullName, Graduated = e.Student.Status == "Graduated", e.Student.Student.DateOfBirth, e.Student.Student.Gender })
            .Distinct().ToListAsync();
        var ids = students.Select(s => s.StudentId).ToList();
        var placed = await db.Enrollment.AsNoTracking()
            .Where(e => ids.Contains(e.StudentId) && e.Class.AcademicYearId == to.AcademicYearId)
            .Select(e => new { e.StudentId, e.ClassId, ClassName = e.Class.Name })
            .ToListAsync();
        var byStudent = placed.GroupBy(p => p.StudentId).ToDictionary(g => g.Key, g => g.First());

        return students.OrderBy(s => s.FullName).Select(s => byStudent.TryGetValue(s.StudentId, out var p)
            ? new RolloverStudentDto(s.StudentId, s.PersonCode, s.FullName, p.ClassId, p.ClassName, s.Graduated, s.DateOfBirth, s.Gender)
            : new RolloverStudentDto(s.StudentId, s.PersonCode, s.FullName, null, null, s.Graduated, s.DateOfBirth, s.Gender)).ToList();
    }

    /// <summary>
    /// Chuyển các học sinh được chọn của một lớp năm cũ sang một lớp năm mới (lên lớp hoặc lưu ban do người dùng tự chọn lớp đích).
    /// Chỉ nhận học sinh còn học ở ngày cuối năm cũ trong lớp nguồn; em đã có ghi danh năm mới thì bỏ qua nên chạy lại không tạo trùng.
    /// </summary>
    public async Task<RolloverMoveResult> MoveSelectedAsync(RolloverStudentsMoveRequest r)
    {
        if (r.StudentIds is not { Count: > 0 }) throw ApiException.BadRequest("Chọn ít nhất một học sinh để chuyển.");
        var classes = await db.Class.Include(c => c.AcademicYear).Where(c => c.ClassId == r.FromClassId || c.ClassId == r.ToClassId)
            .ToDictionaryAsync(c => c.ClassId);
        if (!classes.TryGetValue(r.FromClassId, out var source) || !classes.TryGetValue(r.ToClassId, out var target))
            throw ApiException.BadRequest("Có lớp không tồn tại. Tải lại danh sách rồi chọn lại.");
        if (source.ClassId == target.ClassId) throw ApiException.BadRequest("Lớp chuyển đến trùng lớp nguồn.");
        if (!target.IsActive) throw ApiException.BadRequest($"Lớp {target.Name} đã ngừng hoạt động nên không chuyển học sinh vào được.");
        if (target.AcademicYear.StartDate <= source.AcademicYear.StartDate)
            throw ApiException.BadRequest($"Lớp {target.Name} (năm học {target.AcademicYear.Name}) không thuộc năm học sau năm học {source.AcademicYear.Name}.");

        var wanted = r.StudentIds.Distinct().ToList();
        var eligible = await db.Enrollment.Where(EnrollmentRules.ActiveAt(source.AcademicYear.EndDate))
            .Where(e => e.ClassId == source.ClassId && e.Student.Status == Studying && wanted.Contains(e.StudentId))
            .Select(e => e.StudentId).Distinct().ToListAsync();
        if (eligible.Count != wanted.Count)
            throw ApiException.BadRequest("Có học sinh không còn học ở lớp nguồn. Tải lại danh sách rồi chọn lại.");

        var alreadyEnrolled = await db.Enrollment
            .Where(e => eligible.Contains(e.StudentId) && e.Class.AcademicYearId == target.AcademicYearId)
            .Select(e => e.StudentId).Distinct().ToListAsync();
        var toMove = eligible.Except(alreadyEnrolled).ToList();

        db.Enrollment.AddRange(toMove.Select(id => new Enrollment
        {
            StudentId = id, ClassId = target.ClassId, StartDate = target.AcademicYear.StartDate, CreatedAt = clock.Now,
        }));
        await db.SaveChangesAsync();
        var item = new RolloverMoveItemResult(source.ClassId, target.ClassId, toMove.Count, alreadyEnrolled.Count);
        return new RolloverMoveResult(item.Moved, item.Skipped, [item]);
    }

    /// <summary>
    /// Bỏ ghi danh năm mới đã tạo khi kết chuyển (gỡ học sinh khỏi cột lên lớp / lưu ban). Chỉ gỡ được ghi danh mở từ ngày đầu năm mới
    /// khi năm mới chưa bắt đầu; sau đó dùng Chuyển lớp hoặc Kết thúc ghi danh để dữ liệu điểm danh không bị mồ côi.
    /// </summary>
    public async Task<int> UndoAsync(RolloverUndoRequest r)
    {
        if (r.EnrollmentIds is not { Count: > 0 }) throw ApiException.BadRequest("Chọn ít nhất một học sinh để gỡ.");
        var ids = r.EnrollmentIds.Distinct().ToList();
        var items = await db.Enrollment.Include(e => e.Class).ThenInclude(c => c.AcademicYear)
            .Where(e => ids.Contains(e.EnrollmentId)).ToListAsync();
        if (items.Count != ids.Count) throw ApiException.NotFound("Không tìm thấy ghi danh. Tải lại danh sách rồi chọn lại.");

        var today = clock.Today;
        foreach (var e in items)
        {
            var year = e.Class.AcademicYear;
            if (e.EndDate != null || e.StartDate != year.StartDate)
                throw ApiException.BadRequest("Chỉ gỡ được ghi danh do kết chuyển tạo ra (mở từ ngày đầu năm học).");
            if (year.StartDate <= today)
                throw ApiException.BadRequest($"Năm học {year.Name} đã bắt đầu nên không gỡ được. Dùng Chuyển lớp hoặc Kết thúc ghi danh ở màn Lớp & Ghi danh.");
        }
        db.Enrollment.RemoveRange(items);
        await db.SaveChangesAsync();
        return items.Count;
    }

    private async Task<(AcademicYear From, AcademicYear To)> LoadYearsAsync(int fromYearId, int toYearId)
    {
        var from = await db.AcademicYear.AsNoTracking().FirstOrDefaultAsync(y => y.AcademicYearId == fromYearId)
                   ?? throw ApiException.BadRequest("Chọn năm học nguồn.");
        var to = await db.AcademicYear.AsNoTracking().FirstOrDefaultAsync(y => y.AcademicYearId == toYearId)
                 ?? throw ApiException.BadRequest("Chọn năm học đích.");
        if (to.StartDate <= from.StartDate)
            throw ApiException.BadRequest($"Năm học đích ({to.Name}) phải sau năm học nguồn ({from.Name}).");
        return (from, to);
    }
}
