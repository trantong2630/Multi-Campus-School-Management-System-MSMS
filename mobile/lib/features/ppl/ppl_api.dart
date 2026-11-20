import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/auth/auth_controller.dart';
import '../../core/json.dart';
import '../att/att_api.dart';

/// Hồ sơ một học sinh, chỉ xem (PPL-02, 4.3.12). Backend giới hạn chủ nhiệm ở học sinh lớp mình.
class StudentProfile {
  StudentProfile.fromJson(Json j)
    : id = j.integer('studentId'),
      code = j.str('personCode'),
      name = j.str('fullName'),
      dateOfBirth = j.dateOrNull('dateOfBirth'),
      nationalId = j.strOrNull('nationalId'),
      gender = j.strOrNull('gender'),
      phone = j.strOrNull('phoneNumber'),
      address = j.strOrNull('address'),
      status = j.str('status'),
      note = j.strOrNull('note'),
      admissionDate = j.dateOrNull('admissionDate'),
      leftDate = j.dateOrNull('leftDate'),
      className = j.strOrNull('className'),
      campusName = j.strOrNull('campusName'),
      guardian = j.objOrNull('guardian') == null ? null : StudentGuardian.fromJson(j.obj('guardian')),
      enrollments = j.list('enrollments', StudentEnrollment.fromJson),
      attendance = j.objOrNull('attendance') == null ? null : StudentAttOverview.fromJson(j.obj('attendance'));

  final int id;
  final String code;
  final String name;
  final DateTime? dateOfBirth;
  final String? nationalId;
  final String? gender;
  final String? phone;
  final String? address;
  final String status;
  final String? note;
  final DateTime? admissionDate;
  final DateTime? leftDate;
  final String? className;
  final String? campusName;
  final StudentGuardian? guardian;
  final List<StudentEnrollment> enrollments;
  final StudentAttOverview? attendance;

  bool get isStudying => status == 'Studying';
}

class StudentGuardian {
  StudentGuardian.fromJson(Json j)
    : name = j.str('fullName'),
      relationship = j.strOrNull('relationship'),
      phone = j.strOrNull('phoneNumber'),
      email = j.strOrNull('email');

  final String name;
  final String? relationship;
  final String? phone;
  final String? email;
}

class StudentEnrollment {
  StudentEnrollment.fromJson(Json j)
    : id = j.integer('enrollmentId'),
      yearName = j.str('academicYearName'),
      className = j.str('className'),
      campusName = j.str('campusName'),
      start = DateTime.parse(j.str('startDate')),
      end = DateTime.parse(j.str('endDate')),
      reason = j.strOrNull('reason'),
      isCurrent = j.flag('isCurrent');

  final int id;
  final String yearName;
  final String className;
  final String campusName;
  final DateTime start;
  final DateTime end;
  final String? reason;
  final bool isCurrent;
}

/// Chuyên cần năm học hiện hành; [rate] null khi chưa có lượt điểm danh nào.
class StudentAttOverview {
  StudentAttOverview.fromJson(Json j)
    : yearName = j.str('academicYearName'),
      to = DateTime.parse(j.str('to')),
      rate = j.dbl('rate'),
      counts = AttCounts.fromJson(j.obj('counts')),
      recent = j.list('recent', StudentAttItem.fromJson);

  final String yearName;
  final DateTime to;
  final double? rate;
  final AttCounts counts;
  final List<StudentAttItem> recent;
}

final studentProfileProvider = FutureProvider.autoDispose.family<StudentProfile, int>((ref, id) async {
  ref.watch(currentUserProvider);
  final j = await ref.read(apiProvider).get<Json>('/api/ppl/students/$id');
  return StudentProfile.fromJson(j);
});
