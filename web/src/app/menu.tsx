import {
  BookOpen, CalendarDays, ChartColumn, ClipboardCheck, FileText, HeartHandshake, House, LayoutDashboard, Mail, Newspaper, Settings2, ShieldCheck, Users,
} from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import type { AccountType } from '@/shared/lib/labels';

export interface MenuLeaf {
  key: string;
  label: string;
  path: string;
  /** Loại tài khoản thấy được mục này. Mục không có quyền bị ẩn hẳn, không khóa (Conventions §4.7). */
  roles: AccountType[];
  /** false = màn hình chưa làm, chưa đưa lên menu. Đổi thành true khi trang có thật. */
  ready: boolean;
  icon?: LucideIcon;
}

export interface MenuGroup {
  key: string;
  label: string;
  icon: LucideIcon;
  /** Cụm trên sider: menu chia thành vài cụm có nhãn nhỏ, thứ tự các nhóm vẫn giữ nguyên. */
  section: MenuSection;
  /** Nhóm một màn hình thì không có cấp 2: dùng `path` thay `children`. */
  path?: string;
  roles?: AccountType[];
  ready?: boolean;
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
  { key: 'lrn-dept', label: 'Tiến độ giao bài của tổ', icon: ChartColumn, section: 'overview', path: '/lrn/department', roles: ['Teacher'], ready: true },
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
      { key: 'scd-timetable', label: 'Thời khóa biểu', path: '/scd/timetable', roles: ['VicePrincipal'], ready: true },
      { key: 'scd-responsibility', label: 'Phụ trách chuyên môn', path: '/scd/responsibilities', roles: ['VicePrincipal'], ready: true },
      { key: 'scd-schedule', label: 'Lịch dạy', path: '/scd/schedule', roles: ['Principal', 'VicePrincipal', 'Teacher'], ready: true },
      { key: 'scd-my-class', label: 'Lịch học', path: '/scd/my-class', roles: ['Student', 'Parent'], ready: true },
      { key: 'scd-absence', label: 'Giáo viên vắng', path: '/scd/absences', roles: ['VicePrincipal'], ready: true },
    ],
  },
  { key: 'att', label: 'Điểm danh', icon: ClipboardCheck, section: 'manage', path: '/att', roles: [...STAFF, 'Student', 'Parent'], ready: false },
  {
    key: 'lrn', label: 'Bài tập rèn luyện', icon: BookOpen, section: 'manage',
    children: [
      { key: 'lrn-bank', label: 'Kho câu hỏi', path: '/lrn/questions', roles: ['Teacher'], ready: true },
      { key: 'lrn-assign', label: 'Giao bài tập', path: '/lrn/assign', roles: ['Teacher'], ready: true },
      { key: 'lrn-assigned', label: 'Bài tập đã giao', path: '/lrn/assigned', roles: ['Teacher'], ready: true },
      { key: 'lrn-todo', label: 'Bài tập được giao', path: '/lrn/todo', roles: ['Student'], ready: true },
      { key: 'lrn-done', label: 'Danh sách bài tập', path: '/lrn/done', roles: ['Student'], ready: true },
      { key: 'lrn-children', label: 'Kết quả của con', path: '/lrn/children', roles: ['Parent'], ready: true },
    ],
  },
  {
    key: 'par', label: 'Phụ huynh', icon: HeartHandshake, section: 'manage',
    children: [
      { key: 'par-leave', label: 'Đơn xin nghỉ', path: '/par/leave-requests', roles: ['VicePrincipal', 'Teacher', 'Parent'], ready: true, icon: FileText },
      { key: 'par-report', label: 'Sổ liên lạc', path: '/par/weekly-reports', roles: ['Teacher', 'Student', 'Parent'], ready: false, icon: HeartHandshake },
      { key: 'par-inbox', label: 'Hộp thư', path: '/par/inbox', roles: ['Teacher', 'Parent'], ready: true, icon: Mail },
    ],
  },
  { key: 'com', label: 'Bảng tin', icon: Newspaper, section: 'manage', path: '/com/posts', roles: ALL, ready: false },
  {
    key: 'adm', label: 'Quản trị', icon: ShieldCheck, section: 'system',
    children: [
      { key: 'adm-account', label: 'Tài khoản', path: '/adm/accounts', roles: ['SchoolAdmin'], ready: true },
      { key: 'adm-audit', label: 'Nhật ký', path: '/adm/audit-log', roles: ['SchoolAdmin'], ready: false },
      { key: 'adm-export', label: 'Xuất dữ liệu', path: '/adm/exports', roles: ['VicePrincipal', 'Teacher'], ready: false },
    ],
  },
];

/** Menu đã lọc theo loại tài khoản và theo trạng thái "đã có trang". Nhóm hết mục con thì ẩn cả nhóm. */
export function visibleMenu(type: AccountType): MenuGroup[] {
  const result: MenuGroup[] = [];
  for (const g of menu) {
    if (g.children) {
      const children = g.children.filter((c) => c.ready && c.roles.includes(type));
      if (type === 'Parent' && g.key === 'par') {
        // Đối với Phụ huynh: phẳng hóa các mục con của nhóm 'par' ra menu cấp 1 (ngang hàng với Lịch học, không cần nhóm cha "Phụ huynh").
        for (const c of children) {
          result.push({
            key: c.key,
            label: c.label,
            icon: c.icon ?? g.icon,
            section: g.section,
            path: c.path,
            roles: c.roles,
            ready: c.ready,
          });
        }
      } else if (children.length === 1) {
        // Nhóm chỉ còn một màn hình thì không có cấp 2 (Conventions §4.7): dùng thẳng tên và đường dẫn của màn hình đó.
        result.push({
          ...g,
          label: children[0].label,
          path: children[0].path,
          icon: children[0].icon ?? g.icon,
          children: undefined,
        });
      } else if (children.length) {
        result.push({ ...g, children });
      }
    } else if (g.ready && g.roles?.includes(type)) {
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

export function resolveCrumbs(pathname: string, type?: AccountType): string[] {
  if (extraCrumbs[pathname]) return extraCrumbs[pathname];
  for (const g of menu) {
    if (g.path === pathname) return [g.label];
    const leaf = g.children?.find((c) => c.path === pathname);
    if (leaf) {
      if (type === 'Parent' && g.key === 'par') {
        return [leaf.label];
      }
      return [g.label, leaf.label];
    }
  }
  // Trang con của một mục menu (vd. chi tiết bài tập) lấy breadcrumb của mục cha.
  for (const g of menu) {
    if (g.path && g.path !== '/' && pathname.startsWith(g.path + '/')) return [g.label];
    const leaf = g.children?.find((c) => pathname.startsWith(c.path + '/'));
    if (leaf) return [g.label, leaf.label];
  }
  return [];
}

/** Đường dẫn có nằm trong quyền của loại tài khoản không. Đường dẫn ngoài menu (hồ sơ cá nhân, đổi mật khẩu) ai cũng vào được. */
export function isPathAllowed(pathname: string, type: AccountType): boolean {
  for (const g of menu) {
    if (g.path && (pathname === g.path || (g.path !== '/' && pathname.startsWith(g.path + '/')))) return !!g.roles?.includes(type);
    const leaf = g.children?.find((c) => pathname === c.path || pathname.startsWith(c.path + '/'));
    if (leaf) return leaf.roles.includes(type);
  }
  return true;
}
