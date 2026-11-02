using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Lrn.Dtos;
using MSMS.Api.Modules.Lrn.Services;

namespace MSMS.Api.Modules.Lrn.Controllers;

/// <summary>Phía giáo viên: giao bài (LRN-02) và xem kết quả từng học sinh (LRN-03, LRN-04). Bài đã giao không sửa, không xóa.</summary>
[ApiController, Route("api/lrn/assignments"), Authorize(Policy = Policies.Teacher)]
public class AssignmentsController(AssignmentService service, ResultService results) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<AssignmentItemDto>> List() => service.ListAsync();

    [HttpGet("class-subjects")]
    public Task<IReadOnlyList<ClassSubjectOptionDto>> ClassSubjects([FromQuery] int? subjectId) => service.MyClassSubjectsAsync(subjectId);

    [HttpGet("{id:int}")]
    public Task<AssignmentDetailDto> Get(int id) => service.GetAsync(id);

    [HttpPost]
    public Task<AssignmentDetailDto> Create(CreateAssignmentDto dto) => service.CreateAsync(dto);

    [HttpGet("{id:int}/results")]
    public Task<ClassResultDto> Results(int id) => results.ClassResultAsync(id);

    [HttpGet("{id:int}/results/{studentId:int}")]
    public Task<PracticeResultDto> StudentResult(int id, int studentId) => results.StudentResultForTeacherAsync(id, studentId);
}
