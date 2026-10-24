import type { RouteObject } from 'react-router-dom';
import { DashboardPage } from './pages/DashboardPage';
import { HomePage } from './pages/HomePage';

export const admRoutes: RouteObject[] = [
  { index: true, element: <HomePage /> },
  { path: 'dashboard', element: <DashboardPage /> },
];
