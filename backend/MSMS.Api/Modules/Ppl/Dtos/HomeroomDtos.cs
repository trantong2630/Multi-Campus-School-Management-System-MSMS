namespace MSMS.Api.Modules.Ppl.Dtos;

/// <summary>Chủ nhiệm đang hiệu lực của một lớp.</summary>
public record HomeroomDto(int ClassId, int StaffId, string StaffName, string? TeacherAbbr);
