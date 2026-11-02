import dayjs from 'dayjs';
import { ArrowRight } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import {
  Bar, BarChart, CartesianGrid, Cell, ComposedChart, LabelList, Line, ResponsiveContainer, Tooltip, XAxis, YAxis,
} from 'recharts';
import { accent, color } from '@/shared/lib/tokens';
import type { AccentName } from '@/shared/lib/tokens';
import type { GradeCount, SubjectRate, WeekPoint } from '../types';
import { fmt, pct, rateTone } from './dashboardFormat';
import type { RateTone } from './dashboardFormat';

const toneColor: Record<RateTone, string> = { good: color.green600, warn: color.amberFg, low: color.coralFg, none: color.disabled };

/** Thanh tỷ lệ mảnh + số, màu theo ngưỡng. Dùng trong bảng và danh sách. */
export function RateBar({ rate, width = 96 }: { rate: number | null | undefined; width?: number }) {
  const tone = rateTone(rate);
  return (
    <div className="dash-rate">
      <span className="dash-rate__track" style={{ width }}>
        <i style={{ width: `${Math.min(100, rate ?? 0)}%`, background: toneColor[tone] }} />
      </span>
      <span className={`dash-rate__value dash-rate__value--${tone} tabular`}>{pct(rate)}</span>
    </div>
  );
}

export interface AttentionItem {
  key: string;
  label: string;
  count: number;
  icon: LucideIcon;
  /** Có đường dẫn (và người dùng được mở) thì chip bấm được. */
  to?: string;
}

/** Dải "Cần chú ý": chip hổ phách khi số > 0, chip xám "ổn" khi bằng 0. */
export function AttentionStrip({ title, items }: { title: string; items: AttentionItem[] }) {
  const navigate = useNavigate();
  const pending = items.filter((i) => i.count > 0).length;
  return (
    <div className="bento-tile dash-attn dash-bento__full">
      <div className="dash-attn__head">
        <span className="dash-attn__title">{title}</span>
        <span className="dash-card__meta">{pending ? `${pending} mục cần xử lý` : 'Mọi thứ đang ổn'}</span>
      </div>
      <div className="dash-attn__chips">
        {items.map(({ key, label, count, icon: Icon, to }) => {
          const cls = ['dash-chip', count > 0 ? 'dash-chip--alert' : 'dash-chip--ok', to && 'dash-chip--link'].filter(Boolean).join(' ');
          return (
            <button key={key} type="button" className={cls} disabled={!to} onClick={() => to && navigate(to)}>
              <Icon size={16} />
              <b className="tabular">{fmt(count)}</b>
              <span>{label}</span>
              {to && <ArrowRight size={13} className="dash-chip__go" />}
            </button>
          );
        })}
      </div>
    </div>
  );
}

// Mỗi khối một tông để cột biểu đồ phân biệt được ngay, vẫn trong họ màu hệ thống.
const GRADE_TONES: AccentName[] = ['green', 'sky', 'violet', 'amber', 'teal', 'coral'];

export function GradeChart({ data, height = 280 }: { data: GradeCount[]; height?: number }) {
  if (!data.length) return <div className="dash-empty">Năm học hiện hành chưa có lớp nào.</div>;
  const rows = data.map((g) => ({ ...g, label: `Khối ${g.gradeLevel}` }));
  return (
    <ResponsiveContainer width="100%" height={height}>
      <BarChart data={rows} margin={{ top: 28, right: 8, bottom: 0, left: -16 }} barCategoryGap="35%">
        <CartesianGrid vertical={false} stroke={color.border} strokeDasharray="2 4" />
        <XAxis dataKey="label" tickLine={false} axisLine={{ stroke: color.borderStrong }} tick={{ fill: color.text2, fontSize: 12 }} />
        <YAxis allowDecimals={false} tickLine={false} axisLine={false} tick={{ fill: color.text3, fontSize: 12 }} />
        <Tooltip
          cursor={{ fill: color.sunken }}
          content={({ active, payload }) => {
            if (!active || !payload?.length) return null;
            const g = payload[0].payload as GradeCount & { label: string };
            return (
              <div className="dash-tip">
                <b>{g.label}</b>
                <span className="tabular">{fmt(g.studentCount)} học sinh</span>
                <span className="tabular">{g.classCount} lớp · TB {g.classCount ? Math.round(g.studentCount / g.classCount) : 0} HS/lớp</span>
              </div>
            );
          }}
        />
        <Bar dataKey="studentCount" radius={[8, 8, 0, 0]} maxBarSize={52}>
          {rows.map((g, i) => <Cell key={g.gradeLevel} fill={accent[GRADE_TONES[i % GRADE_TONES.length]].fg} />)}
          <LabelList dataKey="studentCount" position="top" offset={10} formatter={(v) => fmt(Number(v))}
            style={{ fill: color.ink, fontSize: 13, fontWeight: 600 }} />
        </Bar>
      </BarChart>
    </ResponsiveContainer>
  );
}

/** Xu hướng 8 tuần: cột tiết dạy đã xếp lịch, đường tỷ lệ nộp bài (trục phải). */
export function WeekChart({ weeks }: { weeks: WeekPoint[] }) {
  if (!weeks.some((w) => w.sessions || w.assignments)) return <div className="dash-empty">Chưa có tiết dạy hay bài tập trong 8 tuần gần đây.</div>;
  const rows = weeks.map((w, i) => ({ ...w, label: i === weeks.length - 1 ? 'Tuần này' : dayjs(w.weekStart).format('DD/MM') }));
  return (
    <>
      <div className="dash-legend">
        <span><i style={{ background: color.green500 }} />Tiết dạy</span>
        <span><i style={{ background: color.skyFg }} />Bài giao</span>
        <span><i className="dash-legend__line" style={{ background: color.amberFg }} />Tỷ lệ nộp</span>
      </div>
      <ResponsiveContainer width="100%" height={248}>
        <ComposedChart data={rows} margin={{ top: 12, right: 0, bottom: 0, left: -16 }} barGap={3} barCategoryGap="28%">
          <CartesianGrid vertical={false} stroke={color.border} strokeDasharray="2 4" />
          <XAxis dataKey="label" tickLine={false} axisLine={{ stroke: color.borderStrong }} tick={{ fill: color.text2, fontSize: 12 }} />
          <YAxis yAxisId="n" allowDecimals={false} tickLine={false} axisLine={false} tick={{ fill: color.text3, fontSize: 12 }} />
          <YAxis yAxisId="r" orientation="right" domain={[0, 100]} unit="%" tickLine={false} axisLine={false} tick={{ fill: color.text3, fontSize: 12 }} width={44} />
          <Tooltip
            cursor={{ fill: color.sunken }}
            content={({ active, payload }) => {
              if (!active || !payload?.length) return null;
              const w = payload[0].payload as WeekPoint & { label: string };
              return (
                <div className="dash-tip">
                  <b>Tuần từ {dayjs(w.weekStart).format('DD/MM/YYYY')}</b>
                  <span className="tabular">{fmt(w.sessions)} tiết dạy</span>
                  <span className="tabular">{fmt(w.assignments)} bài giao</span>
                  <span className="tabular">Nộp bài: {pct(w.submissionRate)}</span>
                </div>
              );
            }}
          />
          <Bar yAxisId="n" dataKey="sessions" fill={color.green500} radius={[6, 6, 0, 0]} maxBarSize={22} />
          <Bar yAxisId="n" dataKey="assignments" fill={color.skyFg} radius={[6, 6, 0, 0]} maxBarSize={22} />
          <Line yAxisId="r" dataKey="submissionRate" stroke={color.amberFg} strokeWidth={2.5} dot={{ r: 3, fill: color.amberFg }} connectNulls />
        </ComposedChart>
      </ResponsiveContainer>
    </>
  );
}

/** Danh sách môn xếp từ tỷ lệ nộp thấp lên: môn cần chú ý nằm trên cùng. */
export function SubjectRateList({ subjects, limit = 8 }: { subjects: SubjectRate[]; limit?: number }) {
  if (!subjects.length) return <div className="dash-empty">Chưa có bài tập nào trong năm học.</div>;
  return (
    <ul className="dash-list">
      {subjects.slice(0, limit).map((s) => (
        <li key={s.subjectId} className="dash-list__row">
          <div className="dash-list__main">
            <b>{s.name}</b>
            <span>{s.assignmentCount} bài</span>
          </div>
          <RateBar rate={s.submissionRate} width={140} />
        </li>
      ))}
    </ul>
  );
}
