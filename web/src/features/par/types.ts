/**
 * Child information from parent's perspective
 */
export interface MyChildItem {
  studentId: number;
  studentCode: string;
  fullName: string;
  className: string | null;
  classId: number | null;
}

export type LeaveSessionOfDay = 'Morning' | 'Afternoon' | 'AllDay';

/**
 * Leave request item for parent's child
 */
export interface LeaveRequestItem {
  leaveRequestId: number;
  studentId: number;
  studentName: string;
  classId: number;
  className: string;
  fromDate: string;
  toDate: string;
  sessionOfDay: LeaveSessionOfDay;
  reason?: string;
  status: string;
  createdAt: string;
  decidedByPersonName?: string;
  decidedAt?: string;
  decisionNote?: string;
}

/**
 * Input data for creating a new leave request
 */
export interface CreateLeaveRequestInput {
  studentId: number;
  fromDate: string;
  toDate: string;
  sessionOfDay: LeaveSessionOfDay | string;
  reason?: string;
}

/**
 * Leave request item viewed by homeroom teacher
 */
export interface HomeroomLeaveItem {
  leaveRequestId: number;
  studentId: number;
  studentName: string;
  studentCode: string;
  classId: number;
  className: string;
  parentName: string;
  fromDate: string;
  toDate: string;
  sessionOfDay: LeaveSessionOfDay | string;
  reason?: string;
  status: string;
  createdAt: string;
  decidedByPersonName?: string;
  decidedAt?: string;
  decisionNote?: string;
}

/**
 * Available teacher for parent to chat with
 */
export interface AvailableTeacherItem {
  staffId: number;
  fullName: string;
  teacherAbbr: string | null;
  roleDescription: string;
}

/**
 * Available parent for teacher to chat with
 */
export interface AvailableParentItem {
  parentId: number;
  fullName: string;
  relationship: string | null;
  phone: string | null;
}

/**
 * Student taught by teacher
 */
export interface TeachingStudentItem {
  studentId: number;
  fullName: string;
  studentCode: string;
  classId: number;
  className: string;
}

/**
 * Input to create or get conversation
 */
export interface CreateConversationInput {
  studentId: number;
  staffId?: number;
  parentId?: number;
}

/**
 * Conversation item in conversation list
 */
export interface ConversationItem {
  conversationId: number;
  studentId: number;
  studentName: string;
  studentCode: string;
  className: string;
  staffId: number;
  staffName: string;
  staffAbbr: string | null;
  roleDescription: string;
  lastMessageContent: string | null;
  lastMessageAt: string | null;
  unreadCount: number;
  isReadOnly: boolean;
}

/**
 * Message item inside conversation
 */
export interface MessageItem {
  messageId: number;
  conversationId: number;
  senderPersonId: number;
  senderName: string;
  isMine: boolean;
  content: string;
  sentAt: string;
  readAt: string | null;
}

