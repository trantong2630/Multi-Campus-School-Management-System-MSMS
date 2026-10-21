import { Collapse } from 'antd';
import { CircleAlert } from 'lucide-react';
import { DataTable } from './DataTable';
import './shared.css';

export interface ImportError {
  location: string;
  column: string;
  value: string;
  reason: string;
}

export interface SkippedRow {
  row: number;
  fullName: string;
  reason: string;
}

interface Props {
  errors: ImportError[];
  totalErrors: number;
  skipped?: SkippedRow[];
}

/** Bảng lỗi import (Design_Web §9.2): panel đỏ nhạt, bốn cột, địa chỉ ô viết monospace; "Đã có, bỏ qua" là bảng xám riêng, không phải lỗi. */
export function ImportErrorPanel({ errors, totalErrors, skipped = [] }: Props) {
  return (
    <div className="import-errors">
      {errors.length > 0 && (
        <>
          <div className="import-errors__banner">
            <CircleAlert size={18} />
            <strong>Từ chối 1 file · {totalErrors} lỗi</strong>
            <span>Không ghi gì vào hệ thống. Sửa file rồi tải lên lại.</span>
          </div>
          <DataTable<ImportError>
            rowKey={(r) => `${r.location}|${r.column}|${r.reason}`}
            dataSource={errors}
            pagination={{ pageSize: 10 }}
            columns={[
              { title: 'Ô/Dòng', dataIndex: 'location', width: 110, render: (v: string) => <code className="mono">{v}</code> },
              { title: 'Cột', dataIndex: 'column', width: 140 },
              { title: 'Giá trị gốc', dataIndex: 'value', width: 160, render: (v: string) => v || <span className="muted">(trống)</span> },
              { title: 'Nguyên nhân', dataIndex: 'reason' },
            ]}
          />
          {totalErrors > errors.length && (
            <p className="muted">Chỉ hiện {errors.length} lỗi đầu tiên. Sửa các lỗi này rồi tải lên lại để xem tiếp.</p>
          )}
        </>
      )}

      {skipped.length > 0 && (
        <Collapse
          className="import-errors__skipped"
          items={[{
            key: 'skipped',
            label: `Đã có, bỏ qua (${skipped.length})`,
            children: (
              <DataTable<SkippedRow>
                rowKey="row"
                dataSource={skipped}
                pagination={{ pageSize: 10 }}
                columns={[
                  { title: 'Dòng', dataIndex: 'row', width: 90, render: (v: number) => <code className="mono">{v}</code> },
                  { title: 'Họ và tên', dataIndex: 'fullName', width: 240 },
                  { title: 'Lý do', dataIndex: 'reason' },
                ]}
              />
            ),
          }]}
        />
      )}
    </div>
  );
}
