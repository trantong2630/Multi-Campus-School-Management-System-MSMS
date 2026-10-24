export interface DashboardCampusRow {
  campusId: number;
  name: string;
  classCount: number;
  studentCount: number;
  staffCount: number;
  sessionsToday: number;
}

export interface GradeCount {
  gradeLevel: number;
  classCount: number;
  studentCount: number;
}

export interface Dashboard {
  campusId: number | null;
  academicYearName: string | null;
  classCount: number;
  studentCount: number;
  staffCount: number;
  sessionsToday: number;
  byGrade: GradeCount[];
  campuses: DashboardCampusRow[];
}
