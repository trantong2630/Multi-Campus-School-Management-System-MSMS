import dayjs from 'dayjs';
import { useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import { ATTENDANCE_ALERT, OUTCOME, rateTone } from './dashboardFormat';
import type { RateTone, Segment } from './dashboardFormat';
import './board.css';

/**
 * Bộ thành phần của các màn Tình hình: thẻ số pastel, biểu đồ đường 8 tuần, cột theo thứ, cột chồng theo tuần, thanh cơ cấu,
 * thanh tỷ lệ, khối tổng quan và danh sách top 5. Màu đường theo slot cố định của đối tượng; màu trạng thái chỉ cho trạng thái và luôn kèm chữ.
 */

export type Tone = RateTone;
export const toneOfRate = rateTone;

/** Số đếm lên khi dữ liệu về; tôn trọng người dùng tắt chuyển động. */
export function useCountUp(target: number, ms = 650) {
  const [v, setV] = useState(target);
  useEffect(() => {
    if (window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) { setV(target); return; }
    const t0 = performance.now();
    let raf = 0;
    const tick = (now: number) => {
      const p = Math.min(1, (now - t0) / ms);
      setV(target * (1 - Math.pow(1 - p, 3)));
      if (p < 1) raf = requestAnimationFrame(tick);
    };
    raf = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(raf);
  }, [target, ms]);
  return v;
}

export function Count({ value, digits = 0, suffix }: { value: number | null | undefined; digits?: number; suffix?: string }) {
  const v = useCountUp(value ?? 0);
  if (value === null || value === undefined) return <>—</>;
  return <>{v.toLocaleString('vi-VN', { minimumFractionDigits: digits, maximumFractionDigits: digits })}{suffix}</>;
}

/** Thanh ngang một màu theo ngưỡng; dài ra khi hiện. */
export function Meter({ value, tone, height = 8 }: { value: number | null | undefined; tone?: Tone; height?: number }) {
  const t = tone ?? toneOfRate(value);
  return (
    <span className="bd-meter" style={{ height }} aria-hidden>
      <i className={`bd-tone--${t}`} style={{ width: `${Math.min(100, Math.max(value ?? 0, value ? 2 : 0))}%` }} />
    </span>
  );
}

/** Nhóm nút chọn một (chọn lớp của chủ nhiệm). */
export function Seg<T extends string | number>({ options, value, onChange, label }: {
  options: ReadonlyArray<{ value: T; label: string }>; value: T; onChange: (v: T) => void; label: string;
}) {
  return (
    <div className="bd-seg" role="tablist" aria-label={label}>
      {options.map((o) => (
        <button key={String(o.value)} type="button" role="tab" aria-selected={o.value === value} className={o.value === value ? 'is-on' : ''} onClick={() => onChange(o.value)}>
          {o.label}
        </button>
      ))}
    </div>
  );
}

export function Empty({ children }: { children: ReactNode }) {
  return <p className="bd-empty">{children}</p>;
}

// ---------- Thẻ số ----------

/** Mức thay đổi so với 4 tuần trước; ẩn hẳn khi chưa có kỳ trước. higherIsBetter quyết định màu. */
export function Delta({ value, previous, higherIsBetter, unit = 'pt' }: { value: number | null; previous: number | null; higherIsBetter: boolean; unit?: 'pt' | '' }) {
  if (value === null || previous === null) return null;
  const d = Math.round((value - previous) * 10) / 10;
  if (d === 0) return <span className="bd-delta">= 4 tuần trước</span>;
  const good = d > 0 === higherIsBetter;
  return (
    <span className={`bd-delta ${good ? 'bd-delta--good' : 'bd-delta--bad'}`}>
      {d > 0 ? '▲' : '▼'} {Math.abs(d).toLocaleString('vi-VN')}{unit === 'pt' ? ' điểm' : ''} <em>so 4 tuần trước</em>
    </span>
  );
}

/** Đường xu hướng mini trong thẻ: chỉ vẽ khi có từ 3 tuần dữ liệu. */
function Mini({ values }: { values: (number | null)[] }) {
  const known = values.map((v, i) => [i, v] as const).filter((p): p is readonly [number, number] => p[1] !== null);
  if (known.length < 3) return null;
  const W = 84, H = 30, pad = 3;
  const lo = Math.min(...known.map((p) => p[1])), hi = Math.max(...known.map((p) => p[1]));
  const x = (i: number) => pad + ((W - 2 * pad) * i) / Math.max(1, values.length - 1);
  const y = (v: number) => H - pad - ((H - 2 * pad) * (v - lo)) / (hi - lo || 1);
  const d = known.map(([i, v], k) => `${k ? 'L' : 'M'}${x(i).toFixed(1)} ${y(v).toFixed(1)}`).join(' ');
  const [li, lv] = known[known.length - 1];
  return (
    <svg className="bd-tile__mini" width={W} height={H} viewBox={`0 0 ${W} ${H}`} aria-hidden>
      <path d={d} />
      <circle cx={x(li)} cy={y(lv)} r={2.6} />
    </svg>
  );
}

export type Hue = 'mint' | 'sky' | 'sun' | 'lilac' | 'peach' | 'aqua';

/**
 * Thẻ số pastel: nhãn, số lớn, đường mini 8 tuần, một dòng so sánh hoặc ngữ cảnh.
 * alert là việc tồn đọng gắn với chỉ số này (chấm đỏ kèm chữ), thay cho một ô "Cần xử lý" riêng.
 */
export function Tile({ label, value, unit, foot, hue, trend, alert, onClick }: {
  label: string; value: ReactNode; unit?: string; foot?: ReactNode; hue: Hue; trend?: (number | null)[];
  alert?: { text: string; onClick?: () => void } | null; onClick?: () => void;
}) {
  const body = (
    <>
      <span className="bd-tile__label">{label}</span>
      <span className="bd-tile__row">
        <b className="bd-tile__value tabular">{value}{unit && <small>{unit}</small>}</b>
        {trend && <Mini values={trend} />}
      </span>
      <span className="bd-tile__foot">{foot}</span>
    </>
  );
  return (
    <div className={`bd-tile bd-tile--${hue}`}>
      {onClick ? <button type="button" className="bd-tile__body" onClick={onClick}>{body}</button> : <div className="bd-tile__body">{body}</div>}
      {alert && (alert.onClick
        ? <button type="button" className="bd-tile__alert" onClick={alert.onClick}><i aria-hidden />{alert.text}<em aria-hidden>›</em></button>
        : <span className="bd-tile__alert"><i aria-hidden />{alert.text}</span>)}
    </div>
  );
}

// ---------- Khung khối ----------

export function Panel({ title, meta, children, className }: { title: string; meta?: ReactNode; children: ReactNode; className?: string }) {
  return (
    <section className={`bd-panel${className ? ` ${className}` : ''}`} aria-label={title}>
      <div className="bd-panel__title">
        <h2>{title}</h2>
        {meta && <span className="bd-panel__meta">{meta}</span>}
      </div>
      {children}
    </section>
  );
}

/** Nhãn nhóm khối (Chuyên cần, Giảng dạy, Rèn luyện…). */
export function Section({ title, children }: { title: string; children?: ReactNode }) {
  return <h2 className="bd-section">{title}{children}</h2>;
}

// ---------- Biểu đồ ----------

export interface LineSeries { key: string; name: string; values: (number | null)[]; slot: number; dashed?: boolean }

/**
 * Biểu đồ đường 8 tuần (0–100%): mỗi đường một màu theo slot cố định của đối tượng, nhãn ở cuối đường khi không quá 4 đường,
 * vạch đứt san hô ở ngưỡng. Rê chuột lên một tuần hiện giá trị mọi đường. Trục dọc bắt đầu gần giá trị thấp nhất.
 */
export function LineChart({ labels, series, threshold, height = 230, unit = '%' }: {
  labels: string[]; series: LineSeries[]; threshold?: number; height?: number; unit?: string;
}) {
  const [hover, setHover] = useState<number | null>(null);
  const known = series.flatMap((s) => s.values).filter((v): v is number => v !== null);
  if (known.length === 0) return <Empty>Chưa có dữ liệu trong 8 tuần qua.</Empty>;
  const multi = series.length > 1;
  const W = 640, H = height, left = 40, right = multi && series.length <= 4 ? 110 : 44, top = 12, bottom = H - 26;
  const lo = Math.max(0, Math.floor((Math.min(threshold ?? 100, ...known) - 5) / 10) * 10);
  const hi = 100;
  const x = (i: number) => left + ((W - left - right) * i) / Math.max(1, labels.length - 1);
  const y = (v: number) => bottom - ((bottom - top) * (v - lo)) / (hi - lo || 1);
  const ticks = [lo, Math.round((lo + hi) / 2), hi];
  const path = (vals: (number | null)[]) => {
    let d = '', pen = false;
    vals.forEach((v, i) => { if (v === null) { pen = false; return; } d += `${pen ? 'L' : 'M'}${x(i).toFixed(1)} ${y(v).toFixed(1)} `; pen = true; });
    return d;
  };
  const ends = series
    .map((s) => { const i = s.values.map((v, k) => (v === null ? -1 : k)).filter((k) => k >= 0).pop(); return i === undefined ? null : { s, i, v: s.values[i] as number }; })
    .filter((e): e is { s: LineSeries; i: number; v: number } => e !== null)
    .sort((a, b) => a.v - b.v);
  const labelY: Record<string, number> = {};
  let floor = Infinity;
  ends.forEach((e) => { const yy = Math.min(y(e.v), floor - 14); labelY[e.s.key] = yy; floor = yy; });
  const step = (W - left - right) / Math.max(1, labels.length - 1);

  return (
    <div className="bd-line">
      {multi && (
        <ul className="bd-legend">
          {series.map((s) => <li key={s.key}><i className={`bd-key bd-s${s.slot}${s.dashed ? ' bd-key--dash' : ''}`} />{s.name}</li>)}
        </ul>
      )}
      <div className="bd-line__plot">
        <svg viewBox={`0 0 ${W} ${H}`} role="img" aria-label={`${series.map((s) => s.name).join(', ')} theo tuần`} onMouseLeave={() => setHover(null)}>
          {ticks.map((t) => (
            <g key={t}>
              <line x1={left} x2={W - right} y1={y(t)} y2={y(t)} className="bd-line__grid" />
              <text x={left - 8} y={y(t) + 4} textAnchor="end" className="bd-line__tick">{t}{unit}</text>
            </g>
          ))}
          {threshold !== undefined && threshold > lo && (
            <g>
              <line x1={left} x2={W - right} y1={y(threshold)} y2={y(threshold)} className="bd-line__limit" />
              <text x={left + 4} y={y(threshold) - 5} className="bd-line__limitText">Ngưỡng {threshold}{unit}</text>
            </g>
          )}
          {labels.map((l, i) => <text key={l + i} x={x(i)} y={H - 6} textAnchor="middle" className="bd-line__tick">{l}</text>)}
          {hover !== null && <line x1={x(hover)} x2={x(hover)} y1={top} y2={bottom} className="bd-line__cross" />}
          {series.map((s) => (
            <g key={s.key} className={`bd-s${s.slot}`}>
              <path d={path(s.values)} className={`bd-line__path${s.dashed ? ' is-dashed' : ''}`} />
              {s.values.map((v, i) => v !== null && (!s.dashed || hover === i) && (
                <circle key={i} cx={x(i)} cy={y(v)} r={hover === i ? 5 : 3.5} className="bd-line__dot" />
              ))}
            </g>
          ))}
          {series.length <= 4 && ends.map((e) => (
            <text key={e.s.key} x={x(e.i) + 9} y={(multi ? labelY[e.s.key] : y(e.v)) + 4} className="bd-line__end">
              <tspan className="bd-line__endValue">{e.v.toLocaleString('vi-VN')}{unit}</tspan>{multi ? ` ${e.s.name}` : ''}
            </text>
          ))}
          {labels.map((l, i) => (
            <rect key={`h${l}${i}`} x={x(i) - step / 2} y={top} width={step} height={bottom - top} fill="transparent" onMouseEnter={() => setHover(i)} />
          ))}
        </svg>
        {hover !== null && (
          <div className={`bd-tip${hover > labels.length / 2 ? ' bd-tip--left' : ''}`} style={{ left: `${(x(hover) / W) * 100}%` }} role="status">
            <strong>{labels[hover]}</strong>
            {series.map((s) => (
              <span key={s.key}><i className={`bd-key bd-s${s.slot}${s.dashed ? ' bd-key--dash' : ''}`} />{s.name}
                <b className="tabular">{s.values[hover] === null ? '—' : `${(s.values[hover] as number).toLocaleString('vi-VN')}${unit}`}</b></span>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

/** Cột theo thứ trong tuần: cột dưới ngưỡng tô san hô, số in trên đầu cột. */
export function WeekdayBars({ values, threshold = ATTENDANCE_ALERT, height = 170 }: { values: (number | null)[]; threshold?: number; height?: number }) {
  const known = values.filter((v): v is number => v !== null);
  if (known.length === 0) return <Empty>Chưa có điểm danh trong 4 tuần qua.</Empty>;
  const lo = Math.max(0, Math.floor((Math.min(threshold, ...known) - 5) / 10) * 10);
  const h = (v: number) => `${((v - lo) / (100 - lo)) * 100}%`;
  return (
    <div className="bd-wd">
      <div className="bd-wd__plot" style={{ height }}>
        <i className="bd-wd__limit" style={{ bottom: h(threshold) }} title={`Ngưỡng ${threshold}%`} />
        {values.map((v, i) => (
          <div key={i} className="bd-wd__col" title={v === null ? 'Chưa có điểm danh' : `${v}%`}>
            <span className={`bd-wd__bar${v !== null && v < threshold ? ' is-low' : ''}`} style={{ height: v === null ? 0 : h(v) }}>
              <b className="tabular">{v === null ? '—' : Math.round(v)}</b>
            </span>
          </div>
        ))}
      </div>
      <div className="bd-wd__labels">{['T2', 'T3', 'T4', 'T5', 'T6', 'T7'].map((d) => <span key={d}>{d}</span>)}</div>
    </div>
  );
}

export interface AbsenceWeekItem { weekStart: string; teachers: number; substituted: number; cancelled: number; pending: number }

/** Cột chồng theo tuần: tiết dạy thay, hủy, chưa xử lý; số giáo viên vắng ghi trên đầu cột. */
export function WeekStack({ labels, weeks, height = 170 }: { labels: string[]; weeks: AbsenceWeekItem[]; height?: number }) {
  const total = (w: AbsenceWeekItem) => w.substituted + w.cancelled + w.pending;
  const max = Math.max(1, ...weeks.map(total));
  if (weeks.every((w) => total(w) === 0)) return <Empty>8 tuần qua không có tiết nào bị ảnh hưởng.</Empty>;
  return (
    <div className="bd-ws">
      <div className="bd-ws__plot" style={{ height }}>
        {weeks.map((w, i) => (
          <div key={w.weekStart} className="bd-ws__col" title={`${labels[i]}: ${w.teachers} giáo viên vắng · ${w.substituted} dạy thay · ${w.cancelled} hủy · ${w.pending} chưa xử lý`}>
            {total(w) > 0 && <b className="tabular">{w.teachers} GV</b>}
            <span className="bd-ws__stack" style={{ height: `${(total(w) / max) * 100}%` }}>
              {w.pending > 0 && <i className="bd-tone--low" style={{ flexGrow: w.pending }} />}
              {w.cancelled > 0 && <i className="bd-tone--warn" style={{ flexGrow: w.cancelled }} />}
              {w.substituted > 0 && <i className="bd-tone--good" style={{ flexGrow: w.substituted }} />}
            </span>
          </div>
        ))}
      </div>
      <div className="bd-ws__labels">{labels.map((l) => <span key={l}>{l.replace('Tuần ', 'T')}</span>)}</div>
      <ul className="bd-legend bd-legend--center">
        <li><i className="bd-dot bd-tone--good" />Dạy thay</li><li><i className="bd-dot bd-tone--warn" />Hủy</li><li><i className="bd-dot bd-tone--low" />Chưa xử lý</li>
      </ul>
    </div>
  );
}


/** Thanh cơ cấu: một tổng, một thanh chia phần, chú giải kèm số và tỷ lệ. */
export function SegmentBar({ segments, total, unit, empty }: { segments: Segment[]; total?: ReactNode; unit?: string; empty: string }) {
  const sum = segments.reduce((n, s) => n + s.value, 0);
  if (sum === 0) return <Empty>{empty}</Empty>;
  return (
    <div className="bd-sb">
      <b className="bd-sb__total tabular">{total ?? sum.toLocaleString('vi-VN')}{unit && <small> {unit}</small>}</b>
      <span className="bd-sb__bar" role="img" aria-label={segments.map((s) => `${s.label} ${s.value}`).join(', ')}>
        {segments.filter((s) => s.value > 0).map((s) => <i key={s.key} className={`bd-tone--${s.tone}`} style={{ flexGrow: s.value }} />)}
      </span>
      <ul className="bd-sb__legend">
        {segments.map((s) => (
          <li key={s.key}>
            <i className={`bd-tone--${s.tone}`} />
            <span>{s.label}</span>
            <b className="tabular">{s.value.toLocaleString('vi-VN')}</b>
            <small className="tabular">{Math.round((s.value / sum) * 100)}%</small>
          </li>
        ))}
      </ul>
    </div>
  );
}

/** Danh sách tỷ lệ: mỗi hàng một tên, một thanh có vạch ngưỡng, một số. */
export function MeterList({ items, threshold }: { items: { key: string; label: string; sub?: string; value: number | null }[]; threshold: number }) {
  return (
    <ul className="bd-mlist">
      {items.map((it) => (
        <li key={it.key}>
          <span className="bd-mlist__name"><strong>{it.label}</strong>{it.sub && <small>{it.sub}</small>}</span>
          <span className="bd-mlist__track">
            <Meter value={it.value} tone={it.value === null ? 'none' : it.value < threshold ? 'low' : 'good'} height={8} />
            <i style={{ left: `${threshold}%` }} />
          </span>
          <b className={`tabular${it.value !== null && it.value < threshold ? ' bd-text--low' : ''}`}>{it.value === null ? '—' : `${it.value.toLocaleString('vi-VN')}%`}</b>
        </li>
      ))}
    </ul>
  );
}

/** Khối tổng quan: lưới 2×2 các con số nhỏ có nhãn. */
export function Figures({ items }: { items: { key: string; label: string; value: number; tone?: 'warn' | 'low'; onClick?: () => void }[] }) {
  return (
    <ul className="bd-figs">
      {items.map((f) => {
        const inner = <><b className={`tabular${f.tone && f.value ? ` bd-text--${f.tone}` : ''}`}><Count value={f.value} /></b><span>{f.label}</span></>;
        return <li key={f.key}>{f.onClick ? <button type="button" onClick={f.onClick}>{inner}</button> : <div>{inner}</div>}</li>;
      })}
    </ul>
  );
}

export function OutcomeTag({ outcome }: { outcome: string }) {
  const o = OUTCOME[outcome] ?? { label: outcome, tone: 'warn' as const };
  return <em className={`bd-tag bd-tag--${o.tone}`}>{o.label}</em>;
}

// ---------- Thống kê hệ thống ----------

export interface TrendPoint { weekStart: string; count: number; rate: number | null; rate2?: number | null }

/** Xu hướng dạng cột (số lượng theo ngày): dùng ở Thống kê hệ thống. */
export function Trend({ points, countLabel, height = 200, quiet }: { points: TrendPoint[]; countLabel?: string; height?: number; quiet?: boolean }) {
  const W = 560, H = height, left = 38, right = 14, base = H - 28, top = 16;
  const maxCount = Math.max(1, ...points.map((p) => p.count));
  const step = (W - left - right) / points.length;
  const bw = Math.min(30, step * 0.5);
  const x = (i: number) => left + step * i + step / 2;
  return (
    <figure className="bd-trend">
      <figcaption className="bd-legend" hidden={quiet}>{countLabel && <span><i className="bd-legend__bar" />{countLabel}</span>}</figcaption>
      <svg viewBox={`0 0 ${W} ${H}`} role="img" aria-label={`${countLabel ?? 'Số lượng'} theo ngày`}>
        <line x1={left} x2={W - right} y1={base} y2={base} className="bd-trend__axis" />
        <line x1={left} x2={W - right} y1={top} y2={top} className="bd-trend__grid" />
        <text x={left - 8} y={top + 4} textAnchor="end" className="bd-trend__tick">{maxCount}</text>
        {points.map((p, i) => {
          const h = (p.count / maxCount) * (base - top);
          return (
            <g key={p.weekStart}>
              <rect x={x(i) - bw / 2} y={base - h} width={bw} height={h} rx={4} className="bd-trend__bar" style={{ animationDelay: `${i * 40}ms` }}>
                <title>{`${dayjs(p.weekStart).format('DD/MM')}: ${p.count}`}</title>
              </rect>
              {p.count > 0 && <text x={x(i)} y={base - h - 6} textAnchor="middle" className="bd-trend__value">{p.count}</text>}
              <text x={x(i)} y={H - 8} textAnchor="middle" className="bd-trend__tick">{i === points.length - 1 ? 'Nay' : dayjs(p.weekStart).format('DD/MM')}</text>
            </g>
          );
        })}
      </svg>
    </figure>
  );
}

export interface BarItem { key: string; label: string; sub?: string; value: number; display?: string; tone?: Tone; onClick?: () => void }

/** Thanh ngang xếp hạng (tải dạy, Thống kê hệ thống); mark là vạch dọc mảnh, vd. trung bình. */
export function HBars({ items, max, height = 10, mark }: { items: BarItem[]; max?: number; height?: number; mark?: { value: number; label: string } }) {
  const top = max ?? Math.max(1, ...items.map((i) => i.value), mark?.value ?? 0);
  return (
    <ul className="bd-bars bd-bars--wide">
      {items.map((it) => {
        const body = (
          <>
            <span className="bd-bars__name"><strong>{it.label}</strong>{it.sub && <span>{it.sub}</span>}</span>
            <span className="bd-bars__track">
              <Meter value={(it.value / top) * 100} tone={it.tone ?? 'good'} height={height} />
              {mark && <i style={{ left: `${(mark.value / top) * 100}%` }} title={mark.label} />}
            </span>
            <b className="tabular">{it.display ?? it.value.toLocaleString('vi-VN')}</b>
          </>
        );
        return <li key={it.key}>{it.onClick ? <button type="button" className="bd-bars__btn" onClick={it.onClick}>{body}</button> : body}</li>;
      })}
    </ul>
  );
}
