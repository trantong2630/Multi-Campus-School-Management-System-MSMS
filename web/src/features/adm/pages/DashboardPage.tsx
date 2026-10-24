import { useQuery } from '@tanstack/react-query';
import { Card, Select, Skeleton } from 'antd';
import dayjs from 'dayjs';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Bar, BarChart, CartesianGrid, Cell, LabelList, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { useAuth } from '@/app/AuthProvider';
import { scdApi } from '@/features/scd/api';
import { DataTable } from '@/shared/components/DataTable';
import { PageHeader } from '@/shared/components/PageHeader';
import { StatCard } from '@/shared/components/StatCard';
import { accent, color } from '@/shared/lib/tokens';
import type { AccentName } from '@/shared/lib/tokens';
import { admApi } from '../api';
import type { DashboardCampusRow, GradeCount } from '../types';
import './dashboard.css';

const ALL = 0;

/**
 * ADM-01: Principal xem toàn trường và lọc được theo phân hiệu; Vice Principal xem cố định phân hiệu mình.
 * Bốn số quy mô và biểu đồ học sinh theo khối; số cán bộ và số tiết bấm vào mở danh sách gốc.
 */
export function DashboardPage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const isPrincipal = user?.accountType === 'Principal';
  const campuses = useQuery({ queryKey: ['scd', 'campuses'], queryFn: scdApi.campuses });
  const [campusId, setCampusId] = useState<number>(ALL);
  const canFilter = isPrincipal || (campuses.data?.length ?? 0) > 1;

  const scope = campusId === ALL ? undefined : campusId;
  const data = useQuery({ queryKey: ['adm', 'dashboard', scope], queryFn: () => admApi.dashboard(scope) });
  const d = data.data;
  const n = (v: number | undefined) => (v === undefined ? '—' : v.toLocaleString('vi-VN'));

  const scopeName = scope === undefined
    ? (isPrincipal ? 'Toàn trường' : campuses.data?.map((c) => c.name).join(', '))
    : campuses.data?.find((c) => c.campusId === scope)?.name;
  const showCompare = !!d && d.campuses.length > 1;

  // Dòng phụ của dải chỉ số: bối cảnh suy ra từ chính số liệu Dashboard, không gọi thêm API.
  const today = dayjs().format('dddd, DD/MM');
  const hints = {
    class: d && d.classCount > 0 && d.studentCount > 0 ? `Bình quân ${Math.round(d.studentCount / d.classCount)} học sinh mỗi lớp` : undefined,
    student: d && d.byGrade.length > 0 ? `${d.byGrade.length} khối đang có lớp` : undefined,
    staff: d && d.campuses.length > 1 ? `Trên ${d.campuses.length} phân hiệu` : undefined,
    session: today.charAt(0).toUpperCase() + today.slice(1),
  };
  const maxStudents = Math.max(1, ...(d?.campuses.map((c) => c.studentCount) ?? []));
  const totalStudents = d?.byGrade.reduce((sum, g) => sum + g.studentCount, 0) ?? 0;

  return (
    <>
      <PageHeader
        title="Dashboard"
        description={[scopeName, d?.academicYearName && `Năm học ${d.academicYearName}`, dayjs().format('DD/MM/YYYY')].filter(Boolean).join(' · ')}
        extra={canFilter && (
          <Select
            className="dash-scope" value={campusId} onChange={setCampusId} loading={campuses.isLoading}
            options={[
              { value: ALL, label: isPrincipal ? 'Toàn trường' : 'Tất cả phân hiệu phụ trách' },
              ...(campuses.data?.map((c) => ({ value: c.campusId, label: c.name })) ?? []),
            ]}
          />
        )}
      />

      {/* Bento: hàng trên 4 ô chỉ số (ô nhấn xanh là quy mô học sinh), hàng dưới biểu đồ và bảng so sánh chia đôi. */}
      <div className="dash-bento">
        <StatCard variant="accent" label="Học sinh đang học" value={n(d?.studentCount)} hint={hints.student} />
        <StatCard label="Lớp đang hoạt động" value={n(d?.classCount)} hint={hints.class} />
        <StatCard label="Cán bộ đang công tác" value={n(d?.staffCount)} hint={hints.staff} onClick={() => navigate('/ppl/staff')} />
        <StatCard label="Tiết dạy hôm nay" value={n(d?.sessionsToday)} hint={hints.session} onClick={() => navigate('/scd/schedule')} />

        <Card
          title="Học sinh theo khối" className={`dash-card ${showCompare ? 'dash-bento__half' : 'dash-bento__full'}`}
          extra={totalStudents > 0 && <span className="dash-card__meta tabular">Tổng {totalStudents.toLocaleString('vi-VN')} học sinh</span>}
        >
          {data.isLoading ? <Skeleton active paragraph={{ rows: 6 }} title={false} /> : <GradeChart data={d?.byGrade ?? []} />}
        </Card>

        {showCompare && (
          <Card title="So sánh phân hiệu" className="dash-card dash-card--table dash-bento__half">
            <DataTable<DashboardCampusRow>
              rowKey="campusId" dataSource={d.campuses} pagination={false}
              onRow={(r) => ({ onClick: () => setCampusId(r.campusId), className: 'dash-row-click' })}
              columns={[
                { title: 'Phân hiệu', dataIndex: 'name' },
                { title: 'Lớp', dataIndex: 'classCount', align: 'right', className: 'tabular' },
                {
                  title: 'Học sinh', dataIndex: 'studentCount', align: 'right', className: 'tabular', width: 200,
                  // Thanh mảnh cho thấy quy mô tương đối giữa các phân hiệu; con số vẫn là thông tin chính.
                  render: (v: number) => (
                    <div className="dash-share">
                      <span className="dash-share__track"><i style={{ width: `${(v / maxStudents) * 100}%` }} /></span>
                      <span>{v.toLocaleString('vi-VN')}</span>
                    </div>
                  ),
                },
                { title: 'Cán bộ', dataIndex: 'staffCount', align: 'right', className: 'tabular' },
              ]}
            />
          </Card>
        )}
      </div>
    </>
  );
}

// Mỗi khối một tông để cột biểu đồ phân biệt được ngay, vẫn trong họ màu hệ thống.
const GRADE_TONES: AccentName[] = ['green', 'sky', 'violet', 'amber', 'teal', 'coral'];

function GradeChart({ data }: { data: GradeCount[] }) {
  if (!data.length) return <div className="dash-empty">Năm học hiện hành chưa có lớp nào.</div>;
  const rows = data.map((g) => ({ ...g, label: `Khối ${g.gradeLevel}` }));
  return (
    <ResponsiveContainer width="100%" height={280}>
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
                <span className="tabular">{g.studentCount.toLocaleString('vi-VN')} học sinh</span>
                <span className="tabular">{g.classCount} lớp</span>
              </div>
            );
          }}
        />
        <Bar dataKey="studentCount" fill={color.green600} radius={[8, 8, 0, 0]} maxBarSize={52}>
          {rows.map((g, i) => <Cell key={g.gradeLevel} fill={accent[GRADE_TONES[i % GRADE_TONES.length]].fg} />)}
          <LabelList
            dataKey="studentCount" position="top" offset={10}
            formatter={(v) => Number(v).toLocaleString('vi-VN')}
            style={{ fill: color.ink, fontSize: 13, fontWeight: 600 }}
          />
        </Bar>
      </BarChart>
    </ResponsiveContainer>
  );
}
