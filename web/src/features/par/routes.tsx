import type { RouteObject } from 'react-router-dom';
import { LeaveRequestsPage } from './pages/LeaveRequestsPage';

export const parRoutes: RouteObject[] = [
  { path: 'par/leave-requests', element: <LeaveRequestsPage /> },
];
