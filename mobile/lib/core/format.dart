import 'package:intl/intl.dart';

/// Định dạng ngày giờ một chỗ (Dev_Conventions §4.8): `dd/MM/yyyy`, `HH:mm`, tiếng Việt.
abstract final class Fmt {
  static final _date = DateFormat('dd/MM/yyyy');
  static final _short = DateFormat('dd/MM');
  static final _time = DateFormat('HH:mm');

  static String date(DateTime d) => _date.format(d);
  static String shortDate(DateTime d) => _short.format(d);
  static String time(DateTime d) => _time.format(d);
  static String dateTime(DateTime d) => '${_time.format(d)} ${_date.format(d)}';

  /// Thứ 2 … Thứ 7, Chủ nhật.
  static String weekday(DateTime d) => d.weekday == DateTime.sunday ? 'Chủ nhật' : 'Thứ ${d.weekday + 1}';
  static String weekdayShort(DateTime d) => d.weekday == DateTime.sunday ? 'CN' : 'T${d.weekday + 1}';

  /// "Thứ 4, 18/11/2026".
  static String longDate(DateTime d) => '${weekday(d)}, ${date(d)}';

  /// Mốc thời gian tương đối cho danh sách: "Vừa xong", "5 phút trước", "Hôm qua", hoặc ngày.
  static String ago(DateTime d, [DateTime? now]) {
    final n = now ?? DateTime.now();
    final diff = n.difference(d);
    if (diff.inMinutes < 1) return 'Vừa xong';
    if (diff.inMinutes < 60) return '${diff.inMinutes} phút trước';
    if (diff.inHours < 24 && n.day == d.day) return time(d);
    if (diff.inDays < 2 && isSameDay(n.subtract(const Duration(days: 1)), d)) return 'Hôm qua';
    if (diff.inDays < 7) return weekday(d);
    return date(d);
  }

  /// Thời gian còn lại tới hạn: "Còn 2 ngày", "Còn 5 giờ", "Còn 20 phút", "Đã hết hạn".
  static String remaining(DateTime due, [DateTime? now]) {
    final left = due.difference(now ?? DateTime.now());
    if (left.isNegative) return 'Đã hết hạn';
    if (left.inDays >= 1) return 'Còn ${left.inDays} ngày';
    if (left.inHours >= 1) return 'Còn ${left.inHours} giờ';
    return 'Còn ${left.inMinutes.clamp(1, 59)} phút';
  }

  static String score(num? s) {
    if (s == null) return '—';
    final v = s.toDouble();
    return v == v.roundToDouble() ? v.toStringAsFixed(0) : v.toStringAsFixed(2).replaceAll(RegExp(r'0$'), '').replaceAll('.', ',');
  }
}

bool isSameDay(DateTime a, DateTime b) => a.year == b.year && a.month == b.month && a.day == b.day;

DateTime dateOnly(DateTime d) => DateTime(d.year, d.month, d.day);

/// Thứ 2 của tuần chứa ngày d.
DateTime weekStart(DateTime d) => dateOnly(d).subtract(Duration(days: d.weekday - 1));
