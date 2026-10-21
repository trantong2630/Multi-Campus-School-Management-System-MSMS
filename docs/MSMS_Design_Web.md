# MSMS — Web Design System

20/10/2026 · V1.3 · Trần Đức Kiên

**Audience: coding agents.** Companion to `MSMS_Dev_Conventions.md` (§4 = display dictionary) and Business Specification V3.

**Binding rules for any agent implementing web UI:**
1. Never hard-code a hex value in a component. Import from `shared/lib/tokens.ts`.
2. All user-visible strings are Vietnamese and come from `shared/lib/labels/`. Code identifiers stay English.
3. Ant Design is the only component library. Do not add a second one. Do not add Tailwind.
4. **One UI, many authors.** Build screens only from the shared components in §12. Never write a per-screen version of something that exists there, never set `style={{ width|marginBottom }}` on a shared control, and if a pattern is missing, add it to `shared/` (and §12) instead of copying it into a feature.
5. If a requirement here conflicts with `MSMS_Dev_Conventions.md`, that file wins — stop and report the conflict.

---

## 1. Design intent

Five adjectives: **fresh · soft · rounded · spacious · accented.** Reference: Schoolastica (solid green sider, white floating cards, pill controls) and the mobile mint header.

| Do | Don't |
| --- | --- |
| Very pale grey-green page background; content on white cards | White full-bleed page, tables flush to viewport edge |
| **Solid green floating sider** (radius 24) with white text; selected item = white pill | White or dark sider |
| Large radii (12–20px), 1px hairline borders; round (pill) header buttons | 2px default-bootstrap radii |
| Soft wide low-opacity shadows | Hard dark shadows |
| Solid color only on primary buttons; badges = tinted bg + bold text | Solid-filled badges that look clickable |
| Green leads; **sprinkle one pastel accent now and then** (a KPI card, an avatar, a chart series) | Entire app in a single green — or a rainbow with no lead color |
| Generous whitespace between blocks | Maximum data density |

---

## 2. Color tokens

### 2.1 Primary (green)

| Token | Hex | Usage |
| --- | --- | --- |
| `green50` | `#EEF8F3` | Table row hover, subtle section bg |
| `green100` | `#D8F0E5` | Badge bg, icon chip bg |
| `green200` | `#A9E0C8` | Accent borders, gradient stop |
| `green300` | `#6CCBA6` | Card header gradient, chart series |
| `green500` | `#16A47A` | Brand mark, icons, focus ring color |
| **`green600`** | **`#0E8A63`** | **Primary button fill, links, active tab** |
| `green700` | `#0B6E4F` | Primary button hover, text on green100 |
| `green800` | `#08523B` | Text on light green gradients |

**Rule:** solid fills use `green600` (white text = 4.6:1). Never fill a button with `green500` — white on it is ~3:1 and fails WCAG AA.

### 2.2 Accent colors

Used on icons, KPI cards/tiles, avatars, left card borders, hero side-cards and chart series. **Never on buttons, links, table headers or badges.**

**Sprinkling rule (soft, not rigid):** green stays the dominant color on every screen. Within one block of cards use at most **one non-green pastel per ~3 cards**, never the same accent twice side by side, and never use `coral` for anything that is not a warning. Use accent `Bg` as the fill and `Fg` only for the icon/number — never put text on an accent `Fg` fill. A screen with no data to color simply stays green.

| Name | Fg | Bg | Semantic hint |
| --- | --- | --- | --- |
| `coral` | `#E0574B` | `#FDEDEB` | Soft warning, pending sessions |
| `amber` | `#C97F0A` | `#FDF3E2` | Awaiting approval, nearing deadline |
| `sky` | `#2E74B5` | `#E9F2FA` | Neutral information |
| `violet` | `#6A5BC9` | `#EFEDFB` | Learning / assignments |
| `teal` | `#128E90` | `#E3F4F4` | Attendance |

### 2.3 Neutrals

| Token | Hex | Usage |
| --- | --- | --- |
| `bg` | `#F1F5F2` | Page background |
| `surface` | `#FFFFFF` | Cards, tables, modals |
| `border` | `#E8ECEA` | Card borders, table rules |
| `text1` | `#16211C` | Headings, values |
| `text2` | `#5B6B64` | Labels, descriptions |
| `text3` | `#92A09A` | Placeholder, meta |
| `disabled` | `#C4CCC8` | Disabled fg |
| `danger` | `#D64535` | Destructive button fill |

### 2.4 Status badge palette

Conventions §4.4 says "green = active". Green is also the brand color, so a badge and a button would look identical.

**Resolution — enforce by shape, not hue:**
- Badge = **pill** (radius 999) + tinted background + bold text + **no border, no icon, no dot**.
- Button = solid fill, radius 12 (never a pill).

Map every status label from Conventions §4.4 to exactly one of these five groups:

| Group | Fg | Bg | Vietnamese labels in this group |
| --- | --- | --- | --- |
| `active` | `#0B6E4F` | `#D8F0E5` | Đang hoạt động · Đã xếp lịch · Sẵn dùng · Đã phát hành · Đã duyệt · Đã giao · Đã công bố · Đang học · Đã đăng · Có mặt |
| `pending` | `#9A6206` | `#FDF3E2` | Chờ duyệt · Chờ xử lý · Chờ đổi mật khẩu |
| `neutral` | `#5B6B64` | `#EFF2F0` | Nháp · Đã ẩn · Chưa điểm danh · Đã rút · Đã đóng · Không nộp · Ngừng hoạt động |
| `danger` | `#B3382C` | `#FDEDEB` | Đã hủy · Đã khóa · Lỗi · Đã từ chối · Vắng không phép · Đã chuyển trường |
| `info` | `#2E74B5` | `#E9F2FA` | Sinh tự động · Chỉnh tay · Muộn · Vắng có phép · Đã tốt nghiệp |

`Vắng có phép` is deliberately **not** in `danger` — an excused absence is not a fault.

Implement as a single `statusGroup(enumValue): BadgeGroup` function next to `labels.ts`. No per-screen mapping.

---

## 3. Typography

Font: **Be Vietnam Pro**, fallback `Inter, system-ui, sans-serif`. Load weights 400 / 500 / 600 only. The one exception is the brand wordmark (§3.1).

| Role | Size / Line | Weight |
| --- | --- | --- |
| Page title | 24 / 32 | 600 |
| Card title | 16 / 24 | 600 |
| Body, table cell | 14 / 22 | 400 |
| Form label, table header | 13 / 20 | 500 |
| Caption, meta | 12 / 18 | 400 |
| KPI number | 30 / 36 | 600 |

### 3.1 Brand wordmark (the MSMS logo)

**One logo for the whole product — web and mobile.** It is the text `msms.`, drawn with type, never an image file.

| Part | Spec |
| --- | --- |
| Font | **Montserrat**, Latin subset only, weights 700 and 300. Used for the logo and nowhere else |
| First "ms" | weight 700 |
| Second "ms" | weight 300 |
| Dot | weight 700, amber `logoDot` (`#F5B75C`) |
| Tracking / leading | letter-spacing −0.05em, line-height 1 |
| `tone="green"` | text `green500` — on white or pale backgrounds (login left half, light headers) |
| `tone="white"` | text `surface` — on green backgrounds (sider, login right half, mobile header); the dot stays amber |

Rules: render it only through `shared/components/BrandLogo.tsx` (`tone`, `size` = font-size in px). Never export it as PNG/SVG, recolor it, change the weights, add a tagline or the school name beside it, or typeset it in Be Vietnam Pro. Standard sizes: sider 42, login 96. The school's own logo and name (SCH-07) are a separate identity and never replace the wordmark.

**Rule:** any column containing numbers, scores, times, or codes sets `font-variant-numeric: tabular-nums`. Without it columns jitter while scrolling.

---

## 4. Spacing, radius, elevation

**Spacing scale (px):** `4 · 8 · 12 · 16 · 24 · 32 · 48`. Values outside this scale are not allowed.

| Radius | px | Applies to |
| --- | --- | --- |
| `sm` | 10 | Input, select, tag, sider icon tile |
| `md` | 12 | Button, sider item |
| `lg` | 20 | Card, modal, table container |
| `pill` | 999 | Filter chip, avatar, status badge, header icon buttons, user chip |

| Shadow | Value |
| --- | --- |
| `card` | `0 2px 4px rgba(16,40,32,.03), 0 10px 28px rgba(16,40,32,.07)` |
| `pop` | `0 8px 28px rgba(16,40,32,.12)` |
| `modal` | `0 16px 48px rgba(16,40,32,.18)` |

**Rule:** every card uses shadow **and** a 1px `border` line. Shadow alone disappears against the `bg` tone.

---

## 5. Token implementation

Create `web/src/shared/lib/tokens.ts` exporting a plain object, and feed it into AntD's `ConfigProvider`. Nothing else reads raw hex.

```ts
// web/src/shared/lib/tokens.ts
export const color = {
  green50:'#EEF8F3', green100:'#D8F0E5', green200:'#A9E0C8', green300:'#6CCBA6',
  green500:'#16A47A', green600:'#0E8A63', green700:'#0B6E4F', green800:'#08523B',
  coralFg:'#E0574B', coralBg:'#FDEDEB',
  amberFg:'#C97F0A', amberBg:'#FDF3E2',
  skyFg:'#2E74B5',   skyBg:'#E9F2FA',
  violetFg:'#6A5BC9',violetBg:'#EFEDFB',
  tealFg:'#128E90',  tealBg:'#E3F4F4',
  bg:'#F1F5F2', surface:'#FFFFFF', border:'#E8ECEA',
  text1:'#16211C', text2:'#5B6B64', text3:'#92A09A',
  disabled:'#C4CCC8', danger:'#D64535',
} as const;

export const space = { xs:4, sm:8, md:12, lg:16, xl:24, xxl:32, xxxl:48 } as const;
export const radius = { sm:10, md:12, lg:20, pill:999 } as const;
export const shadow = {
  card:'0 2px 4px rgba(16,40,32,.03), 0 10px 28px rgba(16,40,32,.07)',
  pop:'0 8px 28px rgba(16,40,32,.12)',
  modal:'0 16px 48px rgba(16,40,32,.18)',
} as const;

// Sider/hero gradients; text on sider = white (AA on both ends), text on hero = green800.
export const gradient = {
  sider:'linear-gradient(170deg, #27B084 0%, #16996F 100%)',
  hero:'linear-gradient(135deg, #8FDCC0 0%, #CFF0E2 100%)',
} as const;
```

```ts
// web/src/app/antdTheme.ts
import { theme } from 'antd';
import { color, radius } from '@/shared/lib/tokens';

export const antdTheme = {
  algorithm: theme.defaultAlgorithm,
  token: {
    colorPrimary: color.green600,
    colorLink: color.green600,
    colorError: color.danger,
    colorBgLayout: color.bg,
    colorBgContainer: color.surface,
    colorBorderSecondary: color.border,
    colorText: color.text1,
    colorTextSecondary: color.text2,
    colorTextTertiary: color.text3,
    borderRadius: radius.md,
    borderRadiusLG: radius.lg,
    borderRadiusSM: radius.sm,
    fontFamily: "'Be Vietnam Pro', Inter, system-ui, sans-serif",
    fontSize: 14,
    controlHeight: 36,
    wireframe: false,
  },
  components: {
    Table:  { headerBg: '#FAFBFB', rowHoverBg: color.green50, headerSplitColor: 'transparent' },
    Layout: { siderBg: 'transparent', headerBg: 'transparent', bodyBg: color.bg },
    Menu:   { itemSelectedBg: color.surface, itemSelectedColor: color.green700, itemHeight: 46 },
    Card:   { paddingLG: 24 },
  },
};
```

Wrap the app with `<ConfigProvider theme={antdTheme} locale={viVN}>` and call `dayjs.locale('vi')` once in `app/`.

---

## 6. Layout shell

```
 ┌─ Sider 248 ──┐   Breadcrumb                    ( bell ) ( avatar · name )
 │ floating     │  ┌──────────────────────────────────────────────────┐
 │ green panel  │  │  Hero / page title                               │
 │ radius 24    │  ├──────────────────────────────────────────────────┤
 │ inset 12     │  │  Filter bar (card)                               │
 │              │  ├──────────────────────────────────────────────────┤
 │              │  │  Content (card)                                  │
 └──────────────┘  └──────────────────────────────────────────────────┘
```

- Page background is `bg` with a faint `green100` radial glow top-right. Content padding `24`, max width `1440`, centered.
- **All content sits inside a white card** (radius 20). Never render a bare table on the page background.
- Sider is a **solid green panel** (`gradient.sider`, clearly lighter than `green600` so it feels airy; white 14px/500 text is only ~2.6–3.4:1 on it, accepted by the owner as a style trade-off — keep text weight ≥ 500), inset 12 from the viewport edges, radius 24, shadow `pop`, sticky. Do not use a dark or white sider.
- Header is transparent over `bg`, height 72, no bottom border.

### 6.0 Auth layout (login, forced password change)

All pre-login screens use `app/AuthLayout.tsx` — never a bespoke page. Full-viewport split, no sider/header:

- **Left (50%)**: white, only the MSMS wordmark (§3.1, `<BrandLogo tone="green" size={96} />`, centered). No taglines, chips or decorative text — the screen carries nothing but the logo and the form. Hidden below 900px.
- **Right (50%)**: green panel, radius 32, inset 12, `gradient.sider`, shadow `pop`. Content column max 400, centered: title 28/600 white, optional one-line description (≤ 6 words, `onGreenMuted`; e.g. "Chào mừng" / "Đăng nhập vào tài khoản của bạn"), labels white, inputs stay white (AntD default) so contrast holds.
- The submit button on this panel is **inverted**: white fill, `green700` text, height 44, full width (the one exception to §8.1 primary = green fill, because green-on-green is invisible).
- Errors use `InlineAlert` (§8.7) directly above the button. Secondary actions (Đăng xuất) are link buttons in white.

### 6.1 Sider — two-level menu

Structure is fixed by Conventions §4.7. This section fixes only appearance. All text and icons are white on the green panel.

| Element | Spec |
| --- | --- |
| L1 item | height 46, radius 12, white icon left in a 32×32 tile, label 14/500, chevron right |
| Icon tile | transparent; `rgba(255,255,255,.12)` when the group is open. Module accent colors are **not** used in the sider |
| L2 item | height 38, indent-left 36, long labels truncate with an ellipsis on the label only (never clip a glyph), 5px round bullet, **no icon**, label 13/400 at 78% white; a 1px vertical line at 18% white joins the items |
| Selected | **white pill** (radius 12), text `green700` weight 600, icon/bullet `green600` |
| Parent of selected | white text weight 600, transparent bg, auto-expanded |
| Hover | bg `rgba(255,255,255,.12)` |
| No permission | **Not rendered at all** — never rendered-then-disabled |
| Collapsed | 64px rail, icons only, submenu opens as a popover |

Sider header (height 84, no divider) shows the **MSMS wordmark** (§3.1, `<BrandLogo tone="white" size={42} />`, centered in an 84px header); the tooltip carries the school name from SCH-07. When collapsed to the 64px rail it shows the school logo (32×32), or the **white tile with a green graduation cap** when none is saved. Logo spec is §3.1.

Sider footer holds the collapse button. The **current academic year selector** (read-only for every account type except School Admin) also lives here once the academic-year feature ships; all data is scoped by academic year.

### 6.2 Header

Height 72, transparent over `bg`. Left: 2-level breadcrumb, then a 1px divider and the school name from SCH-07 (14/600 `text1`, ellipsis; hidden until a name is saved). Right, in order: **quick search pill (when built) · notification bell with count badge · user chip**. The bell is a 40×40 white circle with 1px `border` and shadow `card`; the user chip is a white pill (height 48) holding a 32px avatar, name and account type.

Avatar menu: `Hồ sơ cá nhân` · `Đổi mật khẩu` · `Đăng xuất`.

Render the account type under the name at 12px `text2`. Six account types with materially different permissions exist; the user must be able to see which one they are.

---

## 7. Module accent map

Each module gets one accent, applied **only** to its KPI icon tiles, left card borders and chart series (the sider is solid green, so its icons are white). Backgrounds, buttons, and badges stay global.

| Module | Menu (VI) | Accent |
| --- | --- | --- |
| ADM Dashboard | Dashboard | green |
| SCH | Thiết lập | sky |
| PPL | Hồ sơ | teal |
| SCD | Thời khóa biểu | violet |
| ATT | Điểm danh | green |
| LRN | Học tập | amber |
| PAR | Phụ huynh | coral |
| COM | Bảng tin | sky |
| ADM | Quản trị | teal |

**Cost:** one extra axis to remember. **Benefit:** a 9-item sider is not swallowed by a single hue, and users locate by color faster than by reading.

---

## 8. Components

### 8.1 Buttons

| Variant | Spec |
| --- | --- |
| Primary | fill `green600`, white text, height 36, radius `md`, h-padding 16, soft green shadow `0 6px 14px rgba(22,164,122,.15)` |
| Default | white fill, 1px `border`, text `text1` |
| Text | no fill/border, text `green600` |
| Danger | fill `danger`, white text — **only** for Hủy tiết, Khóa tài khoản, Xóa |
| Icon-only | 36×36, radius `md`, tooltip mandatory |

**Rules:**
- Exactly **one** primary button per screen. Everything else is Default.
- The dialog-dismiss button is always labelled **"Đóng"**, never "Hủy" (Conventions §4.5 — "Hủy" means cancelling a teaching session).

### 8.2 Tables

The screen users spend most time in. Follow exactly.

| Aspect | Spec |
| --- | --- |
| Header | height 40, bg `#FAFBFB`, 13/500 `text2`, sticky on scroll |
| Row | height 44, 1px bottom `border`, hover bg `green50` |
| Zebra striping | **Not used** — hairline + hover is sufficient |
| Alignment | text left · numbers and scores right · badges and actions center |
| Action column | sticky right, icon-only, max 3 icons then collapse into "…" |
| Multi-select | checkbox first column; once rows are selected show a **floating action bar** docked to the viewport bottom, not scattered buttons |
| Pagination | bottom right, default 20 rows |
| Empty | 48px grey icon + one sentence + primary action |
| Loading | 5-row skeleton. **Never** a centered spinner |

Column headers use the short forms from Conventions §4.6: `Lớp · Môn · Giáo viên · Ngày · Tiết · Trạng thái`.

### 8.2b People rows (avatar cell)

Any table or list whose rows are people (staff, students, parents, accounts) leads with a **person cell** — the Gymerlean pattern, not a full clone:

- 36px round avatar (initial of the given name) on an accent `Bg` with `Fg` letter; the accent is picked from a stable hash of the name so the list has gentle color variety. If a photo exists, show the photo.
- Name 14/500 `text1`; below it one 12px `text3` line (person code or email, `tabular-nums`). Inactive people: name in `text3`.
- The code/email moves into this cell — do not keep a separate "Mã hồ sơ" column next to it.
- Status is a pill badge (§8.5); row actions are quiet icon buttons on the right.
- Implemented once as `shared/components/PersonCell.tsx`. No per-screen avatar code.

### 8.3 Filter bar

Its own card directly above the table, padding 16, controls height 36, gap 12. **Control widths are fixed by `FilterBar` CSS, not by the screen:** every `Select` is 220, the search input is 280. Do not pass `style={{ width }}` to controls inside a `FilterBar`. Active filters render below as `pill` chips with an × plus a "Xóa tất cả" link. Do not hide filters in a drawer — nearly every MSMS screen filters by academic year / campus / class.

### 8.4 Forms and modals

- Range pickers: the default AntD arrow icon loses a stroke at this height, so the separator is a plain `→` glyph in `text3` (global CSS in `index.css`). Do not reintroduce the icon.

- Label **above** the input, not inline-left. Input height 36, radius `sm`.
- Focus: border `green500` + 3px ring `rgba(22,164,122,.15)`.
- Error: border `danger`, 12px message below the field. The message must state **cause and remedy**, not just failure (Conventions §4.6).
- Modal: radius `lg`, width 520 (standard) / 720 (contains a table). Footer right-aligned, Đóng left of Lưu.
- **Irreversible actions** (cancel session, publish timetable, declare ad-hoc holiday, lock account) require a confirm modal containing a **numeric impact summary**, e.g. `"Sẽ hủy 23 tiết của 6 lớp."`

### 8.5 Status badge

Height 22, radius `pill`, h-padding 10, text 12/500, tinted bg per §2.4. No border, no dot, no icon. Rendered by one shared `<StatusBadge value={enum} />` that calls `statusGroup()` and `labels.ts`.

### 8.5b Hero banner

Rendered by `shared/components/HeroBanner.tsx` (props: `eyebrow`, `title`, `description`, `extra`). Top of Trang chủ and Dashboard: card radius `lg`, padding 32, background `gradient.hero`, text `green800` (never white), greeting 24/600 with the weekday + date line above it, and two translucent white circles as decoration (same language as the mobile header).

### 8.6 KPI card (Dashboard)

Rendered by `shared/components/StatCard.tsx` (props: `label`, `value`, `icon`, `tone`, `pastel`, `onClick`) laid out in a `.stat-grid`; never hand-build a KPI card.

White card, radius `lg`, padding 24. Optional pastel variant: background = accent `*Bg`, icon in a white tile, as in the mobile quick-action grid. A dashboard mixes white and pastel cards under the sprinkling rule in §2.2 — e.g. 4 white KPI cards plus one `violet` or `amber` highlight card, or a gradient feature card (`green300` → `green600`, white text only when the gradient is the deep end) beside the hero. Top: 40×40 icon tile, radius 10, bg = module accent bg. Middle: value at 30/600. Bottom: label 13 `text2`. Whole card is clickable; hover raises shadow to `pop`.

The six ADM-01 metrics lay out 3-up on desktop, 2-up below 1440px.

### 8.7 System feedback

| Channel | When |
| --- | --- |
| Toast, top-right | Saved, deleted — auto-dismiss 3s |
| In-page alert | Must-read warnings: teacher conflicts, sessions about to be deleted |
| Modal | Irreversible actions only |

In-page alerts are always rendered with `shared/components/InlineAlert.tsx` (icon on, 16px space below built in) — never AntD `Alert` with a hand-set `marginBottom`.

Never use a toast for an error the user must fix — that error has to stay on screen.

---

## 9. Three screens that need explicit specs

### 9.1 Timetable grid (SCD-01)

Sticky header row **and** sticky first column. Rows = Thứ × Buổi × Tiết, columns = classes.

- Filled cell: bg `green50`, two lines — subject 13/600, teacher 11 `text2`.
- Empty cell: white, shows a faint `+` on hover.
- Morning/Afternoon separated by a thin band row, bg `#F1F5F3`.
- Conflicting cell: 2px `danger` border + red dot top-right.

### 9.2 Import error table (BR-11, BR-12)

Top panel in `danger` tint: `"Từ chối 1 file · 7 lỗi"`. Error table has 4 columns: **Ô/Dòng · Cột · Giá trị gốc · Nguyên nhân**. Cell addresses render in monospace (`D12`). Clicking an error row scrolls the file preview to that cell.

The "Đã có, bỏ qua" section is a separate collapsed table below, neutral grey — it is not an error.

### 9.3 Attendance (ATT-01)

Students render as **row cards**, not a table: avatar + name on the left, three segmented buttons **Có mặt / Muộn / Vắng** on the right, colored per §2.4.

Top bar shows live counts per status. When School Admin is backfilling, show an amber banner: `"Nhập bù cho tiết ngày 12/10 — ghi nhận người sửa."`

---

## 10. Responsive and accessibility

MSMS web is a workstation UI. **Do not build a mobile layout.** Minimum breakpoint **1280px**; below it render a "use a wider screen" notice instead of letting the layout break.

- Every action reachable by keyboard; focus ring always visible.
- Color is never the sole signal — badges always carry text.
- Minimum text contrast 4.5:1.

---

## 11. Out of scope

| Excluded | Reason |
| --- | --- |
| Tailwind alongside AntD | Two resets and two spacing systems; debugging cost exceeds benefit |
| A second UI library (shadcn, MUI) | Duplicate role, divergent theming |
| Dark mode | Doubles tokens and QA; not an assessment criterion |
| Complex animation | Keep 150ms transitions for hover and menu expansion only |
| Hand-drawn illustration | Use a free set (unDraw / Storyset) recolored to `green300`, empty states only |

Only two additional libraries are permitted: `lucide-react` (icons) and `recharts` (Dashboard charts).

---

## 12. Shared component registry

Check here before writing any UI. Everything below already exists; use it as-is.

| Need | Use | Location |
| --- | --- | --- |
| Page title + actions | `PageHeader` | `shared/components/` |
| Greeting banner | `HeroBanner` | `shared/components/` |
| KPI / count tile | `StatCard` | `shared/components/` |
| Filter card above a table | `FilterBar` (fixed control widths) | `shared/components/` |
| Table | `DataTable` (skeleton, empty state, pagination) | `shared/components/` |
| Person row | `PersonCell` | `shared/components/` |
| Status pill | `StatusBadge` | `shared/components/` |
| Row action icons | `RowActions` | `shared/components/` |
| Create / edit dialog | `FormModal` | `shared/components/` |
| In-page alert | `InlineAlert` | `shared/components/` |
| File picker, import errors | `FileButton`, `ImportErrorPanel` | `shared/components/` |
| Pre-login screen | `AuthLayout` | `app/` |

Full-width form controls (`DatePicker`, `InputNumber`, `TimePicker`) inside a form use `style={{ width: '100%' }}` — the only inline style allowed on a shared control. Any other one-off spacing goes in the feature's own `.css`.
