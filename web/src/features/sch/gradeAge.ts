/**
 * Cảnh báo mềm khi xếp lớp: học sinh lệch tuổi nhiều so với khối (nhập nhầm năm sinh, hoặc em đi học muộn / học lại sau thời gian nghỉ).
 * Chỉ nhắc để School Admin xác nhận, không chặn: trường có học sinh vùng cao vào lớp 6 ở tuổi 15–17 là chuyện có thật.
 * Tuổi tính theo năm sinh: năm bắt đầu năm học trừ năm sinh. Tuổi thường gặp của khối N là N + 5 (lớp 6 → 11 tuổi).
 */
export const GRADE_AGE_TOLERANCE = 3;

export interface GradeAgeNotice {
  age: number;
  typicalAge: number;
  older: boolean;
  text: string;
}

export function gradeAgeNotice(dateOfBirth: string | null, gradeLevel: number, yearStartDate: string | undefined): GradeAgeNotice | null {
  if (!dateOfBirth || !yearStartDate) return null;
  const age = Number(yearStartDate.slice(0, 4)) - Number(dateOfBirth.slice(0, 4));
  const typicalAge = gradeLevel + 5;
  if (!Number.isFinite(age) || Math.abs(age - typicalAge) < GRADE_AGE_TOLERANCE) return null;
  const older = age > typicalAge;
  return {
    age, typicalAge, older,
    text: `${age} tuổi, ${older ? 'lớn' : 'nhỏ'} hơn nhiều so với tuổi thường gặp của khối ${gradeLevel} (khoảng ${typicalAge} tuổi)`,
  };
}
