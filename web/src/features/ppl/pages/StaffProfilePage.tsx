import { useQuery } from '@tanstack/react-query';
import { Button, Skeleton } from 'antd';
import { ArrowLeft } from 'lucide-react';
import type { ReactNode } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { getErrorMessage } from '@/app/apiClient';
import { genderText } from '@/features/sch/components/rosterColumns';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { PageHeader } from '@/shared/components/PageHeader';
import { PersonAvatar } from '@/shared/components/PersonAvatar';
import { StatusBadge } from '@/shared/components/StatusBadge';
import { formatDate } from '@/shared/lib/format';
import { accountTypeLabel } from '@/shared/lib/labels';
import type { AccountType } from '@/shared/lib/labels';
import { pplApi } from '../api';
import type { ResponsibilityKind, StaffResponsibility, StaffTeaching } from '../types';
import '../components/student-profile.css';

const KIND_LABEL: Record<ResponsibilityKind, string> = { campus: 'Phụ trách phân hiệu', department: 'Tổ trưởng', homeroom: 'Chủ nhiệm lớp' };
const dash = <span className="muted">—</span>;

function Fact({ label, value, numeric }: { label: string; value: ReactNode; numeric?: boolean }) {
  return (
    <div className="sp-fact">
      <dt>{label}</dt>
      <dd className={numeric ? 'tabular' : undefined}>{value || dash}</dd>
    </div>
  );
}

function ResponsibilityCard({ rows }: { rows: StaffResponsibility[] }) {
  return (
    <section className="sp-card">
      <h3 className="sp-card__title">Trách nhiệm</h3>
      {rows.length === 0 ? (
        <p className="sp-empty">Chưa giữ trách nhiệm nào.</p>
      ) : (
        <ol className="sp-timeline">
          {rows.map((r) => (
            <li key={`${r.kind}-${r.targetName}-${r.startDate}`} className={r.isCurrent ? 'is-current' : undefined}>
              <span className="sp-timeline__year">{r.isCurrent ? 'Đang giữ' : 'Đã kết thúc'}</span>
              <div className="sp-timeline__body">
                <div className="sp-timeline__class">
                  <b>{KIND_LABEL[r.kind]} {r.targetName}</b>
                  {r.targetGroup && <span>{r.targetGroup}</span>}
                </div>
                <span className="sp-timeline__range tabular">
                  Từ {formatDate(r.startDate)}{r.endDate ? ` đến ${formatDate(r.endDate)}` : ''}
                </span>
              </div>
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}

function TeachingCard({ rows }: { rows: StaffTeaching[] }) {
  const total = rows.reduce((n, r) => n + r.periodsPerWeek, 0);
  return (
    <section className="sp-card">
      <header className="sp-card__head">
        <h3 className="sp-card__title">Giảng dạy học kỳ này</h3>
        {rows.length > 0 && <span className="sp-card__hint">{total} tiết mỗi tuần</span>}
      </header>
      {rows.length === 0 ? (
        <p className="sp-empty">Chưa có tiết nào trong thời khóa biểu đã phát hành.</p>
      ) : (
        <ul className="sp-att__recent">
          {rows.map((r) => (
            <li key={`${r.classId}-${r.subjectId}`} style={{ gridTemplateColumns: '88px minmax(0, 1fr) auto' }}>
              <b className="sp-att__when">Lớp {r.className}</b>
              <span className="sp-att__slot">{r.subjectName}<span className="sp-att__note">{r.campusName}</span></span>
              <span className="tabular">{r.periodsPerWeek} tiết</span>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

/** PPL-01 Hồ sơ cán bộ (4.3.2), chỉ xem. Mở từ Hồ sơ › Cán bộ; sửa và ngừng hoạt động vẫn làm ở danh bạ. */
export function StaffProfilePage() {
  const { id } = useParams();
  const staffId = Number(id);
  const navigate = useNavigate();
  const detail = useQuery({
    queryKey: ['ppl', 'staff', 'detail', staffId],
    queryFn: () => pplApi.staff.detail(staffId),
    enabled: Number.isInteger(staffId) && staffId > 0,
  });

  const back = () => ((window.history.state?.idx ?? 0) > 0 ? navigate(-1) : navigate('/ppl/staff'));
  const backButton = <Button icon={<ArrowLeft size={16} />} onClick={back}>Quay lại</Button>;

  if (detail.isLoading) return (<><PageHeader title="Hồ sơ cán bộ" /><Skeleton active avatar paragraph={{ rows: 8 }} /></>);
  if (detail.error || !detail.data) {
    return (
      <>
        <PageHeader title="Hồ sơ cán bộ" extra={backButton} />
        <InlineAlert type="error" title="Không mở được hồ sơ cán bộ" description={detail.error ? getErrorMessage(detail.error) : 'Đường dẫn không đúng.'} />
      </>
    );
  }

  const d = detail.data;
  const p = d.profile;
  return (
    <>
      <PageHeader title="Hồ sơ cán bộ" extra={backButton} />
      <section className={`sp-hero${p.isActive ? '' : ' sp-hero--left'}`}>
        <PersonAvatar name={p.fullName} size={76} muted={!p.isActive} />
        <div className="sp-hero__id">
          <div className="sp-hero__name">
            <h2>{p.fullName}</h2>
            <StatusBadge domain="catalog" value={p.isActive ? 'Active' : 'Inactive'} />
          </div>
          <p className="sp-hero__meta">
            <span className="tabular">{p.personCode}</span>
            {p.position && <span>{p.position}</span>}
            {p.teacherAbbr && <span>Viết tắt {p.teacherAbbr}</span>}
          </p>
        </div>
        <div className="sp-hero__class">
          <span className="sp-hero__class-label">Phân hiệu chính</span>
          <b>{p.primaryCampusName}</b>
          <span className="sp-hero__class-campus">{p.departmentName ?? 'Chưa thuộc tổ chuyên môn'}</span>
        </div>
      </section>
      <div className="sp-grid">
        <div className="sp-col">
          <section className="sp-card">
            <h3 className="sp-card__title">Thông tin cá nhân</h3>
            <dl className="sp-facts">
              <Fact label="Ngày sinh" numeric value={p.dateOfBirth && formatDate(p.dateOfBirth)} />
              <Fact label="Giới tính" value={p.gender && genderText(p.gender)} />
              <Fact label="Số định danh" numeric value={p.nationalId} />
              <Fact label="Số điện thoại" numeric value={p.phoneNumber} />
              <Fact label="Email" value={p.email} />
              <Fact label="Địa chỉ" value={d.address} />
              <Fact label="Ngày vào làm" numeric value={d.hireDate && formatDate(d.hireDate)} />
              <Fact label="Tài khoản" value={p.accountId === null ? 'Chưa có' : accountTypeLabel[p.accountType as AccountType]} />
            </dl>
          </section>
        </div>
        <div className="sp-col">
          <ResponsibilityCard rows={d.responsibilities} />
          <TeachingCard rows={d.teaching} />
        </div>
      </div>
    </>
  );
}
