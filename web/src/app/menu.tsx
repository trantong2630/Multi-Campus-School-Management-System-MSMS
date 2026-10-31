import {
  BookOpen, CalendarDays, ClipboardCheck, HeartHandshake, House, LayoutDashboard, Newspaper, Settings2, ShieldCheck, Users,
} from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import type { AccountType } from '@/shared/lib/labels';
import type { UserInfo } from '@/features/iam/types';

type ScopeRequirement = 'homeroom' | 'department' | 'subject' | 'campus';
type NavigationUser = Pick<UserInfo, 'accountType' | 'scope'>;

interface MenuAccess {
  /** Mục không có quyền bị ẩn hẳn, không khóa (Conventions §4.7). */
  roles: AccountType[];
  requires?: Partial<Record<AccountType, ScopeRequirement>>;
  /** Chỉ đưa màn hình đã triển khai lên menu. */
  ready: boolean;
}

export interface MenuLeaf extends MenuAccess {
  key: string;
  label: string;
  path: string;
}

export interface MenuGroup extends Partial<MenuAccess> {
  key: string;
  label: string;
  icon: LucideIcon;
  /** Cụm trên sider: menu chia thành vài cụm có nhãn nhỏ, thứ tự các nhóm vẫn giữ nguyên. */
  section: MenuSection;
  /** Nhóm một màn hình thì không có cấp 2: dùng `path` thay `children`. */
  path?: string;
  children?: MenuLeaf[];
}

export type MenuSection = 'overview' | 'manage' | 'system';
export const sectionLabel: Record<MenuSection, string> = { overview: 'Tổng quan', manage: 'Quản lý', system: 'Hệ thống' };

const ALL: AccountType[] = ['SchoolAdmin', 'Principal', 'VicePrincipal', 'Teacher', 'Student', 'Parent'];
const MGMT: AccountType[] = ['SchoolAdmin', 'Principal', 'VicePrincipal'];
const STAFF: AccountType[] = [...MGMT, 'Teacher'];

// Cấu trúc menu theo Dev_Conventions §4.7.
export const menu: MenuGroup[] = [
  { key: 'home', label: 'Trang chủ', icon: House, section: 'overview', path: '/', roles: ALL, ready: true },
  { key: 'dashboard', label: 'Dashboard', icon: LayoutDashboard, section: 'overview', path: '/dashboard', roles: ['Principal', 'VicePrincipal'], ready: true },
  {
    key: 'sch', label: 'Thiết lập', icon: Settings2, section: 'manage',
    children: [
      { key: 'sch-school', label: 'Thông tin trường', path: '/sch/school', roles: ['SchoolAdmin', 'Principal'], ready: true },
      { key: 'sch-campus', label: 'Phân hiệu', path: '/sch/campuses', roles: ['SchoolAdmin'], ready: true },
      { key: 'sch-subject', label: 'Tổ chuyên môn & Môn học', path: '/sch/subjects', roles: ['SchoolAdmin'], ready: true },
      { key: 'sch-year', label: 'Năm học & Học kỳ', path: '/sch/years', roles: ['SchoolAdmin'], ready: true },
      { key: 'sch-holiday', label: 'Ngày nghỉ', path: '/sch/holidays', roles: ['SchoolAdmin'], ready: true },
      { key: 'sch-slot', label: 'Khung giờ tiết', path: '/sch/period-slots', roles: ['SchoolAdmin'], ready: true },
      { key: 'sch-class', label: 'Lớp & Ghi danh', path: '/sch/classes', roles: ['SchoolAdmin'], ready: true },
      { key: 'sch-rollover', label: 'Kết chuyển năm học', path: '/sch/rollover', roles: ['SchoolAdmin'], ready: false },
    ],
  },
  {
    key: 'ppl', label: 'Hồ sơ', icon: Users, section: 'manage',
    children: [
      { key: 'ppl-staff', label: 'Cán bộ', path: '/ppl/staff', roles: MGMT, ready: true },
      { key: 'ppl-student', label: 'Học sinh', path: '/ppl/students', roles: MGMT, ready: false },
      { key: 'ppl-parent', label: 'Phụ huynh', path: '/ppl/parents', roles: MGMT, ready: false },
      { key: 'ppl-resp', label: 'Trách nhiệm', path: '/ppl/responsibilities', roles: ['SchoolAdmin', 'Principal'], ready: true },
    ],
  },
  {
    key: 'scd', label: 'Thời khóa biểu', icon: CalendarDays, section: 'manage',
    children: [
      { key: 'scd-timetable', label: 'Thời khóa biểu', path: '/scd/timetable', roles: ['VicePrincipal'], requires: { VicePrincipal: 'campus' }, ready: true },
      { key: 'scd-responsibility', label: 'Phụ trách chuyên môn', path: '/scd/responsibilities', roles: ['VicePrincipal'], ready: true },
      { key: 'scd-schedule', label: 'Lịch dạy', path: '/scd/schedule', roles: ['Principal', 'VicePrincipal', 'Teacher'], requires: { VicePrincipal: 'campus' }, ready: true },
      { key: 'scd-my-class', label: 'Lịch học', path: '/scd/my-class', roles: ['Student', 'Parent'], ready: true },
      { key: 'scd-absence', label: 'Giáo viên vắng', path: '/scd/absences', roles: ['VicePrincipal'], requires: { VicePrincipal: 'campus' }, ready: true },
    ],
  },
  { key: 'att', label: 'Điểm danh', icon: ClipboardCheck, section: 'manage', path: '/att', roles: [...STAFF, 'Student', 'Parent'], ready: false },
  {
    key: 'lrn', label: 'Học tập', icon: BookOpen, section: 'manage',
    children: [
      { key: 'lrn-bank', label: 'Ngân hàng câu hỏi', path: '/lrn/questions', roles: ['Teacher'], ready: false },
      { key: 'lrn-exam', label: 'Đề', path: '/lrn/exams', roles: ['Teacher'], ready: false },
      { key: 'lrn-assign', label: 'Bài tập', path: '/lrn/assignments', roles: ['Teacher', 'Student'], requires: { Teacher: 'subject' }, ready: false },
      { key: 'lrn-ai', label: 'Nhận xét AI', path: '/lrn/ai-feedback', roles: ['Teacher'], requires: { Teacher: 'subject' }, ready: false },
      { key: 'lrn-dept', label: 'Giám sát tổ', path: '/lrn/department', roles: ['Teacher'], requires: { Teacher: 'department' }, ready: false },
    ],
  },
  {
    key: 'par', label: 'Phụ huynh', icon: HeartHandshake, section: 'manage',
    children: [
      { key: 'par-leave', label: 'Đơn xin nghỉ', path: '/par/leave-requests', roles: ['VicePrincipal', 'Teacher', 'Parent'], requires: { Teacher: 'homeroom', VicePrincipal: 'campus' }, ready: true },
      { key: 'par-report', label: 'Sổ liên lạc', path: '/par/weekly-reports', roles: ['Teacher', 'Student', 'Parent'], requires: { Teacher: 'homeroom' }, ready: false },
      { key: 'par-inbox', label: 'Hộp thư', path: '/par/inbox', roles: ['Teacher', 'Parent'], ready: true },
    ],
  },
  { key: 'com', label: 'Bảng tin', icon: Newspaper, section: 'manage', path: '/com/posts', roles: ALL, ready: false },
  {
    key: 'adm', label: 'Quản trị', icon: ShieldCheck, section: 'system',
    children: [
      { key: 'adm-account', label: 'Tài khoản', path: '/adm/accounts', roles: ['SchoolAdmin'], ready: true },
      { key: 'adm-audit', label: 'Nhật ký', path: '/adm/audit-log', roles: ['SchoolAdmin'], ready: false },
      { key: 'adm-export', label: 'Xuất dữ liệu', path: '/adm/exports', roles: ['VicePrincipal', 'Teacher'], requires: { Teacher: 'homeroom', VicePrincipal: 'campus' }, ready: false },
    ],
  },
];

function canAccess(item: Pick<MenuGroup, 'roles' | 'requires' | 'ready'>, user: NavigationUser): boolean {
  if (!item.ready || !item.roles?.includes(user.accountType)) return false;
  switch (item.requires?.[user.accountType]) {
    case 'homeroom': return !!user.scope?.homeroomClassIds.length;
    case 'department': return !!user.scope?.headDepartmentIds.length;
    case 'subject': return !!user.scope?.subjectResponsibilities.length;
    case 'campus': return !!user.scope?.ledCampusIds.length;
    default: return true;
  }
}

/** Sidebar, tìm nhanh và chặn URL dùng cùng quyền và trách nhiệm đang hiệu lực từ /auth/me. */
export function visibleMenu(user: NavigationUser): MenuGroup[] {
  const result: MenuGroup[] = [];
  for (const g of menu) {
    if (g.children) {
      const children = g.children.filter((c) => canAccess(c, user));
      // Nhóm chỉ còn một màn hình thì không có cấp 2 (Conventions §4.7): dùng thẳng tên và đường dẫn của màn hình đó.
      if (children.length === 1) result.push({ ...g, label: children[0].label, path: children[0].path, children: undefined });
      else if (children.length) result.push({ ...g, children });
    } else if (canAccess(g, user)) {
      result.push(g);
    }
  }
  return result;
}

// Trang không nằm trong sidebar (góc phải header) vẫn cần breadcrumb.
const extraCrumbs: Record<string, string[]> = {
  '/profile': ['Tài khoản', 'Hồ sơ cá nhân'],
  '/change-password': ['Tài khoản', 'Đổi mật khẩu'],
};

export function resolveCrumbs(pathname: string): string[] {
  if (extraCrumbs[pathname]) return extraCrumbs[pathname];
  for (const g of menu) {
    if (g.path === pathname) return [g.label];
    const leaf = g.children?.find((c) => c.path === pathname);
    if (leaf) return [g.label, leaf.label];
  }
  return [];
}

/** Mục bị ẩn không được mở bằng URL; hồ sơ cá nhân và đổi mật khẩu vẫn dành cho mọi người. */
export function isPathAllowed(pathname: string, user: NavigationUser): boolean {
  pathname = pathname.replace(/\/+$/, '') || '/';
  if (extraCrumbs[pathname]) return true;
  for (const g of menu) {
    if (g.path && (pathname === g.path || (g.path !== '/' && pathname.startsWith(g.path + '/')))) return canAccess(g, user);
    const leaf = g.children?.find((c) => pathname === c.path || pathname.startsWith(c.path + '/'));
    if (leaf) return canAccess(leaf, user);
  }
  return false;
}
