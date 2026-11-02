// ---------- Dùng chung ----------

export interface GradeCount {
  gradeLevel: number;
  classCount: number;
  studentCount: number;
}

/** Tỷ lệ nộp tính trên bài đã đóng; null = chưa có bài đóng. */
export interface SubjectRate {
  subjectId: number;
  name: string;
  assignmentCount: number;
  submissionRate: number | null;
}

export interface WeekPoint {
  weekStart: string;
  sessions: number;
  assignments: number;
  submissionRate: number | null;
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
  submissionRate: number | null;
}

export interface DashboardAttention {
  pendingSessions: number;
  teacherAbsencesToday: number;
  /** Null với Hiệu trưởng: không đọc đơn nghỉ. */
  pendingLeaveRequests: number | null;
  classesWithoutHomeroom: number;
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
  subjects: SubjectRate[];
  weeks: WeekPoint[];
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
  accounts: AccountTypeRow[];
  gaps: DataGap[];
  activity: { date: string; count: number }[];
  activityByCategory: { category: string; count: number }[];
  timetables: { campusId: number; name: string; classCount: number; publishedClassCount: number }[];
}

// ---------- Giáo viên ----------

export interface TeacherSessionItem {
  sessionId: number;
  periodNo: number;
  sessionOfDay: string;
  startTime: string;
  endTime: string;
  classId: number;
  className: string;
  subjectName: string;
}

export interface TeacherClassRow {
  classId: number;
  className: string;
  gradeLevel: number;
  subjects: string[];
  studentCount: number;
  assignmentCount: number;
  submissionRate: number | null;
}

export interface TeacherAssignmentItem {
  assignmentId: number;
  title: string;
  className: string;
  subjectName: string;
  dueAt: string;
  isClosed: boolean;
  recipientCount: number;
  submittedCount: number;
}

export interface TeacherDashboard {
  fullName: string;
  academicYearName: string | null;
  semesterName: string | null;
  sessionsToday: number;
  sessionsThisWeek: number;
  classCount: number;
  studentCount: number;
  subjectCount: number;
  openAssignments: number;
  dueSoon: number;
  assignmentsWithMissing: number;
  submissionRate: number | null;
  today: TeacherSessionItem[];
  classes: TeacherClassRow[];
  recent: TeacherAssignmentItem[];
  homeroomClasses: NamedId[];
  headDepartments: NamedId[];
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
  submissionRate: number | null;
  subjects: SubjectRate[];
  teachers: { subjectName: string; teacherName: string }[];
  students: HomeroomStudentRow[];
}
