import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:image_picker/image_picker.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:material_ui/material_ui.dart';

import '../../core/auth/auth_controller.dart';
import '../../core/format.dart';
import '../../core/http/api_client.dart';
import '../../core/json.dart';
import '../../core/labels.dart';
import '../../core/theme/app_color.dart';
import '../../core/theme/app_spacing.dart';
import '../../core/widgets/auth_image.dart';
import '../../core/widgets/auth_scaffold.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/states.dart';

class Responsibility {
  Responsibility.fromJson(Json j) : kind = j.str('kind'), target = j.str('targetName'), startDate = j.str('startDate');
  final String kind;
  final String target;
  final String startDate;
}

class OwnProfile {
  OwnProfile.fromJson(Json j)
    : personId = j.integer('personId'),
      personCode = j.str('personCode'),
      fullName = j.str('fullName'),
      dateOfBirth = j.strOrNull('dateOfBirth'),
      gender = j.strOrNull('gender'),
      phone = j.strOrNull('phoneNumber'),
      email = j.strOrNull('email'),
      address = j.strOrNull('address'),
      hasAvatar = j.flag('hasAvatar'),
      avatarVersion = j.intOrNull('avatarVersion'),
      username = j.str('username'),
      accountType = j.str('accountType'),
      status = j.str('status'),
      lastLoginAt = j.dateOrNull('lastLoginAt'),
      responsibilities = j.list('activeResponsibilities', Responsibility.fromJson),
      canEditPhone = j.flag('canEditPhone');

  final int personId;
  final String personCode;
  final String fullName;
  final String? dateOfBirth;
  final String? gender;
  final String? phone;
  final String? email;
  final String? address;
  final bool hasAvatar;
  final int? avatarVersion;
  final String username;
  final String accountType;
  final String status;
  final DateTime? lastLoginAt;
  final List<Responsibility> responsibilities;
  final bool canEditPhone;

  String get avatarPath => '/api/iam/profile/avatar/$personId?v=${avatarVersion ?? 0}';
}

const accountTypeLabel = {'Teacher': 'Giáo viên', 'Student': 'Học sinh', 'Parent': 'Phụ huynh'};

final profileProvider = FutureProvider.autoDispose<OwnProfile>(
  (ref) async => OwnProfile.fromJson(await ref.read(apiProvider).get<Json>('/api/iam/profile')),
);

/// Ảnh đại diện tròn của một người (có ảnh thì tải, không thì chữ cái).
class ProfileAvatar extends StatelessWidget {
  const ProfileAvatar({super.key, required this.name, this.path, this.size = 72});
  final String name;
  final String? path;
  final double size;

  @override
  Widget build(BuildContext context) => PersonAvatar(
    name,
    size: size,
    image: path == null ? null : AuthImage(path!, error: PersonAvatar(name, size: size)),
  );
}

/// Tab Cá nhân: hồ sơ, trách nhiệm đang giữ, thông tin liên hệ, đổi mật khẩu, đăng xuất (IAM-04).
class ProfilePage extends ConsumerWidget {
  const ProfilePage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final value = ref.watch(profileProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Cá nhân')),
      body: AsyncView(
        value: value,
        onRetry: () => ref.invalidate(profileProvider),
        loading: const SkeletonList(count: 4, height: 96),
        data: (p) => Refreshable(
          onRefresh: () => ref.refresh(profileProvider.future),
          child: ListView(
            padding: const EdgeInsets.fromLTRB(AppSpacing.screen, 4, AppSpacing.screen, 120),
            children: [
              _IdentityCard(p),
              if (p.responsibilities.isNotEmpty) ...[
                const SectionTitle('Trách nhiệm đang giữ'),
                AppCard(
                  padding: EdgeInsets.zero,
                  child: Column(
                    children: [
                      for (final (i, r) in p.responsibilities.indexed) ...[
                        if (i > 0) const Divider(indent: 16, endIndent: 16),
                        ListTile(
                          title: Text('${r.kind} · ${r.target}', style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 14.5)),
                          subtitle: Text('Từ ${_vnDate(r.startDate)}', style: const TextStyle(fontSize: 12.5, color: AppColor.text3)),
                        ),
                      ],
                    ],
                  ),
                ),
              ],
              SectionTitle(
                'Liên hệ',
                action: 'Sửa',
                onAction: () => context.push('/me/edit', extra: p),
              ),
              AppCard(
                padding: EdgeInsets.zero,
                child: Column(
                  children: [
                    _InfoRow(LucideIcons.phone, 'Số điện thoại', p.phone),
                    const Divider(indent: 52),
                    _InfoRow(LucideIcons.mail, 'Email nhận mã OTP', p.email),
                    const Divider(indent: 52),
                    _InfoRow(LucideIcons.mapPin, 'Địa chỉ', p.address),
                  ],
                ),
              ),
              const SizedBox(height: AppSpacing.xl),
              AppCard(
                padding: EdgeInsets.zero,
                child: Column(
                  children: [
                    _MenuRow(LucideIcons.keyRound, 'Đổi mật khẩu', () => context.push('/me/password')),
                    const Divider(indent: 52),
                    _MenuRow(LucideIcons.logOut, 'Đăng xuất', () async {
                      final ok = await confirm(
                        context,
                        title: 'Đăng xuất?',
                        message: 'Phiên trên máy này sẽ kết thúc. Đăng nhập lại để dùng tiếp.',
                        ok: 'Đăng xuất',
                        danger: true,
                      );
                      if (ok) ref.read(authProvider.notifier).logout();
                    }, danger: true),
                  ],
                ),
              ),
              if (p.lastLoginAt != null)
                Padding(
                  padding: const EdgeInsets.only(top: AppSpacing.lg),
                  child: Text(
                    'Đăng nhập gần nhất ${Fmt.dateTime(p.lastLoginAt!.toLocal())}',
                    textAlign: TextAlign.center,
                    style: const TextStyle(fontSize: 12, color: AppColor.text3),
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

String _vnDate(String iso) {
  final d = DateTime.tryParse(iso);
  return d == null ? iso : Fmt.date(d);
}

class _IdentityCard extends ConsumerWidget {
  const _IdentityCard(this.p);
  final OwnProfile p;

  Future<void> _changeAvatar(BuildContext context, WidgetRef ref) async {
    final choice = await showModalBottomSheet<String>(
      context: context,
      builder: (c) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            ListTile(
              leading: const Icon(LucideIcons.image),
              title: const Text('Chọn ảnh từ thư viện'),
              onTap: () => Navigator.pop(c, 'gallery'),
            ),
            ListTile(leading: const Icon(LucideIcons.camera), title: const Text('Chụp ảnh'), onTap: () => Navigator.pop(c, 'camera')),
            if (p.hasAvatar)
              ListTile(
                leading: const Icon(LucideIcons.trash2, color: AppColor.danger),
                title: const Text('Xóa ảnh đại diện', style: TextStyle(color: AppColor.danger)),
                onTap: () => Navigator.pop(c, 'remove'),
              ),
            const SizedBox(height: 8),
          ],
        ),
      ),
    );
    if (choice == null || !context.mounted) return;
    final api = ref.read(apiProvider);
    try {
      if (choice == 'remove') {
        await api.delete<dynamic>('/api/iam/profile/avatar');
      } else {
        final file = await ImagePicker().pickImage(
          source: choice == 'camera' ? ImageSource.camera : ImageSource.gallery,
          maxWidth: 800,
          maxHeight: 800,
          imageQuality: 85,
        );
        if (file == null) return;
        final form = FormData.fromMap({'file': await MultipartFile.fromFile(file.path, filename: file.name)});
        await api.dio.post<dynamic>('/api/iam/profile/avatar', data: form);
      }
      AuthImage.evict('/api/iam/profile/avatar/${p.personId}');
      ref.invalidate(profileProvider);
      if (context.mounted) toast(context, choice == 'remove' ? 'Đã xóa ảnh đại diện.' : 'Đã cập nhật ảnh đại diện.');
    } catch (e) {
      if (context.mounted) toast(context, errorMessage(e));
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return AppCard(
      padding: const EdgeInsets.all(AppSpacing.xl),
      child: Column(
        children: [
          GestureDetector(
            onTap: () => _changeAvatar(context, ref),
            child: Stack(
              clipBehavior: Clip.none,
              children: [
                ProfileAvatar(name: p.fullName, path: p.hasAvatar ? p.avatarPath : null, size: 88),
                Positioned(
                  right: -2,
                  bottom: -2,
                  child: Container(
                    padding: const EdgeInsets.all(7),
                    decoration: BoxDecoration(
                      color: AppColor.green600,
                      shape: BoxShape.circle,
                      border: Border.all(color: AppColor.surface, width: 2),
                    ),
                    child: const Icon(LucideIcons.camera, size: 14, color: Colors.white),
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: AppSpacing.md),
          Text(
            p.fullName,
            textAlign: TextAlign.center,
            style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w700),
          ),
          const SizedBox(height: 2),
          Text(
            '${accountTypeLabel[p.accountType] ?? p.accountType} · ${p.personCode}',
            style: const TextStyle(fontSize: 13.5, color: AppColor.text2),
          ),
          const SizedBox(height: AppSpacing.md),
          Wrap(
            spacing: 8,
            runSpacing: 6,
            alignment: WrapAlignment.center,
            children: [
              StatusPill(StatusDomain.account, p.status),
              Pill(p.username, group: BadgeGroup.neutral),
            ],
          ),
          if (p.dateOfBirth != null || p.gender != null) ...[
            const SizedBox(height: AppSpacing.md),
            Text(
              [if (p.dateOfBirth != null) 'Sinh ngày ${_vnDate(p.dateOfBirth!)}', if (p.gender != null) _gender(p.gender!)].join(' · '),
              style: const TextStyle(fontSize: 13, color: AppColor.text3),
            ),
          ],
        ],
      ),
    );
  }

  String _gender(String g) => switch (g) {
    'Male' => 'Nam',
    'Female' => 'Nữ',
    _ => g,
  };
}

class _InfoRow extends StatelessWidget {
  const _InfoRow(this.icon, this.label, this.value);
  final IconData icon;
  final String label;
  final String? value;

  @override
  Widget build(BuildContext context) {
    return ListTile(
      leading: Icon(icon, size: 20, color: AppColor.green700),
      title: Text(label, style: const TextStyle(fontSize: 12.5, color: AppColor.text3)),
      subtitle: Text(
        (value ?? '').isEmpty ? 'Chưa có' : value!,
        style: TextStyle(fontSize: 15, color: (value ?? '').isEmpty ? AppColor.text3 : AppColor.text1),
      ),
    );
  }
}

class _MenuRow extends StatelessWidget {
  const _MenuRow(this.icon, this.label, this.onTap, {this.danger = false});
  final IconData icon;
  final String label;
  final VoidCallback onTap;
  final bool danger;

  @override
  Widget build(BuildContext context) {
    final color = danger ? AppColor.danger : AppColor.text1;
    return ListTile(
      minTileHeight: 56,
      leading: Icon(icon, size: 20, color: danger ? AppColor.danger : AppColor.green700),
      title: Text(
        label,
        style: TextStyle(fontSize: 15, fontWeight: FontWeight.w500, color: color),
      ),
      trailing: danger ? null : const Icon(LucideIcons.chevronRight, size: 18, color: AppColor.text3),
      onTap: onTap,
    );
  }
}

/// Sửa số điện thoại, email nhận OTP, địa chỉ (phụ huynh không tự sửa số điện thoại, BR-10).
class EditProfilePage extends ConsumerStatefulWidget {
  const EditProfilePage({super.key, required this.profile});
  final OwnProfile profile;

  @override
  ConsumerState<EditProfilePage> createState() => _EditProfilePageState();
}

class _EditProfilePageState extends ConsumerState<EditProfilePage> {
  final _form = GlobalKey<FormState>();
  late final _phone = TextEditingController(text: widget.profile.phone);
  late final _email = TextEditingController(text: widget.profile.email);
  late final _address = TextEditingController(text: widget.profile.address);
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _phone.dispose();
    _email.dispose();
    _address.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (_busy || !_form.currentState!.validate()) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref
          .read(apiProvider)
          .put<dynamic>(
            '/api/iam/profile',
            data: {
              'phoneNumber': widget.profile.canEditPhone ? _phone.text.trim() : widget.profile.phone,
              'email': _email.text.trim(),
              'address': _address.text.trim(),
            },
          );
      ref.invalidate(profileProvider);
      if (!mounted) return;
      toast(context, 'Đã lưu thông tin liên hệ.');
      context.pop();
    } catch (e) {
      if (mounted) setState(() => _error = errorMessage(e));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final p = widget.profile;
    return PageScaffold(
      title: 'Sửa thông tin liên hệ',
      body: Form(
        key: _form,
        child: ListView(
          padding: const EdgeInsets.all(AppSpacing.screen),
          children: [
            FieldLabel(
              'Số điện thoại',
              child: TextFormField(
                controller: _phone,
                enabled: p.canEditPhone && !_busy,
                keyboardType: TextInputType.phone,
                decoration: InputDecoration(
                  helperText: p.canEditPhone ? null : 'Số điện thoại của phụ huynh do trường quản lý.',
                  helperMaxLines: 2,
                ),
                validator: (v) {
                  final s = (v ?? '').trim();
                  if (!p.canEditPhone || s.isEmpty) return null;
                  return RegExp(r'^0[35789]\d{8}$').hasMatch(s)
                      ? null
                      : 'Số điện thoại gồm 10 chữ số, bắt đầu bằng 03, 05, 07, 08 hoặc 09.';
                },
              ),
            ),
            FieldLabel(
              'Email nhận mã OTP',
              child: TextFormField(
                controller: _email,
                enabled: !_busy,
                keyboardType: TextInputType.emailAddress,
                autocorrect: false,
                decoration: const InputDecoration(helperText: 'Dùng để nhận mã khi quên mật khẩu.'),
                validator: (v) {
                  final s = (v ?? '').trim();
                  if (s.isEmpty) return null;
                  return RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$').hasMatch(s) ? null : 'Email chưa đúng định dạng.';
                },
              ),
            ),
            FieldLabel(
              'Địa chỉ',
              child: TextFormField(controller: _address, enabled: !_busy, maxLines: 3, minLines: 2, maxLength: 300),
            ),
            if (_error != null) ...[InlineAlert(_error!, icon: LucideIcons.circleAlert), const SizedBox(height: 16)],
            BusyButton(label: 'Lưu', busy: _busy, onPressed: _save),
          ],
        ),
      ),
    );
  }
}
