import { useQuery } from '@tanstack/react-query';
import { Select } from 'antd';
import { scdApi } from '../api';

interface Props {
  value?: number;
  onChange?: (teacherId: number) => void;
  /** Chọn cho một (ngày, khung giờ): hiện giáo viên TOÀN TRƯỜNG, ai đang bận hoặc vắng thì mờ đi (BR-13, BR-16, mục 2.3). */
  date?: string;
  slotId?: number;
  excludeSessionId?: number;
  /** Chọn dạy thay cho tiết Chờ xử lý: chỉ hiện gợi ý giáo viên rảnh, tối đa 10, không xếp hạng ưu tiên. */
  suggestForSessionId?: number;
  placeholder?: string;
}

/** Ô chọn giáo viên có kiểm rảnh/bận ngay trong danh sách, để người dùng khỏi chọn nhầm rồi mới nhận lỗi trùng lịch. */
export function TeacherSelect({ value, onChange, date, slotId, excludeSessionId, suggestForSessionId, placeholder = 'Chọn giáo viên' }: Props) {
  const suggest = suggestForSessionId !== undefined;
  const ready = suggest || (!!date && slotId !== undefined);
  const list = useQuery({
    queryKey: ['scd', 'teacherAvailability', suggestForSessionId ?? 'all', date, slotId, excludeSessionId],
    queryFn: () => (suggest ? scdApi.pendingSuggestions(suggestForSessionId) : scdApi.adjustTeachers(date!, slotId!, excludeSessionId)),
    enabled: ready,
    gcTime: 0,
  });

  return (
    <Select
      showSearch optionFilterProp="label" placeholder={ready ? placeholder : 'Chọn ngày và tiết trước'} disabled={!ready}
      loading={list.isLoading} value={value} onChange={onChange}
      notFoundContent={suggest && list.isSuccess ? 'Không có giáo viên rảnh ở khung giờ này' : undefined}
      options={list.data?.map((t) => ({
        value: t.teacherId,
        disabled: t.busy || t.absent,
        label: `${t.name}${t.abbr ? ` (${t.abbr})` : ''} · ${t.campusName}${t.busy ? ` — đang dạy ${t.busyNote}` : t.absent ? ' — đang vắng' : ''}`,
      }))}
    />
  );
}
