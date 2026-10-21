import type { ImportError, SkippedRow } from '@/shared/components/ImportErrorPanel';

export interface Staff {
  staffId: number;
  personCode: string;
  fullName: string;
  dateOfBirth: string | null;
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
}

export interface ResponsibilityHistoryRow {
  recordId: number;
  staffId: number;
  staffName: string;
  startDate: string;
  endDate: string | null;
  isEffective: boolean;
}
