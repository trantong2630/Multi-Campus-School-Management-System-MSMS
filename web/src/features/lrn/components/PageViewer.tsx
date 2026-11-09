import { useQuery } from '@tanstack/react-query';
import { Modal } from 'antd';
import type { ReactNode } from 'react';
import { useEffect } from 'react';
import { pagesText } from '../labels';
import './lrn.css';

interface Props {
  open: boolean;
  onClose: () => void;
  title: ReactNode;
  startPage: number;
  endPage: number;
  /** Khóa bộ nhớ đệm của các trang; mỗi nguồn ảnh (giáo viên, học sinh, phụ huynh) dùng khóa riêng. */
  cacheKey: (string | number)[];
  /** Lấy ảnh một trang (số in) thành địa chỉ tạm. */
  load: (page: number) => Promise<string>;
}

/** Cửa sổ xem ảnh các trang sách của một bài, dùng chung cho giáo viên (đối chiếu nháp AI) và học sinh, phụ huynh (xem lại trong sách). */
export function PageViewer({ open, onClose, title, startPage, endPage, cacheKey, load }: Props) {
  const pages = Array.from({ length: endPage - startPage + 1 }, (_, i) => startPage + i);
  return (
    <Modal open={open} onCancel={onClose} footer={null} width={760} destroyOnHidden title={title}>
      <div className="srcpages">
        {pages.map((p) => <Page key={p} cacheKey={[...cacheKey, p]} page={p} load={load} />)}
      </div>
    </Modal>
  );
}

function Page({ cacheKey, page, load }: { cacheKey: (string | number)[]; page: number; load: (page: number) => Promise<string> }) {
  const img = useQuery({ queryKey: cacheKey, queryFn: () => load(page), staleTime: Infinity, gcTime: 0 });
  const url = img.data;
  useEffect(() => () => { if (url) URL.revokeObjectURL(url); }, [url]);
  return (
    <figure className="srcpages__page">
      {url ? <img src={url} alt={`Trang ${page}`} /> : <div className="srcpages__wait">{img.isError ? 'Không tải được trang này.' : 'Đang tải…'}</div>}
      <figcaption><span className="qsrc__pg">{pagesText(page, page)}</span></figcaption>
    </figure>
  );
}
