import type { RouteObject } from 'react-router-dom';
import { ResponsibilitiesPage } from './pages/ResponsibilitiesPage';
import { StaffPage } from './pages/StaffPage';

export const pplRoutes: RouteObject[] = [
  { path: 'ppl/staff', element: <StaffPage /> },
  { path: 'ppl/responsibilities', element: <ResponsibilitiesPage /> },
];
