// ---------- Dùng chung ----------

export interface GradeCount {
  gradeLevel: number;
  classCount: number;
  studentCount: number;
  attendanceRate?: number | null;
}

/** Một tuần: tỷ lệ có mặt (null khi chưa có lượt điểm danh) và số lượt vắng. */
export interface AttendanceWeek {
  weekStart: string;
  rate: number | null;
  absent: number;
}

/** Chuyên cần theo thứ trong tuần: 1 = Thứ Hai … 6 = Thứ Bảy. */
export interface WeekdayRate {
  day: number;
  rate: number | null;
  absent: number;
}

export interface NamedId {
  id: number;
  name: string;
}

// ---------- Hiệu trưởng, hiệu phó ----------

export interface DashboardCampusRow {
  campusId: number;
  name: string;
  classCount: number;
  studentCount: number;
  staffCount: number;
  sessionsToday: number;
  leaders: string[];
  attendanceRate: number | null;
}

export interface DashboardAttention {
  pendingSessions: number;
  teacherAbsencesToday: number;
  /** Null với Hiệu trưởng: không đọc đơn nghỉ. */
  pendingLeaveRequests: number | null;
  classesWithoutHomeroom: number;
}

/** Chuyên cần 4 tuần gần nhất. Rate null khi chưa có lượt điểm danh nào. */
export interface AttendanceSummary {
  rate: number | null;
  present: number;
  late: number;
  absentExcused: number;
  absentUnexcused: number;
  /** Tiết đã qua ngày, Đã xếp lịch mà chưa có điểm danh. */
  unmarkedSessions: number;
  todaySessions: number;
  todayMarkedSessions: number;
}

export interface ClassRow {
  classId: number;
  className: string;
  gradeLevel: number;
  campusId: number;
  campusName: string;
  homeroom: string | null;
  studentCount: number;
  attendanceRate: number | null;
  unmarkedSessions: number;
}

export interface Dashboard {
  campusId: number | null;
  academicYearName: string | null;
  semesterName: string | null;
  campusCount: number;
  gradeCount: number;
  classCount: number;
  studentCount: number;
  staffCount: number;
  sessionsToday: number;
  attention: DashboardAttention;
  byGrade: GradeCount[];
  campuses: DashboardCampusRow[];
  weeks: AttendanceWeek[];
  attendance: AttendanceSummary;
  classes: ClassRow[];
  weekdays: WeekdayRate[];
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

// ---------- Tiến độ bài tập rèn luyện ----------

export interface ProgressClass { classId: number; className: string; gradeLevel: number; campusId: number }
export interface ProgressTeacher { teacherId: number; name: string; campusId: number; departmentId: number | null; departmentName: string | null; duties: number }
export interface ProgressItem {
  assignmentId: number; title: string; campusId: number; campusName: string; departmentId: number; departmentName: string;
  classId: number; className: string; gradeLevel: number; subjectId: number; subjectName: string; teacherId: number; teacherName: string;
  openAt: string; dueAt: string; isClosed: boolean; recipientCount: number; submittedCount: number;
}
export interface Progress {
  scope: 'School' | 'Campus' | 'Class';
  academicYearName: string | null;
  semesterName: string | null;
  now: string;
  campuses: NamedId[];
  classes: ProgressClass[];
  teachers: ProgressTeacher[];
  items: ProgressItem[];
}

// ---------- Lớp chủ nhiệm ----------

export interface HomeroomStudentRow {
  studentId: number;
  personCode: string;
  fullName: string;
  gender: string | null;
  dateOfBirth: string | null;
  leaveCount: number;
  pendingLeave: number;
  parentCount: number;
  parentName: string | null;
  parentPhone: string | null;
  parentHasAccount: boolean;
  absentExcused: number;
  absentUnexcused: number;
}

export interface HomeroomOverview {
  classId: number;
  className: string;
  gradeLevel: number;
  campusName: string;
  academicYearName: string | null;
  classes: NamedId[];
  studentCount: number;
  maleCount: number;
  femaleCount: number;
  pendingLeave: number;
  leaveLast30Days: number;
  studentsWithoutParent: number;
  parentsWithoutAccount: number;
  teachers: { subjectName: string; teacherName: string }[];
  students: HomeroomStudentRow[];
  attendance: AttendanceSummary;
  attendanceWeeks: AttendanceWeek[];
}

// ---------- Tình hình tổ chuyên môn ----------

export interface DepartmentMemberRow {
  staffId: number;
  name: string;
  campusName: string | null;
  inDepartment: boolean;
  periodsPerWeek: number;
  absentToday: boolean;
  unmarkedSessions: number;
  teaching: { subjectName: string; classNames: string[] }[];
}

export interface DepartmentBoard {
  departments: NamedId[];
  subjectNames: string[];
  semesterName: string | null;
  sessionsToday: number;
  pendingSessions: number;
  absentToday: number;
  attendance: AttendanceSummary;
  attendanceWeeks: AttendanceWeek[];
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
