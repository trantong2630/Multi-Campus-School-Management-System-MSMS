import type { RouteObject } from 'react-router-dom';
import { StudentProfilePage } from '@/features/ppl/pages/StudentProfilePage';
import { CampusPage } from './pages/CampusPage';
import { ClassPlacementPage } from './pages/ClassPlacementPage';
import { ClassesPage } from './pages/ClassesPage';
import { HolidaysPage } from './pages/HolidaysPage';
import { HomeroomStudentsPage } from './pages/HomeroomStudentsPage';
import { PeriodSlotsPage } from './pages/PeriodSlotsPage';
import { RolloverPage } from './pages/RolloverPage';
import { SchoolProfilePage } from './pages/SchoolProfilePage';
import { SubjectsPage } from './pages/SubjectsPage';
import { YearsPage } from './pages/YearsPage';

export const schRoutes: RouteObject[] = [
  { path: 'sch/school', element: <SchoolProfilePage /> },
  { path: 'sch/campuses', element: <CampusPage /> },
  { path: 'sch/subjects', element: <SubjectsPage /> },
  { path: 'sch/years', element: <YearsPage /> },
  { path: 'sch/holidays', element: <HolidaysPage /> },
  { path: 'sch/period-slots', element: <PeriodSlotsPage /> },
  { path: 'sch/classes', element: <ClassesPage /> },
  { path: 'sch/class-placement', element: <ClassPlacementPage /> },
  { path: 'sch/rollover', element: <RolloverPage /> },
  { path: 'sch/homeroom-students', element: <HomeroomStudentsPage /> },
  // Chủ nhiệm mở hồ sơ học sinh lớp mình: cùng trang với danh bạ, quyền theo mục Danh sách học sinh.
  { path: 'sch/homeroom-students/:id', element: <StudentProfilePage /> },
];
