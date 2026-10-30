import { apiClient } from '@/app/apiClient';
import type {
  AnswerInput, AssignmentDetail, AssignmentItem, Attempt, Child, ChildResultItem, ClassResult, ClassSubjectOption,
  CreateAssignmentInput, DepartmentOverview, GenerateQuestionsInput, GenerateQuestionsResult, MyAssignmentItem, PagedResult,
  PracticeResult, QuestionDetail, QuestionItem, QuestionQuery, SaveQuestionInput,
} from './types';

const get = <T>(url: string, params?: object) => apiClient.get<T>(url, { params }).then((r) => r.data);
const post = <T>(url: string, body?: object) => apiClient.post<T>(url, body).then((r) => r.data);
const put = <T>(url: string, body?: object) => apiClient.put<T>(url, body).then((r) => r.data);
const del = (url: string) => apiClient.delete(url).then((r) => r.data);

export const lrnApi = {
  questions: {
    list: (q: QuestionQuery) => get<PagedResult<QuestionItem>>('/lrn/questions', q),
    get: (id: number) => get<QuestionDetail>(`/lrn/questions/${id}`),
    create: (b: SaveQuestionInput) => post<QuestionDetail>('/lrn/questions', b),
    update: (id: number, b: SaveQuestionInput) => put<QuestionDetail>(`/lrn/questions/${id}`, b),
    hide: (id: number) => put(`/lrn/questions/${id}/hide`),
    confirm: (id: number) => put(`/lrn/questions/${id}/confirm`),
    removeDraft: (id: number) => del(`/lrn/questions/${id}`),
    /** AI gọi lâu hơn các API khác nên đặt thời gian chờ riêng. */
    generate: (b: GenerateQuestionsInput) => apiClient.post<GenerateQuestionsResult>('/lrn/questions/ai-drafts', b, { timeout: 150_000 }).then((r) => r.data),
  },
  assignments: {
    list: () => get<AssignmentItem[]>('/lrn/assignments'),
    classSubjects: () => get<ClassSubjectOption[]>('/lrn/assignments/class-subjects'),
    get: (id: number) => get<AssignmentDetail>(`/lrn/assignments/${id}`),
    create: (b: CreateAssignmentInput) => post<AssignmentDetail>('/lrn/assignments', b),
    results: (id: number) => get<ClassResult>(`/lrn/assignments/${id}/results`),
    studentResult: (id: number, studentId: number) => get<PracticeResult>(`/lrn/assignments/${id}/results/${studentId}`),
  },
  student: {
    list: () => get<MyAssignmentItem[]>('/lrn/my-assignments'),
    open: (id: number) => get<Attempt>(`/lrn/my-assignments/${id}/attempt`),
    start: (id: number) => post<Attempt>(`/lrn/my-assignments/${id}/start`),
    saveDraft: (id: number, answers: AnswerInput[]) => put<{ savedAt: string }>(`/lrn/my-assignments/${id}/answers`, { answers }),
    submit: (id: number, answers: AnswerInput[]) => post<{ submittedAt: string }>(`/lrn/my-assignments/${id}/submit`, { answers }),
  },
  results: {
    children: () => get<Child[]>('/lrn/results/children'),
    childList: (studentId: number) => get<ChildResultItem[]>(`/lrn/results/children/${studentId}`),
    detail: (assignmentId: number, studentId?: number) => get<PracticeResult>(`/lrn/results/${assignmentId}`, { studentId }),
  },
  department: {
    overview: (subjectId?: number) => get<DepartmentOverview>('/lrn/department/assignments', { subjectId }),
  },
};
