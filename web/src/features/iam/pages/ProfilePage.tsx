import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, Descriptions, Form, Input, Skeleton, Spin } from 'antd';
import { ImageUp, Save, Trash2 } from 'lucide-react';
import { useEffect, useState } from 'react';
import { getErrorMessage } from '@/app/apiClient';
import { useAuth } from '@/app/AuthProvider';
import { AddressInput } from '@/shared/components/AddressInput';
import { FileButton } from '@/shared/components/FileButton';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { PageHeader } from '@/shared/components/PageHeader';
import { PersonAvatar } from '@/shared/components/PersonAvatar';
import { StatusBadge } from '@/shared/components/StatusBadge';
import { message } from '@/shared/lib/antdStatic';
import { confirmAction, showError } from '@/shared/lib/feedback';
import { formatDate, formatDateTime } from '@/shared/lib/format';
import { accountTypeLabel } from '@/shared/lib/labels';
import { iamApi } from '../api';
import { useAvatarSrc } from '../hooks/useAvatarSrc';
import type { OwnProfile } from '../types';
import './profile.css';

const MAX_AVATAR_BYTES = 2 * 1024 * 1024;
const AVATAR_SIZE = 112;
const PROFILE_KEY = ['iam', 'profile'] as const;

interface EditFormValues {
  phoneNumber?: string;
  email?: string;
  address?: string;
}

const GENDER_LABELS: Record<string, string> = { Male: 'Nam', Female: 'Nữ', Other: 'Khác' };
const PERSON_TYPE_LABELS: Record<OwnProfile['personType'], string> = { Staff: 'Cán bộ', Student: 'Học sinh', Parent: 'Phụ huynh' };

/** Chỉ Hiệu phó và Giáo viên có trách nhiệm theo thời gian (phụ trách phân hiệu, tổ trưởng, chủ nhiệm). */
const HAS_RESPONSIBILITIES = new Set(['VicePrincipal', 'Teacher']);

/** Ảnh đại diện lớn: ảnh thật tải kèm access token (blob); chưa có ảnh thì dùng chữ cái đầu như mọi danh sách người (§8.2b). */
function ProfileAvatar({ profile }: { profile: OwnProfile }) {
  const { src, loading } = useAvatarSrc(profile.personId, profile.hasAvatar ? profile.avatarVersion : null);
  if (loading) return <Skeleton.Avatar active size={AVATAR_SIZE} />;
  if (src) return <img className="profile-avatar-img" src={src} alt={`Ảnh đại diện ${profile.fullName}`} />;
  return <PersonAvatar name={profile.fullName} size={AVATAR_SIZE} />;
}

/**
 * IAM-04: hồ sơ cá nhân của chính người đăng nhập.
 * 4.1.27 xem thông tin và trách nhiệm đang giữ · 4.1.28 sửa liên hệ (gồm email nhận OTP)
 * · 4.1.29 tải/đổi ảnh đại diện · 4.1.30 xem loại tài khoản, trạng thái, lần đăng nhập gần nhất.
 */
export function ProfilePage() {
  const qc = useQueryClient();
  const [form] = Form.useForm<EditFormValues>();
  const [editError, setEditError] = useState<string | null>(null);

  const { data: profile, isLoading, error } = useQuery({ queryKey: PROFILE_KEY, queryFn: iamApi.profile.get });

  useEffect(() => {
    if (profile) {
      form.setFieldsValue({
        phoneNumber: profile.phoneNumber ?? undefined,
        email: profile.email ?? undefined,
        address: profile.address ?? undefined,
      });
    }
  }, [profile, form]);

  const { refresh: refreshUser } = useAuth();

  // Kết quả trả về là hồ sơ mới nhất nên đặt thẳng vào cache, không cần tải lại.
  // Đổi/gỡ ảnh thì tải lại thông tin phiên để avatar ở chip tài khoản góc phải cập nhật theo.
  const apply = (next: OwnProfile) => qc.setQueryData(PROFILE_KEY, next);
  const applyAvatar = (next: OwnProfile) => { apply(next); void refreshUser().catch(() => {}); };

  const save = useMutation({
    mutationFn: (v: EditFormValues) => iamApi.profile.update(v),
    onSuccess: (next) => { apply(next); message.success('Đã lưu thông tin liên hệ.'); },
    onError: (e) => setEditError(getErrorMessage(e)),
  });
  const upload = useMutation({
    mutationFn: (file: File) => iamApi.profile.uploadAvatar(file),
    onSuccess: (next) => { applyAvatar(next); message.success('Đã cập nhật ảnh đại diện.'); },
    onError: (e) => showError(e, 'Không tải được ảnh đại diện'),
  });
  const remove = useMutation({
    mutationFn: () => iamApi.profile.removeAvatar(),
    onSuccess: (next) => { applyAvatar(next); message.success('Đã gỡ ảnh đại diện.'); },
    onError: (e) => showError(e),
  });

  const pickAvatar = (file: File) => {
    if (file.size > MAX_AVATAR_BYTES) {
      showError({ response: { data: { message: 'Ảnh đại diện tối đa 2 MB. Chọn ảnh nhỏ hơn.' } } }, 'Không tải được ảnh đại diện');
      return;
    }
    upload.mutate(file);
  };

  if (error) {
    return (
      <>
        <PageHeader title="Hồ sơ cá nhân" />
        <InlineAlert type="error" title="Không tải được hồ sơ cá nhân" description={getErrorMessage(error)} />
      </>
    );
  }

  const labelStyle = { color: 'var(--c-text2)', width: 160 };

  return (
    <>
      <PageHeader
        title="Hồ sơ cá nhân"
        description="Thông tin cá nhân, trạng thái tài khoản và ảnh đại diện của bạn. Họ tên, ngày sinh do nhà trường quản lý."
      />
      <div className="profile-grid">
        <div className="profile-col">
          {/* 4.1.27: thông tin cá nhân — chỉ xem */}
          <Card title="Thông tin cá nhân" loading={isLoading}>
            {profile && (
              <Descriptions column={2} styles={{ label: labelStyle }}>
                <Descriptions.Item label="Họ và tên"><strong>{profile.fullName}</strong></Descriptions.Item>
                <Descriptions.Item label="Mã hồ sơ"><span className="tabular">{profile.personCode}</span></Descriptions.Item>
                <Descriptions.Item label="Loại hồ sơ">{PERSON_TYPE_LABELS[profile.personType] ?? profile.personType}</Descriptions.Item>
                <Descriptions.Item label="Ngày sinh"><span className="tabular">{formatDate(profile.dateOfBirth)}</span></Descriptions.Item>
                <Descriptions.Item label="Giới tính">{profile.gender ? GENDER_LABELS[profile.gender] ?? profile.gender : '—'}</Descriptions.Item>
              </Descriptions>
            )}
          </Card>

          {/* 4.1.28: trường cá nhân được phép sửa */}
          <Card title="Thông tin liên hệ" loading={isLoading}>
            <Form<EditFormValues>
              form={form}
              layout="vertical"
              requiredMark={false}
              onFinish={(v) => { setEditError(null); save.mutate(v); }}
              onValuesChange={() => setEditError(null)}
            >
              <div className="form-grid">
                <Form.Item
                  label="Số điện thoại"
                  name="phoneNumber"
                  extra={profile && !profile.canEditPhone ? 'Số điện thoại là khóa nhận diện phụ huynh. Liên hệ nhà trường để đổi.' : undefined}
                >
                  <Input maxLength={20} disabled={!profile?.canEditPhone} />
                </Form.Item>
                <Form.Item
                  label="Email"
                  name="email"
                  rules={[{ type: 'email', message: 'Email chưa đúng định dạng.' }]}
                  extra="Dùng để nhận mã OTP khi quên mật khẩu."
                >
                  <Input maxLength={150} placeholder="ten@example.com" />
                </Form.Item>
              </div>
              <Form.Item label="Địa chỉ" name="address" rules={[{ max: 255, message: 'Địa chỉ tối đa 255 ký tự. Rút gọn số nhà, đường.' }]}>
                <AddressInput />
              </Form.Item>
              {editError && <InlineAlert type="error" title={editError} />}
              <Button type="primary" htmlType="submit" icon={<Save size={16} />} loading={save.isPending}>Lưu</Button>
            </Form>
          </Card>
        </div>

        <div className="profile-col">
          {/* 4.1.29: ảnh đại diện */}
          <Card title="Ảnh đại diện">
            <div className="profile-avatar-box">
              {!profile || upload.isPending || remove.isPending
                ? (isLoading ? <Skeleton.Avatar active size={AVATAR_SIZE} /> : <Spin />)
                : <ProfileAvatar profile={profile} />}
            </div>
            <p className="profile-avatar-hint">Ảnh JPG hoặc PNG, tối đa 2 MB.</p>
            <div className="profile-avatar-actions">
              <FileButton
                accept=".png,.jpg,.jpeg"
                icon={<ImageUp size={16} />}
                disabled={!profile || remove.isPending}
                loading={upload.isPending}
                onFile={pickAvatar}
              >
                {profile?.hasAvatar ? 'Đổi ảnh' : 'Tải ảnh lên'}
              </FileButton>
              {profile?.hasAvatar && (
                <Button
                  danger
                  icon={<Trash2 size={16} />}
                  disabled={upload.isPending}
                  onClick={() => confirmAction({
                    title: 'Gỡ ảnh đại diện?',
                    content: 'Ảnh sẽ bị xóa, nơi hiển thị dùng chữ cái đầu tên thay thế.',
                    okText: 'Gỡ ảnh',
                    danger: true,
                    onOk: () => remove.mutateAsync().catch(() => undefined),
                  })}
                >
                  Gỡ ảnh
                </Button>
              )}
            </div>
          </Card>

          {/* 4.1.30: trạng thái tài khoản */}
          <Card title="Tài khoản" loading={isLoading}>
            {profile && (
              <Descriptions column={1} styles={{ label: { ...labelStyle, width: 'auto', whiteSpace: 'nowrap' } }}>
                <Descriptions.Item label="Tên đăng nhập"><span className="tabular">{profile.username}</span></Descriptions.Item>
                <Descriptions.Item label="Loại tài khoản">{accountTypeLabel[profile.accountType]}</Descriptions.Item>
                <Descriptions.Item label="Trạng thái"><StatusBadge domain="account" value={profile.status} /></Descriptions.Item>
                <Descriptions.Item label="Đăng nhập gần nhất">
                  <span className="tabular">{formatDateTime(profile.lastLoginAt)}</span>
                </Descriptions.Item>
              </Descriptions>
            )}
          </Card>

          {/* 4.1.27: trách nhiệm đang giữ tại ngày hiện tại */}
          {profile && HAS_RESPONSIBILITIES.has(profile.accountType) && (
            <Card title="Trách nhiệm đang giữ">
              {profile.activeResponsibilities.length === 0
                ? <p className="muted profile-empty">Hiện chưa giữ trách nhiệm nào.</p>
                : (
                  <ul className="profile-resp-list">
                    {profile.activeResponsibilities.map((r) => (
                      <li key={`${r.kind}-${r.targetName}`} className="profile-resp">
                        <span className="profile-resp__kind">{r.kind}</span>
                        <span className="profile-resp__target">{r.targetName}</span>
                        <span className="profile-resp__since tabular">từ {formatDate(r.startDate)}</span>
                      </li>
                    ))}
                  </ul>
                )}
            </Card>
          )}
        </div>
      </div>
    </>
  );
}
