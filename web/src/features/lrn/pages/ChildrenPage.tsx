import { useQuery } from '@tanstack/react-query';
import { Skeleton } from 'antd';
import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { lrnApi } from '../api';
import { MsmsAi } from '../components/MsmsAi';
import { AiEmpty } from '../components/parts';
import { ScoreWall } from '../components/ScoreWall';
import type { WallItem } from '../components/ScoreWall';
import '../components/lrn.css';

const firstName = (full: string) => full.trim().split(/\s+/).slice(-1)[0];

/** Kết quả của con (phụ huynh): chọn con, xem các bài con đã làm kèm điểm; bấm vào để xem đáp án và phân tích AI (4.6.16). */
export function ChildrenPage() {
  const navigate = useNavigate();
  const children = useQuery({ queryKey: ['lrn', 'children'], queryFn: lrnApi.results.children });
  const [picked, setPicked] = useState<number | null>(null);
  const child = children.data?.find((c) => c.studentId === picked) ?? children.data?.[0];

  const results = useQuery({
    queryKey: ['lrn', 'child-results', child?.studentId], enabled: !!child, queryFn: () => lrnApi.results.childList(child!.studentId),
  });

  const items = useMemo<WallItem[]>(() => (results.data ?? []).map((r) => ({
    assignmentId: r.assignmentId, title: r.title, subjectId: r.subjectId, subjectName: r.subjectName, className: r.className,
    at: r.submittedAt ?? r.dueAt, submitted: r.submitted, score: r.score, correctCount: r.correctCount, questionCount: r.questionCount,
  })), [results.data]);

  return (
    <div className="done">
      <header className="done__head">
        <h1>Kết quả của con</h1>
        {child && items.length > 0 && <MsmsAi size={56} mood="idle" say={`Đây là các bài ${firstName(child.fullName)} đã làm. Bấm vào để xem phân tích chi tiết.`} />}
      </header>

      {children.isLoading ? <Skeleton active paragraph={{ rows: 5 }} /> : !child ? (
        <AiEmpty title="Chưa có con nào được liên kết" hint="Liên hệ nhà trường để liên kết tài khoản với hồ sơ của con." />
      ) : (
        <>
          {(children.data?.length ?? 0) > 1 && (
            <div className="kids" role="tablist" aria-label="Chọn con">
              {children.data!.map((c) => (
                <button
                  key={c.studentId} type="button" role="tab" aria-selected={c.studentId === child.studentId}
                  className={c.studentId === child.studentId ? 'is-on' : ''} onClick={() => setPicked(c.studentId)}
                >
                  <span className="kids__avatar">{firstName(c.fullName).charAt(0).toUpperCase()}</span>
                  <span><strong>{c.fullName}</strong>{c.className && <em>{c.className}</em>}</span>
                </button>
              ))}
            </div>
          )}
          {results.isLoading ? <Skeleton active paragraph={{ rows: 4 }} /> : items.length === 0 ? (
            <AiEmpty title={`${firstName(child.fullName)} chưa có bài nào để xem`} hint="Bài sẽ hiện ở đây sau khi con nộp hoặc đã hết hạn." />
          ) : (
            <ScoreWall
              items={items} averageLabel={`Điểm trung bình của ${firstName(child.fullName)}`}
              onOpen={(i) => navigate(`/lrn/children/${child.studentId}/${i.assignmentId}`)}
            />
          )}
        </>
      )}
    </div>
  );
}
