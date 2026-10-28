import type { RouteObject } from 'react-router-dom';
import { LeaveRequestsPage } from './pages/LeaveRequestsPage';
import { ChatPage } from './pages/ChatPage';

export const parRoutes: RouteObject[] = [
  { path: 'par/leave-requests', element: <LeaveRequestsPage /> },
  { path: 'par/inbox', element: <ChatPage /> },
];

