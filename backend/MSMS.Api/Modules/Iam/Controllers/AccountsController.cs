using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Modules.Iam.Dtos;
using MSMS.Api.Modules.Iam.Services;

namespace MSMS.Api.Modules.Iam.Controllers;

[ApiController, Route("api/iam/accounts"), Authorize(Policy = Policies.SchoolAdmin)]
public class AccountsController(AccountService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<AccountDto>> List([FromQuery] string? search, [FromQuery] string? accountType, [FromQuery] string? status,
        [FromQuery] int? campusId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => service.ListAsync(search, accountType, status, campusId, page, pageSize);

    [HttpPost]
    public Task<TempPasswordItem> Create(CreateAccountRequest r) => service.CreateAsync(r);

    [HttpPost("bulk")]
    public Task<BulkCreateResult> BulkCreate(BulkCreateAccountsRequest r) => service.BulkCreateAsync(r);

    [HttpPost("temp-password-sheet")]
    public IActionResult TempPasswordSheet(TempPasswordSheetRequest r) => service.BuildTempPasswordSheet(r);

    [HttpGet("{id:int}/lock-impact")]
    public Task<LockImpact> LockImpact(int id) => service.LockImpactAsync(id);

    [HttpPut("{id:int}/lock")]
    public async Task<IActionResult> Lock(int id) { await service.LockAsync(id); return NoContent(); }

    [HttpPut("{id:int}/unlock")]
    public async Task<IActionResult> Unlock(int id) { await service.UnlockAsync(id); return NoContent(); }

    [HttpPost("{id:int}/reset-password")]
    public Task<ResetPasswordResult> ResetPassword(int id) => service.ResetPasswordAsync(id);

    [HttpPut("{id:int}/type")]
    public Task<ResetPasswordResult> ChangeType(int id, ChangeAccountTypeRequest r) => service.ChangeTypeAsync(id, r);
}
