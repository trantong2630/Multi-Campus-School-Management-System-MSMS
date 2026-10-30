// Kiểu dữ liệu khớp DTO của Modules/Lrn/Dtos. Trạng thái là giá trị lưu trong DB; nhãn tiếng Việt do từng màn hình tự đặt.

export type QuestionStatus = 'Draft' | 'Ready';
/** Trạng thái bài với học sinh: NotSubmitted là quá hạn mà chưa nộp ("Không nộp"). */
export type AttemptState = 'NotOpen' | 'Open' | 'InProgress' | 'Submitted' | 'NotSubmitted';
export type AnalysisStatus = 'Pending' | 'Done' | 'Failed';

export interface PagedResult<T> { items: T[]; total: number }

export interface OptionInput { label: string; content: string; isCorrect: boolean }
export interface QuestionOption { optionId: number; label: string; content: string; isCorrect: boolean }

export interface QuestionItem {
  questionId: number;
  subjectId: number;
  subjectName: string;
  gradeLevel: number | null;
  topic: string | null;
  content: string;
  status: QuestionStatus;
  isAiGenerated: boolean;
  isHidden: boolean;
  isMultiple: boolean;
  createdByPersonId: number;
  createdByName: string;
  isMine: boolean;
  isLocked: boolean;
  createdAt: string;
}

export interface QuestionDetail extends QuestionItem {
  explanation: string | null;
  options: QuestionOption[];
}

export interface SaveQuestionInput {
  subjectId: number;
  gradeLevel: number;
  topic: string;
  content: string;
  explanation?: string;
  options: OptionInput[];
}

export interface GenerateQuestionsInput {
  subjectId: number;
  gradeLevel: number;
  topic: string;
  textbook: string;
  count: number;
}
export interface GenerateQuestionsResult { drafts: QuestionDetail[]; discarded: number }

export interface QuestionQuery {
  subjectId?: number;
  gradeLevel?: number;
  status?: QuestionStatus;
  mine?: boolean;
  search?: string;
  page?: number;
  pageSize?: number;
}

// ---- Giao bài ----

export interface ClassSubjectOption { classId: number; className: string; gradeLevel: number; subjectId: number; subjectName: string }

export interface AssignmentLine { questionId: number; points: number }

export interface CreateAssignmentInput {
  subjectId: number;
  classId: number;
  title: string;
  instruction?: string;
  openAt?: string;
  dueAt: string;
  durationMinutes?: number;
  items: AssignmentLine[];
}

export interface AssignmentItem {
  assignmentId: number;
  title: string;
  classId: number;
  className: string;
  subjectId: number;
  subjectName: string;
  openAt: string;
  dueAt: string;
  durationMinutes: number | null;
  questionCount: number;
  recipientCount: number;
  submittedCount: number;
  isClosed: boolean;
  assignedByName: string;
  isMine: boolean;
}

export interface AssignmentQuestion {
  assignmentQuestionId: number;
  questionId: number;
  orderNo: number;
  points: number;
  content: string;
  explanation: string | null;
  isMultiple: boolean;
  options: QuestionOption[];
}

export interface AssignmentDetail {
  assignment: AssignmentItem;
  instruction: string | null;
  totalPoints: number;
  questions: AssignmentQuestion[];
}

// ---- Làm bài ----

export interface QuizOption { optionId: number; label: string; content: string }
export interface QuizQuestion {
  assignmentQuestionId: number;
  orderNo: number;
  points: number;
  content: string;
  isMultiple: boolean;
  options: QuizOption[];
}

export interface AnswerInput { assignmentQuestionId: number; optionIds: number[] }

export interface MyAssignmentItem {
  assignmentId: number;
  title: string;
  subjectId: number;
  subjectName: string;
  className: string;
  openAt: string;
  dueAt: string;
  durationMinutes: number | null;
  questionCount: number;
  state: AttemptState;
  submittedAt: string | null;
  score: number | null;
  correctCount: number | null;
}

export interface Attempt {
  assignmentId: number;
  title: string;
  instruction: string | null;
  subjectId: number;
  subjectName: string;
  className: string;
  openAt: string;
  dueAt: string;
  durationMinutes: number | null;
  started: boolean;
  startedAt: string | null;
  deadline: string;
  serverNow: string;
  questionCount: number;
  totalPoints: number;
  lastSavedAt: string | null;
  questions: QuizQuestion[];
  answers: AnswerInput[];
}

// ---- Kết quả và phân tích ----

export interface ResultRow {
  studentId: number;
  studentCode: string;
  studentName: string;
  submitted: boolean;
  submittedAt: string | null;
  score: number | null;
  correctCount: number | null;
  analysisStatus: AnalysisStatus | null;
}

export interface ClassResult {
  assignment: AssignmentItem;
  recipientCount: number;
  submittedCount: number;
  averageScore: number | null;
  rows: ResultRow[];
}

export interface Child { studentId: number; studentCode: string; fullName: string; className: string | null }

export interface ChildResultItem {
  assignmentId: number;
  title: string;
  subjectId: number;
  subjectName: string;
  className: string;
  dueAt: string;
  submitted: boolean;
  submittedAt: string | null;
  score: number | null;
  correctCount: number | null;
  questionCount: number;
}

export interface ResultOption { optionId: number; label: string; content: string; isCorrect: boolean; selected: boolean }
export interface ResultQuestion {
  assignmentQuestionId: number;
  orderNo: number;
  points: number;
  content: string;
  explanation: string | null;
  isMultiple: boolean;
  isCorrect: boolean | null;
  earnedPoints: number | null;
  options: ResultOption[];
}

export interface AnalysisMistake { questionNo: number; why: string }
export interface Analysis {
  status: AnalysisStatus;
  summary: string | null;
  strengths: string[];
  weaknesses: string[];
  mistakes: AnalysisMistake[];
  suggestions: string[];
}

export interface PracticeResult {
  assignmentId: number;
  title: string;
  subjectName: string;
  className: string;
  studentName: string;
  dueAt: string;
  submitted: boolean;
  submittedAt: string | null;
  score: number | null;
  correctCount: number | null;
  totalPoints: number;
  questions: ResultQuestion[];
  analysis: Analysis | null;
}

// ---- Tiến độ giao bài của tổ ----

export interface SubjectOption { subjectId: number; name: string }
export interface TeacherProgress { personId: number; name: string; assignmentCount: number; openCount: number }
export interface DepartmentItem {
  assignmentId: number;
  title: string;
  campusId: number;
  campusName: string;
  classId: number;
  className: string;
  subjectId: number;
  subjectName: string;
  assignedByName: string;
  openAt: string;
  dueAt: string;
  isClosed: boolean;
  recipientCount: number;
  submittedCount: number;
}
export interface DepartmentOverview {
  isHead: boolean;
  subjects: SubjectOption[];
  teachers: TeacherProgress[];
  items: DepartmentItem[];
}
