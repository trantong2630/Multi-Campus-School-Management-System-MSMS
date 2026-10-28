import { useQuery } from '@tanstack/react-query';
import { Input, Select } from 'antd';
import { composeAddress, fetchProvinces, fetchWards, matchesSearch, parseAddress } from '@/shared/lib/vnAddress';
import type { Province, Ward } from '@/shared/lib/vnAddress';
import './shared.css';

interface Props {
  /** Địa chỉ đầy đủ dạng một chuỗi; Form.Item truyền vào qua `name`. */
  value?: string;
  onChange?: (value: string) => void;
  disabled?: boolean;
}

/**
 * Ô địa chỉ dùng chung: chọn tỉnh/thành, phường/xã từ danh mục hành chính rồi gõ số nhà, đường.
 * Giá trị vẫn là một chuỗi nên dùng như Input thường trong Form.Item. Không tải được danh mục thì vẫn gõ tay được ở ô số nhà, đường.
 */
export function AddressInput({ value, onChange, disabled }: Props) {
  const text = value ?? '';
  const provinces = useQuery({ queryKey: ['vn-address', 'provinces'], queryFn: fetchProvinces, staleTime: Infinity, retry: 1 });
  const provinceCode = parseAddress(text, provinces.data).province?.code;
  const wards = useQuery({
    queryKey: ['vn-address', 'wards', provinceCode],
    queryFn: () => fetchWards(provinceCode!),
    enabled: provinceCode !== undefined,
    staleTime: Infinity,
    retry: 1,
  });
  const { street, province, ward } = parseAddress(text, provinces.data, wards.data);

  const emit = (nextStreet: string, nextWard?: Ward, nextProvince?: Province) =>
    onChange?.(composeAddress(nextStreet, nextWard?.name, nextProvince?.name));

  const pickProvince = (code?: number) => emit(street, undefined, provinces.data?.find((p) => p.code === code));
  const pickWard = (code?: number) => emit(street, wards.data?.find((w) => w.code === code), province);

  return (
    <div className="address-input">
      <div className="address-input__row">
        <Select
          allowClear showSearch filterOption={(input, option) => matchesSearch(String(option?.label ?? ''), input)} placeholder="Tỉnh / Thành phố" disabled={disabled}
          loading={provinces.isLoading} value={province?.code} onChange={pickProvince}
          options={provinces.data?.map((p) => ({ value: p.code, label: p.name }))}
        />
        <Select
          allowClear showSearch filterOption={(input, option) => matchesSearch(String(option?.label ?? ''), input)} placeholder="Phường / Xã" disabled={disabled || !province}
          loading={wards.isLoading} value={ward?.code} onChange={pickWard}
          options={wards.data?.map((w) => ({ value: w.code, label: w.name }))}
        />
      </div>
      <Input
        placeholder="Số nhà, tên đường, khu phố" disabled={disabled}
        value={street} onChange={(e) => emit(e.target.value, ward, province)}
      />
      {(provinces.isError || wards.isError) && (
        <div className="address-input__hint">Không tải được danh mục tỉnh, phường. Nhập địa chỉ đầy đủ vào ô số nhà, đường.</div>
      )}
    </div>
  );
}
