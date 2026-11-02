import { apiClient } from '@/app/apiClient';
import type { AdminDashboard, Dashboard, HomeroomOverview, TeacherDashboard } from './types';

export const admApi = {
  dashboard: (campusId?: number) => apiClient.get<Dashboard>('/adm/dashboard', { params: { campusId } }).then((r) => r.data),
  adminDashboard: () => apiClient.get<AdminDashboard>('/adm/dashboard/admin').then((r) => r.data),
  teacherDashboard: () => apiClient.get<TeacherDashboard>('/adm/dashboard/teacher').then((r) => r.data),
  homeroom: (classId?: number) => apiClient.get<HomeroomOverview>('/adm/homeroom', { params: { classId } }).then((r) => r.data),
};
