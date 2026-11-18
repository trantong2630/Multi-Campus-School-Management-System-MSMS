/// Đọc JSON an toàn: Backend trả camelCase, DateOnly dạng `yyyy-MM-dd`, TimeOnly dạng `HH:mm:ss`, DateTime giờ máy chủ không kèm múi giờ.
typedef Json = Map<String, dynamic>;

extension JsonRead on Json {
  String str(String k) => this[k]?.toString() ?? '';
  String? strOrNull(String k) => this[k]?.toString();

  int integer(String k) => (this[k] as num?)?.toInt() ?? 0;
  int? intOrNull(String k) => (this[k] as num?)?.toInt();
  double? dbl(String k) => (this[k] as num?)?.toDouble();
  bool flag(String k) => this[k] == true;
  DateTime date(String k) => DateTime.parse(this[k] as String);
  DateTime? dateOrNull(String k) => this[k] == null ? null : DateTime.parse(this[k] as String);
  List<T> list<T>(String k, T Function(Json) map) => ((this[k] as List?) ?? const []).map((e) => map(e as Json)).toList();
  List<String> strings(String k) => ((this[k] as List?) ?? const []).map((e) => e.toString()).toList();
  List<int> ints(String k) => ((this[k] as List?) ?? const []).map((e) => (e as num).toInt()).toList();
  Json obj(String k) => this[k] as Json;
  Json? objOrNull(String k) => this[k] as Json?;
}

/// `HH:mm:ss` → `HH:mm`.
String hhmm(String time) => time.length >= 5 ? time.substring(0, 5) : time;

/// DateOnly gửi lên Backend.
String isoDate(DateTime d) =>
    '${d.year.toString().padLeft(4, '0')}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';
