import type { AccountType } from '@/shared/lib/labels';

/** Loại tài khoản chọn được khi tạo cho cán bộ (BR-04). Student và Parent do danh bạ học sinh / phụ huynh quyết định. */
export const STAFF_ACCOUNT_TYPES: AccountType[] = ['SchoolAdmin', 'Principal', 'VicePrincipal', 'Teacher'];
