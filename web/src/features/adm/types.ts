// ---------- Dùng chung ----------

export interface NamedId {
  id: number;
  name: string;
}

/** Chỉ số chính: 4 tuần gần nhất, 4 tuần liền trước và 8 giá trị theo tuần (tuần cuối là tuần này). */
export interface Kpi {
  value: number | null;
  previous: number | null;
  weeks: (number | null)[];
}

export type AffectedOutcome = 'Substituted' | 'Cancelled' | 'Pending';

/** Tiết bị ảnh hưởng: của giáo viên vắng (dạy thay, hủy, chưa xử lý) hoặc đã hủy vì lý do khác. */
export interface AffectedSession {
  sessionId: number;
  date: string;
  sessionOfDay: string;
  periodNo: number;
  startTime: string;
  className: string;
  subjectName: string;
  absentTeacher: string | null;
  teacher: string | null;
  outcome: AffectedOutcome;
}

/** Cơ cấu lượt điểm danh 4 tuần. */
export interface AttendanceMix { present: number; late: number; absentExcused: number; absentUnexcused: number }
/** Tiết bị ảnh hưởng của một tuần: số giáo viên vắng và số tiết theo kết quả. */
export interface AbsenceWeek { weekStart: string; teachers: number; substituted: number; cancelled: number; pending: number }
/** Tổng quan bài rèn luyện: mở trong 4 tuần, đang mở, hết hạn trong 7 ngày tới, đã hết hạn mà nộp dưới 50%. */
export interface AssignmentSummary { given: number; open: number; dueSoon: number; lowCount: number }
export interface RateRow { id: number; name: string; rate: number | null }

// ---------- Hiệu trưởng, hiệu phó ----------

export interface LeadershipAlerts {
  streakStudents: number;
  uncoveredSessions: number;
  unmarkedSessions: number;
  /** Chỉ Hiệu trưởng có (người phân công chủ nhiệm). */
  classesWithoutHomeroom: number | null;
}

export interface LeadershipClassRow {
  classId: number; className: string; gradeLevel: number; campusName: string; homeroom: string | null; studentCount: number;
  rate: number | null; previousRate: number | null; unexcusedRate: number | null; streakStudents: number; unmarkedSessions: number; submissionRate: number | null;
}
export interface LeadershipDepartmentRow {
  departmentId: number; name: string; teacherCount: number; absentTeachers: number; affectedSessions: number; unmarkedSessions: number;
  assignmentCount: number; submissionRate: number | null;
}
export interface LeadershipTeacherRow { staffId: number; name: string; departmentName: string | null; absentDays: number; unmarkedSessions: number }

export interface Dashboard {
  campusId: number | null;
  weekNo: number | null;
  weekStarts: string[];
  campusCount: number;
  classCount: number;
  studentCount: number;
  alerts: LeadershipAlerts;
  attendance: Kpi;
  unexcused: Kpi;
  affected: Kpi;
  submission: Kpi;
  mix: AttendanceMix;
  /** Chuyên cần 4 tuần theo phân hiệu, theo khối (chỉ nhóm có học sinh). */
  campuses: RateRow[];
  grades: RateRow[];
  /** Chuyên cần 4 tuần theo thứ Hai tới thứ Bảy. */
  weekdays: (number | null)[];
  /** Tỷ lệ nộp 4 tuần theo khối. */
  submissionGrades: RateRow[];
  assignments: AssignmentSummary;
  today: AffectedSession[];
  /** Số liệu đầy đủ cho phân tích sâu (MSMS AI); màn hình không liệt kê. */
  classes: LeadershipClassRow[];
  departments: LeadershipDepartmentRow[];
  teachers: LeadershipTeacherRow[];
  gradeOptions: number[];
  gradeLevel: number | null;
}

// ---------- School Admin ----------

export interface AccountTypeRow {
  accountType: string;
  total: number;
  active: number;
  pendingPasswordChange: number;
  locked: number;
  neverLoggedIn: number;
}

export interface DataGap {
  key: string;
  count: number;
}

export interface AdminDashboard {
  academicYearName: string | null;
  semesterName: string | null;
  semesterStart: string | null;
  semesterEnd: string | null;
  campusCount: number;
  classCount: number;
  studentCount: number;
  staffCount: number;
  accounts: AccountTypeRow[];
  gaps: DataGap[];
  activity: { date: string; count: number }[];
  activityByCategory: { category: string; count: number }[];
  timetables: { campusId: number; name: string; classCount: number; publishedClassCount: number }[];
}

// ---------- Lớp chủ nhiệm ----------

export interface SubjectSubmissionRow { subjectId: number; subjectName: string; assignmentCount: number; rate: number | null }

export interface HomeroomOverview {
  classId: number;
  className: string;
  gradeLevel: number;
  weekNo: number | null;
  classes: NamedId[];
  studentCount: number;
  /** Null khi hôm nay chưa tiết nào được điểm danh. */
  presentToday: number | null;
  todaySessions: number;
  todayMarkedSessions: number;
  pendingLeave: number;
  streakStudents: number;
  attendance: Kpi;
  gradeRate: number | null;
  submission: Kpi;
  mix: AttendanceMix;
  weekStarts: string[];
  gradeWeeks: (number | null)[];
  assignments: AssignmentSummary;
  subjects: SubjectSubmissionRow[];
}

// ---------- Tình hình tổ chuyên môn ----------

export interface DepartmentMemberRow {
  staffId: number;
  name: string;
  inDepartment: boolean;
  periodsPerWeek: number;
  classNames: string[];
  absentToday: boolean;
  absentDays: number;
  unmarkedSessions: number;
  assignmentCount: number;
  openAssignments: number;
  submissionRate: number | null;
}

export interface DepartmentBoard {
  departments: NamedId[];
  weekNo: number | null;
  weekStarts: string[];
  averagePeriods: number | null;
  affected: Kpi;
  unmarkedSessions: number;
  submission: Kpi;
  absentToday: string[];
  absenceWeeks: AbsenceWeek[];
  /** Tỷ lệ nộp 4 tuần theo môn của tổ. */
  subjects: RateRow[];
  assignments: AssignmentSummary;
  members: DepartmentMemberRow[];
}

// ---------- Nhật ký (ADM-02) ----------

export interface AuditLogFilters {
  category?: string;
  actorPersonId?: number;
  from?: string;
  to?: string;
}

export interface AuditLogItem {
  auditId: number;
  category: string;
  action: string;
  actorName: string;
  actorCode: string;
  createdAt: string;
  targetCount: number;
}

export interface AuditActor {
  personId: number;
  fullName: string;
  personCode: string;
}

export interface AuditLogDetail {
  auditId: number;
  category: string;
  action: string;
  actorName: string;
  actorCode: string;
  createdAt: string;
  targetKind: string;
  targets: { id: number; label: string | null }[];
  fields: { key: string; value: string }[];
}

/** MSMS AI (ADM-01): một lượt gửi kèm; câu của AI gửi lại đúng historyText đã nhận. */
export interface AiChatTurn {
  role: 'user' | 'ai';
  text: string;
}

export interface AiChart {
  title: string;
  kind: 'bar' | 'line';
  unit: '%' | '';
  threshold: number | null;
  higherIsBetter: boolean;
  max: number | null;
  rows: { label: string; value: number | null }[];
}

export interface AiChatAnswer {
  text: string;
  historyText: string;
  charts: AiChart[];
  steps: string[];
}

export interface AiPerson {
  code: string;
  name: string;
  detail: string;
}
