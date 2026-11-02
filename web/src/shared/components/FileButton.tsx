import { Button } from 'antd';
import type { ButtonProps } from 'antd';
import { useRef } from 'react';
import type { ReactNode } from 'react';

interface Props extends Omit<ButtonProps, 'onClick' | 'children'> {
  accept?: string;
  onFile?: (file: File) => void;
  /** Cho chọn nhiều file một lần; khi bật thì dùng onFiles. */
  multiple?: boolean;
  onFiles?: (files: File[]) => void;
  /** Ẩn hẳn ô input gốc bằng style, dùng khi CSS toàn cục đè thuộc tính hidden làm lộ "Choose File". */
  forceHideInput?: boolean;
  children: ReactNode;
}

/** Nút chọn file: bấm nút thì mở hộp chọn file, chọn xong gọi onFile. Dùng thay Upload của AntD vì import chỉ cần một file một bước. */
export function FileButton({ accept = '.xlsx', onFile, multiple, onFiles, forceHideInput, children, ...rest }: Props) {
  const input = useRef<HTMLInputElement>(null);
  return (
    <>
      <input
        ref={input}
        type="file"
        accept={accept}
        multiple={multiple}
        hidden
        style={forceHideInput ? { display: 'none' } : undefined}
        onChange={(e) => {
          const files = Array.from(e.target.files ?? []);
          e.target.value = '';   // cho phép chọn lại cùng một file sau khi sửa
          if (files.length === 0) return;
          if (multiple) onFiles?.(files);
          else onFile?.(files[0]);
        }}
      />
      <Button {...rest} onClick={() => input.current?.click()}>
        {children}
      </Button>
    </>
  );
}
