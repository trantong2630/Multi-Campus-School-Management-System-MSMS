import { apiClient } from '@/app/apiClient';
import type {
  CreateLeaveRequestInput,
  HomeroomLeaveItem,
  LeaveRequestItem,
  MyChildItem,
} from './types';

export const parApi = {
  leave: {
    /**
     * Get list of parent's children
     */
    getMyChildren: () =>
      apiClient.get<MyChildItem[]>('/par/leave-requests/my-children').then((r) => r.data),

    /**
     * Get leave requests for a specific child
     */
    getMyChildLeaves: (studentId: number) =>
      apiClient.get<LeaveRequestItem[]>(`/par/leave-requests/student/${studentId}`).then((r) => r.data),

    /**
     * Submit a new leave request
     */
    submitLeaveRequest: (data: CreateLeaveRequestInput) =>
      apiClient.post('/par/leave-requests', data).then((r) => r.data),

    /**
     * Withdraw a leave request
     */
    withdrawLeaveRequest: (id: number) =>
      apiClient.put(`/par/leave-requests/${id}/withdraw`).then((r) => r.data),

    /**
     * Get leave requests for homeroom class (homeroom teacher)
     */
    getHomeroomLeaves: (classId?: number) =>
      apiClient
        .get<HomeroomLeaveItem[]>('/par/leave-requests/homeroom', { params: { classId } })
        .then((r) => r.data),

    /**
     * Approve a leave request (homeroom teacher)
     */
    approveLeaveRequest: (id: number) =>
      apiClient.put(`/par/leave-requests/${id}/approve`).then((r) => r.data),

    /**
     * Reject a leave request (homeroom teacher)
     */
    rejectLeaveRequest: (id: number, decisionNote?: string) =>
      apiClient.put(`/par/leave-requests/${id}/reject`, { decisionNote }).then((r) => r.data),
  },
};

export const leaveApi = parApi.leave;
