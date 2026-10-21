export type AccountType = 'SchoolAdmin' | 'Principal' | 'VicePrincipal' | 'Teacher' | 'Student' | 'Parent';

export const accountTypeLabel: Record<AccountType, string> = {
  SchoolAdmin: 'Quản trị hệ thống',
  Principal: 'Hiệu trưởng',
  VicePrincipal: 'Hiệu phó',
  Teacher: 'Giáo viên',
  Student: 'Học sinh',
  Parent: 'Phụ huynh',
};
