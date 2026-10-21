import { Skeleton, Table } from 'antd';
import type { TableProps } from 'antd';
import { Inbox } from 'lucide-react';
import type { ReactNode } from 'react';
import { color } from '@/shared/lib/tokens';
import './shared.css';

type Props<T> = TableProps<T> & {
  emptyText?: string;
  emptyAction?: ReactNode;
};

/** Bảng chuẩn của MSMS (Design_Web §8.2): đang tải thì hiện skeleton 5 dòng, không dùng spinner giữa màn hình. */
export function DataTable<T extends object>({ loading, dataSource, emptyText = 'Chưa có dữ liệu.', emptyAction, pagination, ...rest }: Props<T>) {
  if (loading && !dataSource?.length) {
    return <Skeleton active title={false} paragraph={{ rows: 5, width: '100%' }} />;
  }
  return (
    <Table<T>
      className="data-table"
      size="middle"
      loading={loading}
      dataSource={dataSource}
      scroll={{ x: 'max-content' }}
      sticky
      pagination={pagination === false ? false : { pageSize: 20, showSizeChanger: false, hideOnSinglePage: true, placement: ['bottomEnd'], ...pagination }}
      locale={{
        emptyText: (
          <div className="empty-state">
            <Inbox size={48} strokeWidth={1.25} color={color.disabled} />
            <p>{emptyText}</p>
            {emptyAction}
          </div>
        ),
      }}
      {...rest}
    />
  );
}
