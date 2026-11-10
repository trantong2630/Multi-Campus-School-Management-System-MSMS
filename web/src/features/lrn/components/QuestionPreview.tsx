import { useQuery } from '@tanstack/react-query';
import { lrnApi } from '../api';
import { SourcePages } from './SourcePages';
import './lrn.css';

/** Chi tiết một câu hỏi (các đáp án, đáp án đúng, giải thích): lấy chi tiết (có đáp án) khi giáo viên bấm vào một dòng. */
export function QuestionPreview({ id }: { id: number }) {
  const q = useQuery({ queryKey: ['lrn', 'questions', 'detail', id], queryFn: () => lrnApi.questions.get(id) });
  if (!q.data) return <div className="qrow__detail qrow__detail--wait">Đang tải…</div>;
  return (
    <div className="qrow__detail">
      <ul>
        {q.data.options.map((o) => (
          <li key={o.optionId} className={o.isCorrect ? 'is-correct' : ''}><b>{o.label}</b><span>{o.content}</span>{o.isCorrect && <em>Đáp án đúng</em>}</li>
        ))}
      </ul>
      {q.data.explanation && <p className="qrow__why"><strong>Giải thích: </strong>{q.data.explanation}</p>}
      {q.data.lessonId !== null && q.data.sourceBook && q.data.sourceLesson && q.data.sourceStartPage !== null && q.data.sourceEndPage !== null && (
        <SourcePages lessonId={q.data.lessonId} book={q.data.sourceBook} lesson={q.data.sourceLesson} startPage={q.data.sourceStartPage} endPage={q.data.sourceEndPage} />
      )}
    </div>
  );
}
