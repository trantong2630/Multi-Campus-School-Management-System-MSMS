import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/auth/auth_controller.dart';
import '../../core/json.dart';
import '../par/children.dart';

class AttCounts {
  AttCounts.fromJson(Json j)
    : present = j.integer('present'),
      late = j.integer('late'),
      excused = j.integer('excusedAbsent'),
      unexcused = j.integer('unexcusedAbsent'),
      notMarked = j.integer('notMarked');

  final int present;
  final int late;
  final int excused;
  final int unexcused;
  final int notMarked;

  int get absent => excused + unexcused;
  int get marked => present + late + absent;
  int get total => marked + notMarked;
}

class AttSession {
  AttSession.fromJson(Json j)
    : id = j.integer('sessionId'),
      date = DateTime.parse(j.str('date')),
      sessionOfDay = j.str('sessionOfDay'),
      periodNo = j.integer('periodNo'),
      start = hhmm(j.str('startTime')),
      end = hhmm(j.str('endTime')),
      classId = j.integer('classId'),
      className = j.str('className'),
      subjectName = j.str('subjectName'),
      teacherName = j.strOrNull('teacherName'),
      status = j.str('status'),
      rosterCount = j.integer('rosterCount'),
      markedCount = j.integer('markedCount'),
      isMissing = j.flag('isMissing'),
      canWrite = j.flag('canWrite');

  final int id;
  final DateTime date;
  final String sessionOfDay;
  final int periodNo;
  final String start;
  final String end;
  final int classId;
  final String className;
  final String subjectName;
  final String? teacherName;
  final String status;
  final int rosterCount;
  final int markedCount;
  final bool isMissing;
  final bool canWrite;

  bool get done => rosterCount > 0 && markedCount >= rosterCount;
}

class SheetRow {
  SheetRow.fromJson(Json j)
    : studentId = j.integer('studentId'),
      studentCode = j.str('studentCode'),
      fullName = j.str('fullName'),
      status = j.strOrNull('status'),
      label = j.str('label'),
      note = j.strOrNull('note');

  final int studentId;
  final String studentCode;
  final String fullName;

  /// Giá trị đã lưu: Present / Late / Absent; null nếu chưa điểm danh.
  final String? status;

  /// Nhãn cuối suy ra lúc đọc (BR-18), vd. ExcusedAbsent khi có đơn nghỉ đã duyệt.
  final String label;
  final String? note;
}

class AttendanceSheet {
  AttendanceSheet.fromJson(Json j)
    : session = AttSession.fromJson(j.obj('session')),
      readOnlyReason = j.strOrNull('readOnlyReason'),
      counts = AttCounts.fromJson(j.obj('counts')),
      rows = j.list('students', SheetRow.fromJson);

  final AttSession session;
  final String? readOnlyReason;
  final AttCounts counts;
  final List<SheetRow> rows;
}

class StudentAttItem {
  StudentAttItem.fromJson(Json j)
    : sessionId = j.integer('sessionId'),
      date = DateTime.parse(j.str('date')),
      sessionOfDay = j.str('sessionOfDay'),
      periodNo = j.integer('periodNo'),
      subjectName = j.str('subjectName'),
      teacherName = j.strOrNull('teacherName'),
      label = j.str('label'),
      note = j.strOrNull('note');

  final int sessionId;
  final DateTime date;
  final String sessionOfDay;
  final int periodNo;
  final String subjectName;
  final String? teacherName;
  final String label;
  final String? note;
}

class StudentAttendance {
  StudentAttendance.fromJson(Json j)
    : fullName = j.str('fullName'),
      counts = AttCounts.fromJson(j.obj('counts')),
      items = j.list('items', StudentAttItem.fromJson);

  final String fullName;
  final AttCounts counts;
  final List<StudentAttItem> items;
}

class ClassOption {
  ClassOption.fromJson(Json j) : classId = j.integer('classId'), name = j.str('name'), isHomeroom = j.flag('isHomeroom');
  final int classId;
  final String name;
  final bool isHomeroom;
}

class ClassAttendance {
  ClassAttendance.fromJson(Json j)
    : className = j.str('className'),
      sessions = j.list('sessions', AttSession.fromJson),
      students = j.list(
        'students',
        (s) => (
          id: s.integer('studentId'),
          name: s.str('fullName'),
          code: s.str('studentCode'),
          labels: ((s['labels'] as Map?) ?? const {}).map((k, v) => MapEntry(int.parse(k.toString()), v.toString())),
        ),
      );

  final String className;
  final List<AttSession> sessions;
  final List<({int id, String name, String code, Map<int, String> labels})> students;
}

typedef AttRange = ({DateTime from, DateTime to});

class AttApi {
  AttApi(this.ref);
  final Ref ref;

  Future<AttendanceSheet> sheet(int sessionId) async =>
      AttendanceSheet.fromJson(await ref.read(apiProvider).get<Json>('/api/att/sessions/$sessionId'));

  Future<AttendanceSheet> save(int sessionId, List<Map<String, dynamic>> items) async =>
      AttendanceSheet.fromJson(await ref.read(apiProvider).put<Json>('/api/att/sessions/$sessionId', data: {'items': items}));
}

final attApiProvider = Provider<AttApi>(AttApi.new);

/// Tiết hôm nay của giáo viên kèm tiến độ điểm danh.
final todayAttendanceProvider = FutureProvider.autoDispose<List<AttSession>>((ref) async {
  ref.watch(currentUserProvider);
  final list = await ref.read(apiProvider).get<List<dynamic>>('/api/att/today');
  return list.map((e) => AttSession.fromJson(e as Json)).toList()..sort((a, b) => a.start.compareTo(b.start));
});

final sheetProvider = FutureProvider.autoDispose.family<AttendanceSheet, int>((ref, id) => ref.read(attApiProvider).sheet(id));

/// Chuyên cần của học sinh đang đăng nhập hoặc của con đang chọn trong một khoảng ngày.
final myAttendanceProvider = FutureProvider.autoDispose.family<StudentAttendance?, AttRange>((ref, r) async {
  final user = ref.watch(currentUserProvider);
  final api = ref.read(apiProvider);
  final range = {'from': isoDate(r.from), 'to': isoDate(r.to)};
  if (user.isStudent) return StudentAttendance.fromJson(await api.get<Json>('/api/att/me', query: range));
  final child = await ref.watch(currentChildProvider.future);
  if (child == null) return null;
  return StudentAttendance.fromJson(await api.get<Json>('/api/att/child', query: {...range, 'studentId': child.studentId}));
});

/// Lớp chủ nhiệm của giáo viên (để xem chuyên cần cả lớp).
final homeroomClassesProvider = FutureProvider.autoDispose<List<ClassOption>>((ref) async {
  ref.watch(currentUserProvider);
  final list = await ref.read(apiProvider).get<List<dynamic>>('/api/att/classes');
  return list.map((e) => ClassOption.fromJson(e as Json)).where((c) => c.isHomeroom).toList();
});

final classDayProvider = FutureProvider.autoDispose.family<ClassAttendance, ({int classId, DateTime day})>((ref, k) async {
  final d = isoDate(k.day);
  return ClassAttendance.fromJson(
    await ref.read(apiProvider).get<Json>('/api/att/class-summary', query: {'classId': k.classId, 'from': d, 'to': d}),
  );
});
