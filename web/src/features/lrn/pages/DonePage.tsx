import { useQuery } from '@tanstack/react-query';
import { Skeleton } from 'antd';
import { useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import { lrnApi } from '../api';
import { MsmsAi } from '../components/MsmsAi';
import { AiEmpty } from '../components/parts';
import { ScoreWall } from '../components/ScoreWall';
import type { WallItem } from '../components/ScoreWall';
import '../components/lrn.css';

/** Danh sách bài tập (học sinh): các bài đã nộp hoặc đã quá hạn, mỗi bài một ô điểm; bấm vào để xem đáp án và phân tích AI. */
export function DonePage() {
  const navigate = useNavigate();
  const list = useQuery({ queryKey: ['lrn', 'my-assignments'], queryFn: lrnApi.student.list });

  const items = useMemo<WallItem[]>(() => (list.data ?? [])
    .filter((a) => a.state === 'Submitted' || a.state === 'NotSubmitted')
    .sort((a, b) => (b.submittedAt ?? b.dueAt).localeCompare(a.submittedAt ?? a.dueAt))
    .map((a) => ({
      assignmentId: a.assignmentId, title: a.title, subjectId: a.subjectId, subjectName: a.subjectName, className: a.className,
      at: a.submittedAt ?? a.dueAt, submitted: a.state === 'Submitted', score: a.score, correctCount: a.correctCount, questionCount: a.questionCount,
    })), [list.data]);

  return (
    <div className="done">
      <header className="done__head">
        <h1>Danh sách bài tập</h1>
        <MsmsAi size={56} mood="idle" say="Bấm vào một bài để xem đáp án và lời phân tích của mình." />
      </header>
      {list.isLoading ? <Skeleton active paragraph={{ rows: 5 }} /> : items.length === 0 ? (
        <AiEmpty title="Chưa có bài nào để xem" hint="Làm xong bài ở mục Bài tập được giao, kết quả sẽ hiện ở đây." />
      ) : (
        <ScoreWall items={items} averageLabel="Điểm trung bình" onOpen={(i) => navigate(`/lrn/done/${i.assignmentId}`)} />
      )}
    </div>
  );
}
