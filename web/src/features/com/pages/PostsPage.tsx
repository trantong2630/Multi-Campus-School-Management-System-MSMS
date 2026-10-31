import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, Form, Input, Segmented, Select } from 'antd';
import { Eye, EyeOff, ImageIcon, Pencil, Plus, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { useAuth } from '@/app/AuthProvider';
import { DataTable } from '@/shared/components/DataTable';
import { FilterBar } from '@/shared/components/FilterBar';
import { FormModal } from '@/shared/components/FormModal';
import { PageHeader } from '@/shared/components/PageHeader';
import { RowActions } from '@/shared/components/RowActions';
import { StatusBadge } from '@/shared/components/StatusBadge';
import { message } from '@/shared/lib/antdStatic';
import { confirmAction, showError } from '@/shared/lib/feedback';
import { formatDateTime } from '@/shared/lib/format';
import { postVisibilityLabel } from '@/shared/lib/labels';
import { postApi } from '../api';
import { PostDetailDrawer } from '../components/PostDetailDrawer';
import { PostFormModal } from '../components/PostFormModal';
import { PostImage } from '../components/PostImage';
import type { PostDetail, PostListItem } from '../types';
import '../components/posts.css';

const PAGE_SIZE = 10;

interface HideValues { reason?: string }

/**
 * COM-02: bảng tin. Mọi cán bộ đăng bài và chọn phạm vi Nội bộ hoặc Toàn trường; học sinh, phụ huynh chỉ đọc bài Toàn trường.
 * Bảng tin không tạo thông báo (BR-27).
 */
export function PostsPage() {
  const { user } = useAuth();
  const isStaff = user?.accountType !== 'Student' && user?.accountType !== 'Parent';
  const qc = useQueryClient();
  const [status, setStatus] = useState<'Posted' | 'Hidden'>('Posted');
  const [visibility, setVisibility] = useState<string | undefined>();
  const [page, setPage] = useState(1);
  const [openId, setOpenId] = useState<number | null>(null);
  const [editing, setEditing] = useState<PostDetail | null>(null);
  const [composing, setComposing] = useState(false);
  const [hiding, setHiding] = useState<{ postId: number; title: string; byAuthor: boolean } | null>(null);
  const [hideForm] = Form.useForm<HideValues>();

  const posts = useQuery({
    queryKey: ['com', 'posts', status, visibility, page],
    queryFn: () => postApi.list({ page, pageSize: PAGE_SIZE, status, visibility }),
    placeholderData: (prev) => prev,
  });
  const refresh = () => qc.invalidateQueries({ queryKey: ['com'] });

  const run = async (action: () => Promise<unknown>, success: string) => {
    try {
      await action();
      await refresh();
      message.success(success);
    } catch (e) {
      showError(e);
    }
  };

  const edit = async (id: number) => {
    try { setEditing(await postApi.get(id)); } catch (e) { showError(e); }
  };
  const askHide = (p: { postId: number; title: string; authorPersonId: number }) =>
    setHiding({ postId: p.postId, title: p.title, byAuthor: p.authorPersonId === user?.personId });
  const unhide = (p: { postId: number; title: string }) => confirmAction({
    title: `Hiện lại bài "${p.title}"?`, content: 'Bài sẽ hiện lại trên bảng tin cho những người được đọc.', okText: 'Hiện lại',
    onOk: () => run(() => postApi.unhide(p.postId), 'Đã hiện lại bài.'),
  });
  const remove = (p: { postId: number; title: string }) => confirmAction({
    title: `Xóa bài "${p.title}"?`, content: 'Xóa bài sẽ xóa luôn các ảnh đính kèm và không khôi phục được. Muốn giữ lại thì chọn Ẩn bài.',
    okText: 'Xóa bài', danger: true,
    onOk: () => run(async () => { await postApi.remove(p.postId); setOpenId(null); }, 'Đã xóa bài.'),
  });

  return (
    <>
      <PageHeader
        title="Bảng tin"
        description={isStaff
          ? 'Bài Nội bộ chỉ cán bộ đọc được; bài Toàn trường mọi người đọc được, gồm học sinh và phụ huynh.'
          : 'Các bài đăng của nhà trường.'}
        extra={isStaff && <Button type="primary" icon={<Plus size={16} />} onClick={() => setComposing(true)}>Đăng bài</Button>}
      />
      {isStaff && (
        <FilterBar>
          <Segmented<string>
            value={status} onChange={(v) => { setStatus(v as 'Posted' | 'Hidden'); setPage(1); }}
            options={[{ value: 'Posted', label: 'Đã đăng' }, { value: 'Hidden', label: 'Đã ẩn' }]}
          />
          <Select
            allowClear placeholder="Phạm vi" value={visibility} onChange={(v) => { setVisibility(v); setPage(1); }}
            options={Object.entries(postVisibilityLabel).map(([value, label]) => ({ value, label }))}
          />
        </FilterBar>
      )}

      <Card className="table-card">
        <DataTable<PostListItem>
          rowKey="postId" loading={posts.isLoading} dataSource={posts.data?.items}
          emptyText={status === 'Hidden' ? 'Chưa có bài nào bị ẩn.' : 'Chưa có bài đăng nào.'}
          emptyAction={isStaff && status === 'Posted' ? <Button type="primary" onClick={() => setComposing(true)}>Đăng bài đầu tiên</Button> : undefined}
          pagination={{ current: page, pageSize: PAGE_SIZE, total: posts.data?.total, onChange: setPage }}
          columns={[
            {
              title: 'Bài đăng', key: 'post',
              render: (_: unknown, p: PostListItem) => (
                <div className="post-cell" onClick={() => setOpenId(p.postId)}>
                  <span className="post-cell__cover">
                    {p.coverImageId ? <PostImage postId={p.postId} imageId={p.coverImageId} alt={`Ảnh bìa ${p.title}`} /> : <span className="post-img post-img--empty" style={{ display: 'grid', placeItems: 'center' }}><ImageIcon size={20} color="var(--c-text3)" /></span>}
                  </span>
                  <span className="post-cell__text">
                    <span className="post-cell__title">{p.title}</span>
                    <span className="post-cell__excerpt">{p.excerpt}</span>
                  </span>
                </div>
              ),
            },
            ...(isStaff ? [{
              title: 'Phạm vi', dataIndex: 'visibility', width: 130,
              render: (v: string) => <span className="post-scope">{postVisibilityLabel[v]}</span>,
            }] : []),
            { title: 'Người đăng', dataIndex: 'authorName', width: 190 },
            { title: 'Ngày đăng', dataIndex: 'createdAt', width: 150, className: 'tabular', render: (v: string) => formatDateTime(v) },
            ...(isStaff && status === 'Hidden' ? [{
              title: 'Trạng thái', dataIndex: 'status', width: 130, align: 'center' as const,
              render: (v: string) => <StatusBadge domain="post" value={v} />,
            }] : []),
            ...(isStaff ? [{
              title: 'Thao tác', key: 'actions', width: 140, align: 'center' as const, fixed: 'right' as const,
              render: (_: unknown, p: PostListItem) => (
                <RowActions actions={[
                  { key: 'edit', label: 'Sửa bài', icon: <Pencil size={16} />, hidden: !p.canEdit, onClick: () => void edit(p.postId) },
                  { key: 'hide', label: 'Ẩn bài', icon: <EyeOff size={16} />, hidden: !p.canHide, onClick: () => askHide(p) },
                  { key: 'unhide', label: 'Hiện lại', icon: <Eye size={16} />, hidden: !p.canUnhide, onClick: () => unhide(p) },
                  { key: 'delete', label: 'Xóa bài', icon: <Trash2 size={16} />, danger: true, hidden: !p.canDelete, onClick: () => remove(p) },
                ]} />
              ),
            }] : []),
          ]}
        />
      </Card>

      <PostDetailDrawer
        postId={openId} onClose={() => setOpenId(null)}
        onEdit={(p) => { setOpenId(null); setEditing(p); }}
        onHide={askHide} onUnhide={unhide} onDelete={remove}
      />

      <PostFormModal
        open={composing || editing !== null} post={editing}
        onClose={() => { setComposing(false); setEditing(null); }}
        onSaved={() => { void refresh(); message.success(editing ? 'Đã lưu bài đăng.' : 'Đã đăng bài.'); }}
      />

      <FormModal<HideValues>
        open={hiding !== null} title={`Ẩn bài "${hiding?.title ?? ''}"`} form={hideForm} okText="Ẩn bài"
        onClose={() => setHiding(null)}
        onSubmit={async (v) => {
          await postApi.hide(hiding!.postId, v.reason);
          await refresh();
          message.success('Đã ẩn bài. Ảnh đính kèm được giữ lại.');
        }}
      >
        <p style={{ marginTop: 0, color: 'var(--c-text2)' }}>
          Bài ẩn không hiện trên bảng tin nhưng vẫn giữ nguyên nội dung và ảnh. Người ẩn bài hiện lại được bất cứ lúc nào.
        </p>
        <Form.Item
          label={hiding?.byAuthor ? 'Lý do (không bắt buộc)' : 'Lý do ẩn bài'} name="reason"
          rules={hiding?.byAuthor ? [] : [{ required: true, whitespace: true, message: 'Nhập lý do ẩn bài của người khác.' }]}
        >
          <Input.TextArea rows={3} maxLength={500} showCount />
        </Form.Item>
      </FormModal>
    </>
  );
}
