import { useQuery } from '@tanstack/react-query';
import { Skeleton } from 'antd';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '@/app/AuthProvider';
import { isPathAllowed } from '@/app/menu';
import { accountTypeLabel } from '@/shared/lib/labels';
import type { AccountType } from '@/shared/lib/labels';
import { admApi } from '../api';
import { Count, Empty, Meter, Trend } from '../components/BoardKit';
import { fmt } from '../components/dashboardFormat';
import '../components/board.css';
import './system-board.css';

/** Thiếu sót dữ liệu: nhãn và màn hình School Admin mở ra để sửa. */
const gapMeta: Record<string, { label: string; to: string }> = {
  ClassesWithoutHomeroom: { label: 'Lớp chưa có giáo viên chủ nhiệm', to: '/ppl/responsibilities' },
  StudentsWithoutClass: { label: 'Học sinh đang học chưa ghi danh vào lớp', to: '/sch/classes' },
  StudentsWithoutParent: { label: 'Học sinh chưa liên kết phụ huynh', to: '/ppl/students' },
  StaffWithoutAccount: { label: 'Cán bộ chưa có tài khoản', to: '/adm/accounts' },
  TeachersWithoutDepartment: { label: 'Giáo viên chưa thuộc tổ chuyên môn', to: '/ppl/staff' },
  DepartmentsWithoutHead: { label: 'Tổ chuyên môn chưa có tổ trưởng', to: '/ppl/responsibilities' },
  CampusesWithoutLeader: { label: 'Phân hiệu chưa có hiệu phó phụ trách', to: '/ppl/responsibilities' },
};

/**
 * Thống kê hệ thống (School Admin): quy mô, tài khoản, dữ liệu cần bổ sung, hoạt động và tiến độ thời khóa biểu.
 * Không có số liệu giảng dạy đứng tên người khác (BR-02); chi tiết cấu trúc nằm trên các màn Thiết lập.
 */
export function SystemBoardPage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const q = useQuery({ queryKey: ['adm', 'dashboard', 'admin'], queryFn: admApi.adminDashboard });
  const d = q.data;
  const can = (path: string) => !!user && isPathAllowed(path, user);

  if (q.isLoading || !d) return <Skeleton active paragraph={{ rows: 10 }} />;

  const total = d.accounts.reduce((s, a) => s + a.total, 0);
  const active = d.accounts.reduce((s, a) => s + a.active, 0);
  const pending = d.accounts.reduce((s, a) => s + a.pendingPasswordChange, 0);
  const locked = d.accounts.reduce((s, a) => s + a.locked, 0);
  const never = d.accounts.reduce((s, a) => s + a.neverLoggedIn, 0);
  const gaps = d.gaps.filter((g) => g.count > 0);
  const gapTotal = gaps.reduce((s, g) => s + g.count, 0);
  const week = d.activity.slice(-7);
  const activityTotal = week.reduce((s, a) => s + a.count, 0);
  const activity = week.map((a) => ({ weekStart: a.date, count: a.count, rate: null }));

  return (
    <div className="bd">
      <header className="bd-head">
        <div>
          <h1>Thống kê hệ thống</h1>
          <p>{['Toàn trường', d.academicYearName && `Năm học ${d.academicYearName}`].filter(Boolean).join(' · ')}</p>
        </div>
      </header>

      <section className="bd-panel bd-panel--tint sb-hero" aria-label="Tổng quan">
        <div className="sb-hero__main">
          <div className="sb-hero__acc">
            <p className="sb-eyebrow">Tài khoản</p>
            <div className="sb-acc__total"><b className="tabular"><Count value={total} /></b><span>{fmt(active)} đang hoạt động</span></div>
            <div className="sb-acc__bar" role="img" aria-label={`${active} hoạt động, ${pending} chờ đổi mật khẩu, ${locked} khóa`}>
              <i className="is-ok" style={{ flexGrow: Math.max(active - pending, 0) }} />
              <i className="is-wait" style={{ flexGrow: pending }} />
              <i className="is-lock" style={{ flexGrow: locked }} />
            </div>
            <ul className="sb-chips">
              {([
                ['is-wait', 'chờ đổi mật khẩu', pending],
                ['is-lock', 'đang khóa', locked],
                ['', 'chưa đăng nhập', never],
              ] as const).map(([cls, label, n]) => {
                const inner = (<><em />{fmt(n)} {label}</>);
                const tone = n ? cls : 'is-zero';
                return <li key={label}>{can('/adm/accounts') ? <button type="button" className={tone} onClick={() => navigate('/adm/accounts')}>{inner}</button> : <span className={tone}>{inner}</span>}</li>;
              })}
            </ul>
          </div>
          <div className="sb-hero__trend">
            <p className="sb-eyebrow">Hoạt động 7 ngày qua · <b className="tabular">{fmt(activityTotal)}</b> thao tác</p>
            <Trend points={activity} countLabel="Thao tác ghi nhật ký mỗi ngày" height={170} />
          </div>
        </div>
      </section>

      <section className="bd-panel" aria-label="Quy mô">
        <h2 className="sb-eyebrow">Quy mô trường</h2>
        <ul className="sb-scale">
          {([
            ['Phân hiệu', d.campusCount, '/sch/campuses', 'mint'],
            ['Lớp đang hoạt động', d.classCount, '/sch/classes', 'sky'],
            ['Học sinh đang học', d.studentCount, '/ppl/students', 'sun'],
            ['Cán bộ', d.staffCount, '/ppl/staff', 'lilac'],
          ] as const).map(([label, value, to, hue]) => {
            const inner = (<><span>{label}</span><b className="tabular"><Count value={value} /></b></>);
            return <li key={label}>{can(to) ? <button type="button" className={`sb-scale__${hue}`} onClick={() => navigate(to)}>{inner}</button> : <div className={`sb-scale__${hue}`}>{inner}</div>}</li>;
          })}
        </ul>
      </section>

      {gaps.length > 0 && (
        <section className="bd-panel" aria-label="Dữ liệu cần bổ sung">
          <div className="sb-gap__head">
            <h2 className="sb-eyebrow">Dữ liệu cần bổ sung</h2>
            <span className="sb-pill"><b className="tabular">{fmt(gapTotal)}</b> mục cần xử lý</span>
          </div>
          <ul className="sb-gap">
            {gaps.map((g) => {
              const meta = gapMeta[g.key] ?? { label: g.key, to: '' };
              const link = can(meta.to);
              const inner = (<><span className="sb-gap__n tabular">{fmt(g.count)}</span><span>{meta.label}</span>{link ? <span className="sb-gap__go" title="Mở màn hình để sửa">→</span> : <span />}</>);
              return <li key={g.key}>{link ? <button type="button" onClick={() => navigate(meta.to)}>{inner}</button> : <div>{inner}</div>}</li>;
            })}
          </ul>
        </section>
      )}

      <div className="bd-split">
        <section className="bd-panel" aria-label="Tài khoản theo loại">
          <div className="bd-panel__title"><h2>Tài khoản theo loại</h2></div>
          <ul className="bd-bars bd-bars--wide">
            {d.accounts.map((a) => (
              <li key={a.accountType}>
                <span className="bd-bars__name"><strong>{accountTypeLabel[a.accountType as AccountType] ?? a.accountType}</strong>
                  <span>{a.locked ? `${a.locked} khóa` : ''}{a.locked && a.pendingPasswordChange ? ' · ' : ''}{a.pendingPasswordChange ? `${a.pendingPasswordChange} chờ đổi mật khẩu` : ''}{!a.locked && !a.pendingPasswordChange ? 'Ổn định' : ''}</span></span>
                <Meter value={a.total ? (a.active / a.total) * 100 : 0} tone="good" height={12} />
                <b className="tabular">{fmt(a.active)}/{fmt(a.total)}</b>
              </li>
            ))}
          </ul>
        </section>

        <section className="bd-panel" aria-label="Thời khóa biểu">
          <div className="bd-panel__title"><h2>Thời khóa biểu học kỳ này</h2></div>
          {!d.semesterName ? <Empty>Hôm nay không nằm trong học kỳ nào.</Empty> : d.timetables.length === 0 ? <Empty>Chưa có phân hiệu nào.</Empty> : (
            <ul className="bd-bars bd-bars--wide">
              {d.timetables.map((t) => {
                const share = t.classCount ? Math.round((t.publishedClassCount / t.classCount) * 100) : null;
                return (
                  <li key={t.campusId}>
                    <span className="bd-bars__name"><strong>{t.name}</strong><span className="tabular">{t.publishedClassCount}/{t.classCount} lớp</span></span>
                    <Meter value={share} tone="good" height={12} />
                    <b className="tabular">{share === null ? '—' : `${share}%`}</b>
                  </li>
                );
              })}
            </ul>
          )}
        </section>
      </div>

    </div>
  );
}
