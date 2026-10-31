using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Par.Dtos;
using MSMS.Api.Modules.Par.Services;

namespace MSMS.Api.Modules.Par.Controllers;

[ApiController, Route("api/par/conversations"), Authorize(Policy = Policies.ConversationParticipant)]
public class ConversationsController(ConversationService service) : ControllerBase
{
    [HttpGet("available-teachers"), Authorize(Policy = Policies.Parent)]
    public Task<List<AvailableTeacherDto>> GetAvailableTeachers([FromQuery] int studentId)
        => service.GetAvailableTeachersAsync(studentId);

    [HttpGet("my-teaching-students"), Authorize(Policy = Policies.Teacher)]
    public Task<List<TeachingStudentDto>> GetMyTeachingStudents()
        => service.GetMyTeachingStudentsAsync();

    [HttpGet("available-parents"), Authorize(Policy = Policies.Teacher)]
    public Task<List<AvailableParentDto>> GetAvailableParents([FromQuery] int studentId)
        => service.GetAvailableParentsAsync(studentId);

    [HttpPost, Authorize]
    public Task<int> CreateOrGetConversation(CreateConversationDto dto)
        => service.CreateOrGetConversationAsync(dto);

    [HttpGet, Authorize]
    public Task<List<ConversationItemDto>> GetMyConversations()
        => service.GetMyConversationsAsync();

    [HttpGet("unread-count"), Authorize]
    public Task<int> GetTotalUnreadCount()
        => service.GetTotalUnreadCountAsync();

    [HttpGet("{id:int}/messages"), Authorize]
    public Task<List<MessageItemDto>> GetMessages(int id)
        => service.GetMessagesAsync(id);

    [HttpPost("{id:int}/messages"), Authorize]
    public Task<MessageItemDto> SendMessage(int id, SendMessageDto dto)
        => service.SendMessageAsync(id, dto.Content);
}
