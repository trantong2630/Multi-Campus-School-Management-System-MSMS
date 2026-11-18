import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/auth/auth_controller.dart';
import '../../core/format.dart';
import '../../core/json.dart';
import '../par/children.dart';

/// Một tiết trong lịch cá nhân (MySessionDto của giáo viên, ViewSessionDto của học sinh, phụ huynh).
class Session {
  Session.fromJson(Json j)
    : id = j.integer('sessionId'),
      date = DateTime.parse(j.str('date')),
      sessionOfDay = j.str('sessionOfDay'),
      periodNo = j.integer('periodNo'),
      start = hhmm(j.str('startTime')),
      end = hhmm(j.str('endTime')),
      className = j.str('className'),
      campusName = j.str('campusName'),
      subjectName = j.str('subjectName'),
      subjectAbbr = j.str('subjectAbbr'),
      teacherName = j.strOrNull('teacherName'),
      status = j.str('status'),
      source = j.str('source');

  final int id;
  final DateTime date;
  final String sessionOfDay;
  final int periodNo;
  final String start;
  final String end;
  final String className;
  final String campusName;
  final String subjectName;
  final String subjectAbbr;
  final String? teacherName;
  final String status;
  final String source;

  bool get isCancelled => status == 'Cancelled';
  bool get isPending => status == 'Pending';

  /// Thời điểm bắt đầu và kết thúc trong ngày của tiết.
  DateTime get startsAt => _at(start);
  DateTime get endsAt => _at(end);
  DateTime _at(String hm) => DateTime(date.year, date.month, date.day, int.parse(hm.substring(0, 2)), int.parse(hm.substring(3, 5)));
}

class WeekKey {
  const WeekKey(this.monday, [this.studentId]);
  final DateTime monday;
  final int? studentId;

  @override
  bool operator ==(Object other) => other is WeekKey && other.monday == monday && other.studentId == studentId;
  @override
  int get hashCode => Object.hash(monday, studentId);
}

/// Lịch một tuần của người đang đăng nhập (giáo viên, học sinh) hoặc của con đang chọn (phụ huynh).
final weekScheduleProvider = FutureProvider.autoDispose.family<List<Session>, DateTime>((ref, monday) async {
  final user = ref.watch(currentUserProvider);
  final api = ref.read(apiProvider);
  final range = {'from': isoDate(monday), 'to': isoDate(monday.add(const Duration(days: 6)))};
  final List<dynamic> list;
  if (user.isTeacher) {
    list = await api.get<List<dynamic>>('/api/scd/schedule/mine', query: range);
  } else if (user.isStudent) {
    list = await api.get<List<dynamic>>('/api/scd/schedule/student', query: range);
  } else {
    final child = await ref.watch(currentChildProvider.future);
    if (child == null) return const [];
    list = await api.get<List<dynamic>>('/api/scd/schedule/child', query: {...range, 'studentId': child.studentId});
  }
  final sessions = list.map((e) => Session.fromJson(e as Json)).toList()
    ..sort((a, b) {
      final d = a.date.compareTo(b.date);
      if (d != 0) return d;
      return a.start.compareTo(b.start);
    });
  return sessions;
});

/// Lịch hôm nay (dùng cho trang chủ học sinh, phụ huynh).
final todayScheduleProvider = FutureProvider.autoDispose<List<Session>>((ref) async {
  final today = dateOnly(DateTime.now());
  final week = await ref.watch(weekScheduleProvider(weekStart(today)).future);
  return week.where((s) => isSameDay(s.date, today)).toList();
});

/// Lớp học sinh đang học (hoặc của con) để hiện ở đầu lịch.
final myClassProvider = FutureProvider.autoDispose<String?>((ref) async {
  final user = ref.watch(currentUserProvider);
  final api = ref.read(apiProvider);
  final date = isoDate(DateTime.now());
  Json? j;
  if (user.isStudent) {
    j = await api.get<Json?>('/api/scd/schedule/student/class', query: {'date': date});
  } else if (user.isParent) {
    final child = await ref.watch(currentChildProvider.future);
    if (child == null) return null;
    j = await api.get<Json?>('/api/scd/schedule/child/class', query: {'studentId': child.studentId, 'date': date});
  }
  return j == null ? null : '${j.str('className')} · ${j.str('campusName')}';
});
