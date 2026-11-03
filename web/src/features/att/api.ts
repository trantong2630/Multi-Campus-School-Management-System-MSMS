import { apiClient } from '@/app/apiClient';
import type {
  AttendanceClassOption, AttendanceSession, AttendanceSheet, ClassAttendance, SaveAttendanceItem, StudentAttendance,
} from './types';

const get = <T>(url: string, params?: object) => apiClient.get<T>(url, { params }).then((r) => r.data);

/** ATT-01 Điểm danh theo tiết (4.5.1–4.5.13). */
export const attApi = {
  today: () => get<AttendanceSession[]>('/att/today'),
  classes: () => get<AttendanceClassOption[]>('/att/classes'),
  sessions: (classId: number, from: string, to: string, missing: boolean) =>
    get<AttendanceSession[]>('/att/sessions', { classId, from, to, missing }),
  sheet: (sessionId: number) => get<AttendanceSheet>(`/att/sessions/${sessionId}`),
  save: (sessionId: number, items: SaveAttendanceItem[]) =>
    apiClient.put<AttendanceSheet>(`/att/sessions/${sessionId}`, { items }).then((r) => r.data),
  classSummary: (classId: number, from: string, to: string) => get<ClassAttendance>('/att/class-summary', { classId, from, to }),
  mine: (from: string, to: string) => get<StudentAttendance>('/att/me', { from, to }),
  child: (studentId: number, from: string, to: string) => get<StudentAttendance>('/att/child', { studentId, from, to }),
};
