import { useQuery } from '@tanstack/react-query';
import { Drawer, Skeleton } from 'antd';
import { BookOpenCheck, ChevronRight, Search, Users, X } from 'lucide-react';
import { useMemo, useState } from 'react';
import { PersonAvatar } from '@/shared/components/PersonAvatar';
import { formatDateTime } from '@/shared/lib/format';
import { deptLine } from '../aiLines';
import { lrnApi } from '../api';
import { AssignmentPanel } from '../components/AssignmentPanel';
import { MsmsAi } from '../components/MsmsAi';
import { AiEmpty } from '../components/parts';
import { subjectVars } from '../components/tone';
import { useNow } from '../hooks';
import { dueHint } from '../time';
import type { DepartmentItem } from '../types';
import '../components/lrn.css';

type Status = 'all' | 'open' | 'closed' | 'low';

/** Bỏ dấu và chữ hoa để gõ "minh tuan" vẫn ra "Phạm Minh Tuấn". */
const fold = (v: string) => v.normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/gi, 'd').toLowerCase();
const rateOf = (i: DepartmentItem) => (i.recipientCount ? i.submittedCount / i.recipientCount : 0);
/** Bài đã đóng mà dưới một nửa lớp nộp: đáng để tổ trưởng hỏi lại giáo viên. */
const isLowRate = (i: DepartmentItem) => i.isClosed && i.recipientCount > 0 && rateOf(i) < 0.5;
const pctText = (v: number | null) => (v == null ? '—' : `${v}%`);

/** Vòng tròn tiến độ: phần trăm nằm giữa, màu theo tình trạng bài. */
function Ring({ pct, size, stroke, tone }: { pct: number; size: number; stroke: number; tone: 'ok' | 'warn' | 'idle'; }) {
  const r = (size - stroke) / 2;
  const c = 2 * Math.PI * r;
  return (
    <span className={`dring dring--${tone}`} style={{ width: size, height: size }}>
      <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`} aria-hidden>
        <g transform={`rotate(-90 ${size / 2} ${size / 2})`} fill="none" strokeWidth={stroke} strokeLinecap="round">
          <circle className="dring__bg" cx={size / 2} cy={size / 2} r={r} />
          {pct > 0 && <circle className="dring__fg" cx={size / 2} cy={size / 2} r={r} strokeDasharray={`${(c * Math.min(pct, 100)) / 100} ${c}`} />}
        </g>
      </svg>
      <b className="tabular">{pct}<small>%</small></b>
    </span>
  );
}

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
  const nowMs = useNow(0, 30_000);
  const q = useQuery({ queryKey: ['lrn', 'department', subjectId], queryFn: () => lrnApi.department.overview(subjectId) });
  const d = q.data;

  /** Bài đang mở xếp theo hạn gần nhất trước, bài đã đóng xuống cuối (mới đóng trước). */
  const shown = useMemo(() => {
    const key = fold(search.trim());
    return (d?.items ?? [])
      .filter((i) => (status === 'all' || (status === 'low' ? isLowRate(i) : (status === 'open') === !i.isClosed))
        && (!teacher || i.assignedByName === teacher)
        && (!key || fold(`${i.title} ${i.className} ${i.assignedByName}`).includes(key)))
      .sort((a, b) => Number(a.isClosed) - Number(b.isClosed)
        || (a.isClosed ? b.dueAt.localeCompare(a.dueAt) : a.dueAt.localeCompare(b.dueAt)));
  }, [d, search, status, teacher]);

  const stats = useMemo(() => {
    const items = d?.items ?? [];
    const closed = items.filter((i) => i.isClosed);
    const sub = closed.reduce((n, i) => n + i.submittedCount, 0);
    const rec = closed.reduce((n, i) => n + i.recipientCount, 0);
    return { total: items.length, open: items.length - closed.length, closed: closed.length, rate: rec ? Math.round((sub / rec) * 100) : null, low: items.filter(isLowRate).length };
  }, [d]);

  /** Tỷ lệ nộp trung bình của các bài đã đóng, tính riêng cho từng giáo viên. */
  const teacherRate = useMemo(() => {
    const acc = new Map<string, { sub: number; rec: number }>();
    for (const i of d?.items ?? []) {
      if (!i.isClosed) continue;
      const cur = acc.get(i.assignedByName) ?? { sub: 0, rec: 0 };
      acc.set(i.assignedByName, { sub: cur.sub + i.submittedCount, rec: cur.rec + i.recipientCount });
    }
    return acc;
  }, [d]);

  /** Chỉ một giáo viên (hoặc đang lọc một người) thì tên giáo viên trên từng dòng là thừa. */
  const showTeacher = !teacher && (d?.teachers.length ?? 0) > 1;

  if (q.isLoading) return <Skeleton active paragraph={{ rows: 8 }} />;
  if (!d?.isHead) {
    return (
      <div className="dept">
        <AiEmpty title="Màn hình dành cho tổ trưởng" hint="Thầy cô cần đang giữ trách nhiệm tổ trưởng một tổ chuyên môn mới xem được tiến độ giao bài của tổ." />
      </div>
    );
  }

  const maxCount = Math.max(1, ...d.teachers.map((t) => t.assignmentCount));
  const blocks: ReadonlyArray<readonly [Status, string, number, string]> = [
    ['all', 'Đã giao', stats.total, 'bài trong tổ'],
    ['open', 'Đang mở', stats.open, 'học sinh đang làm'],
    ['closed', 'Đã đóng', stats.closed, 'đã hết hạn nộp'],
    ['low', 'Cần nhắc', stats.low, 'nộp chưa đến một nửa'],
  ];

  return (
    <div className="dept">
      <header className="dept__hero">
        <div className="dept__heroText">
          <h1>Nhịp giao bài của tổ</h1>
          <p>Xem thầy cô nào đã giao gì, lớp nào đang nộp tới đâu.</p>
          {d.subjects.length > 1 && (
            <div className="pillset pillset--wrap" role="tablist" aria-label="Lọc theo môn">
              <button type="button" role="tab" aria-selected={subjectId === undefined} className={subjectId === undefined ? 'is-on' : ''} onClick={() => setSubjectId(undefined)}>Cả tổ</button>
              {d.subjects.map((s) => (
                <button key={s.subjectId} type="button" role="tab" aria-selected={subjectId === s.subjectId} className={subjectId === s.subjectId ? 'is-on' : ''} onClick={() => setSubjectId(s.subjectId)}>{s.name}</button>
              ))}
            </div>
          )}
        </div>
        <MsmsAi
          mood={stats.low > 0 ? 'warn' : stats.total === 0 ? 'idle' : 'happy'} tone={stats.low > 0 ? 'warn' : 'plain'} size={84}
          say={deptLine({ total: stats.total, open: stats.open, low: stats.low, teachers: d.teachers.length })}
        />
      </header>

      <section className="dept__pulse" aria-label="Tổng quan">
        <div className="dept__gauge">
          <Ring pct={stats.rate ?? 0} size={116} stroke={12} tone={stats.rate == null ? 'idle' : stats.rate < 50 ? 'warn' : 'ok'} />
          <div>
            <strong>Tỷ lệ nộp bài</strong>
            <span>{stats.rate == null ? 'Chưa có bài nào hết hạn để tính.' : 'trung bình các bài đã đóng'}</span>
          </div>
        </div>
        <div className="dept__stats" role="tablist" aria-label="Lọc theo tình trạng">
          {blocks.map(([v, label, n, hint]) => (
            <button key={v} type="button" role="tab" aria-selected={status === v} className={`dept__stat dept__stat--${v}${status === v ? ' is-on' : ''}`} onClick={() => setStatus(status === v ? 'all' : v)}>
              <span>{label}</span>
              <strong className="tabular">{n}</strong>
              <small>{hint}</small>
            </button>
          ))}
        </div>
      </section>

      <div className="dept__grid">
        <aside className="dept__teachers">
          <h2><Users size={18} aria-hidden />Thầy cô trong tổ</h2>
          {d.teachers.length === 0 ? <p className="dept__none">Chưa thầy cô nào giao bài.</p> : (
            <ul>
              {d.teachers.map((t) => {
                const tr = teacherRate.get(t.name);
                const rate = tr && tr.rec ? Math.round((tr.sub / tr.rec) * 100) : null;
                return (
                  <li key={t.personId}>
                    <button type="button" className={teacher === t.name ? 'is-on' : ''} aria-pressed={teacher === t.name} onClick={() => setTeacher(teacher === t.name ? null : t.name)}>
                      <PersonAvatar name={t.name} size={40} />
                      <span className="dept__tmain">
                        <strong>{t.name}</strong>
                        <span className="dept__tmeta">{t.campusName ?? 'Chưa gắn phân hiệu'}</span>
                        <span className="dept__split" style={{ width: `${(t.assignmentCount / maxCount) * 100}%` }} title={`${t.openCount} đang mở, ${t.assignmentCount - t.openCount} đã đóng`}>
                          <b style={{ flexGrow: t.openCount }} /><i style={{ flexGrow: t.assignmentCount - t.openCount }} />
                        </span>
                      </span>
                      <span className="dept__tnum tabular">
                        <strong>{t.assignmentCount}</strong>
                        <small>bài</small>
                        <em className={rate != null && rate < 50 ? 'is-low' : ''}>nộp {pctText(rate)}</em>
                      </span>
                    </button>
                  </li>
                );
              })}
            </ul>
          )}
          <p className="dept__legend"><b className="is-open" />đang mở<b />đã đóng</p>
        </aside>

        <section className="dept__items">
          <div className="dept__tools">
            <h2>Danh sách bài</h2>
            <span className="dept__count">{shown.length} bài</span>
            {teacher && (
              <button type="button" className="dept__who" onClick={() => setTeacher(null)} aria-label={`Bỏ lọc ${teacher}`}>
                {teacher}<X size={13} aria-hidden />
              </button>
            )}
            <label className="dept__search">
              <Search size={16} aria-hidden />
              <input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Tìm tên bài, lớp, thầy cô" aria-label="Tìm bài" />
            </label>
          </div>
          {d.items.length === 0 ? <p className="dept__none">Chưa có bài nào được giao.</p> : shown.length === 0 ? <p className="dept__none">Không có bài nào khớp, thử bỏ bớt bộ lọc nhé.</p> : (
            <ul className="dept__list">
              {shown.map((i) => {
                const pct = Math.round(rateOf(i) * 100);
                const low = isLowRate(i);
                const hint = i.isClosed ? null : dueHint(i.dueAt, nowMs);
                return (
                  <li key={i.assignmentId}>
                    <button type="button" className={`dept__row${i.isClosed ? ' is-closed' : ''}`} style={subjectVars(i.subjectId)} onClick={() => setOpenId(i.assignmentId)}>
                      <Ring pct={pct} size={54} stroke={6} tone={low ? 'warn' : i.isClosed ? 'idle' : 'ok'} />
                      <span className="dept__rowMain">
                        <strong>{i.title}</strong>
                        <span className="dept__rowSub">
                          <b>{i.subjectName}</b><i /> lớp {i.className}
                          {showTeacher && <><i /><PersonAvatar name={i.assignedByName} size={18} />{i.assignedByName}</>}
                        </span>
                      </span>
                      <span className="dept__rowDue">
                        <strong className={hint ? `is-${hint.tone}` : ''}>{hint ? hint.text : 'Đã đóng'}</strong>
                        <small>{formatDateTime(i.dueAt)}</small>
                      </span>
                      <span className="dept__rowDone tabular">
                        <strong>{i.submittedCount}<small>/{i.recipientCount}</small></strong>
                        {low ? <em>Nên nhắc</em> : <small>đã nộp</small>}
                      </span>
                      <ChevronRight size={18} className="dept__go" aria-hidden />
                    </button>
                  </li>
                );
              })}
            </ul>
          )}
        </section>
      </div>

      <section className="dept__members" aria-label="Thầy cô trong tổ">
        <div className="dept__tools">
          <h2><BookOpenCheck size={18} aria-hidden />Thầy cô và phân công dạy</h2>
          <span className="dept__count">{d.members.filter((m) => m.inDepartment).length} người trong tổ</span>
          <span className="dept__hint">
            {d.semesterName ? `Theo thời khóa biểu đã phát hành, ${d.semesterName}` : 'Chưa có học kỳ hiện hành để suy phân công dạy'}
          </span>
        </div>
        {d.members.length === 0 ? <p className="dept__none">Tổ chưa có thầy cô nào.</p> : (
          <ul className="dept__mlist">
            {d.members.map((m) => (
              <li key={m.personId} className="dept__member">
                <PersonAvatar name={m.name} size={36} />
                <span className="dept__tmain">
                  <strong>{m.name}{!m.inDepartment && <em className="dept__tag">Tổ khác</em>}</strong>
                  <span className="dept__tmeta">{m.campusName ?? 'Chưa gắn phân hiệu'}</span>
                </span>
                <span className="dept__teach">
                  {m.teaching.length === 0 ? <span className="dept__tmeta">Chưa dạy môn nào của tổ</span> : m.teaching.map((t) => (
                    <span key={t.subjectId} className="dept__chip" style={subjectVars(t.subjectId)}>
                      <b>{t.subjectName}</b>{t.classNames.join(', ')}
                    </span>
                  ))}
                </span>
                <span className="dept__tnum tabular">
                  <strong>{m.periodsPerWeek}</strong>
                  <small>tiết/tuần</small>
                </span>
              </li>
            ))}
          </ul>
        )}
      </section>

      <Drawer open={openId !== null} onClose={() => setOpenId(null)} size={820} title="Chi tiết bài (chỉ xem)" destroyOnHidden>
        {openId !== null && <AssignmentPanel id={openId} />}
      </Drawer>
    </div>
  );
}
