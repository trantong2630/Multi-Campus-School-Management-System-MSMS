# MSMS — Mobile Design System

18/11/2026 · V1.4 · Trần Đức Kiên

**Audience: coding agents.** Companion to `MSMS_Design_Web.md` (shared tokens) and Business Specification V3 §8.1 (mobile scope).

**Binding rules for any agent implementing mobile UI:**
1. Mobile serves **Teacher, Student, Parent** only. School Admin, Principal, Vice Principal are web-only. Never build a mobile screen for them.
2. Never hard-code a hex value in a widget. Import from `lib/core/theme/`.
3. All user-visible strings are Vietnamese, from the Flutter counterpart of `labels.ts`. Code identifiers stay English.
4. Flutter Material 3 only, imported from `package:material_ui/material_ui.dart` (Flutter 3.50 moved Material out of the SDK into this package). Do not add a UI component package.
5. Reuse across screens the same way web does (Web §12): `AuthScaffold`, hero header, stat/quick-action card, status pill and person row are each one shared widget. Do not write a per-screen copy.
6. If a screen needs a table, a grid, a file import, or a configuration form — it belongs on web. Stop and report.

Governing constraint from the spec:
> Mobile only carries what can be done **one-handed, between two periods, standing in a classroom.**

---

## 1. Design intent

One notch warmer than web — users are teachers on their feet, students, and parents, not desk workers.

| Do | Don't |
| --- | --- |
| Bottom-rounded header with a pale green gradient and a faint illustration | Flat single-color header |
| Large cards, radius 18, very light shadow | Bare `ListTile` rows flush to screen edges |
| Quick actions and figures as solid-color tiles in a white panel | Screens that are all text |
| Big numbers, small labels underneath | Numbers and labels at the same size |
| Floating green bottom nav, pill-shaped, 12 above the screen bottom | Default edge-docked bottom nav |
| Solid color only on primary buttons, FAB, the bottom nav and home tiles | Solid-filled content cards (lists, forms, details) |

---

## 2. Tokens

Color, type, and radius **inherit `MSMS_Design_Web.md` §2–4 (V1.1) unchanged** — the same values as `web/src/shared/lib/tokens.ts`: page `bg` is `#F4FCF7`, `sm/md` radius 8/12, status badges are pills, body font Be Vietnam Pro. The mobile header gradient is spelled out in §2.1 below. Mobile overrides exactly six values:

| Aspect | Web | Mobile |
| --- | --- | --- |
| Card radius | 20 | **18** |
| Card shadow | `0 10px 28px .07` | **`0 4px 14px rgba(16,40,32,.05)`** |
| Screen horizontal padding | 24 | **16** |
| Base font size | 14 | **15** |
| Minimum touch target | — | **48×48** |
| Accent colors | `*Fg` / `*Bg` pastel pairs of §2.2 | pastel pair **one step deeper**, plus a **solid** fill for home tiles (§2.2) — web pastels look washed out on a phone |

### 2.1 Header gradient

```dart
BoxDecoration(
  gradient: LinearGradient(
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
    colors: [Color(0xFF9DEFD3), Color(0xFFD5F5E3)], // aqua mint → pale green
  ),
  borderRadius: BorderRadius.vertical(bottom: Radius.circular(24)),
)
```

The faint illustration is the login landscape (§5.7) at 20% opacity, anchored to the bottom so the hills sit under the pills. Text on the header uses `green800` `#17744A`, **not white** — the gradient is too light for white text. Header height 150–180, containing: the green wordmark (or the parent's child chip) with the notification bell, greeting, class/date line, and two white pill quick-action buttons.

### 2.2 Action card colors

Each accent (`Accent` in `app_color.dart`) has three colors. **Solid** fills the home stat and action tiles, which sit together in one white panel: green `#2FB98B` · teal `#2AAFBC` · amber `#F6A04D` · coral `#F07070` · sky `#4C9BEA` · violet `#857DEB` — the same lightness, so any four side by side look like one set. Tile text is white: the label top-left 15/700, the number inside a white 2.5px ring top-right, one hint line at the bottom; an action tile shows its icon in a white 24% square instead of the ring. Tiles are 104 high with radius 22. **fg / bg** is the pastel pair, one step deeper than web §2.2, for avatars, subject badges, AI-analysis blocks and inline alerts: green `#17744A` / `#C4EED5` · teal `#0B8088` / `#C6EAEC` · amber `#A9660A` / `#FBE2B8` · coral `#D2463A` / `#F9D2CD` · sky `#2468C2` / `#CFE2FA` · violet `#5848CF` / `#DCD7FA`. **No color repeats inside one grid.** Green stays the lead (header, bottom nav, primary buttons, selected states) and `coral` is reserved for things that need attention.

### 2.3 Theme implementation

Define `lib/core/theme/app_color.dart` (`AppColor`, `Accent`, `BadgeGroup`, `AppGradient`), `app_spacing.dart` (`AppSpacing`, `AppRadius`, `AppShadow`) and `app_theme.dart`. Widgets import these; raw hex in a widget file is a review rejection. Excerpt:

```dart
// lib/core/theme/app_color.dart
class AppColor {
  static const green50  = Color(0xFFEEFBF3);
  static const green100 = Color(0xFFD8F6E3);
  static const green200 = Color(0xFFB2EDC8);
  static const green300 = Color(0xFF83DDA6);
  static const green500 = Color(0xFF47C987);
  static const green600 = Color(0xFF2DB872); // primary fill
  static const green700 = Color(0xFF1D8E55);
  static const green800 = Color(0xFF17744A);
  static const ink      = Color(0xFF12353A); // big numbers

  static const coralFg  = Color(0xFFE5594C); static const coralBg  = Color(0xFFFDECEA);
  static const amberFg  = Color(0xFFC97F0A); static const amberBg  = Color(0xFFFDF3E2);
  static const skyFg    = Color(0xFF2F80DB); static const skyBg    = Color(0xFFE8F2FD);
  static const violetFg = Color(0xFF6B5CE0); static const violetBg = Color(0xFFEEECFD);
  static const tealFg   = Color(0xFF0F9AA3); static const tealBg   = Color(0xFFE0F5F6);

  // Login landscape: sky aqua → limeSoft, three hills meadow1 → meadow3.
  static const aqua = Color(0xFF8CF4D2); static const limeSoft = Color(0xFFD9F4B8);
  static const meadow1 = Color(0xFFA6E39A); static const meadow2 = Color(0xFF5CC477); static const meadow3 = Color(0xFF1F9A55);

  static const bg = Color(0xFFF4FCF7);
  static const surface = Color(0xFFFFFFFF);
  static const border = Color(0xFFE6EDF0);
  static const text1 = Color(0xFF13292B);
  static const text2 = Color(0xFF51666A);
  static const text3 = Color(0xFF8A9DA2);
  static const danger = Color(0xFFD64535);
}
```

```dart
// lib/core/theme/app_spacing.dart
class AppSpacing { static const xs=4.0, sm=8.0, md=12.0, lg=16.0, xl=24.0, xxl=32.0; }
class AppRadius  { static const sm=8.0, md=12.0, field=14.0, card=18.0, sheet=24.0, pill=999.0; }
```

Status badge grouping reuses the same five-group mapping as web §2.4. `lib/core/labels.dart` holds one `statusLabel()` / `statusGroup()` mirroring the TypeScript registry — do not re-derive per screen. Avatar tints use the same hash and tone order as web `PersonAvatar`, so one person has the same color on both platforms.

### 2.4 Brand wordmark

The logo is identical to web (Web §3.1): text `msms.` in Montserrat 700 + 300 with an amber `logoDot` period, never an image. Bundle the two Montserrat weights as font assets (not a UI package) and expose one `BrandLogo` widget with `tone: green | white` and `size`. White tone on the landscape (login, splash); green tone on light surfaces and the home header. Same prohibitions as web: no recoloring, no tagline beside it, no other font.

---

## 3. Navigation

### 3.1 Bottom nav — 5 tabs, per account type

| Actor | Tabs |
| --- | --- |
| **Teacher** | Trang chủ · Lịch dạy · Bảng tin · Hộp thư · Cá nhân |
| **Student** | Trang chủ · Lịch học · Bảng tin · Bài tập · Cá nhân |
| **Parent** | Trang chủ · Lịch học · Bảng tin · Hộp thư · Cá nhân |

Tab names follow the web menu (`web/src/app/menu.tsx`): the parent's schedule is "Lịch học" there too.

Spec: height 68, fully rounded (pill), horizontal gradient `green600 → green500`, floated 12 above the bottom and inset 16 each side, soft green shadow. Selected tab = white icon + label 11/700 on a white 22% pill; unselected = icon + label in white 72%. Hộp thư carries a coral unread-count badge with a green outline. Five labels fit at 360dp because each label stays one short word pair.

**Notifications are not a tab** — a bell in the header's top-right, identical to web. Keep both platforms consistent.

### 3.2 Depth

Everything else is entered from Trang chủ or from a list row. Maximum **3 levels**: Tab → List → Detail. Needing a 4th level means the screen belongs on web.

### 3.3 Parent with multiple children

A child selector sits in the Trang chủ header as an avatar+name chip, opening a bottom sheet. **The selected child applies app-wide**; never re-select per screen.

---

## 4. Home screens

Shared skeleton: **gradient header → highlight card → quick-action grid**, plus a short list where the role has one. The news feed is its own tab (§3.1), not a section of Trang chủ.

### 4.1 Teacher

| Block | Content |
| --- | --- |
| Header | `"Chào, {tên}"` · weekday + date · two pills: **Điểm danh** (next session's sheet, or today's list) · **Lịch hôm nay** |
| Highlight card | Next or ongoing session: subject, class, period slot, **Điểm danh** button. If none: MSMS AI says `"Hết tiết hôm nay"` |
| 2×2 grid | Tiết hôm nay (green) · Chưa điểm danh (coral, sessions already started and not fully marked) · Đơn nghỉ chờ duyệt *(homeroom only, amber)*, otherwise Tiết tuần này (violet) · Tin nhắn chưa đọc (sky) |
| Lớp chủ nhiệm *(homeroom only)* | Two action tiles: Chuyên cần lớp · Học sinh lớp |
| List | Next 3 sessions today with attendance progress |

### 4.2 Student

| Block | Content |
| --- | --- |
| Header | `"Chào, {tên}"` · class · weekday + date · two pills: **Báo cáo tuần** · **Chuyên cần** |
| Highlight card | Current or next session; none left: MSMS AI line |
| 2×2 grid | Bài chưa nộp (coral) · Sắp hết hạn (amber, due within 48 h) · Điểm mới (violet, submitted in the last 7 days) · Chuyên cần tuần (teal, % present) |

### 4.3 Parent

| Block | Content |
| --- | --- |
| Header | Child chip (in place of the wordmark) · `"Hôm nay của {tên con}"` · child's class + date · two pills: **Lịch học** · **Chuyên cần** |
| Highlight card | This week's report — 4 figures: có mặt / muộn / vắng có phép / vắng không phép, plus a homework line; opens Báo cáo tuần |
| 2×2 grid | Xin nghỉ (green action tile, opens form) · Đơn đang chờ (amber, opens Đơn xin nghỉ) · Điểm mới (violet, opens Kết quả của con) · Nhắn giáo viên (sky, unread count) |

---

## 5. Key screens

### 5.1 Attendance (ATT-01) — the most important screen

Target: a teacher finishes one class in **under 60 seconds, one-handed**.

| Element | Spec |
| --- | --- |
| Sticky header | Subject · class · period slot · date. Below it, four live counts: Có mặt / Muộn / Vắng / Chưa |
| Bulk action | **"Tất cả có mặt"** button at the top of the list — the common case; the teacher then corrects a few. It fills only students not yet marked, so once some are marked it reads **"Còn lại có mặt (n)"** and never overwrites a choice |
| Student row | Card radius 18, height 68, 40px avatar left (dropped below 380dp width so long names keep room), name + code, three segmented buttons right. A student with an approved leave shows "Có đơn nghỉ đã duyệt" under the name |
| Segmented buttons | C / M / V, height 40, equal width; selected state fills with the matching tint |
| Note | Pencil icon at the far right opens a bottom sheet for an optional note |
| Save | Primary button docked to the bottom, showing progress: `"Lưu · 32/32"` |
| Partial | Save is always enabled — persists what is set, leaves the rest as "Chưa điểm danh" |

**Outside the session's own date:** the whole screen becomes read-only with a grey banner reading `"Tiết đã qua, không sửa được. Liên hệ quản trị để nhập bù."` (subjectless phrasing per Conventions §4.6 — never address the user as "bạn").

### 5.1b People lists (avatar rows)

Lists of people (class list, children, inbox counterparts, teachers) use the web person-cell pattern (`MSMS_Design_Web.md` §8.2b) as a **row card** instead of a table row: card radius 18, height 68, 40px round avatar on a stable-hash accent `Bg`, name 15/500, one 12px `text3` line below (student code, class, role), status pill or trailing control on the right. Same avatar tint function as web so one person has the same color on both platforms. Long accented names truncate with an ellipsis inside the card.

### 5.2 Teaching / learning schedule

Horizontally scrolling 7-day strip at the top; today is highlighted with a `green600` fill and white text. Below, a vertical timeline: start time in the left gutter, session card on the right.

Session card: 4px left border colored by status · subject 15/600 · class + room 13 `text2` · status badge top-right.
- **Đã hủy:** strikethrough text, grey background, not tappable.
- **Chờ xử lý:** amber tint, with the line `"Chờ phân công giáo viên"`.

### 5.3 Weekly report (PAR-02)

Week chosen with left/right arrows in the header. Three stacked cards: **Chuyên cần** (4 figures + a ratio ring) · **Bài tập** (assigned / submitted / not submitted) · **Điểm** (list).

Sections with no data render empty with one grey line. **Do not render `0`** — it reads as a real measurement.

### 5.4 Leave request (PAR-01)

Single-screen form, no wizard: select child → from/to date → session (3 chips: Cả ngày / Sáng / Chiều) → reason.

Dates before today are disabled in the picker (BR-19) with an inline explanation of why. The history list uses status badges; a `Chờ duyệt` request exposes a red text button **Rút đơn**.

### 5.5 Inbox (PAR-03)

Conversation list: avatar · counterpart name · `"về {tên học sinh}"` at 12px `text2` · last message · timestamp · green dot for unread.

Thread: bubbles radius 16; own messages fill `green600` with white text, counterpart `#EFF2F0`. Show a character counter past 1,800 of 2,000.

When the underlying relationship has ended, replace the composer with a grey banner: `"Hội thoại đã kết thúc, chỉ xem lại."`

### 5.6 Assignments and results (LRN-02 list, LRN-03/04, student and parent side)

Students take assignments **on web only**; mobile has no attempt screen. The Bài tập tab is for keeping track of deadlines and reading results.

List cards: title · subject · **countdown to deadline** (switches to coral under 24h) · status badge.

- **Not submitted, still open:** tapping opens a detail card (deadline, duration, question count) with one line `"Làm bài trên web."` and no start button.
- **Submitted or past deadline:** tapping opens the result: score, each question with the chosen and correct options and the explanation, then the MSMS AI analysis box. While the analysis is `Pending`, the MSMS AI avatar shows a waiting bubble and the screen refetches every 3 seconds until it settles; `Failed` leaves the box empty. Not submitted shows `"Không nộp"`, never `0`.

Parents open the same result screen from the child's list.

### 5.7 Login and forced password change

The mobile counterpart of Web §6.0, with the same identity, stacked vertically instead of side by side: the top ~36% of the screen is the web login landscape (aqua sky, sun, two clouds drifting slowly, three hills, two trees) redrawn with `CustomPainter`, with the white MSMS wordmark (Web §3.1) centered over the hills (no tagline or extra copy). Below it a white sheet with 28 top corners overlaps the landscape by 28 and rises in on open (480 ms, same curve as web). The sheet holds the time-of-day greeting as title ("Chào buổi sáng / chiều / tối"), a one-line subtitle, then the form: labels above fields, fields white with radius 14, primary button `green600` full width height 48. Errors appear as a pale in-page banner above the button, never a toast; a session that expired shows an amber banner on arrival. A caption "© {năm} · Hệ thống quản lý trường học đa phân hiệu" closes the page. Forced password change ("Đổi mật khẩu", text link "Đăng xuất"), and the three forgot-password steps (tên đăng nhập → mã 6 số với gửi lại sau 60 giây → mật khẩu mới) reuse the exact layout through one shared `AuthScaffold` widget. The splash screen while the saved session is restored shows the landscape and the wordmark only.

### 5.8 Other screens

| Screen | Treatment |
| --- | --- |
| Điểm danh hôm nay (teacher) | Today's sessions with a progress bar `marked/roster` and Đã xong / Chưa xong / Đã khóa pill |
| Chuyên cần (student, parent) | Range chips Tuần này / Tháng này / 3 tháng; ring of present ratio, four stat tiles (muộn, vắng có phép, vắng không phép, chưa điểm danh), then only the sessions that were not "Có mặt" |
| Chuyên cần lớp chủ nhiệm | Day stepper (no future days); one card per session with marked count and a pill per student who was not present; tap opens the read-only sheet |
| Học sinh lớp | Homeroom student list (search above 12 students) → the student's profile |
| Hồ sơ học sinh (homeroom, read-only) | Header tinted with the avatar's pastel: 60px avatar, name, code · age · gender, a white pill "Lớp 6A · phân hiệu" and the status pill. The note sits right below as an amber banner. A full-width **Báo cáo tuần** button opens the student's weekly book (§5.3). Then Phụ huynh (name, relationship, phone large and copyable), Chuyên cần of the current school year (presence ring, four stat tiles Có mặt · Muộn · Vắng có phép · Vắng không phép, the latest late or absent lessons), Thông tin cá nhân rows and Lịch sử lớp as a vertical timeline with the current class as a filled green dot |
| Thông báo | Pale-green unread cards with a type icon; tap re-checks permission on the server and opens the matching mobile screen, or says the content is on web |
| Bảng tin (tab) | Cover-image cards (16:9, image count badge), "Nội bộ" pill for internal posts, bell in the app bar; detail has a swipeable image strip, then title, author, content. |
| Cá nhân | Identity card (tap avatar to change or remove it), active responsibilities, contact info with "Sửa", then Đổi mật khẩu · Đăng xuất |

---

## 6. Screen states

| State | Treatment |
| --- | --- |
| Loading | Skeletons shaped like the real cards. No spinner |
| Empty | 120px illustration tinted `green200` + one sentence + action button if one exists |
| Network error | Grey centered card + **Thử lại** button; keep previously loaded data visible |
| Permission lost | Return to Trang chủ with a toast — do not render a locked screen |

Pull-to-refresh on every list. No infinite scroll — paginate with a "Xem thêm" button.

---

## 7. Flutter implementation map

No UI package. Material 3 + a custom `ThemeData`.

| Need | Use |
| --- | --- |
| Bottom-rounded gradient header | `Container` + `BoxDecoration` |
| Card | `Container` (not `Card` — its elevation API fights the token shadow) |
| Three attendance buttons | `SegmentedButton` restyled, or `Row` + `InkWell` |
| Floating bottom nav | `Container` with gradient + pill radius, one `InkWell` item per tab (not `NavigationBar`, whose indicator fights the white-on-green style) |
| Bottom sheet | `showModalBottomSheet`, top radius 24 |
| Icons | `lucide_icons_flutter` |
| Illustrations | `CustomPainter` (landscape, empty-state hills); MSMS AI mascot is `assets/images/msms_ai.png` |
| Fonts | Be Vietnam Pro 400/500/600/700 and Montserrat 300/700 bundled under `assets/fonts/` |
| Date / time formatting | `intl` with `Locale('vi')`, `dd/MM/yyyy`, `HH:mm` |

Routing via `go_router`, state via `Riverpod`, HTTP via `dio` — per `MSMS_Dev_Conventions.md` §1.

---

## 8. Out of scope

| Excluded | Reason |
| --- | --- |
| Dark mode | Doubles QA for no assessment credit |
| Push notifications | Spec: in-app notifications only (no FCM/APNs). Realtime while app is open via SignalR; polling as fallback |
| Offline-first | Not required; introduces sync complexity |
| Custom page transitions | Keep Material defaults |
| Table / grid screens | Web-only per spec §8.1 |
| Taking an assignment | Web-only per spec §8.1; mobile lists assignments and shows results (§5.6) |
| Like, comment, share on Bảng tin | Not in BR-27 — mobile **reads** the feed only |

---

## 9. Acceptance checks

An agent must verify all five before calling mobile UI done:

1. One class of 35 students can be marked with a single thumb in under 60 seconds.
2. Every touch target ≥ 48×48; no two targets closer than 8px.
3. No layout break and no clipped text at 360dp width.
4. Every badge carries text; color is never the only signal.
5. A long accented Vietnamese name (e.g. `Nguyễn Hoàng Phương Thảo`) does not overflow its card.
