// Lời nhắn soạn sẵn của MSMS AI: chọn ngẫu nhiên trong từng nhóm, không lưu và không gọi AI.

const pick = <T,>(list: readonly T[]): T => list[Math.floor(Math.random() * list.length)];

const START = ['Bắt đầu thôi, bạn làm được mà!', 'Đọc kỹ đề rồi hãy chọn nhé.', 'Mình ở đây cổ vũ bạn. Cố lên!', 'Cứ bình tĩnh, câu nào chắc thì làm trước.'];
const MID = [
  (done: number, total: number) => `Đã xong ${done}/${total} câu rồi, tiếp tục nào!`,
  (done: number, total: number) => `${done}/${total} câu rồi. Bạn đang làm rất tốt!`,
  (done: number, total: number) => `Còn ${total - done} câu nữa thôi, sắp tới đích!`,
];
const LAST_MINUTE = ['Chỉ còn 1 phút!', 'Còn 1 phút, kiểm tra lại các câu chưa chắc nhé!', 'Sắp hết giờ rồi, nhanh tay nào!'];
const TIME_UP = ['Hết giờ! Mình nộp bài giúp bạn nhé.'];
const SUBMITTED = ['Nộp rồi! Đợi mình phân tích chút nhé.', 'Xong rồi! Mình đang xem bài của bạn đây.'];

export const aiLines = {
  start: () => pick(START),
  mid: (done: number, total: number) => pick(MID)(done, total),
  lastMinute: () => pick(LAST_MINUTE),
  timeUp: () => pick(TIME_UP),
  unanswered: (n: number) => `Bạn còn ${n} câu chưa làm, nộp luôn chứ?`,
  allDone: () => 'Bạn đã trả lời hết rồi. Xem lại một lượt rồi nộp nhé!',
  submitted: () => pick(SUBMITTED),
};

/** Lời nhận xét ngắn theo mức điểm trên trang kết quả (soạn sẵn, không phải phân tích của AI). */
export function resultLine(score: number | null, correct: number | null, total: number): string {
  if (score === null) return 'Bài này chưa được nộp nên mình chưa có gì để xem.';
  if (correct === total && total > 0) return 'Tuyệt đối! Bạn trả lời đúng tất cả các câu.';
  if (score >= 8) return 'Xuất sắc! Bạn nắm bài rất chắc.';
  if (score >= 6.5) return 'Khá tốt! Xem lại vài câu sai là bạn lên điểm ngay.';
  if (score >= 5) return 'Đạt rồi, nhưng còn vài chỗ cần ôn lại. Cùng xem nhé!';
  return 'Đừng nản nhé. Mình cùng xem lại từng câu sai để lần sau tốt hơn.';
}

/** Các bước giả lập trong lúc MSMS AI soạn câu hỏi, đổi dần theo thời gian chờ. */
export const composeSteps = (topic: string) => [
  'Đang mở sách giáo khoa…',
  `Đang nghĩ câu hỏi về “${topic}”…`,
  'Đang soạn các lựa chọn gây nhiễu…',
  'Đang kiểm tra lại đáp án…',
  'Sắp xong rồi, đợi mình chút nữa…',
];

export function todoLine(opts: { total: number; soonest?: { title: string; hint: string } }): string {
  if (opts.total === 0) return 'Hiện không có bài nào chờ bạn. Nghỉ ngơi một chút nhé!';
  if (opts.soonest) return `Bài “${opts.soonest.title}” ${opts.soonest.hint.toLowerCase()}. Làm trước nhé!`;
  return `Bạn có ${opts.total} bài đang chờ.`;
}
