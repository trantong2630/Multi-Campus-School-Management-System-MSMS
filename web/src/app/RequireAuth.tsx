import { Skeleton } from 'antd';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { ForcedChangePasswordPage } from '@/features/iam/pages/ForcedChangePasswordPage';
import { useAuth } from './AuthProvider';

export function RequireAuth() {
  const { user, loading } = useAuth();
  const location = useLocation();

  if (loading) {
    return (
      <div style={{ padding: 48, maxWidth: 480 }}>
        <Skeleton active paragraph={{ rows: 4 }} />
      </div>
    );
  }
  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  if (user.mustChangePassword) return <ForcedChangePasswordPage />;
  return <Outlet />;
}
