import { Navigate } from 'react-router-dom';
import type { RouteObject } from 'react-router-dom';
import { useAuth } from '@/app/AuthProvider';
import { visibleMenu } from '@/app/menu';
import { AuditLogPage } from './pages/AuditLogPage';
import { CampusBoardPage } from './pages/CampusBoardPage';
import { DepartmentBoardPage } from './pages/DepartmentBoardPage';
import { ExportsPage } from './pages/ExportsPage';
import { HomePage } from './pages/HomePage';
import { HomeroomPage } from './pages/HomeroomPage';
import { PrincipalBoardPage } from './pages/PrincipalBoardPage';
import { ProgressPage } from './pages/ProgressPage';
import { SystemBoardPage } from './pages/SystemBoardPage';

/** `/dashboard` không phải màn hình: chuyển tới mục đầu tiên của Bảng điều khiển mà người dùng được mở. */
function DashboardIndex() {
  const { user } = useAuth();
  const first = user && visibleMenu(user).find((g) => g.key === 'dashboard')?.children?.[0];
  return <Navigate to={first?.path ?? '/'} replace />;
}

export const admRoutes: RouteObject[] = [
  { index: true, element: <HomePage /> },
  { path: 'dashboard', element: <DashboardIndex /> },
  { path: 'dashboard/school', element: <PrincipalBoardPage /> },
  { path: 'dashboard/campus', element: <CampusBoardPage /> },
  { path: 'dashboard/department', element: <DepartmentBoardPage /> },
  { path: 'dashboard/homeroom', element: <HomeroomPage /> },
  { path: 'dashboard/progress', element: <ProgressPage /> },
  { path: 'dashboard/system', element: <SystemBoardPage /> },
  { path: 'adm/audit-log', element: <AuditLogPage /> },
  { path: 'adm/exports', element: <ExportsPage /> },
];
