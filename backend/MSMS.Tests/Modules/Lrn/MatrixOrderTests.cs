using MSMS.Api.Modules.Lrn.Services;

namespace MSMS.Tests.Modules.Lrn;

/// <summary>Chủ đề của ma trận đề xếp theo số bài, không theo từng ký tự ("Bài 2" trước "Bài 10", "Bài 19").</summary>
public class MatrixOrderTests
{
    [Fact]
    public void Topics_AreOrderedByLessonNumber()
    {
        var sorted = new[] { "Bài 19. Hình chữ nhật", "Bài 2. Cách ghi số tự nhiên", "Bài 1. Tập hợp", "Bài 10. Số nguyên tố" }
            .OrderBy(t => t, ExamMatrixService.NaturalComparer.Instance).ToList();

        Assert.Equal(["Bài 1. Tập hợp", "Bài 2. Cách ghi số tự nhiên", "Bài 10. Số nguyên tố", "Bài 19. Hình chữ nhật"], sorted);
    }

    [Fact]
    public void SameNumber_FallsBackToText() =>
        Assert.True(ExamMatrixService.NaturalComparer.Instance.Compare("Bài 2. Ba", "Bài 2. Tư") < 0);
}
