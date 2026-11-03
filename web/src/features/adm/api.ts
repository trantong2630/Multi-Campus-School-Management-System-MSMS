import { apiClient } from '@/app/apiClient';
import type { AdminDashboard, Dashboard, DepartmentBoard, HomeroomOverview, Progress } from './types';

export const admApi = {
  dashboard: (campusId?: number, gradeLevel?: number) => apiClient.get<Dashboard>('/adm/dashboard', { params: { campusId, gradeLevel } }).then((r) => r.data),
  adminDashboard: () => apiClient.get<AdminDashboard>('/adm/dashboard/admin').then((r) => r.data),
  departmentBoard: () => apiClient.get<DepartmentBoard>('/adm/dashboard/department').then((r) => r.data),
  progress: (campusId?: number) => apiClient.get<Progress>('/adm/progress', { params: { campusId } }).then((r) => r.data),
  homeroom: (classId?: number) => apiClient.get<HomeroomOverview>('/adm/homeroom', { params: { classId } }).then((r) => r.data),
};
