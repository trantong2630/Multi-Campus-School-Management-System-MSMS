import { apiClient } from '@/app/apiClient';
import type {
  CampusOption, DaySchedule, GridClass, MySession, PublishPreview, PublishResult, TeacherOption, TimetableImportResult, TimetableVersion,
  VersionGrid, ViewSession,
} from './types';

export const scdApi = {
  campuses: () => apiClient.get<CampusOption[]>('/scd/timetable/campuses').then((r) => r.data),
  versions: (campusId: number, semesterId: number) =>
    apiClient.get<TimetableVersion[]>('/scd/timetable/versions', { params: { campusId, semesterId } }).then((r) => r.data),
  versionGrid: (id: number) => apiClient.get<VersionGrid>(`/scd/timetable/versions/${id}`).then((r) => r.data),
  preview: (id: number) => apiClient.get<PublishPreview>(`/scd/timetable/versions/${id}/preview`).then((r) => r.data),
  publish: (id: number, confirmWarnings: boolean) =>
    apiClient.post<PublishResult>(`/scd/timetable/versions/${id}/publish`, { confirmWarnings }).then((r) => r.data),
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
  byTeacher: (teacherId: number, from: string, to: string) =>
    apiClient.get<ViewSession[]>('/scd/schedule/teacher', { params: { teacherId, from, to } }).then((r) => r.data),
};
