import { useQuery } from '@tanstack/react-query';
import { Button, DatePicker, Select, Skeleton, Table } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import type { Dayjs } from 'dayjs';
import { FileDown, X } from 'lucide-react';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { scdApi } from '@/features/scd/api';
import { toDateString } from '@/shared/lib/format';
import { admApi } from '../api';
import { Empty, Seg, toneOfRate } from '../components/BoardKit';
import { fmt, pct } from '../components/dashboardFormat';
import type { ReportAlertKind, ReportClassRow, ReportThresholds } from '../reportTypes';
import '../components/board.css';
import './report.css';

const ALL = 0;
/** Khớp SchoolReportService.MaxDays ở Backend. */
const MAX_DAYS = 186;

type Range = [Dayjs, Dayjs];

/** Nhãn đầy đủ trên dải "Cần xem" và nhãn ngắn trên từng lớp. */
const FLAGS: { kind: ReportAlertKind; label: (t: ReportThresholds) => string; tag: string }[] = [
  { kind: 'LowAttendance', label: (t) => `Chuyên cần dưới ${t.lowAttendanceBelow}%`, tag: 'Chuyên cần thấp' },
  { kind: 'ManyUnmarked', label: (t) => `Từ ${t.unmarkedSessionsFrom} tiết chưa điểm danh`, tag: 'Chưa điểm danh' },
  { kind: 'LowSubmission', label: (t) => `Nộp bài dưới ${t.lowSubmissionBelow}%`, tag: 'Nộp bài thấp' },
];

const rate = (v: number | null) => <span className={`rp-num bd-text--${toneOfRate(v)}`}>{pct(v)}</span>;
const count = (v: number, warn = false) => <span className={`rp-num${warn && v > 0 ? ' bd-text--warn' : ''}`}>{fmt(v)}</span>;

/**
 * Báo cáo toàn trường (Hiệu trưởng): dải "Cần xem" lọc bảng lớp, so sánh phân hiệu khi xem cả trường, rồi bảng lớp.
 * Chỉ đọc; muốn lấy file thì sang Quản trị › Xuất dữ liệu.
 */
export function SchoolReportPage() {
  const navigate = useNavigate();
  const campuses = useQuery({ queryKey: ['scd', 'campuses'], queryFn: scdApi.campuses });
  const [range, setRange] = useState<Range>(() => [dayjs().subtract(27, 'day'), dayjs()]);
  const [campusId, setCampusId] = useState<number>(ALL);
  const [grade, setGrade] = useState<number>(ALL);
  const [flag, setFlag] = useState<ReportAlertKind | null>(null);
  const tooLong = range[1].diff(range[0], 'day') >= MAX_DAYS;
  const from = toDateString(range[0]);
  const to = toDateString(range[1]);

  const q = useQuery({
    queryKey: ['adm', 'report', from, to, campusId || undefined, grade || undefined],
    queryFn: () => admApi.schoolReport({ from, to, campusId: campusId || undefined, gradeLevel: grade || undefined }),
    enabled: !tooLong,
    placeholderData: (prev) => prev,
  });
  const d = q.data;
  const list = campuses.data ?? [];
  const multiCampus = list.length > 1;

  const kindsOf = new Map<number, ReportAlertKind[]>();
  for (const a of d?.alerts ?? []) kindsOf.set(a.classId, [...(kindsOf.get(a.classId) ?? []), a.kind]);
  const countOf = (kind: ReportAlertKind) => d?.alerts.filter((a) => a.kind === kind).length ?? 0;
  const rows = (d?.classes ?? []).filter((c) => !flag || kindsOf.get(c.classId)?.includes(flag));

  const columns: ColumnsType<ReportClassRow> = [
    { title: 'Lớp', dataIndex: 'className', render: (v: string, r) => <span className="rp-cls"><strong>{v}</strong><span>{r.homeroom ?? 'Chưa có chủ nhiệm'}</span></span> },
    ...(multiCampus && !campusId ? [{ title: 'Phân hiệu', dataIndex: 'campusName', width: 170 } as const] : []),
    { title: 'Sĩ số', dataIndex: 'studentCount', align: 'right', width: 80, render: (v: number) => count(v) },
    { title: 'Chuyên cần', dataIndex: 'attendanceRate', align: 'right', width: 120, sorter: (a, b) => (a.attendanceRate ?? 101) - (b.attendanceRate ?? 101), render: rate },
    { title: 'Nộp bài', dataIndex: 'submissionRate', align: 'right', width: 110, sorter: (a, b) => (a.submissionRate ?? 101) - (b.submissionRate ?? 101), render: rate },
    { title: 'Chưa điểm danh', dataIndex: 'unmarkedSessions', align: 'right', width: 150, sorter: (a, b) => a.unmarkedSessions - b.unmarkedSessions, render: (v: number) => count(v, true) },
    {
      title: 'Cần xem', key: 'flags', width: 260,
      render: (_, r) => (
        <span className="rp-tags">
          {FLAGS.filter((f) => kindsOf.get(r.classId)?.includes(f.kind)).map((f) => <i key={f.kind}>{f.tag}</i>)}
        </span>
      ),
    },
  ];

  return (
    <div className="bd rp">
      <header className="bd-head">
        <div>
          <h1>Báo cáo toàn trường</h1>
          <p>{[d?.academicYearName && `Năm học ${d.academicYearName}`, d?.semesterName].filter(Boolean).join(' · ') || 'So sánh phân hiệu và lớp'}</p>
        </div>
        <Button icon={<FileDown size={16} />} onClick={() => navigate('/adm/exports')}>Xuất dữ liệu</Button>
      </header>

      <div className="rp-filters">
        <DatePicker.RangePicker
          format="DD/MM/YYYY" allowClear={false} value={range} status={tooLong ? 'error' : undefined}
          onChange={(v) => v && v[0] && v[1] && setRange([v[0], v[1]])}
        />
        {multiCampus && (
          <Seg label="Chọn phân hiệu" value={campusId} onChange={(v) => { setCampusId(v); setFlag(null); }}
            options={[{ value: ALL, label: 'Cả trường' }, ...list.map((c) => ({ value: c.campusId, label: c.name }))]} />
        )}
        {d && d.gradeOptions.length > 1 && (
          <Select value={grade} onChange={(v) => { setGrade(v); setFlag(null); }} style={{ width: 120 }}
            options={[{ value: ALL, label: 'Mọi khối' }, ...d.gradeOptions.map((g) => ({ value: g, label: `Khối ${g}` }))]} />
        )}
      </div>

      {tooLong && <p className="rp-note" role="alert">Chọn khoảng tối đa {MAX_DAYS} ngày.</p>}
      {!d && !tooLong && (q.isError ? <Empty>Không tải được báo cáo. Thử lại sau.</Empty> : <Skeleton active paragraph={{ rows: 10 }} />)}

      {d && (
        <>
          <section className="rp-flags" aria-label="Cần xem">
            <span className="rp-flags__title">Cần xem</span>
            {FLAGS.map((f) => {
              const n = countOf(f.kind);
              return (
                <button key={f.kind} type="button" disabled={n === 0} aria-pressed={flag === f.kind}
                  className={`rp-flag${n ? ' is-hot' : ''}${flag === f.kind ? ' is-on' : ''}`}
                  onClick={() => setFlag(flag === f.kind ? null : f.kind)}>
                  <b className="tabular">{n}</b><span>{f.label(d.thresholds)}</span>
                </button>
              );
            })}
          </section>

          {multiCampus && !campusId && (
            <section className="bd-panel" aria-label="So sánh phân hiệu">
              <div className="bd-panel__title"><h2>So sánh phân hiệu</h2></div>
              <div className="rp-grid rp-grid--head" aria-hidden>
                <span>Phân hiệu</span><span>Chuyên cần</span><span>Nộp bài</span><span>Chưa điểm danh</span><span>Tiết bị hủy</span>
              </div>
              <ul className="rp-campuses">
                {d.campuses.map((c) => (
                  <li key={c.campusId}>
                    <button type="button" className="rp-grid" onClick={() => setCampusId(c.campusId)} aria-label={`Xem riêng ${c.name}`}>
                      <span className="rp-cls"><strong>{c.name}</strong><span>{fmt(c.classCount)} lớp · {fmt(c.studentCount)} học sinh</span></span>
                      {c.classCount === 0 ? <span className="rp-none">Chưa có lớp trong năm học này</span> : (
                        <>
                          {rate(c.attendanceRate)}
                          {rate(c.assignmentCount ? c.submissionRate : null)}
                          {count(c.unmarkedSessions, true)}
                          {count(c.cancelledSessions)}
                        </>
                      )}
                    </button>
                  </li>
                ))}
              </ul>
            </section>
          )}

          <section className="bd-panel" aria-label="So sánh lớp">
            <div className="bd-panel__title rp-title">
              <h2>So sánh lớp</h2>
              {flag && (
                <button type="button" className="rp-chip" onClick={() => setFlag(null)} aria-label={`Bỏ lọc ${FLAGS.find((f) => f.kind === flag)?.tag}`}>
                  {FLAGS.find((f) => f.kind === flag)?.tag}<X size={14} aria-hidden />
                </button>
              )}
            </div>
            {rows.length === 0
              ? <Empty>Chưa có lớp nào trong phạm vi đã chọn.</Empty>
              : <Table<ReportClassRow> rowKey="classId" size="middle" columns={columns} dataSource={rows} pagination={{ pageSize: 15, hideOnSinglePage: true, showSizeChanger: false }} />}
          </section>
        </>
      )}
    </div>
  );
}
