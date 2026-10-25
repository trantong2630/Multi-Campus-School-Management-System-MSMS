import { useAuth } from '@/app/AuthProvider';
import { HomeroomLeavePage } from './HomeroomLeavePage';
import { ParentLeavePage } from './ParentLeavePage';

/**
 * Dispatcher component for leave requests.
 * Routes to different pages based on user account type:
 * - Parent: Parent leave management
 * - Teacher/VicePrincipal: Homeroom leave approval
 */
export function LeaveRequestsPage() {
  const { user } = useAuth();

  if (user?.accountType === 'Parent') {
    return <ParentLeavePage />;
  }

  if (user?.accountType === 'Teacher' || user?.accountType === 'VicePrincipal') {
    return <HomeroomLeavePage />;
  }

  return null;
}
