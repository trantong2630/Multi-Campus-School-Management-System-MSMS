import type { RouteObject } from 'react-router-dom';
import { DashboardPage } from './pages/DashboardPage';
import { HomePage } from './pages/HomePage';
import { HomeroomPage } from './pages/HomeroomPage';

export const admRoutes: RouteObject[] = [
  { index: true, element: <HomePage /> },
  { path: 'dashboard', element: <DashboardPage /> },
  { path: 'homeroom', element: <HomeroomPage /> },
];
