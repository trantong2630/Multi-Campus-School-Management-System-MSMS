# MSMS — Web Design System

24/10/2026 · V1.4 · Trần Đức Kiên

**Audience: coding agents.** Companion to `MSMS_Dev_Conventions.md` (§4 = display dictionary) and Business Specification V3.

**Binding rules for any agent implementing web UI:**
1. Never hard-code a hex value in a component. Import from `shared/lib/tokens.ts`.
2. All user-visible strings are Vietnamese and come from `shared/lib/labels/`. Code identifiers stay English.
3. Ant Design is the only component library. Do not add a second one. Do not add Tailwind.
4. **One UI, many authors.** Build screens only from the shared components in §12. Never write a per-screen version of something that exists there, never set `style={{ width|marginBottom }}` on a shared control, and if a pattern is missing, add it to `shared/` (and §12) instead of copying it into a feature.
5. If a requirement here conflicts with `MSMS_Dev_Conventions.md`, that file wins — stop and report the conflict.

---

## 1. Design intent

Emerald leads; the page stays light and airy. Five adjectives: **fresh · layered · structured · precise · warm.** Character comes from tinted surfaces, rhythm, a notched sider and a small supporting palette — never from decorative icons or ornaments.

| Do | Don't |
| --- | --- |
| Cool-white page (`bg`) with a **soft aqua sky and low green hills** behind the content; content in **white cards** (radius 20, hairline border, very soft shadow) | Bare black text on one flat white sheet; cream or beige backgrounds; heavy decoration |
| **Solid emerald sider** (light at the bottom) flush to the left edge with a rounded right edge; the selected item is a **notched tab** that melts into the page | White or dark sider; a tab that jumps |
| Every page opens with a large **title block** on the page background: title, description, multi-tone summary chips, actions | A boxed banner around the title |
| **Lists are rows of separate white cards** (radius 16, 10 apart), not a ruled grid | Dense ruled tables |
| **One main color (emerald) plus four supporting tones** (sky, violet, amber, teal; coral only for danger) used in small doses: stat icons, chips, avatars, chart bars, calendar leaves | A rainbow, saturated blocks, or tones on large surfaces |
| Each Dashboard / Trang chủ screen has **exactly one tinted tile** (`gradient.accent`: pale mint → pale sky, dark text) inside a bento grid | Solid dark-green blocks; several tinted tiles competing |
| Icons where they do a job: menu, buttons, row actions, and the tinted icon chip on a stat tile | Decorative circles and orbs |
| **Amber is the marker color** — "you are here / happening now / needs attention" — and the tone of the "Đang diễn ra" pill | Amber for buttons or links |

Rough proportion: 60% white and cool-white surfaces, 30% emerald, 10% supporting tones.

---

## 2. Color tokens

### 2.1 Brand and ink

| Token | Hex | Usage |
| --- | --- | --- |
| `green50` | `#EEFBF3` | Selected table row, code chips, timetable day column, icon-button hover |
| `green100` | `#D8F6E3` | Section chip, timetable subject tint |
| `green200` | `#B2EDC8` | Hover border of a list row, selected-row border, login art panel |
| `green300` | `#83DDA6` | Hover border of the account chip, login hover border |
| `green500` | `#47C987` | Brand wordmark, active status dot, progress ring, selected-row bar, account avatar, selected filter segment, focus ring color |
| **`green600`** | **`#2DB872`** | **Primary button fill, links, selected sider icon** |
| `green700` | `#1D8E55` | Selected sider text, "Xem →" links, clock, status text |
| `green800` | `#17744A` | Section-chip text, code-chip text, text on light green gradients |
| `ink` | `#12353A` | KPI numbers, floating action bar |

**Rule:** solid fills use `green600` (white text ≈ 3.6:1 on a bold 14px button). Never fill a button with `green500`.

### 2.2 Amber — the marker

The amber dot of the `msms.` wordmark is promoted to a system-wide marker. It means exactly one thing: **"here / now / needs attention"**.

| Token | Hex | Usage |
| --- | --- | --- |
| `amber` | `#F2A93B` | Dots: current period on the timeline, attention KPI; selected count on the floating bar |
| `amberInk` | `#9A5B05` | Text on light backgrounds when amber must be read ("Đang diễn ra", attention KPI value) |
| `amberSoft` | `#FDF1DC` | Pill background behind `amberInk` text ("Đang diễn ra"); halo of the current-period dot |

`logoDot` (`#F5B75C`) belongs to the `msms.` wordmark only; never reuse it as a decorative full stop elsewhere. Never use amber for buttons or links.

### 2.2b Supporting accents

Six accents — `green`, `sky`, `violet`, `amber`, `teal`, `coral` (`*Fg` / `*Bg` pairs in `tokens.ts`, exposed as `accent`) — add variety around the mint-jade core. They tell things apart; they do not tag modules.

| Where | How |
| --- | --- |
| Timetable grid | one accent per subject (§9.1) |
| Stat tiles (§8.6) | the icon chip takes the tile's tone |
| Person avatars (§8.2b) | tone chosen by a hash of the name, so a person keeps one color everywhere |
| Dashboard grade chart | one tone per grade |
| Trang chủ holiday leaves | leaves cycle green → sky → violet → amber |

`coral` is reserved for danger and subject variety; do not use it for decoration. Accents sit on small surfaces only (chips, icon tiles, bars) in their pale `*Bg` fill with the `*Fg` ink.

### 2.3 Neutrals

| Token | Hex | Usage |
| --- | --- | --- |
| `bg` | `#F4FCF7` | Page background (the sky-and-hills backdrop sits on it) and the selected sider tab |
| `surface` | `#FFFFFF` | Cards, list rows, bento tiles, modals, inputs |
| `sunken` | `#F1F8F4` | Inset areas (logo box, timetable header), command-palette key cap |
| `border` | `#E6EDF0` | Card and row borders |
| `borderStrong` | `#D6E0E5` | Input outlines, timetable frame |
| `borderMint` | `#D0EFDC` | Mint hairlines on tinted surfaces |
| `fieldBorder` | `#D6E0E5` | Login input outline (§6.0) |
| `tableHeaderBg` | `#F4FCF7` | Sticky list-header strip (same as `bg`) |
| `rowHoverBg` | `#F2FBF5` | List row hover |
| `hoverBg` | `#E6F4EC` | Header button hover, segmented track, empty timetable cell hover |
| `limeSoft` | `#D9F4B8` | Pale lime tail of the page backdrop and login gradient (never a fill for text, buttons or status) |
| `aqua` | `#8CF4D2` | Top of the login art panel's sky |
| `meadow1`–`meadow3` | `#A6E39A` / `#5CC477` / `#1F9A55` | Far, middle and near hills of the login art panel; `meadow3` is dark enough for the white wordmark |
| `neutralBg` | `#EEF4F1` | Inactive avatars, "Tiếp theo" pill, empty progress track |
| `text1` | `#13292B` | Headings, body text, values |
| `text2` | `#51666A` | Labels, descriptions |
| `text3` | `#8A9DA2` | Placeholder, meta, list column headers |
| `disabled` | `#C2CDD1` | Disabled fg |
| `danger` | `#D64535` | Destructive button fill |

### 2.4 Status palette

Conventions §4.4 says "green = active". Green is also the brand color, so a status and a button could look alike.

**Resolution — enforce by shape:**
- Status = **tag** (radius 8, height 26) with a solid mid-tone fill, a 14px group icon and 12/600 white text. No border.
- Button = solid fill or outline, radius 12 (never a pill).

Map every status label from Conventions §4.4 to exactly one of these five groups:

| Group | Text | Background | Vietnamese labels in this group |
| --- | --- | --- | --- |
| `active` | `#FFFFFF` | `#1F9D5C` | Đang hoạt động · Đã xếp lịch · Sẵn dùng · Đã phát hành · Đã duyệt · Đã giao · Đã công bố · Đang học · Đã đăng · Có mặt |
| `pending` | `#FFFFFF` | `#C26A0A` | Chờ duyệt · Chờ xử lý · Chờ đổi mật khẩu |
| `neutral` | `#FFFFFF` | `#64787D` | Nháp · Đã ẩn · Chưa điểm danh · Đã rút · Đã đóng · Không nộp · Ngừng hoạt động |
| `danger` | `#FFFFFF` | `#D0392B` | Đã hủy · Đã khóa · Lỗi · Đã từ chối · Vắng không phép · Đã chuyển trường |
| `info` | `#FFFFFF` | `#2F7BD6` | Sinh tự động · Chỉnh tay · Muộn · Vắng có phép · Đã tốt nghiệp |

`Vắng có phép` is deliberately **not** in `danger` — an excused absence is not a fault.

Implement as a single `statusGroup(enumValue): BadgeGroup` function next to `labels.ts`. No per-screen mapping.

---

## 3. Typography

One family: **Be Vietnam Pro**, fallback `Inter, system-ui, sans-serif`, weights 400 / 500 / 600 / 700 (700 only for titles and large numbers: page titles, the greeting, KPI and stat-tile numbers, the progress ring, the clock, the semester name, calendar-leaf day numbers, stat-chip numbers). The one exception is the brand wordmark (§3.1).

| Role | Size / Line | Weight |
| --- | --- | --- |
| Accent-tile number | 56 / 64, letter-spacing −0.03em | 700 |
| KPI number | 36 / 44, letter-spacing −0.03em | 700 |
| Page title | 32 / 40, letter-spacing −0.025em | 700 |
| Greeting (Trang chủ) | 34 / 42, letter-spacing −0.02em | 700 |
| Card title | 16 / 24 | 600 |
| Modal, drawer title | 18 | 600 |
| Body, table cell | 14 / 22 | 400 |
| Form label, section line | 13 / 20 | 500–600 |
| Table header | 12 / 18 | 600 |
| Sider group label | 11 / 16, uppercase, letter-spacing .08em | 600 |
| Caption, meta | 12 / 18 | 400 |

Table headers are sentence case. The only uppercase text in the app is the sider group label.

### 3.1 Brand wordmark (the MSMS logo)

**One logo for the whole product — web and mobile.** It is the text `msms.`, drawn with type, never an image file.

| Part | Spec |
| --- | --- |
| Font | **Montserrat**, Latin subset only, weights 700 and 300. Used for the logo and nowhere else |
| First "ms" | weight 700 |
| Second "ms" | weight 300 |
| Dot | weight 700, amber `logoDot` (`#F5B75C`) |
| Tracking / leading | letter-spacing −0.05em, line-height 1 |
| `tone="green"` | text `green500` — on white or pale backgrounds |
| `tone="white"` | text `surface` — on green backgrounds (sider, login art panel, mobile header); the dot stays amber |

Rules: render it only through `shared/components/BrandLogo.tsx` (`tone`, `size` = font-size in px). Never export it as PNG/SVG, recolor it, change the weights, add a tagline or the school name beside it, or typeset it in Be Vietnam Pro. Standard sizes: sider 34, collapsed sider 18, login 104. The school's own logo and name (SCH-07) are a separate identity and never replace the wordmark.

**Rule:** any column containing numbers, scores, times, or codes sets `font-variant-numeric: tabular-nums`. Without it columns jitter while scrolling.

---

## 4. Spacing, radius, elevation

**Spacing scale (px):** `4 · 8 · 12 · 16 · 24 · 32 · 48`. Values outside this scale are not allowed.

| Radius | px | Applies to |
| --- | --- | --- |
| `xs` | 6 | Code chips, small tags |
| `sm` | 8 | Small AntD parts (tags, small controls), icon button, sider icon tile |
| `md` | 12 | Input, select, button, sider item, timetable frame, calendar leaf, login fields, inline alert, stat icon chip |
| `lg` | 16 | List rows, modals |
| `xl` | 20 | Cards (AntD Card), bento tiles, the notched tab, empty list frame |
| 32 | — | The sider's right edge (top and bottom corners) |
| `pill` | 999 | Avatar, account chip, search pill, filter controls, primary-filter segments, key caps |

| Shadow | Value |
| --- | --- |
| `card` | `0 1px 2px rgba(16,42,48,.04), 0 6px 20px rgba(16,42,48,.05)` — cards, bento tiles, account chip |
| `lift` | `0 2px 4px rgba(16,42,48,.04), 0 14px 32px rgba(16,42,48,.10)` — clickable tile on hover |
| `pop` | `0 8px 28px rgba(16,40,32,.12)` — dropdowns, chart tooltip |
| `modal` | `0 16px 48px rgba(16,40,32,.18)` — modals, floating action bar |
| `float` | `0 24px 64px rgba(8,52,38,.28)` — only the login card on the green login page (§6.0) |

Primary buttons carry `0 6px 16px rgba(10,156,119,.28)`. The sider has no shadow, so the notched tab stays seamless with the page.

---

## 5. Token implementation

`web/src/shared/lib/tokens.ts` exports plain objects; `app/cssVars.ts` pushes them to CSS variables (`--c-*`, `--radius-*`, `--shadow-*`, `--grad-*`, `--focus-ring`) and `app/antdTheme.ts` feeds AntD's `ConfigProvider`. Nothing else reads raw hex.

```ts
// web/src/shared/lib/tokens.ts (shape — see the file for every value)
export const color = {
  green50…green800, ink,                                         // §2.1
  amber, amberInk, amberSoft,                                    // §2.2
  coralFg/Bg, amberFg/Bg, skyFg/Bg, violetFg/Bg, tealFg/Bg,      // §2.2b (also read through `accent`)
  bg, surface, sunken, border, borderStrong, borderMint, fieldBorder,
  text1, text2, text3, logoDot, limeSoft, aqua, meadow1…3, disabled, danger,
  tableHeaderBg, rowHoverBg, hoverBg, neutralBg,
  onGreenHover, onGreenMuted, onGreenLine,                       // text and overlays on green
} as const;
export const radius = { xs: 6, sm: 8, md: 12, lg: 16, xl: 20, pill: 999 } as const;
export const accent = { green|coral|amber|sky|violet|teal: { fg, bg } };                         // §2.2b
export const shadow = { card, lift, pop, modal, float } as const;    // §4
export const gradient = {
  sider:  'linear-gradient(175deg, #1CAA64 0%, #2CBC74 55%, #50CD8C 100%)',                 // deeper than every other green surface; deepest at the top (brand, labels)
  accent: 'linear-gradient(135deg, #E5F9EC 0%, #F1FCF5 100%)',   // tinted tile (pale mint)
  authPage: 'linear-gradient(160deg, #86F2CF 0%, #A9F0C4 50%, #D2F4B2 100%)', // login only
} as const;
export const badgeColor = { active|pending|neutral|danger|info: { fg, bg } };   // §2.4
export const fontFamily = "'Be Vietnam Pro', Inter, system-ui, sans-serif";
```

```ts
// web/src/app/antdTheme.ts (essentials)
token: {
  colorPrimary: color.green600, colorLink: color.green600, colorError: color.danger,
  colorBgLayout: color.bg, colorBgContainer: color.surface,
  colorBorder: color.borderStrong, colorBorderSecondary: color.border, colorSplit: color.border,
  colorText: color.text1, colorTextSecondary: color.text2, colorTextTertiary: color.text3,
  borderRadius: radius.md, borderRadiusLG: radius.lg, borderRadiusSM: radius.sm,
  fontFamily, fontSize: 14, controlHeight: 38, wireframe: false,
},
components: {
  Table:  { headerBg: 'transparent', headerColor: color.text2, rowHoverBg: color.sunken, cellPaddingBlockMD: 14, cellPaddingInlineMD: 12 },
  Layout: { siderBg: 'transparent', headerBg: 'transparent', bodyBg: color.bg },
  Menu:   { itemSelectedBg: color.surface, itemSelectedColor: color.green700, itemHeight: 46, subMenuItemBg: 'transparent' },
  Card:   { paddingLG: 24, headerFontSize: 16, headerHeight: 56 },
  Button: { primaryShadow: 'none', defaultShadow: 'none', dangerShadow: 'none', fontWeight: 600, paddingInline: 18 },
  Modal:  { titleFontSize: 18, titleColor: color.text1 },
  Progress:  { defaultColor: color.green500, remainingColor: color.neutralBg },
  Segmented: { trackBg: color.hoverBg, itemSelectedBg: color.surface, itemSelectedColor: color.green700, itemColor: color.text2 },
},
```

Global CSS (`index.css`) sets every AntD Card to radius `xl`, a 1px `border` and shadow `card`, with 600 `text1` titles.

Wrap the app with `<ConfigProvider theme={antdTheme} locale={viVN}>` and call `dayjs.locale('vi')` once in `app/`.

---

## 6. Layout shell

```
 ┌─ Sider 256 ───────┐                                                                    ( K Trần Đức Kiên ▾ )
 │ msms.             │
 │ TỔNG QUAN         │  • Hồ sơ                                                 [Tải mẫu][Import][+ Thêm cán bộ]
 │ ⌂ Trang chủ       │  Cán bộ
 │ QUẢN LÝ           │  Mô tả…
 │ ⚙ Thiết lập     ▾ │  ( 4 cán bộ … )
 │ 👤 Hồ sơ         ▴ │
 │  ┆ Cán bộ  ◀tab ╮ │  [Đang hoạt động|Tất cả] (Tìm…)(Tìm kiếm) (Phân hiệu ▾) (Tổ ▾) …
 │  ┆ Trách nhiệm   │
 │ HỆ THỐNG          │    Họ và tên    Ngày sinh …                                       Thao tác
 │ 🛡 Tài khoản      │  ╭──────────────────────────────────────────────────────────────────────╮
 │                   │  │ (M) Lê Thị Mai   21/07/1980 …          (● Đang hoạt động)   ✎ ⏻  │
 │               «   │  ╰──────────────────────────────────────────────────────────────────────╯
 └───────────────────┘   ~~~ aqua sky at the top, low green hills at the bottom ~~~
```

- The page is a flat `bg` with a **sky-and-hills backdrop**: a fixed full-viewport SVG (`.page-backdrop`, starts at the sider's right edge, `pointer-events: none`, behind all content) holding a vertical `aqua` sky wash (40% at the top fading to 0 by mid-height) and two low hills along the bottom (`meadow1` 35%, `meadow2` 28%) — the same sky-and-meadow language as the login art panel. A 220px mask fades it out toward the sider so the left edge stays flat and the sider tab can melt into it. The sider is flush to the left and top edges; the header (72 high) sits directly on the page — no background, no border.
- Content area: padding 4 top, 32 right, 56 bottom, 20 from the sider; inner max width 1400, centered. Blocks are **white cards** (radius `xl`, 1px `border`, shadow `card`), 16–24 apart; lists are rows of separate cards (§8.2).
- Sider is a **solid emerald panel** (`gradient.sider`; white 14px/500 text is only ~3:1 on it, accepted as a style trade-off — keep text weight ≥ 500), 256 wide (72 collapsed), full viewport height, radius 0 32 32 0, no shadow, sticky. Do not use a dark or white sider.
- On every route change the content fades in and rises 6px (240ms ease-out); off under `prefers-reduced-motion`.

### 6.0 Auth layout (login, forgot password, forced password change)

All pre-login screens use `app/AuthLayout.tsx` — never a bespoke page. No sider/header; one **floating card** centered on a soft green page:

```
 ┌───────────────────────────────────────────────────────────────────┐
 │  ┌─ card: radius 28, shadow float, one piece ──────────────────┐  │
 │  │  form column 456       │  art panel                         │  │
 │  │                        │    sky · hills · trees            │  │
 │  │  Chào buổi sáng        │                                    │  │
 │  │  Đăng nhập để tiếp tục │                                    │  │
 │  │                        │                                    │  │
 │  │  Tên đăng nhập         │                                    │  │
 │  │  [ vd. kientdhs186194 ]│                                    │  │
 │  │  Mật khẩu              │                                    │  │
 │  │  [ Nhập mật khẩu CAPS👁]│                                    │  │
 │  │       Quên mật khẩu?   │                                    │  │
 │  │  [ ██ Đăng nhập ██ ]   │                  msms.  (white,104)│  │
 │  │  © 2026 · caption      │                                    │  │
 │  └─────────────────────────────────────────────────────────────┘  │
 └───────────────────────────────────────────────────────────────────┘
```

- **Page**: `gradient.authPage` — a soft mid green, lighter behind the card and slightly deeper toward the edges; padding 48. Saturated enough to frame the card, never the bright `gradient.sider` green.
- **Card**: max width 1120, min height `min(640px, 100vh − 96px)`, radius 28, shadow `float`, `overflow: hidden`. The white form column and the art panel are **one continuous piece** — no gap, no inner radius between them. Rises in (fade + 16px, 480ms) on load.
- **Form column (left, 456 wide)**: white, padding 48/48/32, everything **left-aligned**, form block vertically centered. No decorative icon, logo or date — the title and plain fields carry the hierarchy.
  - **Title** (`title`): 30/38 weight 600, letter-spacing −0.02em, `text1`. Login: time-of-day greeting from the user's clock ("Chào buổi sáng" before 12h, "Chào buổi chiều" before 18h, else "Chào buổi tối"). Forgot password: "Quên mật khẩu" (step 1), "Đặt lại mật khẩu" (step 2). Forced change: "Đổi mật khẩu".
  - **Subtitle** (`subtitle`): 8 below the title, 14/22 `text2`, one line where possible. Login: "Đăng nhập để tiếp tục"; forgot password step 1: "Nhập tên đăng nhập để nhận mã xác nhận qua email trong hồ sơ", step 2: the neutral sent message (below); forced change: "Đặt mật khẩu mới trước khi tiếp tục". The form starts 32 below.
  - **Labels** sit **above** the field (13/500 `text1`, 8 gap) — never inside it.
  - **Inputs**: AntD `large` via `ConfigProvider componentSize`, height 48, padding 0 16, radius `md`, white with a 1px `fieldBorder` line, 15px text. Hover = `green300` border; focus = `green500` border + 4px `focusRing`; error = `danger` border, message below in 12px. No prefix icons. Placeholders give an example or an instruction, never repeat the label (login: "vd. kientdhs186194" — the BR-03 format — and "Nhập mật khẩu"). Browser autofill is forced back to the white fill and `text1` text so Chrome's blue tint never shows.
  - **Field extra line**: right under a field, 8 gap: an optional 12/18 `text3` note on the left and a 13/500 link button on the right — "Quên mật khẩu?" under the login password, "Mã có hiệu lực 10 phút." + "Gửi lại mã" under the OTP. When the field shows an error, the line keeps 16 below it so it never touches the next label.
  - **Caps Lock**: while typing a password with Caps Lock on, a small "CAPS" pill (11/600, `amberFg` on `amberBg`) appears inside the field, just left of the show-password icon, so the form never shifts.
  - **Submit**: §8.1 primary (`green600`), full width, height 48, 15/600, radius `md`, 8 above it; lifts 1px on hover. While submitting, the button ignores clicks and a thin white bar sweeps along its bottom edge instead of AntD's spinner (login and forgot password; the shared change-password form keeps `loading`).
  - **Caption**: bottom of the column, left-aligned 12px `text3`: "© {year} · Hệ thống quản lý trường học đa phân hiệu".
- **Art panel (right, fills the rest)**: drawn in code as one inline SVG, colors set from CSS tokens — a simple landscape instead of abstract shapes. Sky is a vertical `aqua → limeSoft` gradient; a white sun (58 radius at 80% with a 92 halo at 25%) top-right and two white clouds (92% and 70%) sit in it. Three overlapping hills fill the lower half, far to near: `meadow1`, `meadow2`, `meadow3`; two round trees (`meadow3` crown on a thin trunk) stand on the middle hill at left and two bushes (`meadow2`) on the near hill. The near hill rises toward the right so the wordmark always sits on `meadow3`, dark enough for white text. No animation. The only content is the MSMS wordmark (§3.1, `<BrandLogo tone="white" size={104} />`) at **bottom-right**; its amber dot is the visual full stop of the greeting. No taglines, nav links or stock imagery.
- **Forgot password** (`/forgot-password`, BR-04): one page, two steps, same card. "Quên mật khẩu?" carries the typed username over; every way back to login carries it back, and a successful reset returns to login with a success toast and the username filled in.
  - Step 1: username field + "Gửi mã". Any success moves to step 2 — the screen never says whether the account exists or has an email.
  - Step 2: subtitle "Nếu tài khoản {username} hợp lệ và hồ sơ có email, mã 6 chữ số đã được gửi tới email đó." Fields: "Mã xác nhận" as six 48-high boxes (AntD `Input.OTP`, digits only, numeric keypad; same border/hover/focus/error as inputs, 20/600 centered), "Mật khẩu mới", "Nhập lại mật khẩu mới" (same rules as the change-password form). "Gửi lại mã" is disabled for 60 s after each send and reads "Gửi lại mã sau {n} giây"; resending clears the OTP boxes. Wrong, expired or used-up codes come back from the backend as an `InlineAlert`.
  - Below the form: link buttons "Đổi tên đăng nhập" (back to step 1) and "Quay lại đăng nhập", then a 13/20 `text2` hint: "Không nhận được mã? Kiểm tra thư rác. Hồ sơ chưa có email thì liên hệ quản trị trường để được cấp lại mật khẩu tạm."
- Errors use `InlineAlert` (§8.7) directly above the button. Secondary actions (Đăng xuất, Quay lại đăng nhập…) are link buttons, left-aligned below the form, 24 apart.
- Animations are off under `prefers-reduced-motion`.

### 6.1 Sider — two-level menu

Structure is fixed by Conventions §4.7 (the order of groups never changes). This section fixes only appearance. All text and icons are white on the green panel, top to bottom:

| Element | Spec |
| --- | --- |
| Wordmark | `<BrandLogo tone="white" size={34} />`, left-aligned in a 68px row, padding 24; collapsed: size 18, centered. No school block under it — the school name and logo appear on Trang chủ |
| Group labels | the menu is split into three labelled clusters — **Tổng quan** (Trang chủ, Dashboard), **Quản lý** (Thiết lập, Hồ sơ, Thời khóa biểu, Điểm danh, Học tập, Phụ huynh, Bảng tin), **Hệ thống** (Quản trị). Label 11/600 uppercase, letter-spacing .08em, 62% white, padding 16/10/6. The menu column has 20 padding on top so the notch of the first item is not clipped. A cluster with no visible group is not rendered. Collapsed: the label becomes a 1px 18%-white divider |
| L1 item | height 44, radius `md`, white icon in a 32×32 tile (radius `sm`, `rgba(255,255,255,.12)` when the group is open), label 14/500, chevron right; hover `rgba(255,255,255,.12)` |
| L2 item | height 38, indent-left 48, long labels truncate with an ellipsis on the label only, **no icon and no bullet**, label 13/400 at 86% white; a 1px vertical line at 22% white joins the items |
| Selected | a **notched tab**: a layer in the page color (`bg`) under the menu, from the item's left (12 inset) to the sider's right edge, radius `xl` on its left corners, with two 20×20 inverted corners above and below its right end so it merges with the page like a folder tab. The menu column runs 2px past the sider's right edge and hides its scrollbar, so no seam shows where tab and page meet. It **slides** (transform + height, 280ms ease-out) to the selected item on navigation and follows groups as they open and close. Text `green700` weight 600, icon `green600`. When the selected item's group is closed (or the sider is collapsed) the tab sits on the group title, which turns `green700` |
| Parent of selected (open) | white text weight 600, transparent bg, auto-expanded |
| No permission | **Not rendered at all** — never rendered-then-disabled |
| Collapsed | 72px rail, icons only, submenu opens as a popover |
| Collapse | bottom of the sider: a 36×36 ghost button with `ChevronsLeft` / `ChevronsRight` at 86% white, right-aligned (centered when collapsed), hover `rgba(255,255,255,.14)`, tooltip "Thu gọn menu" / "Mở rộng menu" |

The tab does not animate under `prefers-reduced-motion`.

### 6.2 Header

Height 72, transparent on the page, no border, padding 20 left and 32 right. Left: the **location bar** — a white pill (height 44, padding 0 20, 1px `border`) holding the path (14 `text3`, current page 500 `text2`, `ChevronRight` between levels; pages outside the menu show "Trang chủ"), a 1px `border` divider, then the school name from SCH-07 (15/700 `text1`, ellipsis, no logo; "MSMS" until a name is saved). Middle-right: the **quick-search pill** — height 44, white, 1px `border`, `Search` icon, "Tìm nhanh màn hình" 13 `text3` and a `Ctrl K` key cap; click opens §6.3. Far right: the **account chip** — pill, height 48, white, 1px `border`, shadow `card`: 34px `green500` avatar with a white initial, name 13/600 `text1` over account type 12 `text3`, chevron; hover border `green300`. Click opens `Hồ sơ cá nhân` · `Đổi mật khẩu` · `Đăng xuất` below it.

No current-year chip: the year lives on Trang chủ and in each screen's own filters. A notification bell with count badge joins the right side only once COM notifications ship — never render a bell that does nothing.

### 6.3 Command palette (quick search)

`app/CommandPalette.tsx`, opened with Ctrl/⌘+K anywhere in the shell or by clicking the header quick-search pill (§6.2). A 560-wide modal, 96 from the top, no footer:

- Top: borderless input with a search icon and an `Esc` key cap; placeholder "Tìm màn hình, ví dụ: cán bộ, thời khóa biểu…". Focused and empty on every open.
- List: exactly the screens on the viewer's menu (`visibleMenu`, so hidden screens never leak) plus Hồ sơ cá nhân and Đổi mật khẩu, as text rows "Group / Screen" (group in `text3`). No icons. Matching ignores Vietnamese diacritics and case ("can bo" finds "Cán bộ").
- ↑/↓ move the highlight (`green50`, ↵ at the right); Enter or click opens the screen and closes the palette. No match: one centred line "Không có màn hình nào khớp …".
- It navigates only — it never searches records.

---

## 7. Module identity

Modules are told apart by **words**: the sider label and cluster and the header path (§6.2). There is no per-module accent color; supporting accents (§2.2b) vary the look inside a screen, they do not tag modules.

---

## 8. Components

### 8.0 Page header

Every screen except Trang chủ opens with `shared/components/PageHeader.tsx` (`title`, `description`, `extra`), drawn **directly on the page background** — no band, no card — with padding 8×4 and 24 above the next block.

- **Title** — 32/40 700 `text1`, letter-spacing −0.025em (§3).
- **Description** — 14/22 `text2`, max width 720.
- **Actions** (`extra`) — the screen's buttons, bottom-aligned on the right, gap 10, wrapping.

No icon tile and no decoration in the header.

### 8.1 Buttons

| Variant | Spec |
| --- | --- |
| Primary | fill `green600`, white text 600, height 38, radius `md`, h-padding 18, shadow `0 6px 16px rgba(10,156,119,.28)` |
| Default | white fill, 1px `borderStrong`, text `text1` |
| Text | no fill/border; header button, quiet actions |
| Danger | fill `danger`, white text — **only** for Hủy tiết, Khóa tài khoản, Xóa |
| Icon-only | 38×38, radius `sm`, `text2` icon, `green50` + `green700` on hover, tooltip mandatory |

**Rules:**
- Exactly **one** primary button per screen. Everything else is Default.
- The dialog-dismiss button is always labelled **"Đóng"**, never "Hủy" (Conventions §4.5 — "Hủy" means cancelling a teaching session).

### 8.2 Lists (card rows)

The screen users spend most time in. Follow exactly. `DataTable` (AntD `Table`) is styled as **rows of separate white cards**.

| Aspect | Spec |
| --- | --- |
| Container | `Card className="table-card"` is transparent (no border, no shadow, body padding 0): rows sit directly on the page background |
| Header | height 40, on the page color (`tableHeaderBg`), 12/600 `text3`, **sentence case**, no rule; sticky on scroll with a `bg` holder so rows scrolling underneath stay hidden |
| Row | one card per row: height 64, white, 1px `border`, radius `lg`, **10px gap** between rows; hover `rowHoverBg` with a `green200` border |
| Selected row | bg `green50`, `green200` border and a 4px `green500` bar on the left edge |
| Cell padding | 14 × 12; first and last column 20 on the outer side |
| Zebra striping | **Not used** |
| Alignment | text left · numbers and scores right · statuses and actions center |
| Action column | sticky right, icon-only, max 3 icons then collapse into "…". Icons are always fully opaque (19px, stroke 2.1) |
| Multi-select | checkbox first column; once rows are selected a **floating action bar** rises from the bottom: `ink` panel, radius `lg`, shadow `modal`, white text with the count in `amber`, translucent white secondary button + primary |
| Pagination | below the last row, on the page background: left "a–b trên tổng N" 13 `text2`, right page buttons; default 20 rows; hidden when everything fits on one page |
| Empty | a dashed `borderStrong` frame (radius `xl`) with one 15/500 `text2` sentence and an optional primary action; no illustration |
| Loading | 5-row skeleton in a white card. **Never** a centered spinner |
| Codes | usernames, abbreviations, cell addresses render as a monospace chip (`.mono`: 12.5px, `green50` fill, `green800` text, radius `xs`) |

Column headers use the short forms from Conventions §4.6: `Lớp · Môn · Giáo viên · Ngày · Tiết · Trạng thái`.

A list inside a titled card (Dashboard) does not nest cards: it keeps flat rows with a hairline between them, runs flush to the card edges, indents its first and last column 24 to match the card title, and drops pagination.

### 8.2b People rows (avatar cell)

Any table or list whose rows are people (staff, students, parents, accounts) leads with a **person cell**:

- 38px round avatar with the initial of the given name on a pale accent (§2.2b): the tone is a hash of the name, so a person has the same color on every screen. Inactive people get `neutralBg` / `text3`. If a photo exists, show the photo.
- Name 14/500 `text1`; below it one 12px `text3` line (person code or email, `tabular-nums`). Inactive people: name in `text3`.
- The code/email moves into this cell — do not keep a separate "Mã hồ sơ" column next to it.
- Implemented once as `shared/components/PersonCell.tsx`. No per-screen avatar code.

### 8.3 Filter bar

`shared/components/FilterBar.tsx` is a flat toolbar row directly above a list — no card, 14 below, gap 10, wraps. It re-themes its controls (height 42, radius 22) so every input, select and search box is a **pill**.

- **Quick status filter first**: when a list has a small set of states, show them as an AntD `Segmented` (pill track, white, shadow `card`; the selected segment is a `green500` pill with white text) instead of a select or switch — Cán bộ: "Đang hoạt động | Tất cả hồ sơ"; Tài khoản: "Tất cả | Đang hoạt động | Chờ đổi mật khẩu | Đã khóa".
- Then the search input (320) and the remaining `Select`s (200 each). **Control widths are fixed by `FilterBar` CSS, not by the screen**; do not pass `style={{ width }}` to controls inside a `FilterBar`.
- Do not hide filters in a drawer — nearly every MSMS screen filters by academic year / campus / class.

### 8.4 Forms, modals and drawers

- Range pickers: the default AntD arrow icon loses a stroke at this height, so the separator is a plain `→` glyph in `text3` (global CSS in `index.css`). Do not reintroduce the icon.
- Label **above** the input, not inline-left. Input height 38, radius `md`, 1px `borderStrong`.
- Focus: border `green500` + 3px ring `focusRing`.
- Error: border `danger`, 12px message below the field. The message must state **cause and remedy**, not just failure (Conventions §4.6).
- Modal: radius `lg`, 18/600 `text1` title, width 520 (standard) / 720 (contains a table). Footer right-aligned, Đóng left of Lưu.
- Long forms (about 8 fields or more, e.g. a staff profile) use `FormModal` with `variant="drawer"`: slides in from the right, width 640, two-column `form-grid`, footer pinned to the bottom with Đóng · Lưu right-aligned. Same validation, error and close rules as the modal.
- **Irreversible actions** (cancel session, publish timetable, declare ad-hoc holiday, lock account) require a confirm modal containing a **numeric impact summary**, e.g. `"Sẽ hủy 23 tiết của 6 lớp."`

### 8.5 Status

A tag (height 26, padding 0 10 0 8, radius 8) with the group's solid mid-tone background, a 14px white icon (active `CircleCheck`, pending `Clock`, neutral `CircleMinus`, danger `CircleAlert`, info `Info`), then the label 12/600 in white (§2.4). No border. Rendered by one shared `<StatusBadge domain value />` that calls `statusGroup()` and `labels.ts`.

### 8.5b Greeting (Trang chủ)

Rendered by `shared/components/HeroBanner.tsx` (props: `eyebrow`, `title`, `description`, `extra`) inside the Trang chủ **tinted tile** (`.bento-tile--accent`: pale mint → pale sky, `borderMint` border, padding 28×32, min height 188):

- Eyebrow: school name · năm học, 12/18 600 uppercase, letter-spacing 0.08em, `text2`; omitted when neither is known.
- Greeting: 34/42 700 `text1`, following the time of day (Chào buổi sáng / trưa / chiều / tối) + the given name, no trailing punctuation.
- Description: weekday + date, 15/22 `text2`, first letter capitalised.
- Spacing: 14 between eyebrow and greeting, 12 between greeting and description.
- `extra`: the school crest — logo at 84px in a 116px white circle with a 10px `green100` halo.

Do not repeat the account here — the header account chip already shows it.

### 8.6 Stat tiles (Dashboard)

`shared/components/StatCard.tsx` (props: `label`, `value`, `hint`, `variant`, `attention`, `icon`, `tone`, `onClick`, `className`, `children`); never hand-build a KPI tile. Each tile is a bento tile: white, radius `xl`, 1px `border`, shadow `card`, padding 22×24.

- Top row: the label (13/500 `text2`) on the left; on the right a 40×40 **icon chip** (radius `md`, a 20px lucide icon) in the pale fill and ink of the tile's `tone` (§2.2b: sky, violet, amber, teal…). The tinted tile uses a white chip with a `green700` icon.
- Value: 36/44 700 `ink`, `tabular-nums`, `vi-VN` thousands separator.
- Foot (pushed to the bottom, min height 20): `hint` 12 `text3` — context derived from the data (an average, a date), never a repeat of the label; on clickable tiles "Xem →" 12/600 `green700` on the right (the arrow slides 3px on hover).
- `variant="accent"`: the tinted tile (`gradient.accent`, dark text), value 56/64 in `green700`. **One per screen.**
- `attention` (a "needs attention" metric whose value is above zero): amber dot before the label, value in `amberInk`. Never as decoration.
- Clickable when a source list screen exists; hover lifts the tile 2px with shadow `lift`. Not clickable when the target screen is not built yet.

### 8.7 System feedback

| Channel | When |
| --- | --- |
| Toast, top-right | Saved, deleted — auto-dismiss 3s |
| In-page alert | Must-read warnings: teacher conflicts, sessions about to be deleted |
| Modal | Irreversible actions only |

In-page alerts are always rendered with `shared/components/InlineAlert.tsx` (icon on, 16px space below built in) — never AntD `Alert` with a hand-set `marginBottom`.

Never use a toast for an error the user must fix — that error has to stay on screen.

---

## 9. Screens that need explicit specs

### 9.1 Timetable grid (SCD-01)

Sticky header row **and** sticky first columns, inside a white card (1px `border`, radius `xl`, shadow `card`). Rows = Thứ × Buổi × Tiết, columns = classes.

- Header row: `sunken` background, class names 12/600 `text2`.
- Day column: the weekday 700 `green700` on `green50`; a 2px `borderStrong` rule starts each day.
- Filled cell: a **rounded chip** (radius `md`) inside a 5px cell padding, colored **by subject** — the subject abbreviation hashes to one of the six accents (§2.2b), so a subject keeps its color across classes and versions. Bg = accent `*Bg` plus a 3px inset left bar in accent `*Fg`; two lines — subject 13/700 `text1`, teacher 11 `text2`. Text never takes the accent color.
- Empty cell: white, `hoverBg` on hover.
- Morning/Afternoon separated by an 8px band row in `sunken`.
- Daily schedule (Lịch dạy): a cancelled period is a `neutralBg` chip with struck-through text; a pending period is an `amberBg` chip with a 1.5px `amber` outline. Filled periods keep their subject color.
- My teaching schedule (Lịch dạy của tôi, teacher): the same grid turned into a week — rows = Buổi × Tiết, columns = Thứ 2 → Thứ 7 of the chosen week, each header showing the weekday and `DD/MM`; today's header is `green700` on `green50`. A chip reads subject 13/700 and `class · status` 11 `text2` (status only for Chờ xử lý / Đã hủy); a cancelled period and its replacement stack in the same cell, the live one first. Week picker with previous/next arrows and a "Tuần này" button.
- Lịch dạy view modes (Principal, Vice Principal): a segmented switch **Theo ngày / Theo lớp / Theo giáo viên** beside the campus select. *Theo ngày* is the daily grid above. *Theo lớp* and *Theo giáo viên* pick one class or one teacher (searchable select) and show the same week grid as My teaching schedule; the second chip line is the teacher name in the class view, and `class · campus` in the teacher view. Read-only — no edit actions on this screen.
- Conflicting cell: 2px `danger` border + red dot top-right.

### 9.2 Import error table (BR-11, BR-12)

Top panel in `danger` tint: `"Từ chối 1 file · 7 lỗi"`. Error table has 4 columns: **Ô/Dòng · Cột · Giá trị gốc · Nguyên nhân**. Cell addresses render in monospace (`D12`). Clicking an error row scrolls the file preview to that cell.

The "Đã có, bỏ qua" section is a separate collapsed table below, neutral grey — it is not an error.

### 9.3 Attendance (ATT-01)

Students render as **row cards**, not a table: avatar + name on the left, three segmented buttons **Có mặt / Muộn / Vắng** on the right, colored per §2.4.

Top bar shows live counts per status. When School Admin is backfilling, show an amber banner: `"Nhập bù cho tiết ngày 12/10 — ghi nhận người sửa."`

### 9.4 Dashboard (ADM-01) and Trang chủ

Two different screens; do not merge them (Business Spec §5.1). Both use a **bento grid** (gap 16) with exactly one tinted tile.

**Dashboard** — Principal and Vice Principal only.

- `PageHeader` titled "Dashboard"; description = scope · năm học · today's date. Right slot: a 240px scope `Select` ("Toàn trường" + each campus) for Principal; Vice Principal gets it only when leading more than one campus.
- 4-column bento. Row 1, four stat tiles (§8.6): **Học sinh đang học (tinted, `GraduationCap`)** · Lớp đang hoạt động (sky, `School`) · Cán bộ đang công tác (violet, `UsersRound`) · Tiết dạy hôm nay (amber, `CalendarClock`). Hints: "n khối đang có lớp" · "Bình quân n học sinh mỗi lớp" (only when both counts are above zero) · "Trên n phân hiệu" (only when the scope covers more than one campus) · today's weekday and date. A tile is clickable only when it has a source screen: Cán bộ opens the staff list, Tiết dạy hôm nay opens Lịch dạy, Lớp and Học sinh are not clickable.
- Row 2: card "Học sinh theo khối" (2 columns; 4 when there is no comparison) — header right shows "Tổng n học sinh" (13/500 `text2`). A single-series `recharts` bar chart, height 280, one tone per grade (green, sky, violet, amber, teal, coral in order; `accent.*.fg`) with 8px rounded tops, max width 52, each bar labelled with its student count above it (13/600 `ink`), dotted horizontal grid in `border`, x-axis line `borderStrong`, axis text 12 `text2`/`text3`, hover tooltip (white, shadow `pop`) with student and class count. No legend (one series).
- Beside it (2 columns), only when the scope covers more than one campus: card "So sánh phân hiệu" — a `DataTable` (Phân hiệu · Lớp · Học sinh · Cán bộ, numbers right-aligned) flush to the card edges as flat rows (§8.2); the Học sinh cell puts a 96×4 share bar (`green600` on `neutralBg`, relative to the largest campus) left of the number. Clicking a row sets the scope filter to that campus.

**Trang chủ** — every account type. 3-column bento:

- Row 1: the **greeting tinted tile** (§8.5b, 2 columns) · card **Năm học** — a 132px `Progress` ring (stroke 9, `green500` on `neutralBg`) showing the week of the current semester: centre "n" 34/700 over "/ N tuần" 12 `text2`; below it the semester name 17/700 and its date range 13 `text2` (or "Sắp tới: …" / "Bắt đầu dd/mm/yyyy" outside a semester).
- Row 2 (staff): card **Hôm nay** (2 columns) — header right shows the clock 22/700 `green700` (refreshed every 30s). Body is a **vertical timeline**: a 1px `border` rail; each active period slot is a row with its start time (13/500 `text1`) over its end time (`text3`) on the left, a node on the rail, then "Tiết n" and, for every staff account except School Admin, the class (600) · subject of their own lesson in that period. Sáng / Chiều captions (12/600 `text3`) open each session. Nodes: past = filled `border`, content at 45% opacity; next = `ink` ring + a "Tiếp theo" pill (`neutralBg`, `ink`); **current = filled `amber` dot with a slow `amberSoft` halo pulse** + an "Đang diễn ra" pill (`amberSoft`, `amberInk`). Sunday or a school-wide holiday replaces the timeline with a centred "Hôm nay trường nghỉ" (17/600) + reason. · card **Ngày nghỉ sắp tới** — up to four rows separated by hairlines: a calendar leaf (48×52, radius `md`, day 20/700 over "Th M" 11) and a "còn n ngày" pill, both `green50`/`green700`, plus the reason and the campus name in `text3` when campus-specific.
- Card titles on Trang chủ have no divider line.
- Student and Parent: the greeting tile spans all 3 columns; below it one card with the school name as title and its address, phone, email, website as hairline-separated rows with a 16px `text3` icon.
- No KPI tiles, charts, shortcuts or news feed on Trang chủ.

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
| Complex animation | Only short transitions: 150ms hover, 180ms tile lift, 200–240ms floating bar and page enter, 280ms sliding sider pill, the slow pulse of the current-period dot; all off under `prefers-reduced-motion` |
| Illustration | Empty states are one short sentence; no stock or hand-drawn illustration |

Only two additional libraries are permitted: `lucide-react` (icons) and `recharts` (Dashboard charts). Fonts come from `@fontsource` packages (Be Vietnam Pro, Montserrat for the wordmark).

---

## 12. Shared component registry

Check here before writing any UI. Everything below already exists; use it as-is.

| Need | Use | Location |
| --- | --- | --- |
| Page title block + actions | `PageHeader` | `shared/components/` |
| Trang chủ greeting (inside the tinted tile) | `HeroBanner` | `shared/components/` |
| KPI tile in a bento grid | `StatCard` (`variant="accent"` once per screen) | `shared/components/` |
| Filter toolbar above a table | `FilterBar` (fixed control widths; `Segmented` quick filter first) | `shared/components/` |
| Table | `DataTable` (skeleton, empty state, pagination with total) inside `Card className="table-card"` | `shared/components/` |
| Person row | `PersonCell` | `shared/components/` |
| Status | `StatusBadge` | `shared/components/` |
| Row action icons | `RowActions` | `shared/components/` |
| Create / edit form | `FormModal` (`variant="drawer"` for long forms) | `shared/components/` |
| In-page alert | `InlineAlert` | `shared/components/` |
| File picker, import errors | `FileButton`, `ImportErrorPanel` | `shared/components/` |
| Pre-login screen | `AuthLayout` | `app/` |
| Quick screen search (Ctrl+K) | `CommandPalette` | `app/` |

Full-width form controls (`DatePicker`, `InputNumber`, `TimePicker`) inside a form use `style={{ width: '100%' }}` — the only inline style allowed on a shared control. Any other one-off spacing goes in the feature's own `.css`.
