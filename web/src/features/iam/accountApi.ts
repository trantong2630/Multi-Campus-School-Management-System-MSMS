import { apiClient } from '@/app/apiClient';
import type { Paged } from '@/features/ppl/types';
import type { Account, BulkCreateResult, ResetPasswordResult, TempPasswordItem } from './types';

export interface AccountQuery {
  search?: string;
  accountType?: string;
  status?: string;
  campusId?: number;
  page?: number;
  pageSize?: number;
}

export const accountApi = {
  list: (params: AccountQuery) => apiClient.get<Paged<Account>>('/iam/accounts', { params }).then((r) => r.data),
  bulkCreate: (personIds: number[], accountType?: string) =>
    apiClient.post<BulkCreateResult>('/iam/accounts/bulk', { personIds, accountType }).then((r) => r.data),
  lock: (id: number) => apiClient.put(`/iam/accounts/${id}/lock`),
  unlock: (id: number) => apiClient.put(`/iam/accounts/${id}/unlock`),
  resetPassword: (id: number) => apiClient.post<ResetPasswordResult>(`/iam/accounts/${id}/reset-password`).then((r) => r.data),
  changeType: (id: number, accountType: string) =>
    apiClient.put<ResetPasswordResult>(`/iam/accounts/${id}/type`, { accountType }).then((r) => r.data),
  sheetUrl: '/iam/accounts/temp-password-sheet',
};

export type { TempPasswordItem };
