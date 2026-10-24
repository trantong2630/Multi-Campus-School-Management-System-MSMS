import { apiClient } from '@/app/apiClient';
import type { Dashboard } from './types';

export const admApi = {
  dashboard: (campusId?: number) => apiClient.get<Dashboard>('/adm/dashboard', { params: { campusId } }).then((r) => r.data),
};
