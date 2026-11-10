import dayjs from 'dayjs';
import { useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import { rateTone } from './dashboardFormat';
import type { RateTone } from './dashboardFormat';
import './board.css';

/** Bộ thành phần chung của các màn Bảng điều khiển: chỉ chữ, số, thanh, vòng và đường xu hướng. */

export type Tone = RateTone;
export const toneOfRate = rateTone;

/** Số đếm lên khi dữ liệu về; tôn trọng người dùng tắt chuyển động. */
export function useCountUp(target: number, ms = 650) {
  const [v, setV] = useState(target);
  useEffect(() => {
    if (window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) { setV(target); return; }
    const from = 0;
    const t0 = performance.now();
    let raf = 0;
    const tick = (now: number) => {
      const p = Math.min(1, (now - t0) / ms);
      setV(from + (target - from) * (1 - Math.pow(1 - p, 3)));
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

export function Ring({ value, size = 120, stroke = 12, tone, caption }: { value: number | null; size?: number; stroke?: number; tone?: Tone; caption?: string }) {
  const t = tone ?? toneOfRate(value);
  const r = (size - stroke) / 2;
  const c = 2 * Math.PI * r;
  const shown = useCountUp(value ?? 0);
  return (
    <span className={`bd-ring bd-ring--${t}`} style={{ width: size, height: size }} role="img" aria-label={value === null ? 'Chưa có số liệu' : `${value}%`}>
      <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`} aria-hidden>
        <g transform={`rotate(-90 ${size / 2} ${size / 2})`} fill="none" strokeWidth={stroke} strokeLinecap="round">
          <circle className="bd-ring__bg" cx={size / 2} cy={size / 2} r={r} />
          {!!value && <circle className="bd-ring__fg" cx={size / 2} cy={size / 2} r={r} strokeDasharray={`${(c * Math.min(shown, 100)) / 100} ${c}`} />}
        </g>
      </svg>
      <span className="bd-ring__text">
        <b className="tabular">{value === null ? '—' : <>{Math.round(shown)}<small>%</small></>}</b>
        {caption && <em>{caption}</em>}
      </span>
    </span>
  );
}

/** Nhóm nút chọn một: dùng cho đổi góc nhìn và đổi phạm vi. */
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

export interface TrendPoint { weekStart: string; count: number; rate: number | null; rate2?: number | null }

/**
 * Xu hướng theo tuần: cột là số lượng, đường mực là tỷ lệ (0–100), đường xanh đậm là tỷ lệ thứ hai (vd. chuyên cần).
 * Có lưới 0/50/100 và nhãn giá trị cuối đường để đọc được mà không cần rê chuột.
 */
export function Trend({ points, countLabel, rateLabel, rate2Label, height = 200, quiet }: {
  points: TrendPoint[]; countLabel?: string; rateLabel?: string; rate2Label?: string; height?: number;
  /** Ẩn chú giải: dùng khi tiêu đề của khối đã nói rõ đường là gì. */
  quiet?: boolean;
}) {
  const W = 560;
  const H = height;
  const left = 38;
  const right = 14;
  const base = H - 28;
  const top = 16;
  const maxCount = Math.max(1, ...points.map((p) => p.count));
  const step = (W - left - right) / points.length;
  const bw = Math.min(30, step * 0.5);
  const x = (i: number) => left + step * i + step / 2;
  const y = (r: number) => base - ((base - top) * r) / 100;
  const line = (pick: (p: TrendPoint) => number | null | undefined) => {
    const pts = points.map((p, i) => { const v = pick(p); return v === null || v === undefined ? null : ([x(i), y(v), v] as const); });
    const path = pts.reduce<string>((acc, pt) => (pt ? `${acc}${acc ? 'L' : 'M'}${pt[0]} ${pt[1]} ` : acc), '');
    const last = [...pts].reverse().find(Boolean) ?? null;
    return { pts, path, last };
  };
  const l1 = line((p) => p.rate);
  const l2 = line((p) => p.rate2);
  const showPct = !!(rateLabel || rate2Label);
  return (
    <figure className="bd-trend">
      <figcaption className="bd-legend" hidden={quiet}>
        {countLabel && <span><i className="bd-legend__bar" />{countLabel}</span>}
        {rateLabel && <span><i className="bd-legend__line" />{rateLabel}</span>}
        {rate2Label && <span><i className="bd-legend__line bd-legend__line--2" />{rate2Label}</span>}
      </figcaption>
      <svg viewBox={`0 0 ${W} ${H}`} role="img" aria-label={[countLabel, rateLabel, rate2Label].filter(Boolean).join(', ') + ' theo tuần'}>
        {(showPct ? [0, 50, 100] : [0]).map((g) => (
          <g key={g}>
            <line x1={left} x2={W - right} y1={y(g)} y2={y(g)} className={g === 0 ? 'bd-trend__axis' : 'bd-trend__grid'} />
            {showPct && <text x={left - 8} y={y(g) + 4} textAnchor="end" className="bd-trend__tick">{g}%</text>}
          </g>
        ))}
        {!showPct && countLabel && (
          <g>
            <line x1={left} x2={W - right} y1={top} y2={top} className="bd-trend__grid" />
            <text x={left - 8} y={top + 4} textAnchor="end" className="bd-trend__tick">{maxCount}</text>
          </g>
        )}
        {points.map((p, i) => {
          const h = countLabel ? (p.count / maxCount) * (base - top) : 0;
          return (
            <g key={p.weekStart}>
              {countLabel && (
                <rect x={x(i) - bw / 2} y={base - h} width={bw} height={h} rx={6} className="bd-trend__bar" style={{ animationDelay: `${i * 40}ms` }}>
                  <title>{`Tuần ${dayjs(p.weekStart).format('DD/MM')}: ${p.count}${p.rate === null ? '' : ` · ${p.rate}%`}${p.rate2 === null || p.rate2 === undefined ? '' : ` · ${p.rate2}%`}`}</title>
                </rect>
              )}
              {countLabel && !showPct && p.count > 0 && <text x={x(i)} y={base - h - 6} textAnchor="middle" className="bd-trend__value">{p.count}</text>}
              <text x={x(i)} y={H - 8} textAnchor="middle" className="bd-trend__tick">{i === points.length - 1 ? 'Nay' : dayjs(p.weekStart).format('DD/MM')}</text>
            </g>
          );
        })}
        {l1.path && <path d={l1.path} className="bd-trend__line" pathLength={1} />}
        {l2.path && <path d={l2.path} className="bd-trend__line bd-trend__line--2" pathLength={1} />}
        {l1.pts.map((pt, i) => pt && <circle key={`a${points[i].weekStart}`} cx={pt[0]} cy={pt[1]} r={3.5} className="bd-trend__dot" />)}
        {l2.pts.map((pt, i) => pt && <circle key={`b${points[i].weekStart}`} cx={pt[0]} cy={pt[1]} r={3.5} className="bd-trend__dot bd-trend__dot--2" />)}
        {l1.last && <text x={l1.last[0]} y={l1.last[1] + (l2.last && l2.last[2] > l1.last[2] ? 20 : -10)} textAnchor="middle" className="bd-trend__value">{l1.last[2]}%</text>}
        {l2.last && <text x={l2.last[0]} y={l2.last[1] + (l1.last && l1.last[2] >= l2.last[2] ? 20 : -10)} textAnchor="middle" className="bd-trend__value bd-trend__value--2">{l2.last[2]}%</text>}
      </svg>
    </figure>
  );
}

export interface StackSegment { key: string; label: string; value: number; tone: 'good' | 'soft' | 'warn' | 'low' | 'ink' | 'mint' }

/** Một thanh chia đoạn theo tỷ lệ, kèm chú giải có số: dùng cho cơ cấu (điểm danh, học sinh theo khối). */
export function StackBar({ segments, height = 14 }: { segments: StackSegment[]; height?: number }) {
  const total = segments.reduce((n, s) => n + s.value, 0);
  return (
    <div className="bd-stack">
      <span className="bd-stack__bar" style={{ height }} role="img" aria-label={segments.map((s) => `${s.label} ${s.value}`).join(', ')}>
        {total > 0 && segments.filter((s) => s.value > 0).map((s) => (
          <i key={s.key} className={`bd-stack--${s.tone}`} style={{ flexGrow: s.value }} title={`${s.label}: ${s.value}`} />
        ))}
      </span>
      <ul className="bd-stack__legend">
        {segments.map((s) => <li key={s.key}><i className={`bd-stack--${s.tone}`} />{s.label}<b className="tabular">{s.value.toLocaleString('vi-VN')}</b></li>)}
      </ul>
    </div>
  );
}

/** Một số lớn có nhãn: dùng thành hàng chỉ số đầu màn. */
export function Figure({ label, value, hint, tone, onClick }: { label: string; value: ReactNode; hint?: ReactNode; tone?: 'warn' | 'low'; onClick?: () => void }) {
  const body = (
    <>
      <span className="bd-figure__label">{label}</span>
      <strong className={`bd-figure__value tabular${tone ? ` bd-figure__value--${tone}` : ''}`}>{value}</strong>
      {hint && <small className="bd-figure__hint">{hint}</small>}
    </>
  );
  return onClick
    ? <button type="button" className="bd-figure bd-figure--link" onClick={onClick}>{body}</button>
    : <div className="bd-figure">{body}</div>;
}

/** Chip "tăng/giảm bao nhiêu điểm so với tuần trước"; chưa đủ dữ liệu thì nói rõ, không để trống. */
export function Compare({ delta }: { delta: number | null }) {
  if (delta === null) return <span className="bd-compare">Chưa đủ dữ liệu để so với tuần trước</span>;
  const text = delta === 0 ? 'Bằng tuần trước' : `${delta > 0 ? 'Tăng' : 'Giảm'} ${Math.abs(delta).toLocaleString('vi-VN')} điểm so với tuần trước`;
  return <span className={`bd-compare${delta > 0 ? ' bd-compare--up' : delta < 0 ? ' bd-compare--down' : ''}`}>{text}</span>;
}

export function Empty({ children }: { children: ReactNode }) {
  return <p className="bd-empty">{children}</p>;
}

export interface ColumnItem { key: string; label: string; value: number | null; sub?: string }

/** Biểu đồ cột đứng cho tỷ lệ 0–100: màu theo ngưỡng, số ở đầu cột, nhãn ở chân; không trục, không chú giải. */
export function Columns({ items, height = 150 }: { items: ColumnItem[]; height?: number }) {
  return (
    <ul className="bd-cols" style={{ ['--h' as string]: `${height}px` }}>
      {items.map((it, i) => (
        <li key={it.key}>
          <b className={`tabular bd-text--${toneOfRate(it.value)}`}>{it.value === null ? '—' : Math.round(it.value)}</b>
          <span className="bd-cols__track" aria-hidden>
            <i className={`bd-tone--${toneOfRate(it.value)}`} style={{ height: `${it.value ?? 0}%`, animationDelay: `${i * 50}ms` }} />
          </span>
          <em>{it.label}</em>
          {it.sub && <small>{it.sub}</small>}
        </li>
      ))}
    </ul>
  );
}

/** Thẻ số màu dịu: nhãn trên, số dưới, một dòng phụ tùy chọn. Bấm được khi có onClick. */
export function Tile({ label, value, hint, hue = 'mint', tone, onClick }: { label: string; value: ReactNode; hint?: ReactNode; hue?: 'mint' | 'sky' | 'sun' | 'lilac'; tone?: 'warn' | 'low'; onClick?: () => void }) {
  const inner = (
    <>
      <span>{label}</span>
      <b className={`tabular${tone ? ` bd-text--${tone}` : ''}`}>{value}</b>
      {hint && <small>{hint}</small>}
    </>
  );
  return onClick
    ? <button type="button" className={`bd-tile bd-tile--${hue}`} onClick={onClick}>{inner}</button>
    : <div className={`bd-tile bd-tile--${hue}`}>{inner}</div>;
}

/** Tên rút gọn thành chữ cái đầu để làm hình đại diện. */
export function initials(name: string) {
  const parts = name.trim().split(/\s+/);
  return (parts.length > 1 ? parts[parts.length - 2][0] + parts[parts.length - 1][0] : parts[0]?.slice(0, 2) ?? '').toUpperCase();
}

export interface DonutSegment { key: string; label: string; value: number; tone: StackSegment['tone'] }

/** Biểu đồ tròn rỗng: tổng ở giữa, chú giải có số và phần trăm bên cạnh. */
export function Donut({ segments, total, caption, size = 168, stroke = 26 }: { segments: DonutSegment[]; total?: number; caption: string; size?: number; stroke?: number }) {
  const sum = segments.reduce((n, s) => n + s.value, 0);
  const shown = useCountUp(total ?? sum);
  const r = (size - stroke) / 2;
  const c = 2 * Math.PI * r;
  const gap = segments.filter((s) => s.value > 0).length > 1 ? 3 : 0;
  let offset = 0;
  return (
    <div className="bd-donut">
      <span className="bd-donut__chart" style={{ width: size, height: size }}>
        <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`} role="img" aria-label={segments.map((s) => `${s.label} ${s.value}`).join(', ')}>
          <g transform={`rotate(-90 ${size / 2} ${size / 2})`} fill="none" strokeWidth={stroke}>
            <circle className="bd-donut__bg" cx={size / 2} cy={size / 2} r={r} />
            {sum > 0 && segments.filter((s) => s.value > 0).map((s) => {
              const len = (s.value / sum) * c;
              const el = <circle key={s.key} className={`bd-donut__seg bd-donut__seg--${s.tone}`} cx={size / 2} cy={size / 2} r={r}
                strokeDasharray={`${Math.max(len - gap, 1)} ${c}`} strokeDashoffset={-offset} />;
              offset += len;
              return el;
            })}
          </g>
        </svg>
        <span className="bd-donut__text"><b className="tabular">{Math.round(shown).toLocaleString('vi-VN')}</b><em>{caption}</em></span>
      </span>
      <ul className="bd-donut__legend">
        {segments.map((s) => (
          <li key={s.key}>
            <i className={`bd-stack--${s.tone}`} />
            <span>{s.label}</span>
            <b className="tabular">{s.value.toLocaleString('vi-VN')}</b>
            <small className="tabular">{sum ? `${Math.round((s.value / sum) * 100)}%` : '—'}</small>
          </li>
        ))}
      </ul>
    </div>
  );
}

export interface BarItem { key: string; label: string; sub?: string; value: number; display?: string; tone?: Tone; onClick?: () => void }

/** Thanh ngang xếp hạng: nhãn, thanh dài theo giá trị lớn nhất, số ở cuối. */
export function HBars({ items, max, height = 12 }: { items: BarItem[]; max?: number; height?: number }) {
  const top = max ?? Math.max(1, ...items.map((i) => i.value));
  return (
    <ul className="bd-bars bd-bars--wide">
      {items.map((it) => {
        const body = (
          <>
            <span className="bd-bars__name"><strong>{it.label}</strong>{it.sub && <span>{it.sub}</span>}</span>
            <Meter value={(it.value / top) * 100} tone={it.tone ?? 'good'} height={height} />
            <b className="tabular">{it.display ?? it.value.toLocaleString('vi-VN')}</b>
          </>
        );
        return <li key={it.key}>{it.onClick ? <button type="button" className="bd-bars__btn" onClick={it.onClick}>{body}</button> : body}</li>;
      })}
    </ul>
  );
}
