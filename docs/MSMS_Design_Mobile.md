# MSMS — Mobile Design System

20/10/2026 · V1.3 · Trần Đức Kiên

**Audience: coding agents.** Companion to `MSMS_Design_Web.md` (shared tokens) and Business Specification V3 §8.1 (mobile scope).

**Binding rules for any agent implementing mobile UI:**
1. Mobile serves **Teacher, Student, Parent** only. School Admin, Principal, Vice Principal are web-only. Never build a mobile screen for them.
2. Never hard-code a hex value in a widget. Import from `lib/core/theme/`.
3. All user-visible strings are Vietnamese, from the Flutter counterpart of `labels.ts`. Code identifiers stay English.
4. Flutter Material 3 only. Do not add a UI component package.
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
| Quick actions as a grid of pastel cards | Screens that are all text |
| Big numbers, small labels underneath | Numbers and labels at the same size |
| Floating bottom nav, rounded, 12 above the screen bottom | Default edge-docked bottom nav |
| Solid color only on primary buttons and FAB | Solid-filled cards |

---

## 2. Tokens

Color, type, and radius **inherit `MSMS_Design_Web.md` §2–4 (V1.1) unchanged** — page `bg` is `#F1F5F2`, `sm/md` radius 10/12, status badges are pills. The mobile header gradient is the same `gradient.hero` used by the web hero banner. Mobile overrides exactly five values:

| Aspect | Web | Mobile |
| --- | --- | --- |
| Card radius | 20 | **18** |
| Card shadow | `0 10px 28px .07` | **`0 4px 14px rgba(16,40,32,.05)`** |
| Screen horizontal padding | 24 | **16** |
| Base font size | 14 | **15** |
| Minimum touch target | — | **48×48** |

### 2.1 Header gradient

```dart
BoxDecoration(
  gradient: LinearGradient(
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
    colors: [Color(0xFF8FDCC0), Color(0xFFCFF0E2)], // green300 → pale green100
  ),
  borderRadius: BorderRadius.vertical(bottom: Radius.circular(24)),
)
```

Text on the header uses `green800` `#08523B`, **not white** — the gradient is too light for white text. Header height 150–180, containing: greeting, class/date line, and two white pill quick-action buttons.

### 2.2 Action card colors

Six tinted backgrounds, using the `*Bg` / `*Fg` pairs from web §2.2: green · teal · amber · coral · sky · violet. **No color repeats inside one grid.** Mobile is the warmer platform, so a 2×2 grid may use up to four different pastels, but green stays the lead (header, bottom nav, primary buttons, selected states) and `coral` is reserved for things that need attention. Web's sprinkling rule (§2.2) is the stricter one; mobile relaxes it only in the quick-action grid.

### 2.3 Theme implementation

Define `lib/core/theme/app_color.dart`, `app_spacing.dart`, `app_radius.dart`, `app_theme.dart`. Widgets import these; raw hex in a widget file is a review rejection.

```dart
// lib/core/theme/app_color.dart
class AppColor {
  static const green50  = Color(0xFFEEF8F3);
  static const green100 = Color(0xFFD8F0E5);
  static const green200 = Color(0xFFA9E0C8);
  static const green300 = Color(0xFF6CCBA6);
  static const green500 = Color(0xFF16A47A);
  static const green600 = Color(0xFF0E8A63); // primary fill
  static const green700 = Color(0xFF0B6E4F);
  static const green800 = Color(0xFF08523B);

  static const coralFg  = Color(0xFFE0574B); static const coralBg  = Color(0xFFFDEDEB);
  static const amberFg  = Color(0xFFC97F0A); static const amberBg  = Color(0xFFFDF3E2);
  static const skyFg    = Color(0xFF2E74B5); static const skyBg    = Color(0xFFE9F2FA);
  static const violetFg = Color(0xFF6A5BC9); static const violetBg = Color(0xFFEFEDFB);
  static const tealFg   = Color(0xFF128E90); static const tealBg   = Color(0xFFE3F4F4);

  static const bg = Color(0xFFF1F5F2);
  static const surface = Color(0xFFFFFFFF);
  static const border = Color(0xFFE8ECEA);
  static const text1 = Color(0xFF16211C);
  static const text2 = Color(0xFF5B6B64);
  static const text3 = Color(0xFF92A09A);
  static const danger = Color(0xFFD64535);
}
```

```dart
// lib/core/theme/app_spacing.dart
class AppSpacing { static const xs=4.0, sm=8.0, md=12.0, lg=16.0, xl=24.0, xxl=32.0; }
class AppRadius  { static const sm=8.0, md=12.0, card=18.0, sheet=24.0, pill=999.0; }
```

Status badge grouping reuses the same five-group mapping as web §2.4. Implement one `statusGroup()` in Dart mirroring the TypeScript one — do not re-derive per screen.

### 2.4 Brand wordmark

The logo is identical to web (Web §3.1): text `msms.` in Montserrat 700 + 300 with an amber `logoDot` period, never an image. Bundle the two Montserrat weights as font assets (not a UI package) and expose one `BrandLogo` widget with `tone: green | white` and `size`. White tone on the green login panel and the home header; green tone on light surfaces. Same prohibitions as web: no recoloring, no tagline beside it, no other font.

---

## 3. Navigation

### 3.1 Bottom nav — max 4 tabs, per account type

| Actor | Tabs |
| --- | --- |
| **Teacher** | Trang chủ · Lịch dạy · Hộp thư · Cá nhân |
| **Student** | Trang chủ · Lịch học · Bài tập · Cá nhân |
| **Parent** | Trang chủ · Lịch của con · Hộp thư · Cá nhân |

Spec: white, radius 20, floated 12 above the bottom and inset 16 each side, shadow `pop`. Selected tab = filled icon + label 11/600 `green700`. Unselected = outline icon + label `text3`.

**Notifications are not a tab** — a bell in the header's top-right, identical to web. Keep both platforms consistent.

### 3.2 Depth

Everything else is entered from Trang chủ or from a list row. Maximum **3 levels**: Tab → List → Detail. Needing a 4th level means the screen belongs on web.

### 3.3 Parent with multiple children

A child selector sits in the Trang chủ header as an avatar+name chip, opening a bottom sheet. **The selected child applies app-wide**; never re-select per screen.

---

## 4. Home screens

Shared skeleton: **gradient header → highlight card → quick-action grid → short list.**

### 4.1 Teacher

| Block | Content |
| --- | --- |
| Header | `"Chào, {tên}"` · weekday + date · two pills: **Điểm danh** · **Lịch hôm nay** |
| Highlight card | Next session: subject, class, period slot, **Điểm danh** button. If none: `"Hết tiết hôm nay"` |
| 2×2 grid | Tiết hôm nay (count) · Chưa điểm danh (coral) · Đơn nghỉ chờ duyệt *(homeroom only, amber)* · Tin nhắn chưa đọc (sky) |
| List | Next 3 sessions today, each with a status badge |

### 4.2 Student

| Block | Content |
| --- | --- |
| Header | Name · class · weekday + date |
| Highlight card | Current or next session |
| 2×2 grid | Bài chưa nộp (coral) · Sắp hết hạn (amber) · Điểm mới công bố (violet) · Chuyên cần tuần (teal) |
| List | Bảng tin — 3 latest posts, cover-image cards |

### 4.3 Parent

| Block | Content |
| --- | --- |
| Header | Child chip · child's class |
| Highlight card | This week's contact book — 4 figures: có mặt / muộn / vắng có phép / vắng không phép |
| 2×2 grid | Xin nghỉ (green, opens form) · Đơn đang chờ (amber) · Điểm mới (violet) · Nhắn giáo viên (sky) |
| List | Bảng tin |

---

## 5. Key screens

### 5.1 Attendance (ATT-01) — the most important screen

Target: a teacher finishes one class in **under 60 seconds, one-handed**.

| Element | Spec |
| --- | --- |
| Sticky header | Subject · class · period slot · date. Below it, four live counts: Có mặt / Muộn / Vắng / Chưa |
| Bulk action | **"Tất cả có mặt"** button at the top of the list — the common case; the teacher then corrects a few |
| Student row | Card radius 18, height 68, 40px avatar left, name + code, three segmented buttons right |
| Segmented buttons | C / M / V, height 40, equal width; selected state fills with the matching tint |
| Note | Pencil icon at the far right opens a bottom sheet for an optional note |
| Save | Primary button docked to the bottom, showing progress: `"Lưu · 32/32"` |
| Partial | Save is always enabled — persists what is set, leaves the rest as "Chưa điểm danh" |

**Outside the session's own date:** the whole screen becomes read-only with a grey banner reading `"Tiết đã qua, không sửa được. Liên hệ quản trị để nhập bù."` (subjectless phrasing per Conventions §4.6 — never address the user as "bạn").

### 5.1b People lists (avatar rows)

Lists of people (class roster, children, inbox counterparts, teachers) use the web person-cell pattern (`MSMS_Design_Web.md` §8.2b) as a **row card** instead of a table row: card radius 18, height 68, 40px round avatar on a stable-hash accent `Bg`, name 15/500, one 12px `text3` line below (student code, class, role), status pill or trailing control on the right. Same avatar tint function as web so one person has the same color on both platforms. Long accented names truncate with an ellipsis inside the card.

### 5.2 Teaching / learning schedule

Horizontally scrolling 7-day strip at the top; today is highlighted with a `green600` fill and white text. Below, a vertical timeline: start time in the left gutter, session card on the right.

Session card: 4px left border colored by status · subject 15/600 · class + room 13 `text2` · status badge top-right.
- **Đã hủy:** strikethrough text, grey background, not tappable.
- **Chờ xử lý:** amber tint, with the line `"Chờ phân công giáo viên"`.

### 5.3 Weekly contact book (PAR-02)

Week chosen with left/right arrows in the header. Three stacked cards: **Chuyên cần** (4 figures + a ratio ring) · **Bài tập** (assigned / submitted / not submitted) · **Điểm và nhận xét đã công bố** (list).

Sections with no data render empty with one grey line. **Do not render `0`** — it reads as a real measurement.

### 5.4 Leave request (PAR-01)

Single-screen form, no wizard: select child → from/to date → session (3 chips: Cả ngày / Sáng / Chiều) → reason.

Dates before today are disabled in the picker (BR-19) with an inline explanation of why. The history list uses status badges; a `Chờ duyệt` request exposes a red text button **Rút đơn**.

### 5.5 Inbox (PAR-03)

Conversation list: avatar · counterpart name · `"về {tên học sinh}"` at 12px `text2` · last message · timestamp · green dot for unread.

Thread: bubbles radius 16; own messages fill `green600` with white text, counterpart `#EFF2F0`. Show a character counter past 1,800 of 2,000.

When the underlying relationship has ended, replace the composer with a grey banner: `"Hội thoại đã kết thúc, chỉ xem lại."`

### 5.6 Assignments (LRN-02, student side)

List cards: title · subject · **countdown to deadline** (switches to coral under 24h) · status badge.

Attempt screen: question progress bar on top, one question per screen, four selectable option cards, Previous/Next at the bottom. A question-number grid in a bottom sheet allows jumping.

**Nộp bài** requires confirmation: `"Nộp một lần, không làm lại được."` (BR-21)

### 5.7 Login and forced password change

The mobile counterpart of Web §6.0, with the same identity: a full-screen `gradient.sider` panel (the web sider gradient) with the white MSMS wordmark (Web §3.1) at the top (no tagline or extra copy), then the form directly on the green — labels white, fields white, radius 14. Primary button is **inverted** (white fill, `green700` text, height 48, full width) for the same reason as on web. Errors appear as a pale in-page banner above the button, never a toast. Forced password change reuses the exact layout with the title "Đổi mật khẩu lần đầu" and a white text link "Đăng xuất". One shared `AuthScaffold` widget serves both screens.

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
| Floating bottom nav | `NavigationBar` wrapped in `Padding` + `ClipRRect` |
| Bottom sheet | `showModalBottomSheet`, top radius 24 |
| Icons | `lucide_icons` |
| Illustrations | `flutter_svg` |
| Date / time formatting | `intl` with `Locale('vi')`, `dd/MM/yyyy`, `HH:mm` |

Routing via `go_router`, state via `Riverpod`, HTTP via `dio` — per `MSMS_Dev_Conventions.md` §1.

---

## 8. Out of scope

| Excluded | Reason |
| --- | --- |
| Dark mode | Doubles QA for no assessment credit |
| Push notifications | Spec: in-app notifications only, polling |
| Offline-first | Not required; introduces sync complexity |
| Custom page transitions | Keep Material defaults |
| Table / grid screens | Web-only per spec §8.1 |
| Like, comment, share on Bảng tin | Not in BR-27 — mobile **reads** the feed only |

---

## 9. Acceptance checks

An agent must verify all five before calling mobile UI done:

1. One class of 35 students can be marked with a single thumb in under 60 seconds.
2. Every touch target ≥ 48×48; no two targets closer than 8px.
3. No layout break and no clipped text at 360dp width.
4. Every badge carries text; color is never the only signal.
5. A long accented Vietnamese name (e.g. `Nguyễn Hoàng Phương Thảo`) does not overflow its card.
