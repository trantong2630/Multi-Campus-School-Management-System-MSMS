// Nguồn duy nhất của mã màu, khoảng cách, bo góc, đổ bóng (Design_Web §2–5). Component không được ghi hex trực tiếp.
export const color = {
  green50: '#EEF8F3', green100: '#D8F0E5', green200: '#A9E0C8', green300: '#6CCBA6',
  green500: '#16A47A', green600: '#0E8A63', green700: '#0B6E4F', green800: '#08523B',
  coralFg: '#E0574B', coralBg: '#FDEDEB',
  amberFg: '#C97F0A', amberBg: '#FDF3E2',
  skyFg: '#2E74B5', skyBg: '#E9F2FA',
  violetFg: '#6A5BC9', violetBg: '#EFEDFB',
  tealFg: '#128E90', tealBg: '#E3F4F4',
  bg: '#F1F5F2', surface: '#FFFFFF', border: '#E8ECEA',
  text1: '#16211C', text2: '#5B6B64', text3: '#92A09A',
  logoDot: '#F5B75C',
  disabled: '#C4CCC8', danger: '#D64535',
  // Giá trị phụ xuất hiện trong spec (header bảng, hover menu, nền badge trung tính)
  tableHeaderBg: '#F7FAF8', hoverBg: '#EAF1ED', neutralBg: '#EFF2F0',
  // Chữ và lớp phủ trên nền sider xanh
  onGreenHover: 'rgba(255,255,255,.12)', onGreenMuted: 'rgba(255,255,255,.78)', onGreenLine: 'rgba(255,255,255,.18)',
} as const;

export const space = { xs: 4, sm: 8, md: 12, lg: 16, xl: 24, xxl: 32, xxxl: 48 } as const;
export const radius = { sm: 10, md: 12, lg: 20, pill: 999 } as const;
export const shadow = {
  card: '0 2px 4px rgba(16,40,32,.03), 0 10px 28px rgba(16,40,32,.07)',
  pop: '0 8px 28px rgba(16,40,32,.12)',
  modal: '0 16px 48px rgba(16,40,32,.18)',
} as const;

// Nền chuyển sắc: sider đặc (chữ trắng đạt AA trên cả hai đầu), hero nhạt (chữ green800).
export const gradient = {
  sider: 'linear-gradient(170deg, #27B084 0%, #16996F 100%)',
  hero: 'linear-gradient(135deg, #8FDCC0 0%, #CFF0E2 100%)',
  page: 'radial-gradient(720px 360px at 100% 0%, #DDF3E8 0%, transparent 70%)',
} as const;

export const focusRing = 'rgba(22,164,122,.15)';

// Màu nhấn theo module (§2.2, §7): chỉ dùng cho icon, ô icon KPI, viền trái card, biểu đồ.
export type AccentName = 'green' | 'coral' | 'amber' | 'sky' | 'violet' | 'teal';
export const accent: Record<AccentName, { fg: string; bg: string }> = {
  green: { fg: color.green500, bg: color.green100 },
  coral: { fg: color.coralFg, bg: color.coralBg },
  amber: { fg: color.amberFg, bg: color.amberBg },
  sky: { fg: color.skyFg, bg: color.skyBg },
  violet: { fg: color.violetFg, bg: color.violetBg },
  teal: { fg: color.tealFg, bg: color.tealBg },
};

// Bảng màu badge trạng thái (§2.4): nền nhạt + chữ đậm, không viền, không icon.
export type BadgeGroup = 'active' | 'pending' | 'neutral' | 'danger' | 'info';
export const badgeColor: Record<BadgeGroup, { fg: string; bg: string }> = {
  active: { fg: '#0B6E4F', bg: '#D8F0E5' },
  pending: { fg: '#9A6206', bg: '#FDF3E2' },
  neutral: { fg: '#5B6B64', bg: '#EFF2F0' },
  danger: { fg: '#B3382C', bg: '#FDEDEB' },
  info: { fg: '#2E74B5', bg: '#E9F2FA' },
};

export const fontFamily = "'Be Vietnam Pro', Inter, system-ui, sans-serif";
