import { useQuery } from '@tanstack/react-query';
import { Select } from 'antd';
import { useState } from 'react';
import { scdApi } from './api';
import './components/scd.css';

/**
 * Phân hiệu đang thao tác ở SCD. Chỉ ai xem được từ hai phân hiệu trở lên mới có ô chọn (`select`);
 * ai chỉ có một phân hiệu thì không có gì để lọc, tên phân hiệu hiện thành dải đầu trang (`head`) cùng kiểu với lịch học của học sinh.
 */
export function useScdCampus() {
  const campuses = useQuery({ queryKey: ['scd', 'campuses'], queryFn: scdApi.campuses });
  const [campusId, setCampusId] = useState<number | undefined>();
  const effective = campusId ?? campuses.data?.[0]?.campusId;
  const name = campuses.data?.find((c) => c.campusId === effective)?.name;
  const select = (campuses.data?.length ?? 0) > 1
    ? (
      <Select
        placeholder="Phân hiệu" value={effective} onChange={setCampusId}
        options={campuses.data?.map((c) => ({ value: c.campusId, label: c.name }))}
      />
    )
    : null;
  const head = !select && name
    ? <div className="tt-class-head"><div className="tt-class-head__text"><span className="tt-class-head__name">{name}</span></div></div>
    : null;
  return { campuses, effective, select, head };
}
