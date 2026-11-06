// Nhật ký (AuditLog, BR-28): ba nhóm sự kiện, các hành động và trường nội dung của bản ghi.
export const auditCategoryLabel: Record<string, string> = {
  Account: 'Tài khoản',
  TimetablePublish: 'Phát hành thời khóa biểu',
  ScheduleAdjust: 'Điều chỉnh lịch',
};

export const auditActionLabel: Record<string, string> = {
  CreateAccount: 'Tạo tài khoản',
  BulkCreateAccounts: 'Tạo tài khoản hàng loạt',
  LockAccount: 'Khóa tài khoản',
  BulkLockAccounts: 'Khóa tài khoản hàng loạt',
  UnlockAccount: 'Mở khóa tài khoản',
  ResetPassword: 'Cấp lại mật khẩu tạm',
  ChangeAccountType: 'Đổi loại tài khoản',
  PublishTimetable: 'Phát hành thời khóa biểu',
  ChangeSessionTeacher: 'Đổi giáo viên của tiết',
  CancelSession: 'Hủy tiết',
  AddMakeUpSession: 'Thêm tiết bù',
  DeclareTeacherAbsence: 'Khai báo giáo viên vắng',
  DeleteTeacherAbsence: 'Xóa khai báo giáo viên vắng',
  ResolvePendingSubstitute: 'Xử lý tiết chờ: dạy thay',
  ResolvePendingCancel: 'Xử lý tiết chờ: hủy tiết',
};

export const auditTargetKindLabel: Record<string, string> = {
  Account: 'Tài khoản',
  TimetableVersion: 'Phiên bản thời khóa biểu',
  TeachingSession: 'Tiết học',
  TeacherAbsence: 'Khai báo vắng',
};

export const auditFieldLabel: Record<string, string> = {
  count: 'Số tài khoản',
  reason: 'Lý do',
  enrollmentId: 'Mã ghi danh',
  enrollmentIds: 'Mã ghi danh',
  pendingSessions: 'Số tiết chuyển Chờ xử lý',
  closedDuties: 'Số trách nhiệm đã kết thúc',
  from: 'Từ',
  to: 'Đến',
  campusId: 'Phân hiệu',
  semesterId: 'Học kỳ',
  classes: 'Số lớp',
  deleted: 'Số tiết xóa',
  created: 'Số tiết tạo',
  sessionId: 'Mã tiết',
  sessionIds: 'Mã các tiết',
  classId: 'Lớp',
  subjectId: 'Môn',
  date: 'Ngày',
  periodSlotId: 'Tiết',
  slotIds: 'Các tiết',
  teacherId: 'Giáo viên',
  fromTeacherId: 'Giáo viên cũ',
  toTeacherId: 'Giáo viên mới',
  pending: 'Số tiết chuyển Chờ xử lý',
  restored: 'Mã các tiết trả lại',
  absenceId: 'Mã khai báo vắng',
};
