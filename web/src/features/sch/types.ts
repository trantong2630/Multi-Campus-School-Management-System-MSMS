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

export interface RolloverMoveResult {
  moved: number;
  skipped: number;
  items: { sourceClassId: number; targetClassId: number; moved: number; skipped: number }[];
}
