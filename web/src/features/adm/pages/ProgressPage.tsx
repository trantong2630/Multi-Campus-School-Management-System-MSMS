import { useQuery } from '@tanstack/react-query';
import { Skeleton } from 'antd';
import { ChevronRight, Search } from 'lucide-react';
import { useMemo, useState } from 'react';
import { useAuth } from '@/app/AuthProvider';
import { MsmsAi } from '@/features/lrn/components/MsmsAi';
import { subjectVars } from '@/features/lrn/components/tone';
import { DepartmentPage } from '@/features/lrn/pages/DepartmentPage';
import { dueHint } from '@/features/lrn/time';
import { formatDateTime } from '@/shared/lib/format';
import { admApi } from '../api';
import { Compare, Meter, Seg, toneOfRate } from '../components/BoardKit';
import { applyFilters, closedRate, groupItems, idleTeachers, isLow, weekCompare } from '../progress/progressModel';
import type { Filters, Lens } from '../progress/progressModel';
import type { Progress, ProgressItem } from '../types';
import '@/features/lrn/components/lrn.css';
import '../components/board.css';

const LENS_ORDER: Lens[] = ['campus', 'department', 'subject', 'class', 'teacher'];
const LENS_LABEL: Record<Lens, string> = { campus: 'Phân hiệu', department: 'Tổ', subject: 'Môn', class: 'Lớp', teacher: 'Giáo viên' };
const FILTER_KEY: Record<Lens, keyof Filters> = {
  campus: 'campusId', department: 'departmentId', subject: 'subjectId', class: 'classId', teacher: 'teacherId',
};
type Status = 'all' | 'open' | 'closed' | 'low';
const PAGE = 12;
const IDLE_DAYS = 14;

/** Bỏ dấu và chữ hoa để gõ "minh tuan" vẫn ra "Phạm Minh Tuấn". */
const fold = (v: string) => v.normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/gi, 'd').toLowerCase();
const pctText = (v: number | null) => (v == null ? '—' : `${Math.round(v)}%`);
const rateOf = (i: ProgressItem) => (i.recipientCount ? i.submittedCount / i.recipientCount : 0);

function Ring({ pct, size, stroke, tone }: { pct: number; size: number; stroke: number; tone: 'ok' | 'warn' | 'idle' }) {
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

const toneOf = (rate: number | null): 'ok' | 'warn' | 'idle' => (rate === null ? 'idle' : rate < 50 ? 'warn' : 'ok');

function aiLine(o: { total: number; open: number; low: number; idle: number; showIdle: boolean }): string {
  if (o.total === 0) return 'Chưa có bài tập nào trong phạm vi này. Có bài mới là mình báo ngay!';
  if (o.low > 0) return `Có ${o.low} bài đã đóng mà chưa đến nửa lớp nộp. Bạn xem nhóm nào đang yếu nhé!`;
  if (o.showIdle && o.idle > 0) return `${o.idle} giáo viên đã hơn ${IDLE_DAYS} ngày chưa giao bài. Nhắc nhẹ một câu là ổn.`;
  if (o.open > 0) return `Đang có ${o.open} bài mở. Hiện chưa bài nào đáng lo.`;
  return 'Các bài đã đóng đều được nộp khá đầy đủ. Làm tốt lắm!';
}

/**
 * Tiến độ bài tập rèn luyện: một màn cho Hiệu trưởng, Hiệu phó và chủ nhiệm.
 * Mỗi vai trò chỉ có các cách xem thuộc phạm vi của mình: Hiệu trưởng (phân hiệu, tổ, môn, lớp, giáo viên), Hiệu phó (tổ, môn, lớp, giáo viên),
 * chủ nhiệm (môn của lớp mình). Tổ trưởng dùng màn của tổ (LRN-05); giáo viên vừa chủ nhiệm vừa làm tổ trưởng chuyển qua lại bằng nút chọn.
 * Chỉ đọc, chỉ số liệu tổng hợp theo bài; không có bài nộp hay điểm từng học sinh.
 */
export function ProgressPage() {
  const { user } = useAuth();
  const isTeacher = user?.accountType === 'Teacher';
  const homeroom = !!user?.scope?.homeroomClassIds.length;
  const head = !!user?.scope?.headDepartmentIds.length;
  const [mode, setMode] = useState<'class' | 'dept'>(homeroom ? 'class' : 'dept');

  if (isTeacher && !homeroom) return <DepartmentPage />;
  return (
    <>
      {isTeacher && head && (
        <div style={{ marginBottom: 16 }}>
          <Seg label="Vai trò theo dõi" value={mode} onChange={setMode}
            options={[{ value: 'class', label: 'Với tư cách chủ nhiệm' }, { value: 'dept', label: 'Với tư cách tổ trưởng' }]} />
        </div>
      )}
      {isTeacher && head && mode === 'dept' ? <DepartmentPage /> : <ProgressBoard />}
    </>
  );
}

function ProgressBoard() {
  const q = useQuery({ queryKey: ['adm', 'progress'], queryFn: () => admApi.progress() });
  if (q.isLoading || !q.data) return <Skeleton active paragraph={{ rows: 10 }} />;
  return <Board d={q.data} />;
}

interface Step { lens: Lens; key: number }

function Board({ d }: { d: Progress }) {
  const nowMs = useMemo(() => new Date(d.now).getTime(), [d.now]);
  const isClass = d.scope === 'Class';
  const [trail, setTrail] = useState<Step[]>([]);
  const [lensPick, setLensPick] = useState<Lens | null>(null);
  const [status, setStatus] = useState<Status>('all');
  const [search, setSearch] = useState('');
  const [shown, setShown] = useState(PAGE);

  const filters = useMemo(() => Object.fromEntries(trail.map((s) => [FILTER_KEY[s.lens], s.key])) as Filters, [trail]);
  // Cách xem theo vai trò: chủ nhiệm chỉ xem theo môn (và lớp nếu chủ nhiệm nhiều lớp), không có tổ, phân hiệu hay giáo viên.
  const available = useMemo(() => LENS_ORDER.filter((l) => {
    if (trail.some((s) => s.lens === l)) return false;
    if (isClass && l !== 'subject' && l !== 'class') return false;
    if (d.scope === 'Campus' && l === 'department') return false;
    if (l === 'campus') return d.campuses.length > 1;
    if (l === 'class') return d.classes.length > 1;
    return true;
  }), [trail, isClass, d.campuses.length, d.classes.length]);
  const defaultLens: Lens = isClass ? 'subject' : d.campuses.length > 1 ? 'campus' : d.scope === 'Campus' ? (d.classes.length > 1 ? 'class' : 'subject') : 'department';
  const lens: Lens = lensPick && available.includes(lensPick) ? lensPick : available.includes(defaultLens) ? defaultLens : (available[0] ?? 'subject');
  const canDrill = available.some((l) => l !== lens);

  const scoped = useMemo(() => applyFilters(d.items, filters), [d.items, filters]);
  const low = useMemo(() => scoped.filter(isLow), [scoped]);
  const open = scoped.filter((i) => !i.isClosed).length;
  const rate = closedRate(scoped);
  const compare = useMemo(() => weekCompare(scoped, nowMs), [scoped, nowMs]);
  const idle = useMemo(() => idleTeachers(d.teachers, d.items, filters, nowMs), [d.teachers, d.items, filters, nowMs]);
  const groups = useMemo(() => groupItems(scoped, lens, nowMs), [scoped, lens, nowMs]);

  const nameOf = (l: Lens, id: number): string => {
    if (l === 'campus') return d.campuses.find((c) => c.id === id)?.name ?? d.items.find((i) => i.campusId === id)?.campusName ?? '';
    const it = d.items.find((i) => (l === 'department' ? i.departmentId : l === 'subject' ? i.subjectId : l === 'class' ? i.classId : i.teacherId) === id);
    if (!it) return d.classes.find((c) => l === 'class' && c.classId === id)?.className ?? '';
    return l === 'department' ? `Tổ ${it.departmentName}` : l === 'subject' ? it.subjectName : l === 'class' ? `Lớp ${it.className}` : it.teacherName;
  };
  const go = (next: Step[]) => { setTrail(next); setLensPick(null); setShown(PAGE); };
  const scopeText = d.scope === 'School' ? 'Toàn trường' : d.scope === 'Campus' ? d.campuses.map((c) => c.name).join(', ') : d.classes.map((c) => `Lớp ${c.className}`).join(', ');

  const list = useMemo(() => {
    const key = fold(search.trim());
    return scoped
      .filter((i) => (status === 'all' || (status === 'low' ? isLow(i) : (status === 'open') === !i.isClosed))
        && (!key || fold(`${i.title} ${i.subjectName} ${i.className} ${isClass ? '' : i.teacherName}`).includes(key)))
      .sort((a, b) => (status === 'low' ? rateOf(a) - rateOf(b)
        : Number(a.isClosed) - Number(b.isClosed) || (a.isClosed ? b.dueAt.localeCompare(a.dueAt) : a.dueAt.localeCompare(b.dueAt))));
  }, [scoped, status, search, isClass]);

  const blocks: ReadonlyArray<readonly [Status, string, number, string]> = [
    ['all', 'Đã giao', scoped.length, 'bài'],
    ['open', 'Đang mở', open, 'bài'],
    ['closed', 'Đã đóng', scoped.length - open, 'bài'],
    ['low', 'Cần nhắc', low.length, 'bài nộp dưới một nửa'],
  ];

  return (
    <div className="dept">
      <header className="dept__hero">
        <div className="dept__heroText">
          <h1>Tiến độ bài tập rèn luyện</h1>
          <p>{scopeText}</p>
        </div>
        <MsmsAi mood={low.length > 0 ? 'warn' : scoped.length === 0 ? 'idle' : 'happy'} tone={low.length > 0 ? 'warn' : 'plain'} size={84}
          say={aiLine({ total: scoped.length, open, low: low.length, idle: idle.length, showIdle: !isClass })} />
      </header>

      <section className="dept__pulse" aria-label="Tổng quan">
        <div className="dept__gauge">
          <Ring pct={rate === null ? 0 : Math.round(rate)} size={116} stroke={12} tone={toneOf(rate)} />
          <div>
            <strong>Tỷ lệ nộp bài</strong>
            {rate === null ? <span>Chưa có bài hết hạn</span> : <Compare delta={compare.delta} />}
          </div>
        </div>
        <div className="dept__stats" role="tablist" aria-label="Lọc danh sách theo tình trạng">
          {blocks.map(([v, label, n, hint]) => (
            <button key={v} type="button" role="tab" aria-selected={status === v} className={`dept__stat dept__stat--${v}${status === v ? ' is-on' : ''}`}
              onClick={() => { setStatus(status === v ? 'all' : v); setShown(PAGE); }}>
              <span>{label}</span>
              <strong className="tabular">{n}</strong>
              <small>{hint}</small>
            </button>
          ))}
        </div>
      </section>

      <section className="pg-panel" aria-label="Tiến độ theo nhóm">
        {available.length > 1 && (
          <div className="pg-tabs" role="tablist" aria-label="Xem theo">
            {available.map((l) => (
              <button key={l} type="button" role="tab" aria-selected={l === lens} className={l === lens ? 'is-on' : ''} onClick={() => setLensPick(l)}>{LENS_LABEL[l]}</button>
            ))}
          </div>
        )}
        {trail.length > 0 && (
          <nav className="pg-trail" aria-label="Phạm vi đang xem">
            <button type="button" onClick={() => go([])}>{isClass ? scopeText : 'Tất cả'}</button>
            {trail.map((s, i) => (
              <span key={s.lens}>
                <ChevronRight size={14} aria-hidden />
                <button type="button" className={i === trail.length - 1 ? 'is-last' : ''} onClick={() => go(trail.slice(0, i + 1))}>{nameOf(s.lens, s.key)}</button>
              </span>
            ))}
          </nav>
        )}
        {groups.length === 0 ? <p className="dept__none">Chưa có bài nào được giao.</p> : (
          <div className="pg-table">
            <div className="pg-trow pg-trow--head" aria-hidden>
              <span>{LENS_LABEL[lens]}</span><span>Tiến độ nộp</span><span>Nộp</span><span>Bài</span><span>Mở</span><span>Cần nhắc</span><span />
            </div>
            {groups.map((g) => {
              const body = (
                <>
                  <span className="pg-name"><strong>{g.name}</strong>{g.sub && !isClass && <small>{g.sub}</small>}</span>
                  <Meter value={g.rate} height={12} />
                  <b className={`pg-rate tabular bd-text--${toneOfRate(g.rate)}`}>{pctText(g.rate)}</b>
                  <span className="pg-num tabular">{g.total}</span>
                  <span className="pg-num tabular">{g.open || '—'}</span>
                  <span className={`pg-num tabular${g.low ? ' bd-text--low' : ''}`}>{g.low || '—'}</span>
                  <span className="pg-go">{canDrill && <ChevronRight size={18} aria-hidden />}</span>
                </>
              );
              return canDrill
                ? <button key={g.key} type="button" className="pg-trow pg-trow--link" onClick={() => go([...trail, { lens, key: g.key }])} aria-label={`Chỉ xem ${g.name}`}>{body}</button>
                : <div key={g.key} className="pg-trow">{body}</div>;
            })}
          </div>
        )}
      </section>

      <section className="pg-panel" aria-label="Danh sách bài">
        <div className="dept__tools">
          <h2>Danh sách bài</h2>
          <span className="dept__count">{list.length} bài</span>
          <label className="dept__search">
            <Search size={16} aria-hidden />
            <input value={search} onChange={(e) => { setSearch(e.target.value); setShown(PAGE); }}
              placeholder={isClass ? 'Tìm theo tên bài hoặc môn' : 'Tìm theo tên bài, lớp hoặc giáo viên'} aria-label="Tìm bài" />
          </label>
        </div>
        {d.items.length === 0 ? <p className="dept__none">Chưa có bài nào được giao.</p> : list.length === 0 ? <p className="dept__none">Không có bài nào khớp.</p> : (
          <>
            <ul className="dept__list">
              {list.slice(0, shown).map((i) => {
                const pct = Math.round(rateOf(i) * 100);
                const lowRow = isLow(i);
                const hint = i.isClosed ? null : dueHint(i.dueAt, nowMs);
                return (
                  <li key={i.assignmentId}>
                    <div className={`dept__row pg-static${i.isClosed ? ' is-closed' : ''}`} style={subjectVars(i.subjectId)}>
                      <Ring pct={pct} size={54} stroke={6} tone={lowRow ? 'warn' : i.isClosed ? 'idle' : 'ok'} />
                      <span className="dept__rowMain">
                        <strong>{i.title}</strong>
                        <span className="dept__rowSub">
                          <b>{i.subjectName}</b>
                          {(!isClass || d.classes.length > 1) && <><i />lớp {i.className}</>}
                          {!isClass && <><i />{i.teacherName}</>}
                        </span>
                      </span>
                      <span className="dept__rowDue">
                        <strong className={hint ? `is-${hint.tone}` : ''}>{hint ? hint.text : 'Đã đóng'}</strong>
                        <small>{formatDateTime(i.dueAt)}</small>
                      </span>
                      <span className="dept__rowDone tabular">
                        <strong>{i.submittedCount}<small>/{i.recipientCount}</small></strong>
                        {lowRow ? <em>Nên nhắc</em> : <small>đã nộp</small>}
                      </span>
                      <ChevronRight size={18} className="dept__go" aria-hidden style={{ visibility: 'hidden' }} />
                    </div>
                  </li>
                );
              })}
            </ul>
            {list.length > shown && <button type="button" className="bd-more" onClick={() => setShown(shown + PAGE)}>Xem thêm {Math.min(PAGE, list.length - shown)} bài</button>}
          </>
        )}
      </section>
    </div>
  );
}
