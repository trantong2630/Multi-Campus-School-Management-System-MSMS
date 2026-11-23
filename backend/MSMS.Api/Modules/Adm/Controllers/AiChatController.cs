using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Adm.Ai;
using MSMS.Api.Modules.Adm.Dtos;

namespace MSMS.Api.Modules.Adm.Controllers;

/// <summary>ADM-01 MSMS AI trên màn Tình hình: Principal (toàn trường), Vice Principal (phân hiệu mình). Phạm vi do service kiểm.</summary>
[ApiController, Route("api/adm/ai"), Authorize(Policy = Policies.DashboardReader)]
public class AiChatController(AdmAiChatService chat) : ControllerBase
{
    [HttpPost("chat"), EnableRateLimiting(AdmModule.AiChatRatePolicy)]
    public Task<AiChatResponse> Ask([FromBody] AiChatRequest request, CancellationToken ct) => chat.AskAsync(request, ct);

    [HttpGet("people")]
    public Task<List<AiPersonDto>> People([FromQuery] string? q) => chat.PeopleAsync(q);
}
