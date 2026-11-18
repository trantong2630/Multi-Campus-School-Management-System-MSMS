import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/auth/auth_controller.dart';
import '../../core/json.dart';

class LeaveItem {
  LeaveItem.fromJson(Json j)
    : id = j.integer('leaveRequestId'),
      studentId = j.integer('studentId'),
      studentName = j.str('studentName'),
      studentCode = j.strOrNull('studentCode'),
      className = j.str('className'),
      parentName = j.strOrNull('parentName'),
      fromDate = DateTime.parse(j.str('fromDate')),
      toDate = DateTime.parse(j.str('toDate')),
      sessionOfDay = j.str('sessionOfDay'),
      slotIds = j.strOrNull('slotIds'),
      reason = j.strOrNull('reason'),
      status = j.str('status'),
      createdAt = j.date('createdAt'),
      decidedBy = j.strOrNull('decidedByPersonName'),
      decidedAt = j.dateOrNull('decidedAt'),
      decisionNote = j.strOrNull('decisionNote');

  final int id;
  final int studentId;
  final String studentName;
  final String? studentCode;
  final String className;
  final String? parentName;
  final DateTime fromDate;
  final DateTime toDate;
  final String sessionOfDay;
  final String? slotIds;
  final String? reason;
  final String status;
  final DateTime createdAt;
  final String? decidedBy;
  final DateTime? decidedAt;
  final String? decisionNote;

  bool get isPending => status == 'Pending';
}

class LeavePage {
  LeavePage(this.items, this.total);
  final List<LeaveItem> items;
  final int total;
}

class ReportWeek {
  ReportWeek.fromJson(Json j)
    : start = DateTime.parse(j.str('weekStart')),
      end = DateTime.parse(j.str('weekEnd')),
      number = j.integer('weekNumber'),
      label = j.str('label'),
      isCurrent = j.flag('isCurrent');
  final DateTime start;
  final DateTime end;
  final int number;
  final String label;
  final bool isCurrent;
}

class WeeklyAssignment {
  WeeklyAssignment.fromJson(Json j)
    : id = j.integer('assignmentId'),
      title = j.str('title'),
      subjectName = j.str('subjectName'),
      dueAt = j.date('dueAt'),
      status = j.str('status'),
      score = j.dbl('score'),
      aiFeedback = j.strOrNull('aiFeedback');
  final int id;
  final String title;
  final String subjectName;
  final DateTime dueAt;
  final String status;
  final double? score;
  final String? aiFeedback;
}

class WeeklyReport {
  WeeklyReport.fromJson(Json j)
    : studentId = j.integer('studentId'),
      studentName = j.str('studentName'),
      className = j.str('className'),
      weekStart = DateTime.parse(j.str('weekStart')),
      weekEnd = DateTime.parse(j.str('weekEnd')),
      total = j.obj('attendance').integer('totalSessions'),
      present = j.obj('attendance').integer('presentCount'),
      late = j.obj('attendance').integer('lateCount'),
      excused = j.obj('attendance').integer('excusedCount'),
      unexcused = j.obj('attendance').integer('unexcusedCount'),
      unmarked = j.obj('attendance').integer('unmarkedCount'),
      assigned = j.obj('assignments').integer('assignedCount'),
      submitted = j.obj('assignments').integer('submittedCount'),
      unsubmitted = j.obj('assignments').integer('unsubmittedCount'),
      missed = j.obj('assignments').integer('missedCount'),
      assignments = j.obj('assignments').list('items', WeeklyAssignment.fromJson);

  final int studentId;
  final String studentName;
  final String className;
  final DateTime weekStart;
  final DateTime weekEnd;
  final int total, present, late, excused, unexcused, unmarked;
  final int assigned, submitted, unsubmitted, missed;
  final List<WeeklyAssignment> assignments;
}

class HomeroomStudent {
  HomeroomStudent.fromJson(Json j)
    : id = j.integer('studentId'),
      code = j.str('studentCode'),
      name = j.str('fullName'),
      className = j.str('className');
  final int id;
  final String code;
  final String name;
  final String className;
}

class ParApi {
  ParApi(this.ref);
  final Ref ref;

  Future<LeavePage> childLeaves(int studentId, int page) async {
    final j = await ref.read(apiProvider).get<Json>('/api/par/leave-requests/student/$studentId', query: {'page': page, 'pageSize': 10});
    return LeavePage(j.list('items', LeaveItem.fromJson), j.integer('total'));
  }

  Future<LeavePage> homeroomLeaves(int page) async {
    final j = await ref.read(apiProvider).get<Json>('/api/par/leave-requests/homeroom', query: {'page': page, 'pageSize': 10});
    return LeavePage(j.list('items', LeaveItem.fromJson), j.integer('total'));
  }

  Future<void> submitLeave({
    required int studentId,
    required DateTime from,
    required DateTime to,
    required String sessionOfDay,
    String? reason,
  }) => ref
      .read(apiProvider)
      .post<dynamic>(
        '/api/par/leave-requests',
        data: {
          'studentId': studentId,
          'fromDate': isoDate(from),
          'toDate': isoDate(to),
          'sessionOfDay': sessionOfDay,
          'slotIds': null,
          'reason': (reason ?? '').trim().isEmpty ? null : reason!.trim(),
        },
      );

  Future<void> withdraw(int id) => ref.read(apiProvider).put<dynamic>('/api/par/leave-requests/$id/withdraw');
  Future<void> approve(int id) => ref.read(apiProvider).put<dynamic>('/api/par/leave-requests/$id/approve');
  Future<void> reject(int id, String note) =>
      ref.read(apiProvider).put<dynamic>('/api/par/leave-requests/$id/reject', data: {'decisionNote': note});

  Future<List<ReportWeek>> weeks(int studentId) async {
    final list = await ref.read(apiProvider).get<List<dynamic>>('/api/par/weekly-reports/weeks', query: {'studentId': studentId});
    return list.map((e) => ReportWeek.fromJson(e as Json)).toList();
  }

  Future<WeeklyReport> report(int studentId, DateTime? weekStart) async => WeeklyReport.fromJson(
    await ref
        .read(apiProvider)
        .get<Json>('/api/par/weekly-reports/student/$studentId', query: {'weekStart': weekStart == null ? null : isoDate(weekStart)}),
  );
}

final parApiProvider = Provider<ParApi>(ParApi.new);

/// Số đơn Chờ duyệt của lớp chủ nhiệm (đếm trên 30 đơn mới nhất, đủ cho trang chủ).
final pendingHomeroomLeavesProvider = FutureProvider.autoDispose<int>((ref) async {
  final user = ref.watch(currentUserProvider);
  if (!user.isHomeroom) return 0;
  final api = ref.read(parApiProvider);
  var count = 0;
  for (var page = 1; page <= 3; page++) {
    final p = await api.homeroomLeaves(page);
    count += p.items.where((i) => i.isPending).length;
    if (page * 10 >= p.total) break;
  }
  return count;
});

final homeroomStudentsProvider = FutureProvider.autoDispose<List<HomeroomStudent>>((ref) async {
  ref.watch(currentUserProvider);
  final list = await ref.read(apiProvider).get<List<dynamic>>('/api/par/weekly-reports/homeroom-students');
  return list.map((e) => HomeroomStudent.fromJson(e as Json)).toList();
});

final reportWeeksProvider = FutureProvider.autoDispose.family<List<ReportWeek>, int>(
  (ref, studentId) => ref.read(parApiProvider).weeks(studentId),
);

final weeklyReportProvider = FutureProvider.autoDispose.family<WeeklyReport, ({int studentId, DateTime? week})>(
  (ref, k) => ref.read(parApiProvider).report(k.studentId, k.week),
);
