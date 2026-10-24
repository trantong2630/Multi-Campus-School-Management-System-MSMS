using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Par.Dtos;
using MSMS.Api.Modules.Par.Services;

namespace MSMS.Api.Modules.Par.Controllers;

[ApiController, Route("api/par/leave-requests"), Authorize(Policy = Policies.Parent)]
public class LeaveRequestsController(LeaveRequestService service) : ControllerBase
{
    [HttpPost]
    public Task<LeaveRequest> Submit(CreateLeaveRequestDto dto) => service.SubmitAsync(dto);

    [HttpGet("student/{studentId:int}")]
    public Task<IEnumerable<ParentLeaveItemDto>> GetParentLeaves(int studentId) => service.GetParentLeavesAsync(studentId);

    [HttpPut("{id:int}/withdraw")]
    public async Task<IActionResult> Withdraw(int id) { await service.WithdrawAsync(id); return NoContent(); }
}
