export interface SchoolProfile {
  name: string | null;
  address: string | null;
  phoneNumber: string | null;
  email: string | null;
  website: string | null;
  hasLogo: boolean;
  logoVersion: number | null;
}

export interface Campus {
  campusId: number;
  code: string;
  name: string;
  address: string | null;
  phoneNumber: string | null;
  isActive: boolean;
  activeClassCount: number;
}

export interface Department {
  departmentId: number;
  name: string;
  description: string | null;
  isActive: boolean;
  subjectCount: number;
}

export interface Subject {
  subjectId: number;
  departmentId: number;
  departmentName: string;
  name: string;
  abbreviation: string;
  isActive: boolean;
}

export interface Semester {
  semesterId: number;
  semesterNo: number;
  name: string;
  startDate: string;
  endDate: string;
}

export interface AcademicYear {
  academicYearId: number;
  name: string;
  startDate: string;
  endDate: string;
  isCurrent: boolean;
  semesters: Semester[];
  /** Ngày kết thúc muộn nhất của học kỳ 1 và 2, do backend tính (SemesterRules). */
  semester1LatestEnd: string;
  semester2LatestEnd: string;
}

export interface Holiday {
  holidayId: number;
  academicYearId: number;
  campusId: number | null;
  campusName: string | null;
  holidayDate: string;
  reason: string | null;
  isAdHoc: boolean;
}

export interface PeriodSlot {
  periodSlotId: number;
  sessionOfDay: 'Morning' | 'Afternoon';
  periodNo: number;
  startTime: string;
  endTime: string;
  isActive: boolean;
}

export interface SchoolClass {
  classId: number;
  campusId: number;
  campusName: string;
  academicYearId: number;
  academicYearName: string;
  name: string;
  gradeLevel: number;
  isActive: boolean;
  studentCount: number;
  homeroomName: string | null;
}

export interface RosterItem {
  enrollmentId: number;
  studentId: number;
  personCode: string;
  fullName: string;
  dateOfBirth: string | null;
  gender: string | null;
  startDate: string;
  endDate: string | null;
  reason: string | null;
}

export interface Roster {
  classId: number;
  className: string;
  date: string;
  students: RosterItem[];
}

export interface StudentCandidate {
  studentId: number;
  personCode: string;
  fullName: string;
  dateOfBirth: string | null;
  gender: string | null;
}

export interface RolloverTarget {
  classId: number;
  name: string;
  gradeLevel: number;
  isActive: boolean;
}

export interface RolloverRow {
  sourceClassId: number;
  sourceName: string;
  campusId: number;
  campusName: string;
  gradeLevel: number;
  studentCount: number;
  alreadyEnrolled: number;
  suggestedName: string;
  suggestedGrade: number | null;
  target: RolloverTarget | null;
}

export interface RolloverCreateResult {
  created: number;
  classes: SchoolClass[];
}

/** Học sinh còn học ở cuối năm cũ của một lớp, kèm lớp năm mới đã xếp (nếu có). */
export interface RolloverStudent {
  studentId: number;
  personCode: string;
  fullName: string;
  newClassId: number | null;
  newClassName: string | null;
  graduated: boolean;
  dateOfBirth: string | null;
  gender: string | null;
}

export interface RolloverMoveResult {
  moved: number;
  skipped: number;
  items: { sourceClassId: number; targetClassId: number; moved: number; skipped: number }[];
}

// ---- Thống kê cấu trúc trên màn Thiết lập (tính tại hôm nay, năm học hiện hành) ----

export interface CampusStat {
  campusId: number;
  classCount: number;
  studentCount: number;
  staffCount: number;
  leaders: string[];
}

export interface DepartmentStat { departmentId: number; headName: string | null; teacherCount: number }
/** Theo thời khóa biểu đã phát hành của học kỳ hiện hành. */
export interface SubjectStat { subjectId: number; teacherCount: number; classCount: number }

export interface CatalogStats {
  semesterName: string | null;
  departments: DepartmentStat[];
  subjects: SubjectStat[];
  /** Chỉ School Admin nhận số; vai trò khác là null. */
  teachersWithoutDepartment: number | null;
}

export interface ClassStats {
  /** Chỉ School Admin nhận số; vai trò khác là null. */
  studentsWithoutClass: number | null;
}
