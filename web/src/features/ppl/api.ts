import { apiClient } from '@/app/apiClient';
import type { Paged, ResponsibilityHistoryRow, ResponsibilityKind, ResponsibilityRow, Staff, StaffImportResult, StudentImportResult } from './types';

export interface StaffQuery {
  search?: string;
  campusId?: number;
  departmentId?: number;
  accountType?: string;
  hasAbbr?: boolean;
  includeInactive?: boolean;
  page?: number;
  pageSize?: number;
}

export const pplApi = {
  staff: {
    list: (params: StaffQuery) => apiClient.get<Paged<Staff>>('/ppl/staff', { params }).then((r) => r.data),
    create: (b: object) => apiClient.post<Staff>('/ppl/staff', b).then((r) => r.data),
    update: (id: number, b: object) => apiClient.put<Staff>(`/ppl/staff/${id}`, b).then((r) => r.data),
    setActive: (id: number, isActive: boolean) => apiClient.put(`/ppl/staff/${id}/active`, { isActive }),
    import: (file: File) => {
      const fd = new FormData();
      fd.append('file', file);
      return apiClient.post<StaffImportResult>('/ppl/staff/import', fd).then((r) => r.data);
    },
  },
  students: {
    import: (file: File) => {
      const fd = new FormData();
      fd.append('file', file);
      return apiClient.post<StudentImportResult>('/ppl/students/import', fd).then((r) => r.data);
    },
  },
  responsibilities: {
    list: (kind: ResponsibilityKind, yearId?: number) =>
      apiClient.get<ResponsibilityRow[]>('/ppl/responsibilities', { params: { kind, yearId } }).then((r) => r.data),
    history: (kind: ResponsibilityKind, targetId: number) =>
      apiClient.get<ResponsibilityHistoryRow[]>('/ppl/responsibilities/history', { params: { kind, targetId } }).then((r) => r.data),
    assign: (b: { kind: ResponsibilityKind; targetId: number; staffId: number; startDate: string }) =>
      apiClient.post('/ppl/responsibilities/assign', b),
    end: (b: { kind: ResponsibilityKind; recordId: number; endDate: string }) => apiClient.post('/ppl/responsibilities/end', b),
  },
};
