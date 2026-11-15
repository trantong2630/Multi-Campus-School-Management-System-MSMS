// ---------- Báo cáo toàn trường (Hiệu trưởng) ----------

export interface ReportCampusRow {
  campusId: number;
  name: string;
  classCount: number;
  studentCount: number;
  attendanceRate: number | null;
  /** Tiết đã qua ngày, Đã xếp lịch mà chưa có điểm danh. */
  unmarkedSessions: number;
  cancelledSessions: number;
  assignmentCount: number;
  submissionRate: number | null;
}

export interface ReportClassRow {
  classId: number;
  className: string;
  gradeLevel: number;
  campusId: number;
  campusName: string;
  homeroom: string | null;
  studentCount: number;
  attendanceRate: number | null;
  unmarkedSessions: number;
  assignmentCount: number;
  submissionRate: number | null;
}

export type ReportAlertKind = 'LowAttendance' | 'ManyUnmarked' | 'LowSubmission';

export interface ReportAlert {
  kind: ReportAlertKind;
  classId: number;
  className: string;
  campusId: number;
  campusName: string;
  value: number | null;
  threshold: number | null;
}

export interface ReportThresholds {
  lowAttendanceBelow: number;
  unmarkedSessionsFrom: number;
  lowSubmissionBelow: number;
}

export interface SchoolReport {
  academicYearName: string | null;
  semesterName: string | null;
  from: string;
  to: string;
  campuses: ReportCampusRow[];
  classes: ReportClassRow[];
  alerts: ReportAlert[];
  thresholds: ReportThresholds;
  gradeOptions: number[];
  campusId: number | null;
  gradeLevel: number | null;
}
