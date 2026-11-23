import { Count, Delta, Figures, LineChart, MeterList, Panel, SegmentBar, Tile, WeekdayBars } from './BoardKit';
import type { Hue } from './BoardKit';
import { SUBMISSION_ALERT, weekLabel } from './dashboardFormat';
import type { AssignmentSummary, AttendanceMix, Kpi, RateRow } from '../types';

/** Các khối dùng chung của bốn màn Tình hình. */

/** Thẻ tỷ lệ (%): số, đường mini 8 tuần, mức thay đổi so với 4 tuần trước (chỉ khi có kỳ trước). */
export function RateTile({ label, k, hue, higherIsBetter, empty, alert, onClick }: {
  label: string; k: Kpi; hue: Hue; higherIsBetter: boolean; empty: string; alert?: { text: string; onClick?: () => void } | null; onClick?: () => void;
}) {
  return (
    <Tile hue={hue} label={label} trend={k.weeks} unit={k.value === null ? undefined : '%'} alert={alert} onClick={onClick}
      value={k.value === null ? '—' : <Count value={k.value} digits={1} />}
      foot={k.value === null ? empty : <Delta value={k.value} previous={k.previous} higherIsBetter={higherIsBetter} />} />
  );
}

/** Thẻ số đếm: số, đường mini 8 tuần, mức thay đổi so với 4 tuần trước. */
export function CountTile({ label, k, unit, hue, alert, onClick }: {
  label: string; k: Kpi; unit: string; hue: Hue; alert?: { text: string; onClick?: () => void } | null; onClick?: () => void;
}) {
  return (
    <Tile hue={hue} label={label} trend={k.weeks} unit={unit} alert={alert} onClick={onClick} value={<Count value={k.value} />}
      foot={<Delta value={k.value} previous={k.previous} higherIsBetter={false} unit="" />} />
  );
}

/** Đường 8 tuần của một chỉ số (một đường, có thể kèm một đường xám làm mốc). */
export function TrendPanel({ title, meta, weekStarts, weekNo, values, name, base, threshold }: {
  title: string; meta?: string; weekStarts: string[]; weekNo: number | null; values: (number | null)[]; name: string;
  base?: { name: string; values: (number | null)[] }; threshold: number;
}) {
  const labels = weekStarts.map((_, i) => weekLabel(weekStarts, i, weekNo));
  return (
    <Panel title={title} meta={meta ?? '8 tuần'}>
      <LineChart labels={labels} threshold={threshold} series={[
        { key: 'main', name, values, slot: 1 },
        ...(base ? [{ key: 'base', name: base.name, values: base.values, slot: 0, dashed: true }] : []),
      ]} />
    </Panel>
  );
}

export function MixPanel({ mix }: { mix: AttendanceMix }) {
  return (
    <Panel title="Cơ cấu điểm danh" meta="4 tuần">
      <SegmentBar unit="lượt" empty="Chưa có lượt điểm danh nào trong 4 tuần qua." segments={[
        { key: 'p', label: 'Có mặt', value: mix.present, tone: 'good' },
        { key: 'l', label: 'Đi muộn', value: mix.late, tone: 'soft' },
        { key: 'e', label: 'Vắng có phép', value: mix.absentExcused, tone: 'warn' },
        { key: 'u', label: 'Vắng không phép', value: mix.absentUnexcused, tone: 'low' },
      ]} />
    </Panel>
  );
}

/** Tỷ lệ theo nhóm (phân hiệu, khối, môn): thanh ngang một màu, dưới ngưỡng tô đỏ; thêm bao nhiêu nhóm cũng không cần thêm màu. */
export function RatePanel({ title, rows, threshold, empty }: { title: string; rows: RateRow[]; threshold: number; empty: string }) {
  return (
    <Panel title={title} meta="4 tuần">
      {rows.length === 0 ? <p className="bd-empty">{empty}</p>
        : <MeterList threshold={threshold} items={rows.map((r) => ({ key: String(r.id), label: r.name, value: r.rate }))} />}
    </Panel>
  );
}

export function WeekdayPanel({ values }: { values: (number | null)[] }) {
  return <Panel title="Chuyên cần theo thứ" meta="4 tuần"><WeekdayBars values={values} /></Panel>;
}

/** Bài rèn luyện: hai con số đáng theo dõi và tỷ lệ nộp theo nhóm (khối hoặc môn). */
export function AssignmentPanel({ a, rows, groupLabel }: { a: AssignmentSummary; rows: { key: string; label: string; value: number | null }[]; groupLabel: string }) {
  return (
    <Panel title="Bài rèn luyện" meta="4 tuần">
      <Figures items={[
        { key: 'open', label: 'bài đang mở', value: a.open },
        { key: 'low', label: 'bài nộp dưới 50%', value: a.lowCount, tone: 'low' },
      ]} />
      {rows.length > 0 && (
        <>
          <p className="bd-sub">Tỷ lệ nộp theo {groupLabel}</p>
          <MeterList threshold={SUBMISSION_ALERT} items={rows} />
        </>
      )}
    </Panel>
  );
}
