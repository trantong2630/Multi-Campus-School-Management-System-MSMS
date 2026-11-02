import type { RouteObject } from 'react-router-dom';
import { LeaveRequestsPage } from './pages/LeaveRequestsPage';
import { ChatPage } from './pages/ChatPage';
import { WeeklyReportPage } from './pages/WeeklyReportPage';

export const parRoutes: RouteObject[] = [
  { path: 'par/leave-requests', element: <LeaveRequestsPage /> },
  { path: 'par/weekly-reports', element: <WeeklyReportPage /> },
  { path: 'par/inbox', element: <ChatPage /> },
];
