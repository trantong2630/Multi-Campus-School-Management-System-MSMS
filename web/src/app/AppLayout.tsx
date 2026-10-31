import { Avatar, Badge, Button, Dropdown, Layout, Menu, Tooltip } from 'antd';
import type { MenuProps } from 'antd';
import { ChevronDown, ChevronRight, ChevronsLeft, ChevronsRight, KeyRound, LogOut, Search, UserRound } from 'lucide-react';
import { BrandLogo } from '@/shared/components/BrandLogo';
import { useSchool } from '@/features/sch/hooks/useSchool';
import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { accountTypeLabel, logoutErrorTitle, logoutLabel } from '@/shared/lib/labels';
import { showError } from '@/shared/lib/feedback';
import { color } from '@/shared/lib/tokens';
import { conversationApi } from '@/features/par/api';
import { useAuth } from './AuthProvider';
import { CommandPalette } from './CommandPalette';
import { isPathAllowed, resolveCrumbs, sectionLabel, visibleMenu } from './menu';
import type { MenuSection } from './menu';
import { Forbidden } from './Forbidden';
import './layout.css';

const { Sider, Header, Content } = Layout;

function initialsOf(fullName: string) {
  const parts = fullName.trim().split(/\s+/);
  return parts[parts.length - 1]?.[0]?.toUpperCase() ?? '?';
}

interface Indicator { top: number; height: number; visible: boolean }

export function AppLayout() {
  const { user, logout, loggingOut, refresh } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();
  const school = useSchool();
  const [collapsed, setCollapsed] = useState(false);
  const [paletteOpen, setPaletteOpen] = useState(false);

  const groups = useMemo(() => visibleMenu(user!), [user]);

  useEffect(() => {
    // Đổi màn hình cũng cập nhật trách nhiệm; API vẫn tự kiểm quyền cho từng request.
    void refresh().catch(() => {});
  }, [location.pathname, refresh]);

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
    // Vào trang con thì nhóm cha tự xổ ra (Conventions §4.7).
    if (parentKey) setOpenKeys((keys) => (keys.includes(parentKey) ? keys : [...keys, parentKey]));
  }, [parentKey]);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        setPaletteOpen(true);
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  const [unreadInboxCount, setUnreadInboxCount] = useState<number>(0);

  useEffect(() => {
    if (user?.accountType === 'Parent' || user?.accountType === 'Teacher') {
      conversationApi.getTotalUnreadCount()
        .then(setUnreadInboxCount)
        .catch(() => {});
    }
  }, [user, location.pathname]);

  // Menu chia cụm (Tổng quan / Quản lý / Hệ thống); cụm chỉ hiện khi còn ít nhất một nhóm.
  const items: MenuProps['items'] = useMemo(() => {
    const toItem = (g: (typeof groups)[number]) => {
      const Icon = g.icon;
      const open = openKeys.includes(g.key) && g.key !== selectedKey;
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
            <span className="nav-sub" title={c.label}>
              <span className="nav-sub__label">{c.label}</span>
              {c.key === 'par-inbox' && unreadInboxCount > 0 && (
                <Badge count={unreadInboxCount} size="small" style={{ marginLeft: 8 }} />
              )}
            </span>
          ),
        })),
      };
    };
    const sections: MenuSection[] = ['overview', 'manage', 'system'];
    return sections
      .map((sec) => ({ sec, list: groups.filter((g) => g.section === sec) }))
      .filter((x) => x.list.length > 0)
      .map(({ sec, list }) => ({ type: 'group' as const, key: `sec-${sec}`, label: sectionLabel[sec], children: list.map(toItem) }));
  }, [groups, openKeys, selectedKey, unreadInboxCount]);

  // Viên trắng của mục đang chọn là một lớp riêng nằm dưới menu, trượt mềm tới vị trí mới khi đổi trang hoặc mở/đóng nhóm.
  const navRef = useRef<HTMLDivElement>(null);
  const [indicator, setIndicator] = useState<Indicator>({ top: 0, height: 0, visible: false });
  const measure = useCallback(() => {
    const nav = navRef.current;
    if (!nav) return;
    const isShown = (el: Element | null): el is HTMLElement =>
      !!el && (el as HTMLElement).offsetParent !== null && el.getBoundingClientRect().height > 0;
    let target: Element | null = nav.querySelector('.ant-menu-item-selected');
    // Nhóm cha đang đóng hoặc đang thu lại (hoặc sider thu gọn): viên trắng nằm ở tiêu đề nhóm cha.
    const parent = target?.closest('.ant-menu-submenu');
    if (!isShown(target) || (parent && !parent.classList.contains('ant-menu-submenu-open'))) {
      target = nav.querySelector('.ant-menu-submenu-selected > .ant-menu-submenu-title');
    }
    if (!isShown(target)) {
      setIndicator((i) => (i.visible ? { ...i, visible: false } : i));
      return;
    }
    const rect = target.getBoundingClientRect();
    // Làm tròn về pixel nguyên để mép tab không bị nhoè thành vạch sáng trên nền xanh.
    const top = Math.round(rect.top - nav.getBoundingClientRect().top + nav.scrollTop);
    const height = Math.round(rect.height);
    setIndicator((i) => (i.top === top && i.height === height && i.visible ? i : { top, height, visible: true }));
  }, []);

  useLayoutEffect(() => {
    measure();
  }, [measure, selectedKey, openKeys, collapsed, items]);

  useEffect(() => {
    const menu = navRef.current?.querySelector('.ant-menu');
    if (!menu) return;
    // Nhóm con xổ ra/thu vào có hiệu ứng chiều cao: đo lại mỗi khi menu đổi kích thước.
    const ro = new ResizeObserver(() => measure());
    ro.observe(menu);
    return () => ro.disconnect();
  }, [measure, collapsed]);

  const userMenu: MenuProps['items'] = [
    { key: 'profile', icon: <UserRound size={16} />, label: 'Hồ sơ cá nhân', onClick: () => navigate('/profile') },
    { key: 'password', icon: <KeyRound size={16} />, label: 'Đổi mật khẩu', onClick: () => navigate('/change-password') },
    { type: 'divider' },
    {
      key: 'logout', icon: <LogOut size={16} />, label: logoutLabel(loggingOut), disabled: loggingOut,
      onClick: () => { void logout().catch((error: unknown) => showError(error, logoutErrorTitle)); },
    },
  ];

  const crumbs = resolveCrumbs(location.pathname);

  // Tab trình duyệt: tên trang trước để nhiều tab vẫn phân biệt được khi bị cắt chữ; Trang chủ chỉ có tên trường.
  const pageTitle = location.pathname === '/' ? undefined : crumbs[crumbs.length - 1];
  useEffect(() => {
    document.title = pageTitle ? `${pageTitle} · ${school.name}` : school.name;
  }, [pageTitle, school.name]);

  return (
    <Layout className="app-shell">
      <Sider
        className={`app-sider ${collapsed ? 'app-sider--collapsed' : 'app-sider--expanded'}`}
        width={256}
        collapsedWidth={72}
        collapsed={collapsed}
        theme="light"
        trigger={null}
      >
        <div className="app-sider__brand">
          <BrandLogo tone="white" size={collapsed ? 18 : 34} />
        </div>

        <div className="app-nav" ref={navRef}>
          <span
            className="nav-indicator"
            aria-hidden
            style={{ transform: `translateY(${indicator.top}px)`, height: indicator.height, opacity: indicator.visible ? 1 : 0 }}
          />
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
        </div>

        <div className="app-sider__footer">
          <Tooltip title={collapsed ? 'Mở rộng menu' : 'Thu gọn menu'} placement="right">
            <Button
              type="text"
              className="sider-collapse"
              aria-label={collapsed ? 'Mở rộng menu' : 'Thu gọn menu'}
              icon={collapsed ? <ChevronsRight size={18} /> : <ChevronsLeft size={18} />}
              onClick={() => setCollapsed((c) => !c)}
            />
          </Tooltip>
        </div>
      </Sider>

      <Layout className="app-main">
        {/* Bầu trời aqua nhạt ở nửa trên và hai lớp đồi xanh mờ ở đáy, cùng ngôn ngữ với màn đăng nhập; cố định theo khung nhìn, mép trái mờ dần để tab khuyết của sider nối liền với nền. */}
        <div className="page-backdrop" aria-hidden style={{ left: collapsed ? 72 : 256 }}>
          <svg viewBox="0 0 1440 900" preserveAspectRatio="none">
            <defs>
              <linearGradient id="bd-sky" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0" style={{ stopColor: 'var(--c-aqua)', stopOpacity: 0.4 }} />
                <stop offset="0.5" style={{ stopColor: 'var(--c-aqua)', stopOpacity: 0 }} />
              </linearGradient>
            </defs>
            <rect width="1440" height="900" fill="url(#bd-sky)" />
            <path d="M0 850 C 300 810, 640 860, 940 828 S 1300 800, 1440 830 L1440 900 L0 900 Z" fill="var(--c-meadow1)" opacity=".35" />
            <path d="M0 884 C 420 856, 800 892, 1100 868 S 1380 858, 1440 870 L1440 900 L0 900 Z" fill="var(--c-meadow2)" opacity=".28" />
          </svg>
        </div>
        <Header className="app-header">
          <nav className="header-crumb" aria-label="Vị trí hiện tại">
            <span className="header-crumb__path">
              {(crumbs.length ? crumbs : ['Trang chủ']).map((c, k, arr) => (
                <span key={`${k}-${c}`} className={k === arr.length - 1 ? 'header-crumb__item header-crumb__item--current' : 'header-crumb__item'}>
                  {k > 0 && <ChevronRight size={14} />}
                  {c}
                </span>
              ))}
            </span>
            <span className="header-crumb__school" title={school.name}>{school.name}</span>
          </nav>
          <button type="button" className="header-search" onClick={() => setPaletteOpen(true)} aria-label="Tìm nhanh màn hình">
            <Search size={16} />
            <span>Tìm nhanh màn hình</span>
            <kbd className="kbd">Ctrl K</kbd>
          </button>
          <Dropdown menu={{ items: userMenu }} trigger={['click']} placement="bottomRight">
            <button type="button" className="header-user" aria-label="Tài khoản">
              <Avatar size={34} className="header-user__avatar">{initialsOf(user!.fullName)}</Avatar>
              <span className="header-user__text">
                <span className="header-user__name">{user!.fullName}</span>
                <span className="header-user__type">{accountTypeLabel[user!.accountType]}</span>
              </span>
              <ChevronDown size={16} className="header-user__more" />
            </button>
          </Dropdown>
        </Header>

        <Content className="app-content">
          {/* key theo đường dẫn để nội dung mỗi trang hiện dần lên khi chuyển trang. */}
          <div className="app-content__inner" key={location.pathname}>
            {isPathAllowed(location.pathname, user!) ? <Outlet /> : <Forbidden />}
          </div>
        </Content>
      </Layout>

      <CommandPalette open={paletteOpen} onClose={() => setPaletteOpen(false)} groups={groups} />
    </Layout>
  );
}
