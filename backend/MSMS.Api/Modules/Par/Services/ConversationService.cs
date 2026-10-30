using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Notifications;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Com;
using MSMS.Api.Modules.Par.Dtos;
using MSMS.Api.Modules.Ppl.Services;
using MSMS.Api.Modules.Scd.Services;

namespace MSMS.Api.Modules.Par.Services;

public sealed class ConversationService(
    MsmsDbContext db,
    IClock clock,
    CurrentUser currentUser,
    NotificationSender notifier,
    IHubContext<CommunicationHub> hubContext,
    SubjectResponsibilityService responsibility,
    HomeroomService homeroomService)
{
    public async Task<List<AvailableTeacherDto>> GetAvailableTeachersAsync(int studentId)
    {
        if (currentUser.IsSchoolAdmin)
            throw ApiException.Forbidden("School Admin không có quyền truy cập hộp thư.");

        var hasLink = await db.ParentStudentLink.AsNoTracking()
            .AnyAsync(l => l.StudentId == studentId && l.ParentId == currentUser.PersonId);
        if (!hasLink)
            throw ApiException.Forbidden("Không có liên kết với học sinh này.");

        var today = clock.Today;
        var enrollment = await db.Enrollment.AsNoTracking()
            .Include(e => e.Class)
            .Where(e => e.StudentId == studentId && e.StartDate <= today && (e.EndDate == null || e.EndDate >= today))
            .OrderByDescending(e => e.StartDate)
            .FirstOrDefaultAsync();

        if (enrollment is null)
            return [];

        var homeroom = await homeroomService.GetHomeroomAsync(enrollment.ClassId, today);

        var teaching = await responsibility.ListAsync(null, null);
        var result = teaching.Teachers
            .Where(t => t.Items.Any(i => i.ClassId == enrollment.ClassId))
            .Select(t => new AvailableTeacherDto(
                t.TeacherId,
                t.TeacherName,
                t.TeacherAbbr,
                RoleDescription(
                    t.TeacherId == homeroom?.StaffId,
                    t.Items.Where(i => i.ClassId == enrollment.ClassId).Select(i => i.SubjectName).ToList())))
            .ToList();

        if (homeroom is not null && result.All(t => t.StaffId != homeroom.StaffId))
            result.Add(new AvailableTeacherDto(
                homeroom.StaffId,
                homeroom.StaffName,
                homeroom.TeacherAbbr,
                RoleDescription(true, null)));

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

        var homeroomClassIds = await homeroomService.ListHomeroomClassIdsAsync(staffPersonId, today);

        var teachingClassIds = (await responsibility.ForTeacherAsync(staffPersonId)).Select(x => x.ClassId);

        var activeClassIds = homeroomClassIds.Union(teachingClassIds).Distinct().ToList();
        if (activeClassIds.Count == 0)
            return [];

        var rawStudents = await db.Enrollment.AsNoTracking()
            .Where(e => activeClassIds.Contains(e.ClassId) && e.StartDate <= today && (e.EndDate == null || e.EndDate >= today))
            .Select(e => new
            {
                e.StudentId,
                FullName = e.Student.Student.FullName,
                PersonCode = e.Student.Student.PersonCode,
                e.ClassId,
                ClassName = e.Class.Name
            })
            .ToListAsync();

        return rawStudents
            .GroupBy(s => new { s.StudentId, s.ClassId })
            .Select(g => g.First())
            .Select(s => new TeachingStudentDto(
                s.StudentId,
                s.FullName,
                s.PersonCode,
                s.ClassId,
                s.ClassName
            ))
            .OrderBy(s => s.ClassName)
            .ThenBy(s => s.FullName)
            .ToList();
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
            throw ApiException.Forbidden("Không giảng dạy học sinh này.");

        var rawParents = await db.ParentStudentLink.AsNoTracking()
            .Where(l => l.StudentId == studentId)
            .Select(l => new
            {
                l.ParentId,
                FullName = l.Parent.FullName,
                l.Relationship,
                PhoneNumber = l.Parent.PhoneNumber
            })
            .ToListAsync();

        return rawParents
            .Select(p => new AvailableParentDto(
                p.ParentId,
                p.FullName,
                p.Relationship,
                p.PhoneNumber
            ))
            .OrderBy(p => p.FullName)
            .ToList();
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
                ParentName = c.Parent.FullName,
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

        var homerooms = await homeroomService.ListHomeroomsAsync(activeClassIds, today);
        var homeroomSet = homerooms.Select(h => (h.ClassId, h.StaffId)).ToHashSet();

        var teachingSubjects = (await responsibility.ListAsync(null, null)).Teachers
            .SelectMany(t => t.Items.Select(i => (i.ClassId, t.TeacherId, i.SubjectName)))
            .GroupBy(x => (x.ClassId, x.TeacherId))
            .ToDictionary(g => g.Key, g => g.Select(x => x.SubjectName).ToList());

        var items = new List<ConversationItemDto>(convos.Count);
        foreach (var c in convos)
        {
            var hasLink = linkSet.Contains((c.ParentId, c.StudentId));
            var hasActiveEnr = studentActiveEnrollmentMap.TryGetValue(c.StudentId, out var activeEnr);
            var classId = activeEnr?.ClassId ?? 0;

            var isHomeroom = activeEnr != null && homeroomSet.Contains((classId, c.StaffId));
            teachingSubjects.TryGetValue((classId, c.StaffId), out var subjects);
            var isTeaching = isHomeroom || subjects is not null;

            var isReadOnly = !(hasLink && hasActiveEnr && isTeaching);

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
                RoleDescription(isHomeroom, subjects),
                c.ParentId,
                c.ParentName,
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
            throw ApiException.Forbidden("Không phải thành viên của cuộc hội thoại này.");

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

            // BR-25, COM-4.8.8: Gửi sự kiện MessagesRead tới người gửi tin cũ sau SaveChanges thành công
            // Thông báo người gửi biết tin đã được xem (Seen indicator)
            // Broadcast tới conversation group để người gửi nhận được
            await hubContext.Clients
                .Group($"conversation_{conversationId}")
                .SendAsync("MessagesRead", conversationId, now);
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
            throw ApiException.Forbidden("Không phải thành viên của cuộc hội thoại này.");

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

        // BR-25, COM-4.8.7, COM-4.8.8: Gửi sự kiện realtime sau SaveChanges thành công
        // Sự kiện MessageReceived: Đẩy tin nhắn mới tức thì tới nhóm hội thoại
        // Sự kiện NotificationCreated: Đẩy thông báo chuông tới người nhận

        var messagePayload = new
        {
            messageId = message.MessageId,
            conversationId = message.ConversationId,
            senderPersonId = message.SenderPersonId,
            senderName = senderName,
            content = message.Content,
            sentAt = message.SentAt,
            readAt = message.ReadAt
        };

        // Gửi MessageReceived tới conversation group (cả hai bên đều nhận nếu đang ở trong hội thoại)
        await hubContext.Clients
            .Group($"conversation_{conversationId}")
            .SendAsync("MessageReceived", messagePayload);

        // Đồng thời gửi tới nhóm cá nhân người nhận (để cập nhật preview/unread dù họ đang ở hội thoại khác)
        await hubContext.Clients
            .Group($"user_{receiverPersonId}")
            .SendAsync("MessageReceived", messagePayload);

        // Gửi NotificationCreated tới người nhận qua nhóm cá nhân
        await hubContext.Clients
            .Group($"user_{receiverPersonId}")
            .SendAsync("NotificationCreated", new
            {
                type = "MessageReceived",
                title = $"Tin nhắn mới từ {senderName}",
                preview = preview,
                module = "PAR",
                targetId = conversationId,
                createdAt = now
            });

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
        var homeroom = await homeroomService.GetHomeroomAsync(classId, today);
        if (homeroom?.StaffId == staffId)
            return true;

        var teaching = await responsibility.ForTeacherAsync(staffId);
        return teaching.Any(x => x.ClassId == classId);
    }

    private static string RoleDescription(bool isHomeroom, IReadOnlyList<string>? subjects)
    {
        var subjectText = subjects is { Count: > 0 } ? string.Join(", ", subjects) : null;

        if (isHomeroom && subjectText is not null)
            return $"Giáo viên chủ nhiệm, phụ trách chuyên môn {subjectText}";
        if (isHomeroom)
            return "Giáo viên chủ nhiệm";
        if (subjectText is not null)
            return $"Phụ trách chuyên môn {subjectText}";
        return "Giáo viên";
    }
}
