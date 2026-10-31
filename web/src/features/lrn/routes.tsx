import type { RouteObject } from 'react-router-dom';
import { AssignBuilderPage } from './pages/AssignBuilderPage';
import { AssignedPage } from './pages/AssignedPage';
import { AttemptPage } from './pages/AttemptPage';
import { ChildrenPage } from './pages/ChildrenPage';
import { DepartmentPage } from './pages/DepartmentPage';
import { DonePage } from './pages/DonePage';
import { QuestionBankPage } from './pages/QuestionBankPage';
import { ResultPage } from './pages/ResultPage';
import { TodoPage } from './pages/TodoPage';

export const lrnRoutes: RouteObject[] = [
  // Giáo viên
  { path: 'lrn/questions', element: <QuestionBankPage /> },
  { path: 'lrn/assign', element: <AssignBuilderPage /> },
  { path: 'lrn/assigned', element: <AssignedPage /> },
  { path: 'lrn/department', element: <DepartmentPage /> },
  // Học sinh
  { path: 'lrn/todo', element: <TodoPage /> },
  { path: 'lrn/todo/:id', element: <AttemptPage /> },
  { path: 'lrn/done', element: <DonePage /> },
  { path: 'lrn/done/:id', element: <ResultPage /> },
  // Phụ huynh
  { path: 'lrn/children', element: <ChildrenPage /> },
  { path: 'lrn/children/:studentId/:id', element: <ResultPage /> },
];
