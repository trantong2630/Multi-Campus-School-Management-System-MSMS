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
  slotIds?: string | null;
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
  slotIds?: number[];
  reason?: string;
}

/**
 * One day (or some periods of one day) in a bulk leave request
 */
export interface LeaveDayInput {
  date: string;
  sessionOfDay: LeaveSessionOfDay | string;
  slotIds?: number[];
}

/**
 * Input data for creating leave requests for several days at once
 */
export interface BulkCreateLeaveRequestInput {
  studentId: number;
  reason?: string;
  days: LeaveDayInput[];
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
  slotIds?: string | null;
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
  parentId?: number;
  parentName?: string;
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


/**
 * A valid week (Monday to Sunday) for viewing student's weekly report
 */
export interface ReportWeekItem {
  weekStart: string;
  weekEnd: string;
  weekNumber: number;
  label: string;
  isCurrent: boolean;
}

/**
 * Attendance summary: 5 metrics for a week
 */
export interface AttendanceSummary {
  totalSessions: number;
  presentCount: number;
  lateCount: number;
  excusedCount: number;
  unexcusedCount: number;
  unmarkedCount: number;
}

/**
 * A single assignment with status and result
 */
export interface WeeklyAssignmentItem {
  assignmentId: number;
  title: string;
  subjectName: string;
  dueAt: string;
  status: 'Submitted' | 'Unsubmitted' | 'Missed';
  score?: number | null;
  aiFeedback?: string | null;
}

/**
 * Assignment summary for a week: 4 metrics + items list
 */
export interface AssignmentSummary {
  assignedCount: number;
  submittedCount: number;
  unsubmittedCount: number;
  missedCount: number;
  items: WeeklyAssignmentItem[];
}

/**
 * Complete weekly report for one student and one week (BR-24)
 */
export interface WeeklyReportData {
  studentId: number;
  studentName: string;
  studentCode: string;
  className: string;
  weekStart: string;
  weekEnd: string;
  attendance: AttendanceSummary;
  assignments: AssignmentSummary;
}

/**
 * Student in homeroom teacher's class
 */
export interface HomeroomStudentItem {
  studentId: number;
  studentCode: string;
  fullName: string;
  className: string;
}
