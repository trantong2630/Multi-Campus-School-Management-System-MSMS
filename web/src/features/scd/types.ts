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
  occupiedViceName: string | null;
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
