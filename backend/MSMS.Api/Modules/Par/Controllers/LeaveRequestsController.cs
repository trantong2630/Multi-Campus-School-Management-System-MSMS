using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Par.Dtos;
using MSMS.Api.Modules.Par.Services;

namespace MSMS.Api.Modules.Par.Controllers;

[ApiController, Route("api/par/leave-requests")]
public class LeaveRequestsController(LeaveRequestService parentService, HomeroomLeaveService homeroomService) : ControllerBase
{
    [HttpPost, Authorize(Policy = Policies.Parent)]
    public Task<LeaveRequest> Submit(CreateLeaveRequestDto dto) => parentService.SubmitAsync(dto);

    [HttpPost("bulk"), Authorize(Policy = Policies.Parent)]
    public Task<IReadOnlyList<LeaveRequest>> SubmitBulk(BulkCreateLeaveRequestDto dto) => parentService.SubmitBulkAsync(dto);

    [HttpGet("student/{studentId:int}"), Authorize(Policy = Policies.Parent)]
    public Task<PagedResult<ParentLeaveItemDto>> GetParentLeaves(int studentId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        => parentService.GetParentLeavesAsync(studentId, page, pageSize);

    [HttpGet("my-children"), Authorize(Policy = Policies.Parent)]
    public Task<IEnumerable<MyChildDto>> GetMyChildren() => parentService.GetMyChildrenAsync();

    [HttpPut("{id:int}/withdraw"), Authorize(Policy = Policies.Parent)]
    public async Task<IActionResult> Withdraw(int id) { await parentService.WithdrawAsync(id); return NoContent(); }

    [HttpGet("homeroom"), Authorize(Policy = Policies.Teacher)]
    public Task<PagedResult<HomeroomLeaveItemDto>> GetHomeroomInbox([FromQuery] int? classId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        => homeroomService.GetHomeroomInboxAsync(classId, page, pageSize);

    [HttpPut("{id:int}/approve"), Authorize(Policy = Policies.Teacher)]
    public async Task<IActionResult> Approve(int id) { await homeroomService.ApproveAsync(id); return NoContent(); }

    [HttpPut("{id:int}/reject"), Authorize(Policy = Policies.Teacher)]
    public async Task<IActionResult> Reject(int id, DecideLeaveRequestDto dto) { await homeroomService.RejectAsync(id, dto); return NoContent(); }
}

