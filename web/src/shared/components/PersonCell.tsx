import { PersonAvatar } from './PersonAvatar';
import './shared.css';

interface Props {
  name: string;
  /** Dòng phụ nhỏ dưới tên (mã hồ sơ, email...). */
  sub?: string;
  muted?: boolean;
}

/** Ô "người" trong bảng: avatar chữ cái đầu trên nền pastel theo tên, tên, dòng phụ (Design_Web §8.2b). */
export function PersonCell({ name, sub, muted }: Props) {
  return (
    <div className={`person-cell${muted ? ' person-cell--muted' : ''}`}>
      <PersonAvatar name={name} muted={muted} />
      <div className="person-cell__text">
        <span className="person-cell__name">{name}</span>
        {sub && <span className="person-cell__sub tabular">{sub}</span>}
      </div>
    </div>
  );
}
