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

export interface StudentImportResult {
  accepted: boolean;
  students: number;
  enrollments: number;
  parents: number;
  skipped: SkippedRow[];
  errors: ImportError[];
  totalErrors: number;
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
}
