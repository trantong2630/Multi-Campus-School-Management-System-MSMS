/** Ba giá trị giáo viên gửi và được lưu (BR-17). */
export type AttendanceStatus = 'Present' | 'Late' | 'Absent';

/** Nhãn cuối, luôn suy ra ở Backend (spec 6.2). Khớp domain `attendance` trong labels/status.ts. */
export type AttendanceLabel = 'Present' | 'Late' | 'ExcusedAbsent' | 'UnexcusedAbsent' | 'NotMarked';

export type SessionStatus = 'Scheduled' | 'Pending' | 'Cancelled';

export interface AttendanceCounts {
  present: number;
  late: number;
  excusedAbsent: number;
  unexcusedAbsent: number;
  notMarked: number;
}

export interface AttendanceSession {
  sessionId: number;
  date: string;
  periodSlotId: number;
  sessionOfDay: string;
  periodNo: number;
  startTime: string;
  endTime: string;
  classId: number;
  className: string;
  campusName: string;
  subjectId: number;
  subjectName: string;
  teacherName: string | null;
  status: SessionStatus;
  rosterCount: number;
  markedCount: number;
  /** Đã xếp lịch, ngày đã qua, chưa có bản ghi nào (4.5.6). */
  isMissing: boolean;
  canWrite: boolean;
}

export interface AttendanceSheetRow {
  studentId: number;
  studentCode: string;
  fullName: string;
  status: AttendanceStatus | null;
  label: AttendanceLabel;
  note: string | null;
  isBackfilled: boolean;
  recordedByName: string | null;
  recordedAt: string | null;
  updatedByName: string | null;
  updatedAt: string | null;
}

export interface AttendanceSheet {
  session: AttendanceSession;
  isAdminMode: boolean;
  readOnlyReason: string | null;
  counts: AttendanceCounts;
  students: AttendanceSheetRow[];
}

export interface SaveAttendanceItem {
  studentId: number;
  status: AttendanceStatus;
  note?: string | null;
}

export interface AttendanceClassOption {
  classId: number;
  name: string;
  campusId: number;
  campusName: string;
  isHomeroom: boolean;
}

export interface StudentAttendanceItem {
  sessionId: number;
  date: string;
  periodSlotId: number;
  sessionOfDay: string;
  periodNo: number;
  className: string;
  subjectId: number;
  subjectName: string;
  teacherName: string | null;
  label: AttendanceLabel;
  note: string | null;
}

export interface StudentAttendance {
  studentId: number;
  fullName: string;
  studentCode: string;
  counts: AttendanceCounts;
  items: StudentAttendanceItem[];
}

export interface ClassAttendanceStudent {
  studentId: number;
  studentCode: string;
  fullName: string;
  counts: AttendanceCounts;
  /** Nhãn theo sessionId (khóa là chuỗi trong JSON); thiếu khóa = học sinh không thuộc lớp vào ngày của tiết. */
  labels: Record<string, AttendanceLabel>;
}

export interface ClassAttendance {
  classId: number;
  className: string;
  from: string;
  to: string;
  sessions: AttendanceSession[];
  students: ClassAttendanceStudent[];
}
