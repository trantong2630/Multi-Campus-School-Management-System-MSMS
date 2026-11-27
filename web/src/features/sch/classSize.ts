/**
 * Cảnh báo mềm sĩ số lớp: hệ thống không giới hạn sĩ số (spec 7.8) nên chỉ nhắc khi xếp hoặc chuyển thêm học sinh làm lớp vượt mức thường gặp.
 * Không chặn; School Admin vẫn xếp được.
 */
export const CLASS_SIZE_WARN = 45;

/** Câu cảnh báo nếu sau khi thêm `adding` học sinh, lớp có hơn <see cref="CLASS_SIZE_WARN"/> em; không vượt thì trả null. */
export function classSizeNotice(className: string, currentCount: number, adding: number): string | null {
  const after = currentCount + adding;
  if (adding <= 0 || after <= CLASS_SIZE_WARN) return null;
  return `Lớp ${className} sẽ có ${after} học sinh, vượt mức thường gặp ${CLASS_SIZE_WARN} em. Vẫn xếp được; kiểm tra lại nếu chưa chủ ý.`;
}
