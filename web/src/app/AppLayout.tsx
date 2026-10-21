import { Avatar, Badge, Breadcrumb, Button, Dropdown, Layout, Menu, Tooltip } from 'antd';
import { message } from '@/shared/lib/antdStatic';
import type { MenuProps } from 'antd';
import { Bell, ChevronsLeft, ChevronsRight, KeyRound, LogOut, UserRound } from 'lucide-react';
import { BrandLogo } from '@/shared/components/BrandLogo';
import { SchoolLogo } from '@/features/sch/components/SchoolLogo';
import { useSchool } from '@/features/sch/hooks/useSchool';
import { useEffect, useMemo, useState } from 'react';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { accountTypeLabel } from '@/shared/lib/labels';
import { color } from '@/shared/lib/tokens';
import { useAuth } from './AuthProvider';
import { isPathAllowed, resolveCrumbs, visibleMenu } from './menu';
import { Forbidden } from './Forbidden';
import './layout.css';

const { Sider, Header, Content } = Layout;

function initialsOf(fullName: string) {
  const parts = fullName.trim().split(/\s+/);
  return parts[parts.length - 1]?.[0]?.toUpperCase() ?? '?';
}

export function AppLayout() {
  const { user, logout } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();
  const school = useSchool();
  const [collapsed, setCollapsed] = useState(false);

  const groups = useMemo(() => visibleMenu(user!.accountType), [user]);

  // Từ key trong menu suy ra đường dẫn, và ngược lại từ đường dẫn tìm mục đang chọn.
  const pathByKey = useMemo(() => {
    const map = new Map<string, string>();
    for (const g of groups) {
      if (g.path) map.set(g.key, g.path);
      g.children?.forEach((c) => map.set(c.key, c.path));
    }
    return map;
  }, [groups]);

  const selectedKey = useMemo(() => {
    let best: { key: string; len: number } | null = null;
    for (const [key, path] of pathByKey) {
      const hit = path === '/' ? location.pathname === '/' : location.pathname === path || location.pathname.startsWith(path + '/');
      if (hit && (!best || path.length > best.len)) best = { key, len: path.length };
    }
    return best?.key;
  }, [pathByKey, location.pathname]);

  const parentKey = groups.find((g) => g.children?.some((c) => c.key === selectedKey))?.key;
  const [openKeys, setOpenKeys] = useState<string[]>(parentKey ? [parentKey] : []);
  useEffect(() => {
    // Vào trang con thì nhóm cha tự xổ ra và sáng lên (Conventions §4.7).
    if (parentKey) setOpenKeys((keys) => (keys.includes(parentKey) ? keys : [...keys, parentKey]));
  }, [parentKey]);

  const items: MenuProps['items'] = groups.map((g) => {
    const Icon = g.icon;
    const open = openKeys.includes(g.key) || g.key === selectedKey;
    const tile = (
      <span className="nav-tile" style={{ background: open ? color.onGreenHover : 'transparent' }}>
        <Icon size={18} strokeWidth={2} />
      </span>
    );
    if (!g.children) return { key: g.key, icon: tile, label: g.label };
    return {
      key: g.key,
      icon: tile,
      label: g.label,
      children: g.children.map((c) => ({
        key: c.key,
        label: (
          <span className="nav-sub">
            <i className="nav-bullet" />
            <span className="nav-sub__label">{c.label}</span>
          </span>
        ),
      })),
    };
  });

  const userMenu: MenuProps['items'] = [
    { key: 'profile', icon: <UserRound size={16} />, label: 'Hồ sơ cá nhân', onClick: () => navigate('/profile') },
    { key: 'password', icon: <KeyRound size={16} />, label: 'Đổi mật khẩu', onClick: () => navigate('/change-password') },
    { type: 'divider' },
    { key: 'logout', icon: <LogOut size={16} />, label: 'Đăng xuất', onClick: () => void logout() },
  ];

  const crumbs = resolveCrumbs(location.pathname);

  return (
    <Layout className="app-shell">
      <Sider
        className={`app-sider ${collapsed ? '' : 'app-sider--expanded'}`}
        width={248}
        collapsedWidth={64}
        collapsed={collapsed}
        theme="light"
        trigger={null}
      >
        <div className="app-sider__brand">
          {collapsed ? <SchoolLogo size={32} /> : <span title={school.name}><BrandLogo tone="white" size={42} /></span>}
        </div>

        <Menu
          className="app-menu"
          mode="inline"
          inlineIndent={0}
          items={items}
          selectedKeys={selectedKey ? [selectedKey] : []}
          openKeys={collapsed ? undefined : openKeys}
          onOpenChange={setOpenKeys}
          onClick={({ key }) => {
            const path = pathByKey.get(key);
            if (path) navigate(path);
          }}
        />

        <div className="app-sider__footer">
          <Tooltip title={collapsed ? 'Mở rộng menu' : 'Thu gọn menu'} placement="right">
            <Button
              type="text"
              className="icon-btn"
              aria-label={collapsed ? 'Mở rộng menu' : 'Thu gọn menu'}
              icon={collapsed ? <ChevronsRight size={18} /> : <ChevronsLeft size={18} />}
              onClick={() => setCollapsed((c) => !c)}
            />
          </Tooltip>
        </div>
      </Sider>

      <Layout>
        <Header className="app-header">
          <div className="app-header__left">
            <Breadcrumb
              items={crumbs.map((title) => ({ title }))}
              separator="/"
            />
            {school.hasName && <span className="app-header__school" title={school.name}>{school.name}</span>}
          </div>
          <div className="app-header__right">
            <Tooltip title="Thông báo">
              <Badge count={0} size="small">
                <Button
                  type="text"
                  className="icon-btn"
                  aria-label="Thông báo"
                  icon={<Bell size={18} />}
                  onClick={() => message.info('Trung tâm thông báo sẽ có ở bản sau.')}
                />
              </Badge>
            </Tooltip>

            <Dropdown menu={{ items: userMenu }} trigger={['click']} placement="bottomRight">
              <button type="button" className="user-chip">
                <Avatar size={32} style={{ background: color.green100, color: color.green700, fontWeight: 600 }}>
                  {initialsOf(user!.fullName)}
                </Avatar>
                <span className="user-chip__text">
                  <span className="user-chip__name">{user!.fullName}</span>
                  <span className="user-chip__type">{accountTypeLabel[user!.accountType]}</span>
                </span>
              </button>
            </Dropdown>
          </div>
        </Header>

        <Content className="app-content">
          <div className="app-content__inner">
            {isPathAllowed(location.pathname, user!.accountType) ? <Outlet /> : <Forbidden />}
          </div>
        </Content>
      </Layout>
    </Layout>
  );
}
