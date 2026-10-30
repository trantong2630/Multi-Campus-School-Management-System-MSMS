import type { BadgeGroup } from '@/shared/lib/tokens';

export type StatusDomain =
  | 'account' | 'session' | 'sessionSource' | 'attendance' | 'leave' | 'question'
  | 'catalog' | 'assignment' | 'aiFeedback' | 'timetableVersion' | 'post' | 'student' | 'submission';

type Entry = [label: string, group: BadgeGroup];

// Nhãn lấy nguyên từ Dev_Conventions §4.4, nhóm màu từ Design_Web §2.4. Khóa là giá trị enum lưu trong DB.
const registry: Record<StatusDomain, Record<string, Entry>> = {
  account: {
    PendingPasswordChange: ['Chờ đổi mật khẩu', 'pending'],
    Active: ['Đang hoạt động', 'active'],
    Locked: ['Đã khóa', 'danger'],
  },
  session: {
    Scheduled: ['Đã xếp lịch', 'active'],
    Pending: ['Chờ xử lý', 'pending'],
    Cancelled: ['Đã hủy', 'danger'],
  },
  sessionSource: {
    Generated: ['Sinh tự động', 'info'],
    Manual: ['Chỉnh tay', 'info'],
  },
  attendance: {
    Present: ['Có mặt', 'active'],
    Late: ['Muộn', 'info'],
    ExcusedAbsent: ['Vắng có phép', 'info'],
    UnexcusedAbsent: ['Vắng không phép', 'danger'],
    NotMarked: ['Chưa điểm danh', 'neutral'],
  },
  leave: {
    Pending: ['Chờ duyệt', 'pending'],
    Approved: ['Đã duyệt', 'active'],
    Rejected: ['Đã từ chối', 'danger'],
    Withdrawn: ['Đã rút', 'neutral'],
  },
  question: {
    Draft: ['Nháp', 'neutral'],
    Ready: ['Sẵn dùng', 'active'],
  },
  catalog: {
    Active: ['Đang hoạt động', 'active'],
    Inactive: ['Ngừng hoạt động', 'neutral'],
  },
  assignment: {
    Open: ['Đang mở', 'active'],
    Closed: ['Đã đóng', 'neutral'],
  },
  aiFeedback: {
    Pending: ['Đang phân tích', 'pending'],
    Done: ['Xong', 'active'],
    Failed: ['Lỗi', 'neutral'],
  },
  timetableVersion: {
    Draft: ['Nháp', 'neutral'],
    Published: ['Đã phát hành', 'active'],
  },
  post: {
    Posted: ['Đã đăng', 'active'],
    Hidden: ['Đã ẩn', 'neutral'],
  },
  student: {
    Studying: ['Đang học', 'active'],
    Transferred: ['Đã chuyển trường', 'danger'],
    Graduated: ['Đã tốt nghiệp', 'info'],
  },
  submission: {
    Submitted: ['Đã nộp', 'info'],
    NotSubmitted: ['Không nộp', 'neutral'],
  },
};

export function statusLabel(domain: StatusDomain, value: string): string {
  return registry[domain][value]?.[0] ?? value;
}

/** Một hàm duy nhất quyết định nhóm màu badge, không map riêng theo màn hình. */
export function statusGroup(domain: StatusDomain, value: string): BadgeGroup {
  return registry[domain][value]?.[1] ?? 'neutral';
}
