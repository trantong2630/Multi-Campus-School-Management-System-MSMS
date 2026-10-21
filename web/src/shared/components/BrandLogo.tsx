import './shared.css';

interface Props {
  /** `green` cho nền sáng, `white` cho nền xanh (Design_Web §6.0). */
  tone?: 'green' | 'white';
  /** Cỡ chữ tính bằng px; chiều rộng tự theo. */
  size?: number;
}

/** Logo chữ "msms.": hai chữ đầu đậm, hai chữ sau nhạt, dấu chấm vàng. Dùng duy nhất component này, không dùng ảnh. */
export function BrandLogo({ tone = 'green', size = 32 }: Props) {
  return (
    <span className={`brand-logo brand-logo--${tone}`} style={{ fontSize: size }} role="img" aria-label="MSMS">
      <b>ms</b>ms<i>.</i>
    </span>
  );
}
