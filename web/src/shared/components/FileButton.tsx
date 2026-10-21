import { Button } from 'antd';
import type { ButtonProps } from 'antd';
import { useRef } from 'react';
import type { ReactNode } from 'react';

interface Props extends Omit<ButtonProps, 'onClick' | 'children'> {
  accept?: string;
  onFile: (file: File) => void;
  children: ReactNode;
}

/** Nút chọn file: bấm nút thì mở hộp chọn file, chọn xong gọi onFile. Dùng thay Upload của AntD vì import chỉ cần một file một bước. */
export function FileButton({ accept = '.xlsx', onFile, children, ...rest }: Props) {
  const input = useRef<HTMLInputElement>(null);
  return (
    <>
      <input
        ref={input}
        type="file"
        accept={accept}
        hidden
        onChange={(e) => {
          const file = e.target.files?.[0];
          e.target.value = '';   // cho phép chọn lại cùng một file sau khi sửa
          if (file) onFile(file);
        }}
      />
      <Button {...rest} onClick={() => input.current?.click()}>
        {children}
      </Button>
    </>
  );
}
