// Nguồn duy nhất của mã màu, khoảng cách, bo góc, đổ bóng (Design_Web §2–5). Component không được ghi hex trực tiếp.
// Xanh lá tươi nhạt (nút chính, nhấn), hòa với đồi và bầu trời trên màn đăng nhập; sider đậm hơn các thành phần còn lại; nền mint nhạt với sóng lượn bạc hà phía sau; hổ phách và đỏ chỉ dùng cho cảnh báo.
export const color = {
  green50: '#EEFBF3', green100: '#D8F6E3', green200: '#B2EDC8', green300: '#83DDA6',
  green500: '#47C987', green600: '#2DB872', green700: '#1D8E55', green800: '#17744A',
  // Xanh mực: số liệu lớn, avatar, thanh thao tác nổi.
  ink: '#12353A',
  // Hổ phách: dấu "đang ở đây / đang diễn ra / cần chú ý". amberInk là bản đậm để làm chữ trên nền sáng.
  amber: '#F2A93B', amberInk: '#9A5B05', amberSoft: '#FDF1DC',
  coralFg: '#E5594C', coralBg: '#FDECEA',
  amberFg: '#C97F0A', amberBg: '#FDF3E2',
  skyFg: '#2F80DB', skyBg: '#E8F2FD',
  violetFg: '#6B5CE0', violetBg: '#EEECFD',
  tealFg: '#0F9AA3', tealBg: '#E0F5F6',
  // Nền trắng lạnh phía sau, card trắng phía trên; sunken là vùng lõm nhẹ trong card.
  bg: '#F4FCF7', surface: '#FFFFFF', sunken: '#F1F8F4',
  border: '#E6EDF0', borderStrong: '#D6E0E5', borderMint: '#D0EFDC',
  // Viền ô nhập trên nền trắng: đậm hơn border một bậc để ô không chìm.
  fieldBorder: '#D6E0E5',
  text1: '#13292B', text2: '#51666A', text3: '#8A9DA2',
  logoDot: '#F5B75C',
  // Xanh non nhạt ở đáy nền trang và màn đăng nhập (đuôi gradient EMIS).
  limeSoft: '#D9F4B8',
  // Aqua sáng ở đầu gradient EMIS: góc trên tấm minh họa đăng nhập.
  aqua: '#8CF4D2',
  // Ba lớp đồi xanh lá (xa → gần) trong tấm minh họa đăng nhập; lớp gần đủ đậm để chữ trắng đọc rõ.
  meadow1: '#A6E39A', meadow2: '#5CC477', meadow3: '#1F9A55',
  disabled: '#C2CDD1', danger: '#D64535',
  tableHeaderBg: '#F4FCF7', rowHoverBg: '#F2FBF5', hoverBg: '#E6F4EC', neutralBg: '#EEF4F1',
  // Chữ và lớp phủ trên nền xanh (sider, màn đăng nhập).
  onGreenHover: 'rgba(255,255,255,.14)', onGreenMuted: 'rgba(255,255,255,.86)', onGreenLine: 'rgba(255,255,255,.22)',
} as const;

export const space = { xs: 4, sm: 8, md: 12, lg: 16, xl: 24, xxl: 32, xxxl: 48 } as const;
// xs: chip mã · sm: ô nhập, nút, ô icon sider · md: mục menu, màn đăng nhập · lg: card, hộp thoại · xl: dải đầu trang, ô bento.
export const radius = { xs: 6, sm: 8, md: 12, lg: 16, xl: 20, pill: 999 } as const;
export const shadow = {
  card: '0 1px 2px rgba(16,42,48,.04), 0 6px 20px rgba(16,42,48,.05)',
  lift: '0 2px 4px rgba(16,42,48,.04), 0 14px 32px rgba(16,42,48,.10)',
  pop: '0 8px 28px rgba(16,40,32,.12)',
  modal: '0 16px 48px rgba(16,40,32,.18)',
  // Thẻ đăng nhập nằm trên nền xanh đậm nên cần bóng sâu hơn modal.
  float: '0 24px 64px rgba(6,90,40,.24)',
} as const;

// Nền chuyển sắc: sider đặc (chữ trắng đạt AA trên cả hai đầu), nền trang có quầng xanh nhạt góc phải trên,
// dải đầu trang mint nhạt dần sang trắng, ô nhấn xanh đậm dần (chữ trắng).
export const gradient = {
  sider: 'linear-gradient(175deg, #1CAA64 0%, #2CBC74 55%, #50CD8C 100%)',
  // Ô nhấn sáng: mint nhạt sang sky nhạt, chữ tối (không còn khối xanh đậm).
  accent: 'linear-gradient(135deg, #E5F9EC 0%, #F1FCF5 100%)',
  // Nền màn đăng nhập: aqua mint ở trên chuyển sang xanh lá non nhạt ở dưới, như ảnh nền EMIS.
  authPage: 'linear-gradient(160deg, #86F2CF 0%, #A9F0C4 50%, #D2F4B2 100%)',
} as const;

export const focusRing = 'rgba(71,201,135,.24)';

// Màu nhấn theo môn học trên lưới thời khóa biểu (§9.1). Không dùng để trang trí ở nơi khác.
export type AccentName = 'green' | 'coral' | 'amber' | 'sky' | 'violet' | 'teal';
export const accent: Record<AccentName, { fg: string; bg: string }> = {
  green: { fg: color.green500, bg: color.green100 },
  coral: { fg: color.coralFg, bg: color.coralBg },
  amber: { fg: color.amberFg, bg: color.amberBg },
  sky: { fg: color.skyFg, bg: color.skyBg },
  violet: { fg: color.violetFg, bg: color.violetBg },
  teal: { fg: color.tealFg, bg: color.tealBg },
};

// Màu trạng thái (§2.4): viên nền nhạt + chấm màu + chữ đậm cùng tông.
export type BadgeGroup = 'active' | 'pending' | 'neutral' | 'danger' | 'info';
export const badgeColor: Record<BadgeGroup, { fg: string; bg: string }> = {
  active: { fg: '#FFFFFF', bg: '#1F9D5C' },
  pending: { fg: '#FFFFFF', bg: '#C26A0A' },
  neutral: { fg: '#FFFFFF', bg: '#64787D' },
  danger: { fg: '#FFFFFF', bg: '#D0392B' },
  info: { fg: '#FFFFFF', bg: '#2F7BD6' },
};

export const fontFamily = "'Be Vietnam Pro', Inter, system-ui, sans-serif";
