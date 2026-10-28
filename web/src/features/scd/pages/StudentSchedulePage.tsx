import { useQuery } from '@tanstack/react-query';
import { Card, Select } from 'antd';
import dayjs from 'dayjs';
import type { Dayjs } from 'dayjs';
import { useEffect, useState } from 'react';
import { useAuth } from '@/app/AuthProvider';
import { parApi } from '@/features/par/api';
import { FilterBar } from '@/shared/components/FilterBar';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { PageHeader } from '@/shared/components/PageHeader';
import { toDateString } from '@/shared/lib/format';
import { scdApi } from '../api';
import { WeekFrame, WeekNav } from '../components/WeekFrame';
import { subjectColorStyle } from '../subjectColor';
import type { ViewSession } from '../types';
import '../components/scd.css';

const STATUS_NOTE: Record<ViewSession['status'], string | null> = { Scheduled: null, Pending: 'Chờ xử lý', Cancelled: 'Đã hủy' };

/** Học sinh xem lịch lớp mình; phụ huynh chọn con rồi xem lịch lớp của con. */
export function StudentSchedulePage() {
  const { user } = useAuth();
  const isParent = user?.accountType === 'Parent';
  const [monday, setMonday] = useState<Dayjs>(() => dayjs().startOf('week'));
  const [childId, setChildId] = useState<number | undefined>();
  const from = toDateString(monday);
  const to = toDateString(monday.add(5, 'day'));

  const children = useQuery({ queryKey: ['par', 'leave', 'my-children'], queryFn: () => parApi.leave.getMyChildren(), enabled: isParent });
  useEffect(() => {
    if (isParent && childId === undefined && children.data?.length) setChildId(children.data[0].studentId);
  }, [isParent, childId, children.data]);

  const ready = !isParent || childId !== undefined;
  const sessions = useQuery({
    queryKey: ['scd', 'student-week', isParent ? childId : 'me', from, to],
    queryFn: () => (isParent ? scdApi.childWeek(childId!, from, to) : scdApi.studentWeek(from, to)),
    enabled: ready,
  });

  const first = sessions.data?.[0];
  const className = first?.className ?? (isParent ? children.data?.find((c) => c.studentId === childId)?.className : undefined);

  return (
    <>
      <PageHeader
        title={isParent ? 'Lịch học của con' : 'Lịch học'}
        description="Lịch học của lớp theo tuần. Tiết đã hủy gạch ngang, tiết chờ xử lý nền vàng."
      />
      {className && (
        <div className="tt-class-head">
          <div className="tt-class-head__text">
            <span className="tt-class-head__name">Lớp {className}</span>
            {first && <span className="tt-class-head__campus">{first.campusName}</span>}
          </div>
          <span className="tt-class-head__week">Tuần {monday.format('DD/MM')} – {monday.add(5, 'day').format('DD/MM')}</span>
        </div>
      )}
      <FilterBar>
        {isParent && (
          <Select
            style={{ minWidth: 240 }} placeholder="Chọn con" value={childId} onChange={setChildId} loading={children.isLoading}
            options={children.data?.map((c) => ({ value: c.studentId, label: c.className ? `${c.fullName} — ${c.className}` : c.fullName }))}
          />
        )}
        <WeekNav monday={monday} onChange={setMonday} />
      </FilterBar>
      {isParent && children.isSuccess && children.data.length === 0 && (
        <InlineAlert type="info" title="Chưa có học sinh nào liên kết" description="Liên hệ nhà trường để liên kết tài khoản với con." />
      )}
      {sessions.isSuccess && sessions.data.length === 0 && (
        <InlineAlert type="info" title="Tuần này không có tiết nào" description="Tuần nghỉ, tuần chưa phát hành thời khóa biểu hoặc chưa được xếp lớp." />
      )}
      {ready && (
        <Card className="table-card">
          <WeekFrame
            monday={monday} loading={sessions.isLoading} usedSlotIds={new Set(sessions.data?.map((s) => s.periodSlotId))}
            renderCell={(slot, d) => {
              const cell = (sessions.data ?? [])
                .filter((s) => s.periodSlotId === slot.periodSlotId && s.date === toDateString(d))
                .sort((a, b) => (a.status === 'Cancelled' ? 1 : 0) - (b.status === 'Cancelled' ? 1 : 0));
              if (cell.length === 0) return <td className="tt-cell tt-cell--empty" />;
              return (
                <td className="tt-cell">
                  <div className="tt-stack">
                    {cell.map((s) => {
                      const state = s.status === 'Cancelled' ? 'tt-chip--cancelled' : s.status === 'Pending' ? 'tt-chip--pending' : '';
                      const note = STATUS_NOTE[s.status];
                      return (
                        <div key={s.sessionId} className={`tt-chip ${state}`} style={subjectColorStyle(s.subjectId)} title={s.subjectName}>
                          <div className="tt-subject">{s.subjectName}</div>
                          <div className="tt-teacher">{s.teacherName ?? 'Chờ GV'}{note && ` · ${note}`}</div>
                        </div>
                      );
                    })}
                  </div>
                </td>
              );
            }}
          />
        </Card>
      )}
    </>
  );
}
