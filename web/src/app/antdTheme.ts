import { theme } from 'antd';
import type { ThemeConfig } from 'antd';
import { color, fontFamily, radius } from '@/shared/lib/tokens';

export const antdTheme: ThemeConfig = {
  algorithm: theme.defaultAlgorithm,
  token: {
    colorPrimary: color.green600,
    colorLink: color.green600,
    colorError: color.danger,
    colorBgLayout: color.bg,
    colorBgContainer: color.surface,
    colorBorder: color.borderStrong,
    colorBorderSecondary: color.border,
    colorSplit: color.border,
    colorText: color.text1,
    colorTextSecondary: color.text2,
    colorTextTertiary: color.text3,
    colorTextPlaceholder: color.text3,
    colorFillAlter: color.sunken,
    borderRadius: radius.md,
    borderRadiusLG: radius.lg,
    borderRadiusSM: radius.sm,
    fontFamily,
    fontSize: 14,
    controlHeight: 38,
    boxShadow: '0 10px 28px rgba(16,42,48,.12)',
    boxShadowSecondary: '0 10px 28px rgba(16,42,48,.12)',
    wireframe: false,
  },
  components: {
    Table: {
      headerBg: 'transparent', headerColor: color.text2, headerSplitColor: 'transparent',
      rowHoverBg: color.sunken, borderColor: color.border, cellPaddingBlockMD: 14, cellPaddingInlineMD: 12,
    },
    Layout: { siderBg: 'transparent', headerBg: 'transparent', bodyBg: color.bg },
    Menu: { itemSelectedBg: color.surface, itemSelectedColor: color.green700, itemHeight: 46, subMenuItemBg: 'transparent' },
    Card: { paddingLG: 24, headerFontSize: 16, headerHeight: 56 },
    Button: { primaryShadow: 'none', defaultShadow: 'none', dangerShadow: 'none', fontWeight: 600, paddingInline: 18 },
    Tabs: { titleFontSize: 14 },
    Modal: { titleFontSize: 18, titleColor: color.text1 },
    Drawer: { footerPaddingBlock: 12, footerPaddingInline: 24 },
    Progress: { defaultColor: color.green500, remainingColor: color.neutralBg },
    Segmented: { trackBg: color.hoverBg, itemSelectedBg: color.surface, itemSelectedColor: color.green700, itemColor: color.text2 },
    Typography: { fontFamilyCode: "ui-monospace, 'Cascadia Mono', Consolas, monospace" },
  },
};
