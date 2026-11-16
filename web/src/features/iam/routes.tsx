import type { RouteObject } from 'react-router-dom';
import { AccountsPage } from './pages/AccountsPage';
import { PermissionsPage } from './pages/PermissionsPage';
import { ChangePasswordPage } from './pages/ChangePasswordPage';
import { ProfilePage } from './pages/ProfilePage';

export const iamRoutes: RouteObject[] = [
  { path: 'profile', element: <ProfilePage /> },
  { path: 'change-password', element: <ChangePasswordPage /> },
  { path: 'adm/accounts', element: <AccountsPage /> },
  { path: 'adm/permissions', element: <PermissionsPage /> },
];
