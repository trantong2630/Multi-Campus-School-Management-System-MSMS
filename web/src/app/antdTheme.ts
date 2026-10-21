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
    colorBorderSecondary: color.border,
    colorText: color.text1,
    colorTextSecondary: color.text2,
    colorTextTertiary: color.text3,
    borderRadius: radius.md,
    borderRadiusLG: radius.lg,
    borderRadiusSM: radius.sm,
    fontFamily,
    fontSize: 14,
    controlHeight: 36,
    wireframe: false,
  },
  components: {
    Table: { headerBg: color.tableHeaderBg, rowHoverBg: color.green50, headerSplitColor: 'transparent' },
    Layout: { siderBg: 'transparent', headerBg: 'transparent', bodyBg: color.bg },
    Menu: { itemSelectedBg: color.surface, itemSelectedColor: color.green700, itemHeight: 44 },
    Card: { paddingLG: 24 },
  },
};
