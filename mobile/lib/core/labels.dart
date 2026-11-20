import 'theme/app_color.dart';

/// Bản Dart của `labels/status.ts` trên web: dịch enum một chỗ (Dev_Conventions §4.4), nhóm màu theo Design_Web §2.4.
enum StatusDomain { account, session, attendance, leave, aiFeedback, submission, post, attempt, student }

typedef _Entry = (String label, BadgeGroup group);

const Map<StatusDomain, Map<String, _Entry>> _registry = {
  StatusDomain.account: {
    'PendingPasswordChange': ('Chờ đổi mật khẩu', BadgeGroup.pending),
    'Active': ('Đang hoạt động', BadgeGroup.active),
    'Locked': ('Đã khóa', BadgeGroup.danger),
  },
  StatusDomain.session: {
    'Scheduled': ('Đã xếp lịch', BadgeGroup.active),
    'Pending': ('Chờ xử lý', BadgeGroup.pending),
    'Cancelled': ('Đã hủy', BadgeGroup.danger),
  },
  StatusDomain.attendance: {
    'Present': ('Có mặt', BadgeGroup.active),
    'Late': ('Muộn', BadgeGroup.info),
    'ExcusedAbsent': ('Vắng có phép', BadgeGroup.info),
    'UnexcusedAbsent': ('Vắng không phép', BadgeGroup.danger),
    'NotMarked': ('Chưa điểm danh', BadgeGroup.neutral),
  },
  StatusDomain.leave: {
    'Pending': ('Chờ duyệt', BadgeGroup.pending),
    'Approved': ('Đã duyệt', BadgeGroup.active),
    'Rejected': ('Đã từ chối', BadgeGroup.danger),
    'Withdrawn': ('Đã rút', BadgeGroup.neutral),
  },
  StatusDomain.aiFeedback: {
    'Pending': ('Đang phân tích', BadgeGroup.pending),
    'Done': ('Xong', BadgeGroup.active),
    'Failed': ('Lỗi', BadgeGroup.neutral),
  },
  StatusDomain.submission: {'Submitted': ('Đã nộp', BadgeGroup.info), 'NotSubmitted': ('Không nộp', BadgeGroup.neutral)},
  StatusDomain.post: {'Posted': ('Đã đăng', BadgeGroup.active), 'Hidden': ('Đã ẩn', BadgeGroup.neutral)},
  // Trạng thái bài với học sinh (MyAssignmentItemDto.State).
  StatusDomain.attempt: {
    'NotOpen': ('Chưa mở', BadgeGroup.neutral),
    'Open': ('Chưa làm', BadgeGroup.pending),
    'InProgress': ('Đang làm', BadgeGroup.pending),
    'Submitted': ('Đã nộp', BadgeGroup.info),
    'NotSubmitted': ('Không nộp', BadgeGroup.neutral),
  },
  StatusDomain.student: {
    'Studying': ('Đang học', BadgeGroup.active),
    'Transferred': ('Đã chuyển trường', BadgeGroup.danger),
    'Graduated': ('Đã tốt nghiệp', BadgeGroup.info),
  },
};

String statusLabel(StatusDomain domain, String value) => _registry[domain]?[value]?.$1 ?? value;

/// Một hàm duy nhất quyết định nhóm màu badge, không map riêng theo màn hình.
BadgeGroup statusGroup(StatusDomain domain, String value) => _registry[domain]?[value]?.$2 ?? BadgeGroup.neutral;

String sessionOfDayLabel(String v) => v == 'Morning' ? 'Sáng' : 'Chiều';

/// Giới tính lưu trên hồ sơ.
String genderLabel(String v) => switch (v) {
  'Male' => 'Nam',
  'Female' => 'Nữ',
  'Other' => 'Khác',
  _ => v,
};

/// Lý do kết thúc ghi danh (SCH-05), như `labels/enrollment.ts` trên web.
const enrollmentCloseReasonLabel = {'ClassTransfer': 'Chuyển lớp', 'SchoolTransfer': 'Chuyển trường', 'Graduated': 'Tốt nghiệp'};

/// Phạm vi trong ngày của đơn xin nghỉ.
String leaveScopeLabel(String sessionOfDay, String? slotIds) {
  if (slotIds != null && slotIds.trim().isNotEmpty) {
    final n = slotIds.split(',').where((s) => s.trim().isNotEmpty).length;
    return '$n tiết buổi ${sessionOfDay == 'Morning' ? 'sáng' : 'chiều'}';
  }
  return switch (sessionOfDay) {
    'AllDay' => 'Cả ngày',
    'Morning' => 'Buổi sáng',
    'Afternoon' => 'Buổi chiều',
    _ => sessionOfDay,
  };
}

const notificationTypeLabel = {
  'TimetablePublished': 'Thời khóa biểu',
  'SessionPending': 'Tiết chờ xử lý',
  'ScheduleChanged': 'Thay đổi lịch',
  'AssignmentAssigned': 'Bài tập mới',
  'MessageReceived': 'Tin nhắn',
};

const difficultyLabel = {'Recognize': 'Nhận biết', 'Understand': 'Thông hiểu', 'Apply': 'Vận dụng', 'HighApply': 'Vận dụng cao'};

const _tones = [Accent.green, Accent.sky, Accent.violet, Accent.amber, Accent.teal, Accent.coral];

/// Màu nhấn ổn định theo một khóa (tên người, môn học): cùng hàm băm và thứ tự tông với `PersonAvatar` trên web,
/// nên một người có cùng màu avatar trên cả hai nền tảng.
Accent accentFor(String key) {
  var h = 0;
  for (final rune in key.runes) {
    final unit = rune > 0xFFFF ? String.fromCharCode(rune).codeUnitAt(0) : rune;
    h = (h * 31 + unit) & 0xFFFFFFFF;
  }
  return _tones[h % _tones.length];
}
