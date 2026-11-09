/** Bộ sách dùng thống nhất toàn quốc (QĐ 3588/QĐ-BGDĐT): chỉ đi kèm yêu cầu sinh câu hỏi, không lưu trên câu hỏi (BR-20). */
export const TEXTBOOK = 'Kết nối tri thức với cuộc sống';

/** Trường THCS: khối 6 đến 9. */
export const GRADES = [6, 7, 8, 9] as const;
export const GRADE_OPTIONS = GRADES.map((g) => ({ value: g, label: `Khối ${g}` }));

export const OPTION_LABELS = ['A', 'B', 'C', 'D'] as const;

/** Thời lượng gợi ý khi giao bài (phút). */
export const DURATION_PRESETS = [15, 30] as const;

/** Trang sách theo số in: "trang 27" khi bài chỉ một trang, "trang 9–12" khi nhiều trang. */
export const pagesText = (start: number, end: number) => (start === end ? `trang ${start}` : `trang ${start}–${end}`);
