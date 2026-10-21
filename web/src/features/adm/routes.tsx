import type { RouteObject } from 'react-router-dom';
import { HomePage } from './pages/HomePage';

export const admRoutes: RouteObject[] = [
  { index: true, element: <HomePage /> },
];
