import type { ImportError } from '@/shared/components/ImportErrorPanel';

export interface CampusOption {
  campusId: number;
  name: string;
}

export interface TimetableVersion {
  versionId: number;
  campusId: number;
  campusName: string;
  semesterId: number;
  semesterName: string;
  academicYearName: string;
  status: 'Draft' | 'Published';
  sourceFileName: string | null;
  importedAt: string;
  importedByName: string | null;
  publishedAt: string | null;
  deletedSessionCount: number | null;
  createdSessionCount: number | null;
  entryCount: number;
}

export interface TimetableImportResult {
  accepted: boolean;
  versionId: number | null;
  entryCount: number;
  errors: ImportError[];
  totalErrors: number;
}

export interface GridClass {
  classId: number;
  name: string;
}

export interface GridCell {
  subjectId: number;
  subjectAbbr: string;
  subjectName: string;
  teacherId: number;
  teacherAbbr: string;
  teacherName: string;
  /** Ô trùng giáo viên (BR-13); chỉ có nghĩa ở bản Nháp. */
  inConflict: boolean;
  /** Ô khác nội dung file import; chỉ có nghĩa ở bản Nháp. */
  edited: boolean;
}

export interface GridRow {
  dayOfWeek: number;
  periodSlotId: number;
  sessionOfDay: 'Morning' | 'Afternoon';
  periodNo: number;
  cells: (GridCell | null)[];
}

export interface VersionGrid {
  versionId: number;
  status: string;
  campusName: string;
  semesterName: string;
  classes: GridClass[];
  rows: GridRow[];
  conflicts: TeacherConflict[];
  /** Bản Nháp còn lưu nội dung file import nên khôi phục được. */
  canReset: boolean;
}

export interface SubjectOption {
  subjectId: number;
  abbr: string;
  name: string;
}

export interface DraftOptions {
  subjects: SubjectOption[];
  teachers: TeacherOption[];
}

export interface TeacherConflict {
  teacherName: string;
  teacherAbbr: string;
  dayOfWeek: number;
  sessionOfDay: 'Morning' | 'Afternoon';
  periodNo: number;
  newClassName: string;
  occupiedCampusName: string;
  occupiedClassName: string;
  occupiedSubject: string;
  sessionCount: number;
  firstDate: string;
  lastDate: string;
}

export interface PublishPreview {
  versionId: number;
  campusName: string;
  semesterName: string;
  fromDate: string;
  toDate: string;
  deleteCount: number;
  createCount: number;
  byClass: { className: string; deleteCount: number; createCount: number }[];
  warnings: { manualEditedCount: number; makeUpCount: number; pendingCount: number; needsConfirmation: boolean };
  conflicts: TeacherConflict[];
  blockingIssues: string[];
  classesWithoutLessons: string[];
  canPublish: boolean;
}

export interface PublishResult {
  versionId: number;
  deletedCount: number;
  createdCount: number;
  notifiedCount: number;
}

export interface SessionCell {
  sessionId: number;
  subjectId: number;
  subjectName: string;
  subjectAbbr: string;
  teacherName: string | null;
  teacherAbbr: string | null;
  status: 'Scheduled' | 'Pending' | 'Cancelled';
  source: string;
}

export interface DaySlotRow {
  periodSlotId: number;
  sessionOfDay: 'Morning' | 'Afternoon';
  periodNo: number;
  startTime: string;
  endTime: string;
  cells: (SessionCell | null)[];
}

export interface DaySchedule {
  date: string;
  holidayReason: string | null;
  isHoliday: boolean;
  classes: GridClass[];
  rows: DaySlotRow[];
}

export interface MySession {
  sessionId: number;
  date: string;
  periodSlotId: number;
  sessionOfDay: 'Morning' | 'Afternoon';
  periodNo: number;
  startTime: string;
  endTime: string;
  className: string;
  campusName: string;
  subjectId: number;
  subjectName: string;
  subjectAbbr: string;
  status: 'Scheduled' | 'Pending' | 'Cancelled';
  source: string;
}

export interface TeacherOption {
  teacherId: number;
  name: string;
  abbr: string | null;
}

export interface ViewSession extends Omit<MySession, 'status'> {
  teacherName: string | null;
  status: MySession['status'];
}

// ---- SCD-04: Phụ trách chuyên môn (tính từ thời khóa biểu đã phát hành) ----
export interface ResponsibilityItem {
  subjectId: number;
  subjectAbbr: string;
  subjectName: string;
  classId: number;
  className: string;
  periodsPerWeek: number;
}

export interface ResponsibilityTeacher {
  teacherId: number;
  teacherName: string;
  teacherAbbr: string | null;
  totalPeriods: number;
  items: ResponsibilityItem[];
}

export interface ResponsibilityList {
  semesterName: string | null;
  teachers: ResponsibilityTeacher[];
}

// ---- SCD-02: Điều chỉnh lịch dạy (BR-15) ----
export type SessionStatus = 'Scheduled' | 'Pending' | 'Cancelled';

export interface AdjustSession {
  sessionId: number;
  date: string;
  periodSlotId: number;
  subjectId: number;
  subjectName: string;
  subjectAbbr: string;
  teacherId: number | null;
  teacherName: string | null;
  teacherAbbr: string | null;
  status: SessionStatus;
  source: 'Generated' | 'Manual';
  /** Tiết bị khóa (mục 6.2): ngày đã qua hoặc đã có điểm danh — không đổi giáo viên, không hủy. */
  locked: boolean;
  isMakeUp: boolean;
  changeNote: string | null;
}

export interface AdjustWeek {
  classId: number;
  className: string;
  campusId: number;
  from: string;
  to: string;
  canManage: boolean;
  sessions: AdjustSession[];
  holidays: { date: string; reason: string | null }[];
}

export interface TeacherAvailability {
  teacherId: number;
  name: string;
  abbr: string | null;
  campusName: string;
  busy: boolean;
  busyNote: string | null;
  absent: boolean;
}

export interface MakeUpBody {
  classId: number;
  date: string;
  periodSlotId: number;
  subjectId: number;
  teacherId: number;
}

export interface AdjustResult {
  sessionId: number;
  notifiedCount: number;
}

// ---- SCD-03: Giáo viên vắng (BR-16) ----
export interface Absence {
  absenceId: number;
  teacherId: number;
  teacherName: string;
  teacherAbbr: string | null;
  fromDate: string;
  toDate: string;
  /** Rỗng nghĩa là vắng cả ngày. */
  slotIds: number[];
  reason: string | null;
  declaredByName: string | null;
  createdAt: string;
  pendingCount: number;
  handledCount: number;
  /** Chỉ xóa được khi chưa có tiết nào của khai báo bị xử lý. */
  canDelete: boolean;
}

export interface DeclareAbsenceBody {
  teacherId: number;
  fromDate: string;
  toDate: string;
  slotIds?: number[];
  reason?: string;
}

export interface PendingSession {
  sessionId: number;
  date: string;
  periodSlotId: number;
  sessionOfDay: 'Morning' | 'Afternoon';
  periodNo: number;
  startTime: string;
  endTime: string;
  classId: number;
  className: string;
  subjectId: number;
  subjectName: string;
  subjectAbbr: string;
  absenceId: number | null;
  absentTeacherName: string | null;
  note: string | null;
}

export type ResolveAction = 'Substitute' | 'Cancel';

export interface ResolveBody {
  action: ResolveAction;
  teacherId?: number;
}
