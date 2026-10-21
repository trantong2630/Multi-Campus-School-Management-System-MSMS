import type { RouteObject } from 'react-router-dom';
import { CampusPage } from './pages/CampusPage';
import { ClassesPage } from './pages/ClassesPage';
import { HolidaysPage } from './pages/HolidaysPage';
import { PeriodSlotsPage } from './pages/PeriodSlotsPage';
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
];
