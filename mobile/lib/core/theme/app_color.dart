import 'package:material_ui/material_ui.dart';

/// Nguồn duy nhất của mã màu (kế thừa token web, Design_Web §2). Widget không ghi hex trực tiếp.
abstract final class AppColor {
  static const green50 = Color(0xFFEEFBF3);
  static const green100 = Color(0xFFD8F6E3);
  static const green200 = Color(0xFFB2EDC8);
  static const green300 = Color(0xFF83DDA6);
  static const green500 = Color(0xFF47C987);
  static const green600 = Color(0xFF2DB872); // nút chính
  static const green700 = Color(0xFF1D8E55);
  static const green800 = Color(0xFF17744A);

  /// Xanh mực: số liệu lớn.
  static const ink = Color(0xFF12353A);

  static const amber = Color(0xFFF2A93B);
  static const amberInk = Color(0xFF9A5B05);
  static const amberSoft = Color(0xFFFDF1DC);

  static const coralFg = Color(0xFFE5594C);
  static const coralBg = Color(0xFFFDECEA);
  static const amberFg = Color(0xFFC97F0A);
  static const amberBg = Color(0xFFFDF3E2);
  static const skyFg = Color(0xFF2F80DB);
  static const skyBg = Color(0xFFE8F2FD);
  static const violetFg = Color(0xFF6B5CE0);
  static const violetBg = Color(0xFFEEECFD);
  static const tealFg = Color(0xFF0F9AA3);
  static const tealBg = Color(0xFFE0F5F6);

  static const bg = Color(0xFFF4FCF7);
  static const surface = Color(0xFFFFFFFF);
  static const sunken = Color(0xFFF1F8F4);
  static const border = Color(0xFFE6EDF0);
  static const borderStrong = Color(0xFFD6E0E5);
  static const fieldBorder = Color(0xFFD6E0E5);

  static const text1 = Color(0xFF13292B);
  static const text2 = Color(0xFF51666A);
  static const text3 = Color(0xFF8A9DA2);

  static const logoDot = Color(0xFFF5B75C);
  static const limeSoft = Color(0xFFD9F4B8);
  static const aqua = Color(0xFF8CF4D2);

  /// Ba lớp đồi (xa → gần) trong tranh đăng nhập và header.
  static const meadow1 = Color(0xFFA6E39A);
  static const meadow2 = Color(0xFF5CC477);
  static const meadow3 = Color(0xFF1F9A55);

  static const disabled = Color(0xFFC2CDD1);
  static const danger = Color(0xFFD64535);
  static const neutralBg = Color(0xFFEEF4F1);

  /// Bong bóng chat của bên kia (Design_Mobile §5.5).
  static const bubbleOther = Color(0xFFEFF2F0);
}

/// Bộ màu nhấn: [fg]/[bg] là cặp chữ / nền pastel (đậm hơn web một bậc) cho avatar, ô chữ môn, khối nhận xét, băng thông báo;
/// [solid] là nền đặc cho ô số liệu và ô hành động trên trang chủ, chữ trắng. Sáu màu đặc cùng độ sáng để đứng cạnh nhau hài hòa
/// (Design_Mobile §2.2).
enum Accent {
  green(Color(0xFF17744A), Color(0xFFC4EED5), Color(0xFF2FB98B)),
  teal(Color(0xFF0B8088), Color(0xFFC6EAEC), Color(0xFF2AAFBC)),
  amber(Color(0xFFA9660A), Color(0xFFFBE2B8), Color(0xFFF6A04D)),
  coral(Color(0xFFD2463A), Color(0xFFF9D2CD), Color(0xFFF07070)),
  sky(Color(0xFF2468C2), Color(0xFFCFE2FA), Color(0xFF4C9BEA)),
  violet(Color(0xFF5848CF), Color(0xFFDCD7FA), Color(0xFF857DEB));

  const Accent(this.fg, this.bg, this.solid);
  final Color fg;
  final Color bg;
  final Color solid;
}

/// Năm nhóm màu trạng thái (Design_Web §2.4).
enum BadgeGroup {
  active(Color(0xFF1F9D5C)),
  pending(Color(0xFFC26A0A)),
  neutral(Color(0xFF64787D)),
  danger(Color(0xFFD0392B)),
  info(Color(0xFF2F7BD6));

  const BadgeGroup(this.color);
  final Color color;
}

abstract final class AppGradient {
  /// Header trang chủ: xanh nhạt, chữ xanh đậm (Design_Mobile §2.1).
  static const header = LinearGradient(
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
    colors: [Color(0xFF9DEFD3), Color(0xFFD5F5E3)],
  );

  /// Nền màn đăng nhập: aqua mint chuyển xanh lá non, như web.
  static const authPage = LinearGradient(
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
    colors: [Color(0xFF86F2CF), Color(0xFFA9F0C4), Color(0xFFD2F4B2)],
  );

  /// Bầu trời trong tranh phong cảnh.
  static const sky = LinearGradient(begin: Alignment.topCenter, end: Alignment.bottomCenter, colors: [AppColor.aqua, AppColor.limeSoft]);
}
