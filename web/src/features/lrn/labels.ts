/** Ba bộ sách của chương trình hiện hành: chỉ đi kèm yêu cầu sinh câu hỏi, không lưu trên câu hỏi (BR-20). */
export const TEXTBOOKS = ['Kết nối tri thức với cuộc sống', 'Chân trời sáng tạo', 'Cánh diều'] as const;

/** Trường THCS: khối 6 đến 9. */
export const GRADES = [6, 7, 8, 9] as const;
export const GRADE_OPTIONS = GRADES.map((g) => ({ value: g, label: `Khối ${g}` }));

export const OPTION_LABELS = ['A', 'B', 'C', 'D'] as const;

/** Thời lượng gợi ý khi giao bài (phút). */
export const DURATION_PRESETS = [15, 30, 45, 60] as const;
