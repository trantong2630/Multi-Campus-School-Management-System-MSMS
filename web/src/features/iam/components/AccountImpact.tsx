import type { LockImpact } from '../types';

const dutyLabel: Record<string, string> = {
  Campus: 'Phụ trách phân hiệu', Department: 'Tổ trưởng', Homeroom: 'Chủ nhiệm lớp',
};

export function AccountImpact({ impact }: { impact: LockImpact }) {
  return (
    <>
      {impact.sessionCount > 0 && (
        <p>
          {impact.sessionCount} tiết từ hôm nay trở đi sẽ chuyển sang <b>Chờ xử lý</b> ({impact.campuses.map((c) => `${c.campus}: ${c.count}`).join(', ')}).
          Hiệu phó phân hiệu sẽ nhận thông báo để đổi giáo viên hoặc hủy tiết.
        </p>
      )}
      {impact.duties.length > 0 && (
        <p>Trách nhiệm sẽ kết thúc vào hôm nay: {impact.duties.map((d) => `${dutyLabel[d.kind] ?? d.kind} ${d.targetName}`).join('; ')}. Cần gán mới để khôi phục trách nhiệm.</p>
      )}
    </>
  );
}
