import { useQuery } from '@tanstack/react-query';
import { Skeleton } from 'antd';
import { ArrowLeft } from 'lucide-react';
import { Link, useParams } from 'react-router-dom';
import { useAuth } from '@/app/AuthProvider';
import { getErrorMessage } from '@/app/apiClient';
import { lrnApi } from '../api';
import { AiBubble, AiAvatar } from '../components/MsmsAi';
import { ResultView } from '../components/ResultView';
import '../components/lrn.css';

/**
 * Kết quả một bài: học sinh vào từ Danh sách bài tập, phụ huynh vào từ Kết quả của con. Ngay sau khi nộp trang hiện điểm và đáp án;
 * phân tích AI về sau, trang tự hỏi lại vài giây một lần cho tới khi có (BR-22, BR-23).
 */
export function ResultPage() {
  const { user } = useAuth();
  const params = useParams();
  const assignmentId = Number(params.id);
  const studentId = params.studentId ? Number(params.studentId) : undefined;
  const isParent = user?.accountType === 'Parent';
  const back = isParent ? '/lrn/children' : '/lrn/done';

  const q = useQuery({
    queryKey: ['lrn', 'result', assignmentId, studentId ?? 'me'],
    queryFn: () => lrnApi.results.detail(assignmentId, studentId),
    refetchInterval: (query) => (query.state.data?.analysis?.status === 'Pending' ? 3000 : false),
    retry: false,
  });

  return (
    <div className="result-page">
      <Link to={back} className="back-link"><ArrowLeft size={16} />{isParent ? 'Kết quả của con' : 'Danh sách bài tập'}</Link>
      {q.isLoading ? <Skeleton active paragraph={{ rows: 8 }} /> : q.isError ? (
        <div className="ai-empty">
          <AiAvatar mood="idle" size={84} />
          <AiBubble tail="none" tone="warn">{getErrorMessage(q.error)}</AiBubble>
        </div>
      ) : (
        <ResultView data={q.data!} viewer={isParent ? 'parent' : 'student'} />
      )}
    </div>
  );
}
