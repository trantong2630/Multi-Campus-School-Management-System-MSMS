import { useQueryClient } from '@tanstack/react-query';
import { Form, Input, Radio } from 'antd';
import { ImagePlus, X } from 'lucide-react';
import { useEffect, useState } from 'react';
import { FileButton } from '@/shared/components/FileButton';
import { FormModal } from '@/shared/components/FormModal';
import { showError } from '@/shared/lib/feedback';
import { MAX_POST_IMAGES, MAX_POST_IMAGE_BYTES, postVisibilityHint, postVisibilityLabel } from '@/shared/lib/labels';
import { postApi } from '../api';
import type { PostDetail, PostInput, PostVisibility } from '../types';
import { LocalImage, PostImage } from './PostImage';
import './posts.css';

interface Props {
  open: boolean;
  /** null là soạn bài mới. */
  post: PostDetail | null;
  onClose: () => void;
  onSaved: () => void;
}

const VISIBILITIES: PostVisibility[] = ['Internal', 'School'];

/** COM-02: soạn hoặc sửa bài đăng (BR-27). Soạn bài chỉ có trên web. Ảnh: JPG hoặc PNG, tối đa 5 ảnh, mỗi ảnh ≤ 2 MB; ảnh đầu là ảnh bìa. */
export function PostFormModal({ open, post, onClose, onSaved }: Props) {
  const qc = useQueryClient();
  const [form] = Form.useForm<PostInput>();
  const [added, setAdded] = useState<File[]>([]);
  const [removed, setRemoved] = useState<number[]>([]);

  useEffect(() => {
    if (open) { setAdded([]); setRemoved([]); }
  }, [open, post?.postId]);

  const kept = (post?.images ?? []).filter((i) => !removed.includes(i.postImageId));
  const total = kept.length + added.length;

  const pick = (file: File) => {
    if (file.size > MAX_POST_IMAGE_BYTES) {
      showError({ response: { data: { message: `Ảnh "${file.name}" lớn hơn 2 MB. Chọn ảnh nhỏ hơn.` } } }, 'Không thêm được ảnh');
      return;
    }
    if (total >= MAX_POST_IMAGES) return;
    setAdded((prev) => [...prev, file]);
  };

  const submit = async (values: PostInput) => {
    if (!post) {
      await postApi.create(values, added);
    } else {
      await postApi.update(post.postId, values);
      for (const id of removed) await postApi.removeImage(post.postId, id);
      if (added.length > 0) await postApi.addImages(post.postId, added);
      void qc.invalidateQueries({ queryKey: ['com', 'post', post.postId] });
    }
    onSaved();
  };

  return (
    <FormModal<PostInput>
      open={open} title={post ? 'Sửa bài đăng' : 'Đăng bài mới'} form={form} width={640}
      okText={post ? 'Lưu' : 'Đăng bài'} onClose={onClose} onSubmit={submit}
      initialValues={post ? { title: post.title, content: post.content, visibility: post.visibility } : { visibility: 'Internal' }}
    >
      <Form.Item label="Tiêu đề" name="title" rules={[{ required: true, whitespace: true, message: 'Nhập tiêu đề bài đăng.' }]}>
        <Input maxLength={200} />
      </Form.Item>
      <Form.Item label="Phạm vi" name="visibility" rules={[{ required: true, message: 'Chọn phạm vi bài đăng.' }]}>
        <Radio.Group>
          {VISIBILITIES.map((v) => (
            <Radio key={v} value={v}>
              <b>{postVisibilityLabel[v]}</b> <span className="muted">— {postVisibilityHint[v]}</span>
            </Radio>
          ))}
        </Radio.Group>
      </Form.Item>
      <Form.Item label="Nội dung" name="content" rules={[{ required: true, whitespace: true, message: 'Nhập nội dung bài đăng.' }]}>
        <Input.TextArea rows={8} maxLength={5000} showCount />
      </Form.Item>
      <Form.Item label={`Ảnh đính kèm (${total}/${MAX_POST_IMAGES})`} extra="JPG hoặc PNG, mỗi ảnh tối đa 2 MB. Ảnh đầu tiên là ảnh bìa.">
        <div className="post-thumbs">
          {kept.map((img, i) => (
            <div className="post-thumb" key={img.postImageId}>
              {post && <PostImage postId={post.postId} imageId={img.postImageId} alt={`Ảnh ${i + 1}`} />}
              <button type="button" aria-label="Gỡ ảnh" className="post-thumb__x" onClick={() => setRemoved((p) => [...p, img.postImageId])}><X size={14} /></button>
            </div>
          ))}
          {added.map((file, i) => (
            <div className="post-thumb" key={`${file.name}-${i}`}>
              <LocalImage file={file} alt={file.name} />
              <button type="button" aria-label="Gỡ ảnh" className="post-thumb__x" onClick={() => setAdded((p) => p.filter((_, j) => j !== i))}><X size={14} /></button>
            </div>
          ))}
        </div>
        <FileButton accept=".png,.jpg,.jpeg" icon={<ImagePlus size={16} />} disabled={total >= MAX_POST_IMAGES} onFile={pick}>
          Thêm ảnh
        </FileButton>
      </Form.Item>
    </FormModal>
  );
}
