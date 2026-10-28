import { apiClient } from '@/app/apiClient';
import type {
  Absence, AdjustResult, AdjustWeek, CampusOption, DaySchedule, DeclareAbsenceBody, DraftOptions,
  GridClass, MakeUpBody, MySession, PendingSession, PublishPreview, PublishResult, ResolveBody, ResponsibilityList, TeacherAvailability, TeacherOption,
  TimetableImportResult, TimetableVersion, VersionGrid, ViewSession,
} from './types';

export const scdApi = {
  campuses: () => apiClient.get<CampusOption[]>('/scd/timetable/campuses').then((r) => r.data),
  versions: (campusId: number, semesterId: number) =>
    apiClient.get<TimetableVersion[]>('/scd/timetable/versions', { params: { campusId, semesterId } }).then((r) => r.data),
  versionGrid: (id: number) => apiClient.get<VersionGrid>(`/scd/timetable/versions/${id}`).then((r) => r.data),
  preview: (id: number) => apiClient.get<PublishPreview>(`/scd/timetable/versions/${id}/preview`).then((r) => r.data),
  publish: (id: number, confirmWarnings: boolean) =>
    apiClient.post<PublishResult>(`/scd/timetable/versions/${id}/publish`, { confirmWarnings }).then((r) => r.data),
  draftOptions: (campusId: number) =>
    apiClient.get<DraftOptions>('/scd/timetable/draft-options', { params: { campusId } }).then((r) => r.data),
  moveEntry: (id: number, body: { classId: number; fromDayOfWeek: number; fromPeriodSlotId: number; toDayOfWeek: number; toPeriodSlotId: number }) =>
    apiClient.post<VersionGrid>(`/scd/timetable/versions/${id}/entries/move`, body).then((r) => r.data),
  addEntry: (id: number, body: { classId: number; dayOfWeek: number; periodSlotId: number; subjectId: number; teacherId: number }) =>
    apiClient.post<VersionGrid>(`/scd/timetable/versions/${id}/entries`, body).then((r) => r.data),
  changeEntryTeacher: (id: number, body: { classId: number; dayOfWeek: number; periodSlotId: number; teacherId: number }) =>
    apiClient.put<VersionGrid>(`/scd/timetable/versions/${id}/entries/teacher`, body).then((r) => r.data),
  removeEntry: (id: number, params: { classId: number; dayOfWeek: number; periodSlotId: number }) =>
    apiClient.delete<VersionGrid>(`/scd/timetable/versions/${id}/entries`, { params }).then((r) => r.data),
  resetDraft: (id: number) => apiClient.post<VersionGrid>(`/scd/timetable/versions/${id}/reset`).then((r) => r.data),
  import: (file: File) => {
    const fd = new FormData();
    fd.append('file', file);
    return apiClient.post<TimetableImportResult>('/scd/timetable/import', fd).then((r) => r.data);
  },
  day: (campusId: number, date: string) =>
    apiClient.get<DaySchedule>('/scd/schedule/day', { params: { campusId, date } }).then((r) => r.data),
  mine: (from: string, to: string) =>
    apiClient.get<MySession[]>('/scd/schedule/mine', { params: { from, to } }).then((r) => r.data),
  classes: (campusId: number) => apiClient.get<GridClass[]>('/scd/schedule/classes', { params: { campusId } }).then((r) => r.data),
  teachers: (campusId: number) => apiClient.get<TeacherOption[]>('/scd/schedule/teachers', { params: { campusId } }).then((r) => r.data),
  byClass: (classId: number, from: string, to: string) =>
    apiClient.get<ViewSession[]>('/scd/schedule/class', { params: { classId, from, to } }).then((r) => r.data),
  studentWeek: (from: string, to: string) =>
    apiClient.get<ViewSession[]>('/scd/schedule/student', { params: { from, to } }).then((r) => r.data),
  childWeek: (studentId: number, from: string, to: string) =>
    apiClient.get<ViewSession[]>('/scd/schedule/child', { params: { studentId, from, to } }).then((r) => r.data),
  responsibilities: (campusId?: number, subjectId?: number) =>
    apiClient.get<ResponsibilityList>('/scd/responsibilities', { params: { campusId, subjectId } }).then((r) => r.data),
  byTeacher: (teacherId: number, from: string, to: string) =>
    apiClient.get<ViewSession[]>('/scd/schedule/teacher', { params: { teacherId, from, to } }).then((r) => r.data),

  // ---- SCD-02: điều chỉnh lịch dạy ----
  adjustWeek: (classId: number, weekStart: string) =>
    apiClient.get<AdjustWeek>('/scd/adjust/week', { params: { classId, weekStart } }).then((r) => r.data),
  adjustTeachers: (date: string, periodSlotId: number, excludeSessionId?: number) =>
    apiClient.get<TeacherAvailability[]>('/scd/adjust/teachers', { params: { date, periodSlotId, excludeSessionId } }).then((r) => r.data),
  changeSessionTeacher: (sessionId: number, teacherId: number) =>
    apiClient.put<AdjustResult>(`/scd/adjust/sessions/${sessionId}/teacher`, { teacherId }).then((r) => r.data),
  cancelSession: (sessionId: number) =>
    apiClient.post<AdjustResult>(`/scd/adjust/sessions/${sessionId}/cancel`).then((r) => r.data),
  addMakeUp: (body: MakeUpBody) => apiClient.post<AdjustResult>('/scd/adjust/makeup', body).then((r) => r.data),

  // ---- SCD-03: giáo viên vắng ----
  absences: (campusId: number) => apiClient.get<Absence[]>('/scd/absences', { params: { campusId } }).then((r) => r.data),
  absenceTeachers: (campusId: number) => apiClient.get<TeacherOption[]>('/scd/absences/teachers', { params: { campusId } }).then((r) => r.data),
  declareAbsence: (body: DeclareAbsenceBody) =>
    apiClient.post<{ absenceId: number; pendingCount: number }>('/scd/absences', body).then((r) => r.data),
  deleteAbsence: (id: number) => apiClient.delete<{ restoredCount: number }>(`/scd/absences/${id}`).then((r) => r.data),
  pending: (campusId: number, params: { from?: string; to?: string; classId?: number }) =>
    apiClient.get<PendingSession[]>('/scd/absences/pending', { params: { campusId, ...params } }).then((r) => r.data),
  pendingSuggestions: (sessionId: number) =>
    apiClient.get<TeacherAvailability[]>(`/scd/absences/pending/${sessionId}/suggestions`).then((r) => r.data),
  resolvePending: (sessionId: number, body: ResolveBody) =>
    apiClient.post<AdjustResult>(`/scd/absences/pending/${sessionId}/resolve`, body).then((r) => r.data),
};
