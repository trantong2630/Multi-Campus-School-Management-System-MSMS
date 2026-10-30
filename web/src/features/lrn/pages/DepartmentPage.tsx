import { useQuery } from '@tanstack/react-query';
import { Drawer, Skeleton } from 'antd';
import { useMemo, useState } from 'react';
import { formatDateTime } from '@/shared/lib/format';
import { lrnApi } from '../api';
import { AssignmentPanel } from '../components/AssignmentPanel';
import { AiEmpty } from '../components/parts';
import { subjectVars } from '../components/tone';
import '../components/lrn.css';

/**
 * Tiến độ giao bài của tổ (LRN-05): tổ trưởng xem mỗi giáo viên trong tổ đã giao bao nhiêu bài và tình hình nộp của từng bài ở mọi lớp.
 * Chỉ đọc: chi tiết một bài mở ra cùng giao diện xem kết quả, không có nút ghi nào.
 */
export function DepartmentPage() {
  const [subjectId, setSubjectId] = useState<number | undefined>();
  const [openId, setOpenId] = useState<number | null>(null);
  const q = useQuery({ queryKey: ['lrn', 'department', subjectId], queryFn: () => lrnApi.department.overview(subjectId) });
  const d = q.data;
  const maxCount = useMemo(() => Math.max(1, ...(d?.teachers.map((t) => t.assignmentCount) ?? [1])), [d]);

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
                  <span className="dept__name">{t.name}</span>
                  <span className="dept__bar"><b style={{ width: `${(t.assignmentCount / maxCount) * 100}%` }} /></span>
                  <span className="dept__count tabular"><strong>{t.assignmentCount}</strong> bài{t.openCount > 0 && <em>{t.openCount} đang mở</em>}</span>
                </li>
              ))}
            </ul>
          )}
        </section>

        <section className="dept__items">
          <h2>Các bài của tổ</h2>
          {d.items.length === 0 ? <p className="dept__none">Chưa có bài nào.</p> : (
            <ul>
              {d.items.map((i) => {
                const pct = i.recipientCount ? Math.round((i.submittedCount / i.recipientCount) * 100) : 0;
                return (
                  <li key={i.assignmentId}>
                    <button type="button" style={subjectVars(i.subjectId)} onClick={() => setOpenId(i.assignmentId)}>
                      <span className="dept__title"><strong>{i.title}</strong><span>{i.subjectName} · lớp {i.className} · {i.campusName}</span></span>
                      <span className="dept__by">{i.assignedByName}</span>
                      <span className="dept__due">{i.isClosed ? 'Đã đóng' : 'Hạn'} {formatDateTime(i.dueAt)}</span>
                      <span className="dept__prog"><i><b style={{ width: `${pct}%` }} /></i><span className="tabular">{i.submittedCount}/{i.recipientCount}</span></span>
                    </button>
                  </li>
                );
              })}
            </ul>
          )}
        </section>
      </div>

      <Drawer open={openId !== null} onClose={() => setOpenId(null)} size={820} title="Chi tiết bài (chỉ xem)" destroyOnHidden>
        {openId !== null && <AssignmentPanel id={openId} />}
      </Drawer>
    </div>
  );
}
