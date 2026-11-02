import { useAuth } from '@/app/AuthProvider';
import { AdminDashboard } from '../components/AdminDashboard';
import { LeadershipDashboard } from '../components/LeadershipDashboard';
import { TeacherDashboard } from '../components/TeacherDashboard';
import './dashboard.css';

/**
 * Một mục Dashboard trên menu, nội dung theo loại tài khoản: Hiệu trưởng / Hiệu phó (vận hành theo phạm vi),
 * School Admin (dữ liệu tổ chức, tài khoản), Giáo viên (việc dạy của chính mình). Mỗi loại gọi API riêng có policy riêng.
 */
export function DashboardPage() {
  const { user } = useAuth();
  switch (user?.accountType) {
    case 'Principal':
    case 'VicePrincipal':
      return <LeadershipDashboard />;
    case 'SchoolAdmin':
      return <AdminDashboard />;
    case 'Teacher':
      return <TeacherDashboard />;
    default:
      return null;
  }
}
