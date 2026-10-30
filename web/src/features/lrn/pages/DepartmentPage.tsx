import { useQuery } from '@tanstack/react-query';
import { Drawer, Skeleton } from 'antd';
import { Search, X } from 'lucide-react';
import { useMemo, useState } from 'react';
import { PersonAvatar } from '@/shared/components/PersonAvatar';
import { formatDateTime } from '@/shared/lib/format';
import { lrnApi } from '../api';
import { AssignmentPanel } from '../components/AssignmentPanel';
import { AiEmpty } from '../components/parts';
import { subjectVars } from '../components/tone';
import type { DepartmentItem } from '../types';
import '../components/lrn.css';

type Status = 'all' | 'open' | 'closed';

/** Bỏ dấu và chữ hoa để gõ "minh tuan" vẫn ra "Phạm Minh Tuấn". */
const fold = (v: string) => v.normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/gi, 'd').toLowerCase();
const rateOf = (i: DepartmentItem) => (i.recipientCount ? i.submittedCount / i.recipientCount : 0);
/** Bài đã đóng mà dưới một nửa lớp nộp: đáng để tổ trưởng hỏi lại giáo viên. */
const isLowRate = (i: DepartmentItem) => i.isClosed && i.recipientCount > 0 && rateOf(i) < 0.5;

/**
 * Tiến độ giao bài của tổ (LRN-05): tổ trưởng xem mỗi giáo viên trong tổ đã giao bao nhiêu bài và tình hình nộp của từng bài ở mọi lớp.
 * Chỉ đọc: chi tiết một bài mở ra cùng giao diện xem kết quả, không có nút ghi nào.
 */
export function DepartmentPage() {
  const [subjectId, setSubjectId] = useState<number | undefined>();
  const [openId, setOpenId] = useState<number | null>(null);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<Status>('all');
  const [teacher, setTeacher] = useState<string | null>(null);
  const q = useQuery({ queryKey: ['lrn', 'department', subjectId], queryFn: () => lrnApi.department.overview(subjectId) });
  const d = q.data;
  const maxCount = useMemo(() => Math.max(1, ...(d?.teachers.map((t) => t.assignmentCount) ?? [1])), [d]);

  /** Bài đang mở xếp theo hạn gần nhất trước, bài đã đóng xuống cuối (mới đóng trước). */
  const shown = useMemo(() => {
    const key = fold(search.trim());
    return (d?.items ?? [])
      .filter((i) => (status === 'all' || (status === 'open') === !i.isClosed)
        && (!teacher || i.assignedByName === teacher)
        && (!key || fold(`${i.title} ${i.className} ${i.assignedByName}`).includes(key)))
      .sort((a, b) => Number(a.isClosed) - Number(b.isClosed)
        || (a.isClosed ? b.dueAt.localeCompare(a.dueAt) : a.dueAt.localeCompare(b.dueAt)));
  }, [d, search, status, teacher]);

  if (q.isLoading) return <Skeleton active paragraph={{ rows: 8 }} />;
  if (!d?.isHead) {
    return (
      <div className="dept">
        <AiEmpty title="Màn hình dành cho tổ trưởng" hint="Thầy cô cần đang giữ trách nhiệm tổ trưởng một tổ chuyên môn mới xem được tiến độ giao bài của tổ." />
      </div>
    );
  }

  return (
    <div className="dept">
      <header className="dept__head">
        <h1>Tiến độ giao bài của tổ</h1>
        {d.subjects.length > 1 && (
          <div className="pillset" role="tablist" aria-label="Lọc theo môn">
            <button type="button" role="tab" aria-selected={subjectId === undefined} className={subjectId === undefined ? 'is-on' : ''} onClick={() => setSubjectId(undefined)}>Cả tổ</button>
            {d.subjects.map((s) => (
              <button key={s.subjectId} type="button" role="tab" aria-selected={subjectId === s.subjectId} className={subjectId === s.subjectId ? 'is-on' : ''} onClick={() => setSubjectId(s.subjectId)}>{s.name}</button>
            ))}
          </div>
        )}
      </header>

      <div className="dept__grid">
        <section className="dept__teachers">
          <h2>Số bài mỗi giáo viên đã giao</h2>
          {d.teachers.length === 0 ? <p className="dept__none">Chưa có bài nào được giao trong tổ.</p> : (
            <ul>
              {d.teachers.map((t) => (
                <li key={t.personId}>
                  <button type="button" className={teacher === t.name ? 'is-on' : ''} aria-pressed={teacher === t.name} onClick={() => setTeacher(teacher === t.name ? null : t.name)}>
                    <span className="dept__line">
                      <span className="dept__name"><PersonAvatar name={t.name} size={30} />{t.name}</span>
                      <span className="dept__count tabular"><strong>{t.assignmentCount}</strong> bài{t.openCount > 0 && <em>{t.openCount} đang mở</em>}</span>
                    </span>
                    {t.campusName && (
                      <span className="dept__campus">{t.campusName}</span>
                    )}
                    <span className="dept__bar"><b style={{ width: `${(t.assignmentCount / maxCount) * 100}%` }} /></span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </section>

        <section className="dept__items">
          <div className="dept__tools">
            <h2>Các bài của tổ</h2>
            {teacher && (
              <button type="button" className="dept__who" onClick={() => setTeacher(null)} aria-label={`Bỏ lọc ${teacher}`}>
                {teacher}<X size={13} aria-hidden />
              </button>
            )}
            <label className="dept__search">
              <Search size={14} aria-hidden />
              <input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Tìm bài, lớp, giáo viên" aria-label="Tìm bài" />
            </label>
            <span className="dept__seg" role="tablist" aria-label="Trạng thái">
              {([['all', 'Tất cả'], ['open', 'Đang mở'], ['closed', 'Đã đóng']] as const).map(([v, label]) => (
                <button key={v} type="button" role="tab" aria-selected={status === v} className={status === v ? 'is-on' : ''} onClick={() => setStatus(v)}>{label}</button>
              ))}
            </span>
          </div>
          {d.items.length === 0 ? <p className="dept__none">Chưa có bài nào.</p> : shown.length === 0 ? <p className="dept__none">Không có bài nào khớp bộ lọc.</p> : (
            <>
            <div className="dept__cols" aria-hidden><span>Bài tập</span><span>Giáo viên</span><span>Hạn nộp</span><span>Đã nộp</span></div>
            <ul>
              {shown.map((i) => {
                const pct = i.recipientCount ? Math.round((i.submittedCount / i.recipientCount) * 100) : 0;
                return (
                  <li key={i.assignmentId}>
                    <button type="button" style={subjectVars(i.subjectId)} onClick={() => setOpenId(i.assignmentId)}>
                      <span className="dept__title"><span className="dept__text"><strong>{i.title}</strong><span><b>{i.subjectName}</b> · lớp {i.className}</span></span></span>
                      <span className="dept__by"><PersonAvatar name={i.assignedByName} size={24} /><span>{i.assignedByName}</span></span>
                      <span className={`dept__due${i.isClosed ? ' is-closed' : ''}`}>{formatDateTime(i.dueAt)}{i.isClosed && <em>Đã đóng</em>}</span>
                      <span className={`dept__prog${isLowRate(i) ? ' is-low' : ''}`}><i><b style={{ width: `${pct}%` }} /></i><span className="tabular">{i.submittedCount}/{i.recipientCount}</span></span>
                    </button>
                  </li>
                );
              })}
            </ul>
            </>
          )}
        </section>
      </div>

      <Drawer open={openId !== null} onClose={() => setOpenId(null)} size={820} title="Chi tiết bài (chỉ xem)" destroyOnHidden>
        {openId !== null && <AssignmentPanel id={openId} />}
      </Drawer>
    </div>
  );
}
