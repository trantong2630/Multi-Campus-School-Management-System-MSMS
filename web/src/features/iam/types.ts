import type { AccountType } from '@/shared/lib/labels';

/** IAM-04: hồ sơ cá nhân trả về từ /api/iam/profile (4.1.27–4.1.30). */
export interface OwnProfile {
  personId: number;
  personCode: string;
  personType: 'Staff' | 'Student' | 'Parent';
  fullName: string;
  dateOfBirth: string | null;
  gender: string | null;
  phoneNumber: string | null;
  email: string | null;
  address: string | null;
  hasAvatar: boolean;
  /** Đổi mỗi lần hồ sơ cập nhật; gắn vào khóa tải ảnh để không dùng ảnh cũ. */
  avatarVersion: number | null;
  // Tài khoản (4.1.30)
  accountId: number;
  username: string;
  accountType: AccountType;
  status: string;
  lastLoginAt: string | null;
  // Trách nhiệm đang hiệu lực tại ngày hiện tại (4.1.27)
  activeResponsibilities: { kind: string; targetName: string; startDate: string }[];
  /** Phụ huynh không tự sửa số điện thoại vì đó là khóa nhận diện (BR-10). */
  canEditPhone: boolean;
}

export interface AccountScope {
  asOfDate: string;
  isSchoolWide: boolean;
  ledCampusIds: number[];
  headDepartmentIds: number[];
  homeroomClassIds: number[];
  subjectResponsibilities: { classId: number; subjectId: number; semesterId: number }[];
  enrollments: { studentId: number; enrollmentId: number; classId: number; campusId: number; academicYearId: number }[];
  linkedStudentIds: number[];
}

export interface UserInfo {
  accountId: number;
  personId: number;
  personCode: string;
  fullName: string;
  accountType: AccountType;
  status: string;
  mustChangePassword: boolean;
  avatarPath: string | null;
  scope: AccountScope;
}

export interface LoginResponse {
  token: string;
  expiresAt: string;
  user: UserInfo;
}

export interface LockImpact {
  sessionCount: number;
  campuses: { campus: string; count: number }[];
  duties: { kind: string; targetName: string }[];
}

export interface Account {
  accountId: number;
  personId: number;
  personCode: string;
  fullName: string;
  personType: string;
  username: string;
  accountType: AccountType;
  status: string;
  lastLoginAt: string | null;
  campusName: string | null;
}

export interface AccountDirectoryItem {
  personId: number;
  personCode: string;
  fullName: string;
  personType: string;
  campusName: string | null;
  hasAccount: boolean;
  isActive: boolean;
}

export interface TempPasswordItem {
  personId: number;
  personCode: string;
  fullName: string;
  username: string;
  accountType: string;
  tempPassword: string;
}

export interface BulkCreateResult {
  created: TempPasswordItem[];
  skipped: { personId: number; fullName: string; reason: string }[];
}

export interface ResetPasswordResult {
  username: string;
  tempPassword: string;
}
