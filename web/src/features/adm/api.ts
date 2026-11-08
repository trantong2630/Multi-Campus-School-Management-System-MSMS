import { apiClient } from '@/app/apiClient';
import { downloadFile } from '@/shared/lib/download';
import type { AdminDashboard, AuditActor, AuditLogDetail, AuditLogFilters, AuditLogItem, Dashboard, DepartmentBoard, HomeroomOverview, Progress } from './types';

export const admApi = {
  dashboard: (campusId?: number, gradeLevel?: number) => apiClient.get<Dashboard>('/adm/dashboard', { params: { campusId, gradeLevel } }).then((r) => r.data),
  adminDashboard: () => apiClient.get<AdminDashboard>('/adm/dashboard/admin').then((r) => r.data),
  departmentBoard: () => apiClient.get<DepartmentBoard>('/adm/dashboard/department').then((r) => r.data),
  progress: (campusId?: number) => apiClient.get<Progress>('/adm/progress', { params: { campusId } }).then((r) => r.data),
  homeroom: (classId?: number) => apiClient.get<HomeroomOverview>('/adm/homeroom', { params: { classId } }).then((r) => r.data),
  auditLog: (params: AuditLogFilters & { page: number; pageSize: number }) =>
    apiClient.get<{ items: AuditLogItem[]; total: number }>('/adm/audit-log', { params }).then((r) => r.data),
  auditActors: () => apiClient.get<AuditActor[]>('/adm/audit-log/actors').then((r) => r.data),
  auditEntry: (id: number) => apiClient.get<AuditLogDetail>(`/adm/audit-log/${id}`).then((r) => r.data),
  /** ADM-03: tải file Excel; lỗi trả dạng Blob, đọc bằng blobErrorMessage. */
  exportAttendance: (classId: number, from: string, to: string) => downloadFile('/adm/exports/attendance', { classId, from, to }),
  exportTimetable: (campusId: number, from: string, to: string) => downloadFile('/adm/exports/timetable', { campusId, from, to }),
};
