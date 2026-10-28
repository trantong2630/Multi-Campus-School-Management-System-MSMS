import { apiClient } from '@/app/apiClient';
import type {
  AvailableParentItem,
  AvailableTeacherItem,
  ConversationItem,
  CreateConversationInput,
  CreateLeaveRequestInput,
  HomeroomLeaveItem,
  LeaveRequestItem,
  MessageItem,
  MyChildItem,
  TeachingStudentItem,
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

  conversation: {
    /**
     * Get valid teachers for child (homeroom & published timetable subject teachers)
     */
    getAvailableTeachers: (studentId: number) =>
      apiClient
        .get<AvailableTeacherItem[]>('/par/conversations/available-teachers', { params: { studentId } })
        .then((r) => r.data),

    /**
     * Get students taught by current teacher (homeroom or timetable)
     */
    getMyTeachingStudents: () =>
      apiClient
        .get<TeachingStudentItem[]>('/par/conversations/my-teaching-students')
        .then((r) => r.data),

    /**
     * Get linked parents of a student taught by teacher
     */
    getAvailableParents: (studentId: number) =>
      apiClient
        .get<AvailableParentItem[]>('/par/conversations/available-parents', { params: { studentId } })
        .then((r) => r.data),

    /**
     * Create or get existing 1-1 conversation
     */
    createConversation: (data: CreateConversationInput) =>
      apiClient.post<number>('/par/conversations', data).then((r) => r.data),

    /**
     * Get current user's conversations
     */
    getMyConversations: () =>
      apiClient.get<ConversationItem[]>('/par/conversations').then((r) => r.data),

    /**
     * Get conversation messages and mark incoming as read
     */
    getMessages: (conversationId: number) =>
      apiClient.get<MessageItem[]>(`/par/conversations/${conversationId}/messages`).then((r) => r.data),

    /**
     * Send a new message
     */
    sendMessage: (conversationId: number, content: string) =>
      apiClient
        .post<MessageItem>(`/par/conversations/${conversationId}/messages`, { content })
        .then((r) => r.data),

    /**
     * Get total unread message count
     */
    getTotalUnreadCount: () =>
      apiClient.get<number>('/par/conversations/unread-count').then((r) => r.data),
  },
};

export const leaveApi = parApi.leave;
export const conversationApi = parApi.conversation;

