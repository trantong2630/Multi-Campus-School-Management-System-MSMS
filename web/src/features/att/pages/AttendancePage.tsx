import { useAuth } from '@/app/AuthProvider';
import { ManageAttendanceView } from './ManageAttendanceView';
import { StudentAttendanceView } from './StudentAttendanceView';
import { TeacherAttendanceView } from './TeacherAttendanceView';

/** Màn Điểm danh (Conventions §4.7: SA, HT, HP, GV, HS, PH) — nội dung theo loại tài khoản. */
export function AttendancePage() {
  const { user } = useAuth();
  switch (user?.accountType) {
    case 'Teacher': return <TeacherAttendanceView />;
    case 'Student':
    case 'Parent': return <StudentAttendanceView />;
    case 'SchoolAdmin':
    case 'Principal':
    case 'VicePrincipal': return <ManageAttendanceView />;
    default: return null;
  }
}
