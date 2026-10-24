import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button, Card, Form, Input } from 'antd';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { ImageUp, Save, Trash2 } from 'lucide-react';
import { useEffect, useState } from 'react';
import { getErrorMessage } from '@/app/apiClient';
import { useAuth } from '@/app/AuthProvider';
import { FileButton } from '@/shared/components/FileButton';
import { PageHeader } from '@/shared/components/PageHeader';
import { message } from '@/shared/lib/antdStatic';
import { confirmAction, showError } from '@/shared/lib/feedback';
import { schApi } from '../api';
import { SchoolLogo } from '../components/SchoolLogo';
import { useSchool } from '../hooks/useSchool';
import '../components/school.css';

interface FormValues {
  name: string;
  address?: string;
  phoneNumber?: string;
  email?: string;
  website?: string;
}

const MAX_LOGO_BYTES = 2 * 1024 * 1024;

/** SCH-07: thông tin nhận diện của trường. Hiệu trưởng và School Admin sửa; các loại còn lại chỉ xem. */
export function SchoolProfilePage() {
  const { user } = useAuth();
  const canWrite = user?.accountType === 'SchoolAdmin' || user?.accountType === 'Principal';
  const qc = useQueryClient();
  const { profile, logoSrc, name, isLoading } = useSchool();
  const [form] = Form.useForm<FormValues>();
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (profile) {
      form.setFieldsValue({
        name: profile.name ?? undefined, address: profile.address ?? undefined, phoneNumber: profile.phoneNumber ?? undefined,
        email: profile.email ?? undefined, website: profile.website ?? undefined,
      });
    }
  }, [profile, form]);

  const refresh = () => qc.invalidateQueries({ queryKey: ['school'] });

  const save = useMutation({
    mutationFn: (v: FormValues) => schApi.school.update(v),
    onSuccess: () => { void refresh(); message.success('Đã lưu thông tin trường.'); },
    onError: (e) => setError(getErrorMessage(e)),
  });
  const upload = useMutation({
    mutationFn: (file: File) => schApi.school.uploadLogo(file),
    onSuccess: () => { void refresh(); message.success('Đã cập nhật logo.'); },
    onError: (e) => showError(e, 'Không tải được logo'),
  });
  const remove = useMutation({
    mutationFn: () => schApi.school.removeLogo(),
    onSuccess: () => { void refresh(); message.success('Đã gỡ logo.'); },
    onError: (e) => showError(e),
  });

  const pickLogo = (file: File) => {
    if (file.size > MAX_LOGO_BYTES) {
      showError({ response: { data: { message: 'Logo tối đa 2 MB. Chọn ảnh nhỏ hơn.' } } }, 'Không tải được logo');
      return;
    }
    upload.mutate(file);
  };

  return (
    <>
      <PageHeader
        title="Thông tin trường"
        description="Tên trường hiện ở đầu trang và Trang chủ; logo trường hiện ở Trang chủ. Hiệu trưởng và quản trị hệ thống được sửa."
      />
      <div className="school-grid">
        <Card title="Thông tin chung" loading={isLoading}>
          <Form<FormValues>
            form={form} layout="vertical" requiredMark={false} disabled={!canWrite}
            onFinish={(v) => { setError(null); save.mutate(v); }}
            onValuesChange={() => setError(null)}
          >
            <Form.Item label="Tên trường" name="name" rules={[{ required: true, whitespace: true, message: 'Nhập tên trường.' }]}>
              <Input maxLength={200} />
            </Form.Item>
            <Form.Item label="Địa chỉ" name="address"><Input maxLength={255} /></Form.Item>
            <div className="form-grid">
              <Form.Item label="Số điện thoại" name="phoneNumber"><Input maxLength={20} /></Form.Item>
              <Form.Item label="Email" name="email" rules={[{ type: 'email', message: 'Email chưa đúng định dạng.' }]}>
                <Input maxLength={150} />
              </Form.Item>
            </div>
            <Form.Item
              label="Website" name="website"
              rules={[{ pattern: /^https?:\/\/.+/i, message: 'Website phải bắt đầu bằng http:// hoặc https://.' }]}
            >
              <Input maxLength={200} placeholder="https://" />
            </Form.Item>
            {error && <InlineAlert type="error" title={error} />}
            {canWrite && (
              <Button type="primary" htmlType="submit" icon={<Save size={16} />} loading={save.isPending}>Lưu</Button>
            )}
          </Form>
        </Card>

        <Card title="Logo">
          <div className="school-logo-box">
            {logoSrc ? <img src={logoSrc} alt={`Logo ${name}`} /> : <SchoolLogo size={72} />}
          </div>
          <p style={{ textAlign: 'center', color: 'var(--c-text2)', margin: '0 0 16px' }}>
            Ảnh JPG hoặc PNG, tối đa 2 MB.
          </p>
          {canWrite && (
            <>
              <div className="school-logo-actions">
                <FileButton accept=".png,.jpg,.jpeg" icon={<ImageUp size={16} />} disabled={!profile?.name} loading={upload.isPending} onFile={pickLogo}>
                  Tải logo lên
                </FileButton>
                {logoSrc && (
                  <Button
                    danger icon={<Trash2 size={16} />}
                    onClick={() => confirmAction({
                      title: 'Gỡ logo trường?', content: 'Trang chủ sẽ dùng biểu tượng mặc định.',
                      okText: 'Gỡ logo', danger: true, onOk: () => remove.mutateAsync().catch(() => undefined),
                    })}
                  >
                    Gỡ logo
                  </Button>
                )}
              </div>
              {!profile?.name && <p className="muted" style={{ textAlign: 'center', marginTop: 12 }}>Lưu tên trường trước, sau đó mới tải logo.</p>}
            </>
          )}
        </Card>
      </div>
    </>
  );
}
