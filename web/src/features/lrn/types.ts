// Kiểu dữ liệu khớp DTO của Modules/Lrn/Dtos. Trạng thái là giá trị lưu trong DB; nhãn tiếng Việt do từng màn hình tự đặt.

export type QuestionStatus = 'Draft' | 'Ready';
/** Bốn mức độ nhận thức của câu hỏi; null là chưa phân loại (câu có từ trước khi có độ khó). */
export type Difficulty = 'Recognize' | 'Understand' | 'Apply' | 'HighApply';
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
  difficulty: Difficulty | null;
}

export interface QuestionDetail extends QuestionItem {
  explanation: string | null;
  options: QuestionOption[];
  /** Bài trong sách mà AI dựa vào; trống với câu soạn tay. */
  lessonId: number | null;
  /** Nguồn của câu do AI soạn từ sách: tên sách, bài và trang in. */
  sourceBook: string | null;
  sourceLesson: string | null;
  sourceStartPage: number | null;
  sourceEndPage: number | null;
}

/** Một bài trong mục lục sách để chọn khi nhờ AI soạn; trang theo số in trên sách. */
export interface TextbookLesson { lessonId: number; bookTitle: string; chapter: string; title: string; startPage: number; endPage: number }

export interface SaveQuestionInput {
  subjectId: number;
  gradeLevel: number;
  topic: string;
  content: string;
  explanation?: string;
  options: OptionInput[];
  difficulty: Difficulty;
}

export interface GenerateQuestionsInput {
  subjectId: number;
  gradeLevel: number;
  lessonId: number;
  /** Gợi ý thu hẹp trong bài, không bắt buộc. */
  note?: string;
  /** Số câu ở từng độ khó; tổng 1 đến 10. */
  levels: { difficulty: Difficulty; count: number }[];
}
export interface ConfirmDraftsResult { confirmed: number; failed: { questionId: number; reason: string }[] }
export interface GenerateQuestionsResult { drafts: QuestionDetail[]; discarded: number }

export interface QuestionQuery {
  subjectId?: number;
  gradeLevel?: number;
  status?: QuestionStatus;
  mine?: boolean;
  includeHidden?: boolean;
  /** Một độ khó, hoặc 'None' để chỉ xem câu chưa phân loại. */
  difficulty?: Difficulty | 'None';
  search?: string;
  /** Tìm theo chủ đề của câu hỏi (không tìm trong nội dung câu). */
  topic?: string;
  page?: number;
  pageSize?: number;
}

// ---- Giao bài ----

export interface ClassSubjectOption { classId: number; className: string; gradeLevel: number; subjectId: number; subjectName: string }

export interface AssignmentLine { questionId: number }

/** Tổng điểm của một phần (độ khó) trong bài; các phần cộng lại đúng 10. */
export interface SectionPoints { difficulty: Difficulty; points: number }

export interface CreateAssignmentInput {
  subjectId: number;
  classId: number;
  title: string;
  instruction?: string;
  openAt?: string;
  dueAt: string;
  durationMinutes?: number;
  items: AssignmentLine[];
  sections: SectionPoints[];
}

/** Đề đang soạn để tạo bản in, chưa giao. */
export interface ExportPaperInput {
  subjectId: number;
  title: string;
  instruction?: string;
  durationMinutes?: number;
  items: AssignmentLine[];
  sections: SectionPoints[];
  format: PaperFormat;
  withAnswers: boolean;
}
export type PaperFormat = 'pdf' | 'docx';

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
  content: string;
  explanation: string | null;
  isMultiple: boolean;
  options: QuestionOption[];
  difficulty: Difficulty | null;
  points: number;
}

/** Một phần của bài theo độ khó (null chỉ có ở bài giao trước khi có độ khó). */
export interface AssignmentSection { difficulty: Difficulty | null; questionCount: number; points: number }

export interface AssignmentDetail {
  assignment: AssignmentItem;
  instruction: string | null;
  questions: AssignmentQuestion[];
  sections: AssignmentSection[];
  totalPoints: number;
}

// ---- Ma trận đề (bài × độ khó) ----

/** Số câu Sẵn dùng trong ngân hàng của một chủ đề ở từng độ khó. */
export interface MatrixRow { topic: string; recognize: number; understand: number; apply: number; highApply: number }
export interface MatrixCell { topic: string; difficulty: Difficulty; count: number }
export interface PickShortage { topic: string; difficulty: Difficulty; wanted: number; available: number }
export interface PickResult { questions: QuestionItem[]; shortages: PickShortage[] }

// ---- Làm bài ----

export interface QuizOption { optionId: number; label: string; content: string }
export interface QuizQuestion {
  assignmentQuestionId: number;
  orderNo: number;
  content: string;
  isMultiple: boolean;
  /** Số đáp án đúng: học sinh biết phải chọn mấy đáp án. */
  correctCount: number;
  options: QuizOption[];
  points: number;
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
  content: string;
  explanation: string | null;
  isMultiple: boolean;
  isCorrect: boolean | null;
  options: ResultOption[];
  points: number;
  difficulty: Difficulty | null;
}

export interface AnalysisMistake { questionNo: number; why: string }
/** Nơi nên ôn trong sách: bài nguồn của các câu sai, trang theo số in. Do Backend ghép, không do AI sinh. */
export interface StudyRef { lessonId: number; bookTitle: string; lessonTitle: string; startPage: number; endPage: number; questionNos: number[] }
export interface Analysis {
  status: AnalysisStatus;
  summary: string | null;
  strengths: string[];
  weaknesses: string[];
  mistakes: AnalysisMistake[];
  suggestions: string[];
  studyRefs?: StudyRef[] | null;
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
  questions: ResultQuestion[];
  analysis: Analysis | null;
}

// ---- Tiến độ giao bài của tổ ----

export interface SubjectOption { subjectId: number; name: string }
export interface TeacherProgress { personId: number; name: string; campusId: number; campusName: string; assignmentCount: number; openCount: number }
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
export interface DepartmentTeaching { subjectId: number; subjectName: string; classNames: string[] }
/** Thầy cô thuộc tổ, cộng giáo viên tổ khác đang dạy môn của tổ (inDepartment = false). */
export interface DepartmentMember {
  personId: number;
  name: string;
  campusName: string | null;
  inDepartment: boolean;
  periodsPerWeek: number;
  teaching: DepartmentTeaching[];
}
export interface DepartmentOverview {
  isHead: boolean;
  subjects: SubjectOption[];
  teachers: TeacherProgress[];
  items: DepartmentItem[];
  /** Học kỳ dùng để suy phân công dạy (thời khóa biểu đã phát hành). */
  semesterName: string | null;
  members: DepartmentMember[];
}
