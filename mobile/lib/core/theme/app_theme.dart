import 'package:material_ui/material_ui.dart';

import 'app_color.dart';
import 'app_spacing.dart';

abstract final class AppTheme {
  static const fontFamily = 'BeVietnamPro';

  static ThemeData light() {
    final scheme = ColorScheme.fromSeed(
      seedColor: AppColor.green600,
      primary: AppColor.green600,
      onPrimary: Colors.white,
      surface: AppColor.surface,
      error: AppColor.danger,
    );
    final base = ThemeData(useMaterial3: true, colorScheme: scheme, fontFamily: fontFamily);
    final text = base.textTheme
        .apply(bodyColor: AppColor.text1, displayColor: AppColor.text1)
        .copyWith(
          bodyMedium: const TextStyle(fontSize: 15, height: 1.45, color: AppColor.text1),
          bodySmall: const TextStyle(fontSize: 13, height: 1.4, color: AppColor.text2),
          titleMedium: const TextStyle(fontSize: 16, fontWeight: FontWeight.w600, color: AppColor.text1),
          titleLarge: const TextStyle(fontSize: 20, fontWeight: FontWeight.w700, color: AppColor.text1),
          labelLarge: const TextStyle(fontSize: 15, fontWeight: FontWeight.w600),
        );

    OutlineInputBorder border(Color c, [double w = 1]) => OutlineInputBorder(
      borderRadius: BorderRadius.circular(AppRadius.field),
      borderSide: BorderSide(color: c, width: w),
    );

    return base.copyWith(
      scaffoldBackgroundColor: AppColor.bg,
      textTheme: text,
      appBarTheme: const AppBarTheme(
        backgroundColor: AppColor.bg,
        foregroundColor: AppColor.text1,
        elevation: 0,
        scrolledUnderElevation: 0,
        centerTitle: false,
        titleTextStyle: TextStyle(fontFamily: fontFamily, fontSize: 18, fontWeight: FontWeight.w700, color: AppColor.text1),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: AppColor.surface,
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
        hintStyle: const TextStyle(color: AppColor.text3),
        border: border(AppColor.fieldBorder),
        enabledBorder: border(AppColor.fieldBorder),
        focusedBorder: border(AppColor.green500, 1.6),
        errorBorder: border(AppColor.danger),
        focusedErrorBorder: border(AppColor.danger, 1.6),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: AppColor.green600,
          foregroundColor: Colors.white,
          disabledBackgroundColor: AppColor.disabled,
          minimumSize: const Size.fromHeight(48),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadius.field)),
          textStyle: const TextStyle(fontFamily: fontFamily, fontSize: 15, fontWeight: FontWeight.w600),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: AppColor.green700,
          minimumSize: const Size.fromHeight(48),
          side: const BorderSide(color: AppColor.borderStrong),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadius.field)),
          textStyle: const TextStyle(fontFamily: fontFamily, fontSize: 15, fontWeight: FontWeight.w600),
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          foregroundColor: AppColor.green700,
          minimumSize: const Size(48, 48),
          textStyle: const TextStyle(fontFamily: fontFamily, fontSize: 14, fontWeight: FontWeight.w600),
        ),
      ),
      bottomSheetTheme: const BottomSheetThemeData(
        backgroundColor: AppColor.surface,
        showDragHandle: true,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(AppRadius.sheet))),
      ),
      snackBarTheme: SnackBarThemeData(
        behavior: SnackBarBehavior.floating,
        backgroundColor: AppColor.ink,
        contentTextStyle: const TextStyle(fontFamily: fontFamily, color: Colors.white, fontSize: 14),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadius.md)),
      ),
      dividerTheme: const DividerThemeData(color: AppColor.border, thickness: 1, space: 1),
      progressIndicatorTheme: const ProgressIndicatorThemeData(color: AppColor.green600),
    );
  }
}
