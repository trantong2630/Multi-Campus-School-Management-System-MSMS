import type { RouteObject } from 'react-router-dom';
import { AbsencePage } from './pages/AbsencePage';
import { StudentSchedulePage } from './pages/StudentSchedulePage';
import { ResponsibilityPage } from './pages/ResponsibilityPage';
import { SchedulePage } from './pages/SchedulePage';
import { TimetablePage } from './pages/TimetablePage';

export const scdRoutes: RouteObject[] = [
  { path: 'scd/timetable', element: <TimetablePage /> },
  { path: 'scd/responsibilities', element: <ResponsibilityPage /> },
  { path: 'scd/schedule', element: <SchedulePage /> },
  { path: 'scd/absences', element: <AbsencePage /> },
  { path: 'scd/my-class', element: <StudentSchedulePage /> },
];
