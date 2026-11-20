import { useQuery } from '@tanstack/react-query';
import { Button, Skeleton } from 'antd';
import dayjs from 'dayjs';
import { ArrowLeft } from 'lucide-react';
import type { ReactNode } from 'react';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import { getErrorMessage } from '@/app/apiClient';
import { dayText } from '@/features/att/attFormat';
import { genderText } from '@/features/sch/components/rosterColumns';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { PageHeader } from '@/shared/components/PageHeader';
import { PersonAvatar } from '@/shared/components/PersonAvatar';
import { StatusBadge } from '@/shared/components/StatusBadge';
import { formatDate, sessionOfDayLabel } from '@/shared/lib/format';
import { enrollmentCloseReasonLabel } from '@/shared/lib/labels';
import { pplApi } from '../api';
import type { StudentAttendanceOverview, StudentDetail, StudentEnrollment, StudentGuardian } from '../types';
import '../components/student-profile.css';

const HOMEROOM_LIST = '/sch/homeroom-students';
const DIRECTORY = '/ppl/students';

const dash = <span className="muted">—</span>;

/** Một dòng thông tin; giá trị trống thì hiện gạch mờ. Số và ngày dùng chữ số đều độ rộng. */
function Fact({ label, value, numeric }: { label: string; value: ReactNode; numeric?: boolean }) {
  return (
    <div className="sp-fact">
      <dt>{label}</dt>
      <dd className={numeric ? 'tabular' : undefined}>{value || dash}</dd>
    </div>
  );
}

function Hero({ s }: { s: StudentDetail }) {
  const studying = s.status === 'Studying';
  const age = s.dateOfBirth ? dayjs().diff(s.dateOfBirth, 'year') : null;
  return (
    <section className={`sp-hero${studying ? '' : ' sp-hero--left'}`}>
      <PersonAvatar name={s.fullName} size={76} muted={!studying} />
      <div className="sp-hero__id">
        <div className="sp-hero__name">
          <h2>{s.fullName}</h2>
          <StatusBadge domain="student" value={s.status} />
        </div>
        <p className="sp-hero__meta">
          <span className="tabular">{s.personCode}</span>
          {age !== null && <span>{age} tuổi</span>}
          {s.gender && <span>{genderText(s.gender)}</span>}
        </p>
      </div>
      <div className="sp-hero__class">
        {s.className ? (
          <>
            <span className="sp-hero__class-label">Đang học lớp</span>
            <b>{s.className}</b>
            <span className="sp-hero__class-campus">{s.campusName}</span>
          </>
        ) : (
          <>
            <span className="sp-hero__class-label">Lớp hiện tại</span>
            <span className="sp-hero__class-none">{studying ? 'Chưa xếp lớp' : `Rời trường ${formatDate(s.leftDate)}`}</span>
          </>
        )}
      </div>
    </section>
  );
}

function GuardianCard({ g }: { g: StudentGuardian | null }) {
  return (
    <section className="sp-card">
      <h3 className="sp-card__title">Phụ huynh</h3>
      {g ? (
        <div className="sp-guardian">
          <PersonAvatar name={g.fullName} size={44} />
          <div className="sp-guardian__text">
            <b>{g.fullName}</b>
            <span>{g.relationship ?? 'Phụ huynh đại diện'}</span>
          </div>
          {g.phoneNumber && <a className="sp-guardian__phone tabular" href={`tel:${g.phoneNumber}`}>{g.phoneNumber}</a>}
          {g.email && <span className="sp-guardian__email">{g.email}</span>}
        </div>
      ) : (
        <p className="sp-empty">Chưa có phụ huynh đại diện.</p>
      )}
    </section>
  );
}

// Bốn nhãn cuối theo thứ tự Conventions §4.4; màu của thanh là màu nhấn, chữ và số luôn đi kèm nên không chỉ dựa vào màu.
const SEGMENTS = [
  { key: 'present', label: 'Có mặt', tone: 'present' },
  { key: 'late', label: 'Muộn', tone: 'late' },
  { key: 'excusedAbsent', label: 'Vắng có phép', tone: 'excused' },
  { key: 'unexcusedAbsent', label: 'Vắng không phép', tone: 'unexcused' },
] as const;

function AttendanceCard({ a }: { a: StudentAttendanceOverview | null }) {
  if (!a) {
    return (
      <section className="sp-card">
        <h3 className="sp-card__title">Chuyên cần</h3>
        <p className="sp-empty">Chưa có năm học hiện hành nên chưa có số liệu chuyên cần.</p>
      </section>
    );
  }
  const marked = SEGMENTS.reduce((n, s) => n + a.counts[s.key], 0);
  return (
    <section className="sp-card sp-att">
      <header className="sp-card__head">
        <h3 className="sp-card__title">Chuyên cần</h3>
        <span className="sp-card__hint">Năm học {a.academicYearName} · tính đến {formatDate(a.to)}</span>
      </header>
      {a.rate === null ? (
        <p className="sp-empty">Chưa có lượt điểm danh nào trong năm học này.</p>
      ) : (
        <>
          <div className="sp-att__summary">
            <div className="sp-att__rate">
              <b className="tabular">{a.rate.toLocaleString('vi-VN')}%</b>
              <span>có mặt trên {marked} lượt đã điểm danh</span>
            </div>
            <div className="sp-att__bar" aria-hidden>
              {SEGMENTS.map((s) => a.counts[s.key] > 0 && (
                <i key={s.key} className={`sp-att__seg sp-att__seg--${s.tone}`} style={{ flexGrow: a.counts[s.key] }} />
              ))}
            </div>
            <ul className="sp-att__legend">
              {SEGMENTS.map((s) => (
                <li key={s.key}>
                  <i className={`sp-att__dot sp-att__seg--${s.tone}`} aria-hidden />
                  <span>{s.label}</span>
                  <b className="tabular">{a.counts[s.key]}</b>
                </li>
              ))}
            </ul>
            {a.counts.notMarked > 0 && <p className="sp-att__unmarked">{a.counts.notMarked} tiết chưa điểm danh, không tính vào tỷ lệ.</p>}
          </div>
          <h4 className="sp-att__recent-title">Muộn và vắng gần đây</h4>
          {a.recent.length === 0 ? (
            <p className="sp-empty">Không có lần muộn hay vắng nào.</p>
          ) : (
            <ul className="sp-att__recent">
              {a.recent.map((r) => (
                <li key={r.sessionId}>
                  <span className="sp-att__when tabular">{dayText(r.date)}</span>
                  <span className="sp-att__slot">
                    {sessionOfDayLabel(r.sessionOfDay)} · Tiết {r.periodNo} · {r.subjectName}
                    {r.note && <span className="sp-att__note">{r.note}</span>}
                  </span>
                  <StatusBadge domain="attendance" value={r.label} />
                </li>
              ))}
            </ul>
          )}
        </>
      )}
    </section>
  );
}

function HistoryCard({ rows }: { rows: StudentEnrollment[] }) {
  return (
    <section className="sp-card">
      <h3 className="sp-card__title">Lịch sử lớp</h3>
      {rows.length === 0 ? (
        <p className="sp-empty">Chưa có ghi danh nào.</p>
      ) : (
        <ol className="sp-timeline">
          {rows.map((e) => (
            <li key={e.enrollmentId} className={e.isCurrent ? 'is-current' : undefined}>
              <span className="sp-timeline__year">{e.academicYearName}</span>
              <div className="sp-timeline__body">
                <div className="sp-timeline__class">
                  <b>{e.className}</b>
                  <span>{e.campusName}</span>
                  {e.isCurrent && <span className="sp-timeline__now">Đang học</span>}
                </div>
                <span className="sp-timeline__range tabular">
                  {formatDate(e.startDate)} – {formatDate(e.endDate)}
                  {e.reason && <> · {enrollmentCloseReasonLabel[e.reason] ?? e.reason}</>}
                </span>
              </div>
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}

/**
 * PPL-02 Hồ sơ học sinh (4.3.12), chỉ xem. Mở từ Hồ sơ › Học sinh (School Admin, Hiệu trưởng, Hiệu phó) và từ Danh sách học sinh
 * (giáo viên chủ nhiệm). Backend giới hạn phạm vi; sửa hồ sơ và đổi phụ huynh vẫn làm ở danh bạ.
 */
export function StudentProfilePage() {
  const { id } = useParams();
  const studentId = Number(id);
  const navigate = useNavigate();
  const { pathname } = useLocation();
  const fromHomeroom = pathname.startsWith(HOMEROOM_LIST);

  const detail = useQuery({
    queryKey: ['ppl', 'students', 'detail', studentId],
    queryFn: () => pplApi.students.detail(studentId),
    enabled: Number.isInteger(studentId) && studentId > 0,
  });

  // Quay lại đúng danh sách đã mở; mở thẳng bằng đường dẫn thì về danh sách của vai trò.
  const back = () => ((window.history.state?.idx ?? 0) > 0 ? navigate(-1) : navigate(fromHomeroom ? HOMEROOM_LIST : DIRECTORY));
  const backButton = <Button icon={<ArrowLeft size={16} />} onClick={back}>Quay lại</Button>;

  if (detail.isLoading) return (<><PageHeader title="Hồ sơ học sinh" /><Skeleton active avatar paragraph={{ rows: 8 }} /></>);
  if (detail.error || !detail.data) {
    return (
      <>
        <PageHeader title="Hồ sơ học sinh" extra={backButton} />
        <InlineAlert type="error" title="Không mở được hồ sơ học sinh" description={detail.error ? getErrorMessage(detail.error) : 'Đường dẫn không đúng.'} />
      </>
    );
  }

  const s = detail.data;
  return (
    <>
      <PageHeader
        title="Hồ sơ học sinh"
        extra={(
          <>
            {backButton}
            {fromHomeroom && <Button type="primary" onClick={() => navigate(`/par/weekly-reports?studentId=${s.studentId}`)}>Sổ liên lạc</Button>}
          </>
        )}
      />
      <Hero s={s} />
      {s.note && (
        <aside className="sp-note">
          <span className="sp-note__label">Ghi chú</span>
          <p>{s.note}</p>
        </aside>
      )}
      <div className="sp-grid">
        <div className="sp-col">
          <section className="sp-card">
            <h3 className="sp-card__title">Thông tin cá nhân</h3>
            <dl className="sp-facts">
              <Fact label="Ngày sinh" numeric value={s.dateOfBirth && formatDate(s.dateOfBirth)} />
              <Fact label="Giới tính" value={s.gender && genderText(s.gender)} />
              <Fact label="Số định danh" numeric value={s.nationalId} />
              <Fact label="Số điện thoại" numeric value={s.phoneNumber} />
              <Fact label="Địa chỉ" value={s.address} />
              <Fact label="Ngày nhập học" numeric value={s.admissionDate && formatDate(s.admissionDate)} />
              {s.leftDate && <Fact label="Ngày rời trường" numeric value={formatDate(s.leftDate)} />}
            </dl>
          </section>
          <GuardianCard g={s.guardian} />
        </div>
        <div className="sp-col">
          <AttendanceCard a={s.attendance} />
          <HistoryCard rows={s.enrollments} />
        </div>
      </div>
    </>
  );
}
