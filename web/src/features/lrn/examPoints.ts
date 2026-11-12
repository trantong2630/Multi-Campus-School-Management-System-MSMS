import { DIFFICULTIES } from './labels';
import type { Difficulty, QuestionItem } from './types';

/** Số câu hoặc điểm của từng phần (độ khó) trong một đề. */
export type PerLevel = Partial<Record<Difficulty, number>>;

/** Mười điểm tính bằng 0,01: cộng điểm các phần bằng số nguyên để khỏi lệch số thực. */
export const TOTAL_CENTS = 1000;
export const cents = (v: number) => Math.round(v * 100);

/** Tỉ lệ điểm gợi ý giữa các phần đang có câu: phần dễ nhiều điểm hơn vì nhiều câu hơn, câu khó vẫn được điểm cao hơn mỗi câu. */
const SUGGEST_WEIGHT: Record<Difficulty, number> = { Recognize: 4, Understand: 3, Apply: 2, HighApply: 1 };

/** Chia 10 điểm cho các phần theo `weights` (đơn vị 0,01 để cộng lại đúng 10); phần dư dồn vào phần đầu. */
function splitTen(weights: PerLevel): PerLevel {
  const levels = DIFFICULTIES.map((d) => d.value).filter((d) => (weights[d] ?? 0) > 0);
  const sum = levels.reduce((s, d) => s + (weights[d] ?? 0), 0);
  const out: PerLevel = {};
  let used = 0;
  for (const d of levels) {
    const c = Math.floor((TOTAL_CENTS * (weights[d] ?? 0)) / sum);
    out[d] = c;
    used += c;
  }
  if (levels.length > 0) out[levels[0]] = (out[levels[0]] ?? 0) + (TOTAL_CENTS - used);
  for (const d of levels) out[d] = (out[d] ?? 0) / 100;
  return out;
}

/** Gợi ý theo độ khó: tỉ lệ 4 : 3 : 2 : 1 giữa các phần đang có câu. */
export const suggestByDifficulty = (counts: PerLevel): PerLevel =>
  splitTen(Object.fromEntries(Object.keys(counts).map((d) => [d, SUGGEST_WEIGHT[d as Difficulty]])) as PerLevel);

/**
 * Chia đều: mỗi câu một điểm bằng nhau tới 0,01. Mười điểm không luôn chia hết cho số câu (10 điểm cho 11 câu là 0,909…), nên phần dư
 * được chia mỗi câu thêm 0,01 từ câu đầu (câu dễ trước): với 11 câu thì 10 câu 0,91 và 1 câu 0,90. Điểm của từng phần là tổng điểm các câu
 * trong phần, nên Backend chia lại trong phần ra đúng các số này.
 */
export function suggestEvenly(counts: PerLevel): PerLevel {
  const levels = DIFFICULTIES.map((d) => d.value).filter((d) => (counts[d] ?? 0) > 0);
  const total = levels.reduce((s, d) => s + (counts[d] ?? 0), 0);
  if (total === 0) return {};
  const base = Math.floor(TOTAL_CENTS / total);
  let extra = TOTAL_CENTS % total;
  const out: PerLevel = {};
  for (const d of levels) {
    const n = counts[d] ?? 0;
    const bonus = Math.min(n, extra);
    extra -= bonus;
    out[d] = (base * n + bonus) / 100;
  }
  return out;
}

/** Số câu theo độ khó của các câu đã chọn; câu chưa phân loại không tính (không vào được đề). */
export function countByLevel(questions: QuestionItem[]): PerLevel {
  const m: PerLevel = {};
  for (const q of questions) if (q.difficulty) m[q.difficulty] = (m[q.difficulty] ?? 0) + 1;
  return m;
}

/**
 * Điểm từng câu: chia điểm của phần cho các câu trong phần theo đơn vị 0,01, phần dư chia từ câu đầu, đúng như Backend,
 * nên điểm hiện ở đây trùng điểm lúc giao.
 */
export function pointsPerQuestion(questions: QuestionItem[], sectionPoints: PerLevel): Map<number, number> {
  const out = new Map<number, number>();
  for (const d of DIFFICULTIES) {
    const group = questions.filter((q) => q.difficulty === d.value);
    if (group.length === 0) continue;
    const total = cents(sectionPoints[d.value] ?? 0);
    const each = Math.floor(total / group.length);
    const extra = total % group.length;
    group.forEach((q, i) => out.set(q.questionId, (each + (i < extra ? 1 : 0)) / 100));
  }
  return out;
}

/** Điểm mỗi câu của một phần: một số khi các câu bằng nhau, hai số "0,90–0,91" khi phần dư làm lệch 0,01. */
export function perQuestionText(sectionPoints: number, questionCount: number): string {
  const c = cents(sectionPoints);
  const lo = Math.floor(c / questionCount);
  const hi = Math.ceil(c / questionCount);
  return lo === hi ? fmtPoints(lo / 100) : `${fmtPoints(lo / 100)}–${fmtPoints(hi / 100)}`;
}

export const fmtPoints = (v: number) => (Math.round(v * 100) / 100).toLocaleString('vi-VN', { maximumFractionDigits: 2 });
