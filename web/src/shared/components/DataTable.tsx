import { Skeleton, Table } from 'antd';
import type { TableProps } from 'antd';
import type { ReactNode } from 'react';
import './shared.css';

type Props<T> = TableProps<T> & {
  emptyText?: string;
  emptyAction?: ReactNode;
};

/**
 * Bảng chuẩn của MSMS (Design_Web §8.2): đang tải thì hiện skeleton 5 dòng, không dùng spinner giữa màn hình;
 * chân bảng ghi "a–b trên tổng N" bên trái, nút trang bên phải.
 */
export function DataTable<T extends object>({ loading, dataSource, emptyText = 'Chưa có dữ liệu.', emptyAction, pagination, ...rest }: Props<T>) {
  if (loading && !dataSource?.length) {
    return <Skeleton active title={false} paragraph={{ rows: 5, width: '100%' }} className="data-table__skeleton" />;
  }
  return (
    <Table<T>
      className="data-table"
      size="middle"
      loading={loading}
      dataSource={dataSource}
      scroll={{ x: 'max-content' }}
      sticky
      pagination={pagination === false ? false : {
        pageSize: 20, showSizeChanger: false, hideOnSinglePage: true, placement: ['bottomEnd'],
        showTotal: (total, [from, to]) => <span className="tabular">{from}–{to} trên tổng {total.toLocaleString('vi-VN')}</span>,
        ...pagination,
      }}
      locale={{
        emptyText: (
          <div className="empty-state">
            <p className="empty-state__title">{emptyText}</p>
            {emptyAction}
          </div>
        ),
      }}
      {...rest}
    />
  );
}
