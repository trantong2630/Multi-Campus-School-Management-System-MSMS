using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Par.Dtos;
using MSMS.Api.Modules.Par.Services;

namespace MSMS.Api.Modules.Par.Controllers;

[ApiController, Route("api/par/leave-requests")]
public class LeaveRequestsController(LeaveRequestService parentService, HomeroomLeaveService homeroomService) : ControllerBase
{
    [HttpPost, Authorize(Policy = Policies.Parent)]
    public Task<LeaveRequest> Submit(CreateLeaveRequestDto dto) => parentService.SubmitAsync(dto);

    [HttpGet("student/{studentId:int}"), Authorize(Policy = Policies.Parent)]
    public Task<IEnumerable<ParentLeaveItemDto>> GetParentLeaves(int studentId) => parentService.GetParentLeavesAsync(studentId);

    [HttpPut("{id:int}/withdraw"), Authorize(Policy = Policies.Parent)]
    public async Task<IActionResult> Withdraw(int id) { await parentService.WithdrawAsync(id); return NoContent(); }

    [HttpGet("homeroom"), Authorize(Policy = Policies.Teacher)]
    public Task<IEnumerable<HomeroomLeaveItemDto>> GetHomeroomInbox([FromQuery] int? classId) => homeroomService.GetHomeroomInboxAsync(classId);

    [HttpPut("{id:int}/approve"), Authorize(Policy = Policies.Teacher)]
    public async Task<IActionResult> Approve(int id) { await homeroomService.ApproveAsync(id); return NoContent(); }

    [HttpPut("{id:int}/reject"), Authorize(Policy = Policies.Teacher)]
    public async Task<IActionResult> Reject(int id, DecideLeaveRequestDto dto) { await homeroomService.RejectAsync(id, dto); return NoContent(); }
}

