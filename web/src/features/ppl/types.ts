import type { AttendanceCounts, StudentAttendanceItem } from '@/features/att/types';
import type { ImportError, SkippedRow } from '@/shared/components/ImportErrorPanel';

export interface Staff {
  staffId: number;
  personCode: string;
  fullName: string;
  dateOfBirth: string | null;
  nationalId: string | null;
  gender: string | null;
  phoneNumber: string | null;
  email: string | null;
  primaryCampusId: number;
  primaryCampusName: string;
  departmentId: number | null;
  departmentName: string | null;
  teacherAbbr: string | null;
  position: string | null;
  isActive: boolean;
  accountId: number | null;
  accountType: string | null;
  accountStatus: string | null;
}

export interface Student {
  studentId: number;
  personCode: string;
  fullName: string;
  dateOfBirth: string | null;
  nationalId: string;
  gender: string | null;
  phoneNumber: string | null;
  address: string | null;
  status: 'Studying' | 'Transferred' | 'Graduated';
  note: string | null;
  campusId: number | null;
  campusName: string | null;
  classId: number | null;
  className: string | null;
  guardianName: string | null;
  guardianPhone: string | null;
}

/** Hồ sơ một học sinh, chỉ xem (PPL-02, 4.3.12). */
export interface StudentDetail {
  studentId: number;
  personCode: string;
  fullName: string;
  dateOfBirth: string | null;
  nationalId: string | null;
  gender: string | null;
  phoneNumber: string | null;
  address: string | null;
  status: Student['status'];
  note: string | null;
  admissionDate: string | null;
  leftDate: string | null;
  classId: number | null;
  className: string | null;
  campusName: string | null;
  guardian: StudentGuardian | null;
  enrollments: StudentEnrollment[];
  attendance: StudentAttendanceOverview | null;
}

export interface StudentGuardian {
  parentId: number;
  personCode: string;
  fullName: string;
  relationship: string | null;
  phoneNumber: string | null;
  email: string | null;
}

export interface StudentEnrollment {
  enrollmentId: number;
  academicYearName: string;
  classId: number;
  className: string;
  campusName: string;
  startDate: string;
  /** Ngày kết thúc thực tế: để trống ở ghi danh nghĩa là hết năm học. */
  endDate: string;
  reason: string | null;
  isCurrent: boolean;
}

/** Chuyên cần năm học hiện hành; rate null khi chưa có lượt điểm danh nào. */
export interface StudentAttendanceOverview {
  academicYearName: string;
  from: string;
  to: string;
  rate: number | null;
  counts: AttendanceCounts;
  recent: StudentAttendanceItem[];
}

export interface Guardian {
  parentId: number;
  personCode: string;
  fullName: string;
  phoneNumber: string | null;
  email: string | null;
  address: string | null;
  childCount: number;
  childrenSummary: string | null;
  gender: string | null;
  accountId: number | null;
  accountStatus: string | null;
}

export interface GuardianChild {
  studentId: number;
  personCode: string;
  fullName: string;
  className: string | null;
  campusName: string | null;
  studentStatus: string;
  relationship: string | null;
  isPrimary: boolean;
}

export interface ChangeGuardianBody {
  existingParentId?: number;
  newGuardian?: { fullName: string; phoneNumber: string; email?: string; address?: string };
  relationship?: string;
}

export interface Paged<T> {
  items: T[];
  total: number;
}

export interface StaffImportResult {
  accepted: boolean;
  created: number;
  skipped: SkippedRow[];
  errors: ImportError[];
  totalErrors: number;
}

export interface GuardianImportResult {
  accepted: boolean;
  created: number;
  skipped: SkippedRow[];
  errors: ImportError[];
  totalErrors: number;
}

export interface StudentImportWarning {
  row: number;
  fullName: string;
  message: string;
  personCode: string | null;
}

export interface StudentImportResult {
  accepted: boolean;
  students: number;
  enrollments: number;
  parents: number;
  skipped: SkippedRow[];
  errors: ImportError[];
  totalErrors: number;
  /** Cảnh báo không chặn: dòng vẫn được nhập nhưng nên kiểm tra lại. */
  warnings?: StudentImportWarning[];
}

export type ResponsibilityKind = 'campus' | 'department' | 'homeroom';

export interface ResponsibilityRow {
  kind: ResponsibilityKind;
  targetId: number;
  targetName: string;
  targetGroup: string | null;
  recordId: number | null;
  staffId: number | null;
  staffName: string | null;
  startDate: string | null;
  /** Người đã được xếp nhưng chưa tới ngày hiệu lực. */
  nextStaffName: string | null;
  nextStartDate: string | null;
  staffCode: string | null;
  endDate: string | null;
  /** Người giữ gần nhất đã kết thúc, khi đích không còn ai đảm nhiệm. */
  endedStaffName: string | null;
  endedDate: string | null;
  /** Giờ người đang giữ nhận trách nhiệm (thời điểm tạo bản ghi). */
  startedAt?: string | null;
  targetCode: string | null;
  nextStaffId: number | null;
}

export interface ResponsibilityHistoryRow {
  recordId: number;
  staffId: number;
  staffName: string;
  startDate: string;
  endDate: string | null;
  isEffective: boolean;
  /** Thời điểm bản ghi được tạo (giờ nhận trách nhiệm khi nhận ngay). */
  assignedAt: string;
  /** Giờ mất quyền, khi thay người hoặc kết thúc có hiệu lực ngay. */
  endedAt: string | null;
}
