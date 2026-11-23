import type { RouteObject } from 'react-router-dom';
import { ResponsibilitiesPage } from './pages/ResponsibilitiesPage';
import { GuardiansPage } from './pages/GuardiansPage';
import { StaffPage } from './pages/StaffPage';
import { StaffProfilePage } from './pages/StaffProfilePage';
import { StudentProfilePage } from './pages/StudentProfilePage';
import { StudentsPage } from './pages/StudentsPage';

export const pplRoutes: RouteObject[] = [
  { path: 'ppl/staff', element: <StaffPage /> },
  { path: 'ppl/staff/:id', element: <StaffProfilePage /> },
  { path: 'ppl/students', element: <StudentsPage /> },
  // Hồ sơ học sinh: trang con của danh bạ nên dùng chung quyền và breadcrumb Hồ sơ › Học sinh.
  { path: 'ppl/students/:id', element: <StudentProfilePage /> },
  { path: 'ppl/parents', element: <GuardiansPage /> },
  { path: 'ppl/responsibilities', element: <ResponsibilitiesPage /> },
];
