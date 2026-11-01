// Danh sách đóng loại thông báo (BR-26). Khóa là NotificationType lưu trong DB.
export const notificationTypeLabel: Record<string, string> = {
  TimetablePublished: 'Thời khóa biểu',
  SessionPending: 'Tiết chờ xử lý',
  ScheduleChanged: 'Thay đổi lịch',
  LeaveRequestSubmitted: 'Đơn xin nghỉ',
  LeaveRequestDecided: 'Kết quả đơn nghỉ',
  AssignmentAssigned: 'Bài tập mới',
  MessageReceived: 'Tin nhắn',
};

export const notificationTitle = 'Thông báo';
export const notificationEmpty = 'Chưa có thông báo nào';
export const notificationMarkAll = 'Đánh dấu tất cả đã đọc';
export const notificationLoadMore = 'Xem thêm';
export const notificationOpenErrorTitle = 'Không mở được thông báo';
