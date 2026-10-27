using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Notifications;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Par.Dtos;

namespace MSMS.Api.Modules.Par.Services;

public sealed class ConversationService(
    MsmsDbContext db,
    IClock clock,
    CurrentUser currentUser,
    NotificationSender notifier)
{
    public async Task<List<AvailableTeacherDto>> GetAvailableTeachersAsync(int studentId)
    {
        if (currentUser.IsSchoolAdmin)
            throw ApiException.Forbidden("School Admin không có quyền truy cập hộp thư.");

        var hasLink = await db.ParentStudentLink.AsNoTracking()
            .AnyAsync(l => l.StudentId == studentId && l.ParentId == currentUser.PersonId);
        if (!hasLink)
            throw ApiException.Forbidden("Bạn không có liên kết với học sinh này.");

        var today = clock.Today;
        var enrollment = await db.Enrollment.AsNoTracking()
            .Include(e => e.Class)
            .Where(e => e.StudentId == studentId && e.StartDate <= today && (e.EndDate == null || e.EndDate >= today))
            .OrderByDescending(e => e.StartDate)
            .FirstOrDefaultAsync();

        if (enrollment is null)
            return [];

        var homeroom = await db.HomeroomAssignment.AsNoTracking()
            .Include(h => h.Staff).ThenInclude(s => s.Staff)
            .Where(h => h.ClassId == enrollment.ClassId && h.StartDate <= today && (h.EndDate == null || h.EndDate >= today))
            .OrderByDescending(h => h.StartDate)
            .FirstOrDefaultAsync();

        var teacherSubjects = new Dictionary<int, List<string>>();
        var staffInfo = new Dictionary<int, (string FullName, string? TeacherAbbr)>();

        if (homeroom is not null)
        {
            staffInfo[homeroom.StaffId] = (homeroom.Staff.Staff.FullName, homeroom.Staff.TeacherAbbr);
        }

        var semester = await CurrentSemesterAsync();
        if (semester is not null)
        {
            var latestVersionId = await db.TimetableVersionClass.AsNoTracking()
                .Where(vc => vc.ClassId == enrollment.ClassId && vc.Version.Status == "Published" && vc.Version.SemesterId == semester.SemesterId)
                .OrderByDescending(vc => vc.Version.PublishedAt)
                .ThenByDescending(vc => vc.VersionId)
                .Select(vc => vc.VersionId)
                .FirstOrDefaultAsync();

            if (latestVersionId > 0)
            {
                var entries = await db.TimetableEntry.AsNoTracking()
                    .Where(e => e.VersionId == latestVersionId && e.ClassId == enrollment.ClassId)
                    .Select(e => new
                    {
                        e.TeacherId,
                        TeacherName = e.Teacher.Staff.FullName,
                        e.Teacher.TeacherAbbr,
                        SubjectName = e.Subject.Name
                    })
                    .ToListAsync();

                foreach (var entry in entries)
                {
                    if (!teacherSubjects.TryGetValue(entry.TeacherId, out var subList))
                    {
                        subList = [];
                        teacherSubjects[entry.TeacherId] = subList;
                    }
                    if (!subList.Contains(entry.SubjectName))
                    {
                        subList.Add(entry.SubjectName);
                    }

                    if (!staffInfo.ContainsKey(entry.TeacherId))
                    {
                        staffInfo[entry.TeacherId] = (entry.TeacherName, entry.TeacherAbbr);
                    }
                }
            }
        }

        var result = new List<AvailableTeacherDto>();
        foreach (var (staffId, (fullName, teacherAbbr)) in staffInfo)
        {
            var isHomeroom = homeroom?.StaffId == staffId;
            teacherSubjects.TryGetValue(staffId, out var subjects);
            var hasSubjects = subjects != null && subjects.Count > 0;

            string roleDesc;
            if (isHomeroom && hasSubjects)
                roleDesc = $"Giáo viên chủ nhiệm, GV bộ môn {string.Join(", ", subjects!)}";
            else if (isHomeroom)
                roleDesc = "Giáo viên chủ nhiệm";
            else if (hasSubjects)
                roleDesc = $"GV bộ môn {string.Join(", ", subjects!)}";
            else
                roleDesc = "Giáo viên";

            result.Add(new AvailableTeacherDto(staffId, fullName, teacherAbbr, roleDesc));
        }

        return result
            .OrderByDescending(t => t.RoleDescription.Contains("chủ nhiệm"))
            .ThenBy(t => t.FullName)
            .ToList();
    }

    public async Task<List<TeachingStudentDto>> GetMyTeachingStudentsAsync()
    {
        if (currentUser.IsSchoolAdmin)
            throw ApiException.Forbidden("School Admin không có quyền truy cập hộp thư.");

        if (currentUser.AccountType == AccountTypes.Student)
            throw ApiException.Forbidden("Học sinh không có quyền truy cập hộp thư.");

        var staffPersonId = currentUser.PersonId;
        var today = clock.Today;

        var homeroomClassIds = await db.HomeroomAssignment.AsNoTracking()
            .Where(h => h.StaffId == staffPersonId && h.StartDate <= today && (h.EndDate == null || h.EndDate >= today))
            .Select(h => h.ClassId)
            .ToListAsync();

        var semester = await CurrentSemesterAsync();
        var timetableClassIds = new List<int>();
        if (semester is not null)
        {
            var published = await db.TimetableVersionClass.AsNoTracking()
                .Where(vc => vc.Version.Status == "Published" && vc.Version.SemesterId == semester.SemesterId)
                .Select(vc => new { vc.ClassId, vc.VersionId, vc.Version.PublishedAt })
                .ToListAsync();

            var latest = published.GroupBy(x => x.ClassId)
                .Select(g => g.OrderByDescending(x => x.PublishedAt).ThenByDescending(x => x.VersionId).First())
                .ToDictionary(x => x.ClassId, x => x.VersionId);

            if (latest.Count > 0)
            {
                var versionIds = latest.Values.Distinct().ToList();
                var teacherEntries = await db.TimetableEntry.AsNoTracking()
                    .Where(e => versionIds.Contains(e.VersionId) && e.TeacherId == staffPersonId)
                    .Select(e => new { e.VersionId, e.ClassId })
                    .ToListAsync();

                timetableClassIds = teacherEntries
                    .Where(e => latest.TryGetValue(e.ClassId, out var vId) && vId == e.VersionId)
                    .Select(e => e.ClassId)
                    .Distinct()
                    .ToList();
            }
        }

        var activeClassIds = homeroomClassIds.Union(timetableClassIds).Distinct().ToList();
        if (activeClassIds.Count == 0)
            return [];

        return await db.Enrollment.AsNoTracking()
            .Where(e => activeClassIds.Contains(e.ClassId) && e.StartDate <= today && (e.EndDate == null || e.EndDate >= today))
            .Select(e => new TeachingStudentDto(
                e.StudentId,
                e.Student.Student.FullName,
                e.Student.Student.PersonCode,
                e.ClassId,
                e.Class.Name
            ))
            .OrderBy(s => s.ClassName)
            .ThenBy(s => s.FullName)
            .ToListAsync();
    }

    public async Task<List<AvailableParentDto>> GetAvailableParentsAsync(int studentId)
    {
        if (currentUser.IsSchoolAdmin)
            throw ApiException.Forbidden("School Admin không có quyền truy cập hộp thư.");

        if (currentUser.AccountType == AccountTypes.Student)
            throw ApiException.Forbidden("Học sinh không có quyền truy cập hộp thư.");

        var staffPersonId = currentUser.PersonId;
        var today = clock.Today;

        var enrollment = await db.Enrollment.AsNoTracking()
            .Include(e => e.Class)
            .Where(e => e.StudentId == studentId && e.StartDate <= today && (e.EndDate == null || e.EndDate >= today))
            .OrderByDescending(e => e.StartDate)
            .FirstOrDefaultAsync();

        if (enrollment is null)
            throw ApiException.BadRequest("Học sinh không có ghi danh hiệu lực.");

        var isTeaching = await IsTeacherOfClassAsync(staffPersonId, enrollment.ClassId, today);
        if (!isTeaching)
            throw ApiException.Forbidden("Bạn không giảng dạy học sinh này.");

        return await db.ParentStudentLink.AsNoTracking()
            .Where(l => l.StudentId == studentId)
            .Select(l => new AvailableParentDto(
                l.ParentId,
                l.Parent.FullName,
                l.Relationship,
                l.Parent.PhoneNumber
            ))
            .OrderBy(p => p.FullName)
            .ToListAsync();
    }

    public async Task<int> CreateOrGetConversationAsync(CreateConversationDto dto)
    {
        if (currentUser.IsSchoolAdmin)
            throw ApiException.Forbidden("School Admin không có quyền truy cập hộp thư.");

        if (currentUser.AccountType == AccountTypes.Student)
            throw ApiException.Forbidden("Học sinh không có quyền truy cập hộp thư.");

        int parentId;
        int staffId;
        var studentId = dto.StudentId;

        if (currentUser.AccountType == AccountTypes.Parent)
        {
            parentId = currentUser.PersonId;
            staffId = dto.StaffId ?? throw ApiException.BadRequest("Vui lòng chọn giáo viên.");
        }
        else
        {
            staffId = currentUser.PersonId;
            parentId = dto.ParentId ?? throw ApiException.BadRequest("Vui lòng chọn phụ huynh.");
        }

        var isActive = await IsRelationshipActiveAsync(parentId, staffId, studentId, clock.Today);
        if (!isActive)
            throw ApiException.BadRequest("Quan hệ giảng dạy không hợp lệ hoặc đã kết thúc.");

        var conversation = await db.Conversation
            .FirstOrDefaultAsync(c => c.ParentId == parentId && c.StaffId == staffId && c.StudentId == studentId);

        if (conversation is null)
        {
            conversation = new Conversation
            {
                ParentId = parentId,
                StaffId = staffId,
                StudentId = studentId,
                CreatedAt = clock.Now,
                LastMessageAt = null,
            };

            db.Conversation.Add(conversation);
            await db.SaveChangesAsync();
        }

        return conversation.ConversationId;
    }

    public async Task<List<ConversationItemDto>> GetMyConversationsAsync()
    {
        if (currentUser.IsSchoolAdmin)
            throw ApiException.Forbidden("School Admin không có quyền truy cập hộp thư.");

        if (currentUser.AccountType == AccountTypes.Student)
            throw ApiException.Forbidden("Học sinh không có quyền truy cập hộp thư.");

        var currentPersonId = currentUser.PersonId;

        var convos = await db.Conversation.AsNoTracking()
            .Where(c => c.ParentId == currentPersonId || c.StaffId == currentPersonId)
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
            .Select(c => new
            {
                c.ConversationId,
                c.ParentId,
                c.StaffId,
                c.StudentId,
                c.CreatedAt,
                c.LastMessageAt,
                StudentName = c.Student.Student.FullName,
                StudentCode = c.Student.Student.PersonCode,
                StaffName = c.Staff.Staff.FullName,
                StaffAbbr = c.Staff.TeacherAbbr,
                LastMessageContent = c.Message.OrderByDescending(m => m.SentAt).Select(m => m.Content).FirstOrDefault(),
                UnreadCount = c.Message.Count(m => m.SenderPersonId != currentPersonId && m.ReadAt == null)
            })
            .ToListAsync();

        if (convos.Count == 0)
            return [];

        var today = clock.Today;
        var studentIds = convos.Select(c => c.StudentId).Distinct().ToList();
        var parentIds = convos.Select(c => c.ParentId).Distinct().ToList();

        var activeLinks = await db.ParentStudentLink.AsNoTracking()
            .Where(l => parentIds.Contains(l.ParentId) && studentIds.Contains(l.StudentId))
            .Select(l => new { l.ParentId, l.StudentId })
            .ToListAsync();
        var linkSet = activeLinks.Select(l => (l.ParentId, l.StudentId)).ToHashSet();

        var allEnrollments = await db.Enrollment.AsNoTracking()
            .Include(e => e.Class)
            .Where(e => studentIds.Contains(e.StudentId))
            .OrderByDescending(e => e.StartDate)
            .ToListAsync();

        var studentActiveEnrollmentMap = allEnrollments
            .Where(e => e.StartDate <= today && (e.EndDate == null || e.EndDate >= today))
            .GroupBy(e => e.StudentId)
            .ToDictionary(g => g.Key, g => g.First());

        var studentLatestEnrollmentMap = allEnrollments
            .GroupBy(e => e.StudentId)
            .ToDictionary(g => g.Key, g => g.First());

        var activeClassIds = studentActiveEnrollmentMap.Values.Select(e => e.ClassId).Distinct().ToList();

        var homerooms = await db.HomeroomAssignment.AsNoTracking()
            .Where(h => activeClassIds.Contains(h.ClassId) && h.StartDate <= today && (h.EndDate == null || h.EndDate >= today))
            .Select(h => new { h.ClassId, h.StaffId })
            .ToListAsync();
        var homeroomSet = homerooms.Select(h => (h.ClassId, h.StaffId)).ToHashSet();

        var semester = await CurrentSemesterAsync();
        var timetableTeacherSubjects = new Dictionary<(int ClassId, int StaffId), List<string>>();
        if (semester is not null && activeClassIds.Count > 0)
        {
            var published = await db.TimetableVersionClass.AsNoTracking()
                .Where(vc => activeClassIds.Contains(vc.ClassId) && vc.Version.Status == "Published" && vc.Version.SemesterId == semester.SemesterId)
                .Select(vc => new { vc.ClassId, vc.VersionId, vc.Version.PublishedAt })
                .ToListAsync();

            var latest = published.GroupBy(x => x.ClassId)
                .Select(g => g.OrderByDescending(x => x.PublishedAt).ThenByDescending(x => x.VersionId).First())
                .ToDictionary(x => x.ClassId, x => x.VersionId);

            if (latest.Count > 0)
            {
                var versionIds = latest.Values.Distinct().ToList();
                var entries = await db.TimetableEntry.AsNoTracking()
                    .Where(e => versionIds.Contains(e.VersionId))
                    .Select(e => new { e.VersionId, e.ClassId, e.TeacherId, SubjectName = e.Subject.Name })
                    .ToListAsync();

                foreach (var entry in entries)
                {
                    if (latest.TryGetValue(entry.ClassId, out var vId) && vId == entry.VersionId)
                    {
                        var key = (entry.ClassId, entry.TeacherId);
                        if (!timetableTeacherSubjects.TryGetValue(key, out var subList))
                        {
                            subList = [];
                            timetableTeacherSubjects[key] = subList;
                        }
                        if (!subList.Contains(entry.SubjectName))
                        {
                            subList.Add(entry.SubjectName);
                        }
                    }
                }
            }
        }

        var items = new List<ConversationItemDto>(convos.Count);
        foreach (var c in convos)
        {
            var hasLink = linkSet.Contains((c.ParentId, c.StudentId));
            var hasActiveEnr = studentActiveEnrollmentMap.TryGetValue(c.StudentId, out var activeEnr);
            var classId = activeEnr?.ClassId ?? 0;

            var isHomeroom = activeEnr != null && homeroomSet.Contains((classId, c.StaffId));
            timetableTeacherSubjects.TryGetValue((classId, c.StaffId), out var subjects);
            var hasSubjects = subjects != null && subjects.Count > 0;
            var isTeaching = isHomeroom || hasSubjects;

            var isReadOnly = !(hasLink && hasActiveEnr && isTeaching);

            string roleDesc;
            if (isHomeroom && hasSubjects)
                roleDesc = $"Giáo viên chủ nhiệm, GV bộ môn {string.Join(", ", subjects!)}";
            else if (isHomeroom)
                roleDesc = "Giáo viên chủ nhiệm";
            else if (hasSubjects)
                roleDesc = $"GV bộ môn {string.Join(", ", subjects!)}";
            else
                roleDesc = "Giáo viên";

            var className = activeEnr?.Class.Name
                ?? (studentLatestEnrollmentMap.TryGetValue(c.StudentId, out var latestEnr) ? latestEnr.Class.Name : "N/A");

            items.Add(new ConversationItemDto(
                c.ConversationId,
                c.StudentId,
                c.StudentName,
                c.StudentCode,
                className,
                c.StaffId,
                c.StaffName,
                c.StaffAbbr,
                roleDesc,
                c.LastMessageContent,
                c.LastMessageAt,
                c.UnreadCount,
                isReadOnly
            ));
        }

        return items;
    }

    public async Task<List<MessageItemDto>> GetMessagesAsync(int conversationId)
    {
        if (currentUser.IsSchoolAdmin)
            throw ApiException.Forbidden("School Admin không có quyền truy cập hộp thư.");

        var conversation = await db.Conversation.AsNoTracking()
            .FirstOrDefaultAsync(c => c.ConversationId == conversationId);

        if (conversation is null)
            throw ApiException.NotFound("Không tìm thấy cuộc hội thoại.");

        var currentPersonId = currentUser.PersonId;
        if (conversation.ParentId != currentPersonId && conversation.StaffId != currentPersonId)
            throw ApiException.Forbidden("Bạn không phải thành viên của cuộc hội thoại này.");

        var messages = await db.Message
            .Include(m => m.SenderPerson)
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.SentAt)
            .ToListAsync();

        var unreadIncomingMessages = messages
            .Where(m => m.SenderPersonId != currentPersonId && m.ReadAt == null)
            .ToList();

        if (unreadIncomingMessages.Count > 0)
        {
            var now = clock.Now;
            foreach (var msg in unreadIncomingMessages)
            {
                msg.ReadAt = now;
            }
            await db.SaveChangesAsync();
        }

        return messages.Select(m => new MessageItemDto(
            m.MessageId,
            m.ConversationId,
            m.SenderPersonId,
            m.SenderPerson.FullName,
            m.SenderPersonId == currentPersonId,
            m.Content,
            m.SentAt,
            m.ReadAt
        )).ToList();
    }

    public async Task<MessageItemDto> SendMessageAsync(int conversationId, string content)
    {
        if (currentUser.IsSchoolAdmin)
            throw ApiException.Forbidden("School Admin không có quyền truy cập hộp thư.");

        if (string.IsNullOrWhiteSpace(content))
            throw ApiException.BadRequest("Nội dung tin nhắn không được để trống.");

        if (content.Length > 2000)
            throw ApiException.BadRequest("Nội dung tin nhắn không được vượt quá 2.000 ký tự.");

        var conversation = await db.Conversation
            .Include(c => c.Parent)
            .Include(c => c.Staff).ThenInclude(s => s.Staff)
            .FirstOrDefaultAsync(c => c.ConversationId == conversationId);

        if (conversation is null)
            throw ApiException.NotFound("Không tìm thấy cuộc hội thoại.");

        var currentPersonId = currentUser.PersonId;
        if (conversation.ParentId != currentPersonId && conversation.StaffId != currentPersonId)
            throw ApiException.Forbidden("Bạn không phải thành viên của cuộc hội thoại này.");

        var isRelationshipActive = await IsRelationshipActiveAsync(
            conversation.ParentId,
            conversation.StaffId,
            conversation.StudentId,
            clock.Today);

        if (!isRelationshipActive)
            throw ApiException.BadRequest("Quan hệ giảng dạy đã kết thúc. Hộp thư đã chuyển sang chế độ chỉ đọc.");

        var receiverPersonId = (conversation.ParentId == currentPersonId)
            ? conversation.StaffId
            : conversation.ParentId;

        var senderName = (conversation.ParentId == currentPersonId)
            ? conversation.Parent.FullName
            : conversation.Staff.Staff.FullName;

        var now = clock.Now;
        var message = new Message
        {
            ConversationId = conversationId,
            SenderPersonId = currentPersonId,
            Content = content.Trim(),
            SentAt = now,
            ReadAt = null,
        };

        db.Message.Add(message);
        conversation.LastMessageAt = now;

        var preview = message.Content.Length > 200
            ? message.Content[..197] + "..."
            : message.Content;

        notifier.Add(
            [receiverPersonId],
            "MessageReceived",
            $"Tin nhắn mới từ {senderName}",
            preview,
            "PAR",
            conversationId);

        await db.SaveChangesAsync();

        return new MessageItemDto(
            message.MessageId,
            message.ConversationId,
            message.SenderPersonId,
            senderName,
            true,
            message.Content,
            message.SentAt,
            message.ReadAt
        );
    }

    public async Task<int> GetTotalUnreadCountAsync()
    {
        if (currentUser.IsSchoolAdmin)
            throw ApiException.Forbidden("School Admin không có quyền truy cập hộp thư.");

        var currentPersonId = currentUser.PersonId;

        return await db.Message.AsNoTracking()
            .Where(m => (m.Conversation.ParentId == currentPersonId || m.Conversation.StaffId == currentPersonId)
                     && m.SenderPersonId != currentPersonId
                     && m.ReadAt == null)
            .CountAsync();
    }

    private async Task<bool> IsRelationshipActiveAsync(int parentId, int staffId, int studentId, DateOnly today)
    {
        var hasLink = await db.ParentStudentLink.AsNoTracking()
            .AnyAsync(l => l.ParentId == parentId && l.StudentId == studentId);
        if (!hasLink)
            return false;

        var enrollment = await db.Enrollment.AsNoTracking()
            .Where(e => e.StudentId == studentId && e.StartDate <= today && (e.EndDate == null || e.EndDate >= today))
            .OrderByDescending(e => e.StartDate)
            .FirstOrDefaultAsync();

        if (enrollment is null)
            return false;

        return await IsTeacherOfClassAsync(staffId, enrollment.ClassId, today);
    }

    private async Task<bool> IsTeacherOfClassAsync(int staffId, int classId, DateOnly today)
    {
        var isHomeroom = await db.HomeroomAssignment.AsNoTracking()
            .AnyAsync(h => h.StaffId == staffId && h.ClassId == classId && h.StartDate <= today && (h.EndDate == null || h.EndDate >= today));
        if (isHomeroom)
            return true;

        var semester = await CurrentSemesterAsync();
        if (semester is null)
            return false;

        var latestVersionId = await db.TimetableVersionClass.AsNoTracking()
            .Where(vc => vc.ClassId == classId && vc.Version.Status == "Published" && vc.Version.SemesterId == semester.SemesterId)
            .OrderByDescending(vc => vc.Version.PublishedAt)
            .ThenByDescending(vc => vc.VersionId)
            .Select(vc => vc.VersionId)
            .FirstOrDefaultAsync();

        if (latestVersionId == 0)
            return false;

        return await db.TimetableEntry.AsNoTracking()
            .AnyAsync(e => e.VersionId == latestVersionId && e.ClassId == classId && e.TeacherId == staffId);
    }

    private async Task<Semester?> CurrentSemesterAsync()
    {
        var today = clock.Today;
        var semesters = await db.Semester.AsNoTracking()
            .Where(s => s.AcademicYear.IsCurrent)
            .OrderBy(s => s.StartDate)
            .ToListAsync();

        return semesters.FirstOrDefault(s => s.StartDate <= today && today <= s.EndDate)
               ?? semesters.LastOrDefault(s => s.StartDate <= today)
               ?? semesters.FirstOrDefault();
    }
}
