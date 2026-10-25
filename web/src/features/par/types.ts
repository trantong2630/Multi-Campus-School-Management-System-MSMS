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
