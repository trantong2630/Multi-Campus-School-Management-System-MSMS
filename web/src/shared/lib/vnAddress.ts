import axios from 'axios';

/** Danh mục tỉnh/thành và phường/xã theo đơn vị hành chính 2025 (Vietnam Provinces online API v2), gọi thẳng từ trình duyệt. */
const client = axios.create({ baseURL: 'https://provinces.open-api.vn/api/v2', timeout: 10000 });

export interface Province { code: number; name: string }
export interface Ward { code: number; name: string }

export const fetchProvinces = () =>
  client.get<Province[]>('/p/').then((r) => [...r.data].sort((a, b) => a.name.localeCompare(b.name, 'vi')));

export const fetchWards = (provinceCode: number) =>
  client.get<{ wards: Ward[] }>(`/p/${provinceCode}`, { params: { depth: 2 } })
    .then((r) => [...r.data.wards].sort((a, b) => a.name.localeCompare(b.name, 'vi')));

const fold = (s: string) => s.normalize('NFD').replace(/\p{M}/gu, '').replace(/đ/g, 'd').replace(/Đ/g, 'D').toLowerCase();

/** Lọc ô chọn: khớp khi gõ có dấu (mọi kiểu mã hóa dấu) hoặc gõ không dấu, vì người dùng hay gõ nhanh "lac" để tìm "Lạc". */
export function matchesSearch(label: string, query: string): boolean {
  const q = query.normalize('NFC').trim().toLowerCase();
  return label.normalize('NFC').toLowerCase().includes(q) || fold(label).includes(fold(query.trim()));
}

/** Địa chỉ lưu thành MỘT chuỗi "số nhà đường, phường/xã, tỉnh/thành" nên không đổi cột trong DB. */
export function composeAddress(street: string, wardName?: string, provinceName?: string): string {
  return [street, wardName, provinceName].filter((p) => p && p.trim().length > 0).join(', ');
}

const norm = (s: string) => s.normalize('NFC').trim().toLowerCase();

/**
 * Tách chuỗi đã lưu ngược lại: phần cuối khớp tên tỉnh thì là tỉnh, phần liền trước khớp tên phường/xã của tỉnh đó thì là phường/xã,
 * còn lại là số nhà, đường. Địa chỉ cũ gõ tay không khớp gì thì toàn bộ nằm ở ô số nhà, đường.
 */
export function parseAddress(value: string, provinces?: Province[], wards?: Ward[]) {
  let parts = value.split(',');
  let province: Province | undefined;
  let ward: Ward | undefined;

  const last = parts.at(-1);
  if (provinces && last !== undefined) {
    province = provinces.find((p) => norm(p.name) === norm(last));
    if (province) parts = parts.slice(0, -1);
  }
  const beforeProvince = parts.at(-1);
  if (province && wards && beforeProvince !== undefined) {
    ward = wards.find((w) => norm(w.name) === norm(beforeProvince));
    if (ward) parts = parts.slice(0, -1);
  }
  return { street: parts.join(','), province, ward };
}
