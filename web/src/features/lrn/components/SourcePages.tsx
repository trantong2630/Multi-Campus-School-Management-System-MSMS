import { useState } from 'react';
import { lrnApi } from '../api';
import { pagesText } from '../labels';
import { PageViewer } from './PageViewer';
import './lrn.css';

interface Props { lessonId: number; book: string; lesson: string; startPage: number; endPage: number }

/** Nguồn của câu do AI soạn từ sách (4.6.6): bài, trang in, tên sách và nút mở ảnh các trang để giáo viên đối chiếu khi duyệt nháp. */
export function SourcePages({ lessonId, book, lesson, startPage, endPage }: Props) {
  const [open, setOpen] = useState(false);
  const pages = pagesText(startPage, endPage);
  return (
    <div className="qsrc">
      <span className="qsrc__k">Nguồn</span>
      <span className="qsrc__lesson">{lesson}</span>
      <span className="qsrc__pg">{pages}</span>
      <span className="qsrc__book">{book}</span>
      <button type="button" className="qsrc__btn" onClick={() => setOpen(true)}>Xem trang sách</button>
      <PageViewer
        open={open} onClose={() => setOpen(false)} startPage={startPage} endPage={endPage}
        title={<span className="qsrc__title">{lesson}<span className="qsrc__pg">{pages}</span></span>}
        cacheKey={['lrn', 'textbook-page', lessonId]} load={(p) => lrnApi.textbooks.page(lessonId, p)}
      />
    </div>
  );
}
