import type { RouteObject } from 'react-router-dom';
import { SchedulePage } from './pages/SchedulePage';
import { TimetablePage } from './pages/TimetablePage';

export const scdRoutes: RouteObject[] = [
  { path: 'scd/timetable', element: <TimetablePage /> },
  { path: 'scd/schedule', element: <SchedulePage /> },
];
