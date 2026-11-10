import type { RouteObject } from 'react-router-dom';
import { AttendancePage } from './pages/AttendancePage';
import { AttendanceSheetPage } from './pages/AttendanceSheetPage';

export const attRoutes: RouteObject[] = [
  { path: 'att', element: <AttendancePage /> },
  { path: 'att/sessions/:sessionId', element: <AttendanceSheetPage /> },
];
