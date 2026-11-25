import { apiClient } from '@/app/apiClient';
import type {
  AdminDashboard, AiChatAnswer, AiChatTurn, AiPerson, AuditActor, AuditLogDetail, AuditLogFilters, AuditLogItem, Dashboard, DepartmentBoard, HomeroomOverview,
} from './types';

export const admApi = {
  dashboard: (campusId?: number, gradeLevel?: number) => apiClient.get<Dashboard>('/adm/dashboard', { params: { campusId, gradeLevel } }).then((r) => r.data),
  adminDashboard: () => apiClient.get<AdminDashboard>('/adm/dashboard/admin').then((r) => r.data),
  departmentBoard: () => apiClient.get<DepartmentBoard>('/adm/dashboard/department').then((r) => r.data),
  homeroom: (classId?: number) => apiClient.get<HomeroomOverview>('/adm/homeroom', { params: { classId } }).then((r) => r.data),
  auditLog: (params: AuditLogFilters & { page: number; pageSize: number }) =>
    apiClient.get<{ items: AuditLogItem[]; total: number }>('/adm/audit-log', { params }).then((r) => r.data),
  auditActors: () => apiClient.get<AuditActor[]>('/adm/audit-log/actors').then((r) => r.data),
  auditEntry: (id: number) => apiClient.get<AuditLogDetail>(`/adm/audit-log/${id}`).then((r) => r.data),
  /** MSMS AI: hội thoại chỉ nằm ở trình duyệt, mỗi lần gửi kèm các lượt gần nhất. */
  aiChat: (question: string, history: AiChatTurn[]) => apiClient.post<AiChatAnswer>('/adm/ai/chat', { question, history }).then((r) => r.data),
  aiPeople: (q: string) => apiClient.get<AiPerson[]>('/adm/ai/people', { params: { q } }).then((r) => r.data),
};
