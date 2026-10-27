import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, Modal, Select } from 'antd';
import dayjs from 'dayjs';
import type { Dayjs } from 'dayjs';
import { Lock, Plus } from 'lucide-react';
import { useState } from 'react';
import { schApi } from '@/features/sch/api';
import { FilterBar } from '@/shared/components/FilterBar';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { message } from '@/shared/lib/antdStatic';
import { confirmAction, showError } from '@/shared/lib/feedback';
import { formatDate, toDateString, weekdayOfDate } from '@/shared/lib/format';
import { scdApi } from '../api';
import { subjectColorStyle } from '../subjectColor';
import type { AdjustSession, AdjustWeek } from '../types';
import { slotLabel } from '../labels';
import { ResolveModal } from './AbsenceModals';
import type { ResolveTarget } from './AbsenceModals';
import { ChangeTeacherModal, MakeUpModal } from './AdjustModals';
import type { MakeUpTarget } from './AdjustModals';
import { WeekFrame, WeekNav } from './WeekFrame';
import './scd.css';

const STATUS_LABEL: Record<AdjustSession['status'], string> = { Scheduled: 'Đã xếp lịch', Pending: 'Chờ xử lý', Cancelled: 'Đã hủy' };

/**
 * SCD-02: lưới tuần của một lớp để điều chỉnh lịch dạy (BR-15). Nhấp vào tiết để đổi giáo viên hoặc hủy, nhấp vào ô trống để thêm tiết bù.
 * Tiết bị khóa (đã qua hoặc đã điểm danh, mục 6.2) hiện rõ và không thao tác được.
 */
export function AdjustView({ campusId, lead }: { campusId: number; lead: React.ReactNode }) {
  const qc = useQueryClient();
  const [classId, setClassId] = useState<number | undefined>();
  const [monday, setMonday] = useState<Dayjs>(() => dayjs().startOf('week'));
  const from = toDateString(monday);

  const classes = useQuery({
    queryKey: ['scd', 'adjustClasses', campusId],
    queryFn: async () => (await scdApi.classes(campusId)).map((c) => ({ value: c.classId, label: c.name })),
  });
  const effectiveClass = classId ?? classes.data?.[0]?.value;
  const week = useQuery({
    queryKey: ['scd', 'adjustWeek', effectiveClass, from],
    queryFn: () => scdApi.adjustWeek(effectiveClass!, from),
    enabled: effectiveClass !== undefined,
  });
  const slots = useQuery({ queryKey: ['sch', 'slots'], queryFn: schApi.periodSlots.list });

  const [picked, setPicked] = useState<AdjustSession | null>(null);
  const [changing, setChanging] = useState<AdjustSession | null>(null);
  const [makeUp, setMakeUp] = useState<MakeUpTarget | null>(null);
  const [resolving, setResolving] = useState<ResolveTarget | null>(null);

  const w = week.data;
  const pickedSlot = slots.data?.find((p) => p.periodSlotId === picked?.periodSlotId);
  const slotText = pickedSlot ? slotLabel(pickedSlot) : '';
  const refresh = () => qc.invalidateQueries({ queryKey: ['scd'] });
  const today = dayjs();

  /** Tiết Chờ xử lý mở thẳng hộp xử lý (dạy thay hoặc hủy); các tiết khác mở hộp thao tác. */
  const open = (s: AdjustSession) => {
    if (s.status !== 'Pending' || s.locked || !w?.canManage) { setPicked(s); return; }
    const slot = slots.data?.find((p) => p.periodSlotId === s.periodSlotId);
    setResolving({
      sessionId: s.sessionId, title: `${s.subjectName} — lớp ${w.className}`,
      summary: `${weekdayOfDate(s.date)} ${formatDate(s.date)}${slot ? `, ${slotLabel(slot)}` : ''}. ${s.changeNote ?? ''}`,
    });
  };

  const renderCell = (slotId: number, d: Dayjs) => {
    const date = toDateString(d);
    const holiday = w?.holidays.find((h) => h.date === date);
    const sessions = (w?.sessions ?? []).filter((s) => s.periodSlotId === slotId && s.date === date);
    const live = sessions.some((s) => s.status !== 'Cancelled');
    const canAdd = !!w?.canManage && !live && !holiday && !d.isBefore(today, 'day');
    return (
      <td className={`tt-cell${holiday ? ' tt-cell--holiday' : ''}${canAdd ? ' tt-cell--addable' : ''}`} title={holiday ? `Ngày nghỉ: ${holiday.reason ?? ''}` : undefined}>
        <div className="tt-stack">
          {sessions.map((s) => <AdjustChip key={s.sessionId} s={s} onClick={() => open(s)} />)}
          {canAdd && (
            <button
              type="button" className="tt-add" aria-label="Thêm tiết bù"
              onClick={() => w && setMakeUp({ classId: w.classId, className: w.className, campusId, date, slotId })}
            >
              <Plus size={14} /> Thêm tiết bù
            </button>
          )}
        </div>
      </td>
    );
  };

  return (
    <>
      <FilterBar>
        {lead}
        <Select
          showSearch optionFilterProp="label" style={{ minWidth: 160 }} value={effectiveClass} onChange={setClassId}
          placeholder="Chọn lớp" loading={classes.isLoading} options={classes.data}
        />
        <WeekNav monday={monday} onChange={setMonday} />
      </FilterBar>

      {classes.isSuccess && classes.data.length === 0 && (
        <InlineAlert type="info" title="Phân hiệu chưa có lớp nào trong năm học hiện hành" />
      )}
      {w && !w.canManage && <InlineAlert type="info" title="Bạn chỉ xem được lịch của phân hiệu này, không thao tác." />}
      {w && w.canManage && w.sessions.length === 0 && (
        <InlineAlert type="info" title="Tuần này lớp chưa có tiết nào" description="Tuần nghỉ, tuần chưa phát hành thời khóa biểu hoặc đã qua trước lần phát hành. Vẫn thêm được tiết bù vào ô trống." />
      )}

      {effectiveClass !== undefined && (
        <Card className="table-card">
          <WeekFrame
            monday={monday} loading={week.isLoading} usedSlotIds={new Set(w?.sessions.map((s) => s.periodSlotId))}
            renderDayHeader={(d) => {
              const h = w?.holidays.find((x) => x.date === toDateString(d));
              return h ? <span className="tt-holiday-tag">Nghỉ</span> : null;
            }}
            renderCell={(slot, d) => renderCell(slot.periodSlotId, d)}
          />
        </Card>
      )}

      <SessionActionModal
        session={picked} week={w} slotText={slotText}
        onClose={() => setPicked(null)}
        onChangeTeacher={(s) => { setPicked(null); setChanging(s); }}
        onChanged={() => { setPicked(null); void refresh(); }}
      />
      <ChangeTeacherModal
        session={changing} className={w?.className ?? ''} onClose={() => setChanging(null)}
        onDone={() => { setChanging(null); void refresh(); }}
      />
      <MakeUpModal target={makeUp} onClose={() => setMakeUp(null)} onDone={() => { setMakeUp(null); void refresh(); }} />
      <ResolveModal target={resolving} onClose={() => setResolving(null)} onDone={() => { setResolving(null); void refresh(); }} />
    </>
  );
}

function AdjustChip({ s, onClick }: { s: AdjustSession; onClick: () => void }) {
  const state = s.status === 'Cancelled' ? 'tt-chip--cancelled' : s.status === 'Pending' ? 'tt-chip--pending' : '';
  const locked = s.locked && s.status !== 'Cancelled';
  const note = s.status === 'Pending' ? 'Chờ GV' : s.teacherAbbr ?? s.teacherName ?? '—';
  return (
    <div
      role="button" tabIndex={0} className={`tt-chip tt-chip--click ${state}`} style={subjectColorStyle(s.subjectId)}
      title={`${s.subjectName} — ${s.teacherName ?? 'Chờ phân công giáo viên'}${locked ? ' (bị khóa)' : ''}`}
      onClick={onClick} onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') onClick(); }}
    >
      <div className="tt-subject">
        {s.subjectAbbr}
        {s.isMakeUp && <span className="tt-tag">Bù</span>}
        {locked && <Lock size={11} className="tt-lock" aria-label="Bị khóa" />}
      </div>
      <div className="tt-teacher">{note}{s.source === 'Manual' && !s.isMakeUp && s.status === 'Scheduled' ? ' · Chỉnh tay' : ''}</div>
    </div>
  );
}

interface ActionProps {
  session: AdjustSession | null;
  week: AdjustWeek | undefined;
  slotText: string;
  onClose: () => void;
  onChangeTeacher: (s: AdjustSession) => void;
  onChanged: () => void;
}

/** Thao tác trên một tiết Đã xếp lịch chưa bị khóa: đổi giáo viên, hủy. Tiết bị khóa hoặc đã hủy chỉ xem; muốn bù thì thêm tiết bù vào ô trống. */
function SessionActionModal({ session: s, week, slotText, onClose, onChangeTeacher, onChanged }: ActionProps) {
  const [busy, setBusy] = useState(false);
  const canManage = week?.canManage ?? false;

  const cancel = () => {
    if (!s || !week) return;
    confirmAction({
      title: `Hủy tiết ${s.subjectName} ngày ${formatDate(s.date)}?`,
      content: `Lớp ${week.className}, ${slotText}. Tiết Đã hủy không dạy, không điểm danh, không tính chuyên cần. Sẽ thông báo cho giáo viên và học sinh của lớp. Muốn dạy lại thì thêm tiết bù.`,
      okText: 'Hủy tiết',
      danger: true,
      onOk: async () => {
        setBusy(true);
        try {
          const res = await scdApi.cancelSession(s.sessionId);
          message.success(`Đã hủy tiết, thông báo cho ${res.notifiedCount} người.`);
          onChanged();
        } catch (e) {
          showError(e, 'Không hủy được tiết');
        } finally {
          setBusy(false);
        }
      },
    });
  };

  const footer = [<Button key="close" onClick={onClose}>Đóng</Button>];
  if (s && canManage) {
    if (s.status === 'Scheduled' && !s.locked) {
      footer.push(
        <Button key="cancel" danger loading={busy} onClick={cancel}>Hủy tiết</Button>,
        <Button key="teacher" type="primary" onClick={() => onChangeTeacher(s)}>Đổi giáo viên</Button>,
      );
    }
  }

  return (
    <Modal
      open={s !== null} width={520} onCancel={onClose} mask={{ closable: false }} footer={footer}
      title={s && week ? `${s.subjectName} — lớp ${week.className}` : ''}
    >
      {s && (
        <>
          <dl className="tt-detail">
            <dt>Ngày</dt><dd>{weekdayOfDate(s.date)} {formatDate(s.date)}</dd>
            <dt>Khung giờ</dt><dd>{slotText}</dd>
            <dt>Giáo viên</dt><dd>{s.teacherName ?? 'Chưa có (đang chờ phân công)'}</dd>
            <dt>Trạng thái</dt><dd>{STATUS_LABEL[s.status]}</dd>
            <dt>Nguồn</dt><dd>{s.isMakeUp ? 'Tiết bù' : s.source === 'Manual' ? 'Chỉnh tay' : 'Sinh tự động từ thời khóa biểu'}</dd>
            {s.changeNote && (<><dt>Ghi chú</dt><dd>{s.changeNote}</dd></>)}
          </dl>
          {s.status === 'Scheduled' && s.locked && (
            <InlineAlert
              type="info" title="Tiết bị khóa"
              description="Tiết đã qua ngày hoặc đã có điểm danh nên không đổi giáo viên, không hủy. School Admin sửa điểm danh của tiết đã qua."
            />
          )}
          {s.status === 'Cancelled' && <InlineAlert type="info" title="Tiết đã hủy" description="Muốn dạy lại thì thêm tiết bù vào một ô trống." />}
          {!canManage && <InlineAlert type="info" title="Chỉ hiệu phó phụ trách phân hiệu mới thao tác được." />}
        </>
      )}
    </Modal>
  );
}
