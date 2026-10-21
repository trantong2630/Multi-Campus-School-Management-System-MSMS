import { apiClient } from '@/app/apiClient';
import type { AcademicYear, Campus, Department, Holiday, PeriodSlot, SchoolClass, SchoolProfile, Subject } from './types';

/** Đường dẫn ảnh logo; tham số v đổi theo lần cập nhật để trình duyệt không giữ logo cũ. */
export const schoolLogoUrl = (version: number | null) => `/api/school/logo?v=${version ?? 0}`;

const get = <T>(url: string, params?: object) => apiClient.get<T>(url, { params }).then((r) => r.data);
const post = <T>(url: string, body?: object) => apiClient.post<T>(url, body).then((r) => r.data);
const put = <T = void>(url: string, body?: object) => apiClient.put<T>(url, body).then((r) => r.data);

export const schApi = {
  school: {
    get: () => get<SchoolProfile>('/school'),
    update: (b: object) => put<SchoolProfile>('/school', b),
    uploadLogo: (file: File) => {
      const fd = new FormData();
      fd.append('file', file);
      return apiClient.post<SchoolProfile>('/school/logo', fd).then((r) => r.data);
    },
    removeLogo: () => apiClient.delete<SchoolProfile>('/school/logo').then((r) => r.data),
  },
  campuses: {
    list: () => get<Campus[]>('/sch/campuses'),
    create: (b: object) => post<Campus>('/sch/campuses', b),
    update: (id: number, b: object) => put(`/sch/campuses/${id}`, b),
    setActive: (id: number, isActive: boolean) => put(`/sch/campuses/${id}/active`, { isActive }),
  },
  departments: {
    list: () => get<Department[]>('/sch/departments'),
    create: (b: object) => post<Department>('/sch/departments', b),
    update: (id: number, b: object) => put(`/sch/departments/${id}`, b),
    setActive: (id: number, isActive: boolean) => put(`/sch/departments/${id}/active`, { isActive }),
  },
  subjects: {
    list: (departmentId?: number) => get<Subject[]>('/sch/subjects', { departmentId }),
    create: (b: object) => post<Subject>('/sch/subjects', b),
    update: (id: number, b: object) => put(`/sch/subjects/${id}`, b),
    setActive: (id: number, isActive: boolean) => put(`/sch/subjects/${id}/active`, { isActive }),
  },
  years: {
    list: () => get<AcademicYear[]>('/sch/academic-years'),
    create: (b: object) => post<AcademicYear>('/sch/academic-years', b),
    update: (id: number, b: object) => put(`/sch/academic-years/${id}`, b),
    setCurrent: (id: number) => put(`/sch/academic-years/${id}/current`),
    configureSemesters: (id: number, b: object) => put<AcademicYear>(`/sch/academic-years/${id}/semesters`, b),
  },
  holidays: {
    list: (params: { yearId?: number; campusId?: number }) => get<Holiday[]>('/sch/holidays', params),
    create: (b: object) => post<Holiday>('/sch/holidays', b),
    remove: (id: number) => apiClient.delete(`/sch/holidays/${id}`),
  },
  periodSlots: {
    list: () => get<PeriodSlot[]>('/sch/period-slots'),
    create: (b: object) => post<PeriodSlot>('/sch/period-slots', b),
    updateTimes: (id: number, b: object) => put(`/sch/period-slots/${id}`, b),
    setActive: (id: number, isActive: boolean) => put(`/sch/period-slots/${id}/active`, { isActive }),
  },
  classes: {
    list: (params: { campusId?: number; yearId?: number; includeInactive?: boolean }) => get<SchoolClass[]>('/sch/classes', params),
    create: (b: object) => post<SchoolClass>('/sch/classes', b),
    update: (id: number, b: object) => put(`/sch/classes/${id}`, b),
    setActive: (id: number, isActive: boolean) => put(`/sch/classes/${id}/active`, { isActive }),
  },
};
