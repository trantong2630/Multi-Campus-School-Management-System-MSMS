import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/auth/auth_controller.dart';
import '../../core/json.dart';
import '../par/children.dart';

/// Một bài trong danh sách của học sinh (MyAssignmentItemDto) hoặc của con (ChildResultItemDto), quy về một dạng.
class AssignmentItem {
  AssignmentItem({
    required this.id,
    required this.title,
    required this.subjectName,
    required this.className,
    required this.openAt,
    required this.dueAt,
    required this.durationMinutes,
    required this.questionCount,
    required this.state,
    required this.submittedAt,
    required this.score,
    required this.correctCount,
  });

  /// Học sinh: State do Backend tính (NotOpen, Open, InProgress, Submitted, NotSubmitted).
  factory AssignmentItem.mine(Json j) => AssignmentItem(
    id: j.integer('assignmentId'),
    title: j.str('title'),
    subjectName: j.str('subjectName'),
    className: j.str('className'),
    openAt: j.dateOrNull('openAt'),
    dueAt: j.date('dueAt'),
    durationMinutes: j.intOrNull('durationMinutes'),
    questionCount: j.integer('questionCount'),
    state: j.str('state'),
    submittedAt: j.dateOrNull('submittedAt'),
    score: j.dbl('score'),
    correctCount: j.intOrNull('correctCount'),
  );

  /// Phụ huynh: suy State từ Submitted và hạn nộp.
  factory AssignmentItem.child(Json j) {
    final due = j.date('dueAt');
    final submitted = j.flag('submitted');
    return AssignmentItem(
      id: j.integer('assignmentId'),
      title: j.str('title'),
      subjectName: j.str('subjectName'),
      className: j.str('className'),
      openAt: null,
      dueAt: due,
      durationMinutes: null,
      questionCount: j.integer('questionCount'),
      state: submitted ? 'Submitted' : (due.isBefore(DateTime.now()) ? 'NotSubmitted' : 'Open'),
      submittedAt: j.dateOrNull('submittedAt'),
      score: j.dbl('score'),
      correctCount: j.intOrNull('correctCount'),
    );
  }

  final int id;
  final String title;
  final String subjectName;
  final String className;
  final DateTime? openAt;
  final DateTime dueAt;
  final int? durationMinutes;
  final int questionCount;
  final String state;
  final DateTime? submittedAt;
  final double? score;
  final int? correctCount;

  bool get isTodo => state == 'NotOpen' || state == 'Open' || state == 'InProgress';

  /// Đã nộp hoặc quá hạn: mở được kết quả.
  bool get hasResult => state == 'Submitted' || state == 'NotSubmitted';
}

class ResultOption {
  ResultOption.fromJson(Json j)
    : label = j.str('label'),
      content = j.str('content'),
      isCorrect = j.flag('isCorrect'),
      selected = j.flag('selected');
  final String label;
  final String content;
  final bool isCorrect;
  final bool selected;
}

class ResultQuestion {
  ResultQuestion.fromJson(Json j)
    : orderNo = j.integer('orderNo'),
      content = j.str('content'),
      explanation = j.strOrNull('explanation'),
      isMultiple = j.flag('isMultiple'),
      isCorrect = j['isCorrect'] as bool?,
      options = j.list('options', ResultOption.fromJson),
      points = j.dbl('points') ?? 0,
      difficulty = j.strOrNull('difficulty');

  final int orderNo;
  final String content;
  final String? explanation;
  final bool isMultiple;
  final bool? isCorrect;
  final List<ResultOption> options;
  final double points;
  final String? difficulty;
}

class StudyRef {
  StudyRef.fromJson(Json j)
    : lessonId = j.integer('lessonId'),
      bookTitle = j.str('bookTitle'),
      lessonTitle = j.str('lessonTitle'),
      startPage = j.integer('startPage'),
      endPage = j.integer('endPage'),
      questionNos = j.ints('questionNos');
  final int lessonId;
  final String bookTitle;
  final String lessonTitle;
  final int startPage;
  final int endPage;
  final List<int> questionNos;

  String get pagesText => startPage == endPage ? 'trang $startPage' : 'trang $startPage–$endPage';
}

class Analysis {
  Analysis.fromJson(Json j)
    : status = j.str('status'),
      summary = j.strOrNull('summary'),
      strengths = j.strings('strengths'),
      weaknesses = j.strings('weaknesses'),
      mistakes = j.list('mistakes', (m) => (no: m.integer('questionNo'), why: m.str('why'))),
      suggestions = j.strings('suggestions'),
      studyRefs = j.list('studyRefs', StudyRef.fromJson);

  final String status;
  final String? summary;
  final List<String> strengths;
  final List<String> weaknesses;
  final List<({int no, String why})> mistakes;
  final List<String> suggestions;
  final List<StudyRef> studyRefs;
}

class PracticeResult {
  PracticeResult.fromJson(Json j)
    : assignmentId = j.integer('assignmentId'),
      title = j.str('title'),
      subjectName = j.str('subjectName'),
      className = j.str('className'),
      studentName = j.str('studentName'),
      dueAt = j.date('dueAt'),
      submitted = j.flag('submitted'),
      submittedAt = j.dateOrNull('submittedAt'),
      score = j.dbl('score'),
      correctCount = j.intOrNull('correctCount'),
      questions = j.list('questions', ResultQuestion.fromJson),
      analysis = j.objOrNull('analysis') == null ? null : Analysis.fromJson(j.obj('analysis'));

  final int assignmentId;
  final String title;
  final String subjectName;
  final String className;
  final String studentName;
  final DateTime dueAt;
  final bool submitted;
  final DateTime? submittedAt;
  final double? score;
  final int? correctCount;
  final List<ResultQuestion> questions;
  final Analysis? analysis;
}

/// Danh sách bài của học sinh đang đăng nhập hoặc của con đang chọn.
final assignmentsProvider = FutureProvider.autoDispose<List<AssignmentItem>>((ref) async {
  final user = ref.watch(currentUserProvider);
  final api = ref.read(apiProvider);
  if (user.isStudent) {
    final list = await api.get<List<dynamic>>('/api/lrn/my-assignments');
    return list.map((e) => AssignmentItem.mine(e as Json)).toList();
  }
  final child = await ref.watch(currentChildProvider.future);
  if (child == null) return const [];
  final list = await api.get<List<dynamic>>('/api/lrn/results/children/${child.studentId}');
  return list.map((e) => AssignmentItem.child(e as Json)).toList();
});

typedef ResultKey = ({int assignmentId, int? studentId});

/// Kết quả một bài; khi phân tích AI đang chạy thì tự tải lại mỗi 3 giây cho tới khi xong (như web).
final resultProvider = FutureProvider.autoDispose.family<PracticeResult, ResultKey>((ref, k) async {
  final j = await ref.read(apiProvider).get<Json>('/api/lrn/results/${k.assignmentId}', query: {'studentId': k.studentId});
  final r = PracticeResult.fromJson(j);
  if (r.analysis?.status == 'Pending') {
    final t = Timer(const Duration(seconds: 3), ref.invalidateSelf);
    ref.onDispose(t.cancel);
  }
  return r;
});
