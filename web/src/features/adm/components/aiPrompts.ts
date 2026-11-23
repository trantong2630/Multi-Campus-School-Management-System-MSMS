/**
 * Ba câu gợi ý trong màn chat MSMS AI: câu hỏi cơ bản quản lý hay hỏi, mỗi câu một mảng (chuyên cần, bài tập rèn luyện, giáo viên vắng).
 * Chỉ gợi những câu số liệu trả lời được, không hỏi nguyên nhân. Soạn sẵn ở frontend, không gọi AI.
 */
export function aiPrompts(scope: 'school' | 'campus'): { suggestions: string[] } {
  return {
    suggestions: [
      scope === 'school' ? 'Chuyên cần toàn trường tuần này thế nào?' : 'Chuyên cần phân hiệu tuần này thế nào?',
      'Lớp nào nộp bài rèn luyện thấp nhất tháng này?',
      'Giáo viên nào vắng nhiều nhất tháng này?',
    ],
  };
}
