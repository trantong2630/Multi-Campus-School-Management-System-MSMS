import 'package:material_ui/material_ui.dart';

abstract final class AppSpacing {
  static const xs = 4.0, sm = 8.0, md = 12.0, lg = 16.0, xl = 24.0, xxl = 32.0;

  /// Lề ngang của màn hình (Design_Mobile §2).
  static const screen = 16.0;
}

abstract final class AppRadius {
  static const sm = 8.0, md = 12.0, field = 14.0, card = 18.0, sheet = 24.0, pill = 999.0;
}

abstract final class AppShadow {
  static const card = [BoxShadow(color: Color(0x0D102820), blurRadius: 14, offset: Offset(0, 4))];
  static const pop = [BoxShadow(color: Color(0x1F102820), blurRadius: 28, offset: Offset(0, 8))];
  static const float = [BoxShadow(color: Color(0x2E065A28), blurRadius: 40, offset: Offset(0, 16))];
}
