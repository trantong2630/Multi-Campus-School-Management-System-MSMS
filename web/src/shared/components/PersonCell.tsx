import { Link } from 'react-router-dom';
import { PersonAvatar } from './PersonAvatar';
import './shared.css';

interface Props {
  name: string;
  /** Dòng phụ nhỏ dưới tên (mã hồ sơ, email...). */
  sub?: string;
  muted?: boolean;
  /** Có thì tên là link mở trang hồ sơ của người đó (vd. Hồ sơ học sinh). */
  to?: string;
}

/** Ô "người" trong bảng: avatar chữ cái đầu trên nền pastel theo tên, tên, dòng phụ (Design_Web §8.2b). */
export function PersonCell({ name, sub, muted, to }: Props) {
  return (
    <div className={`person-cell${muted ? ' person-cell--muted' : ''}`}>
      <PersonAvatar name={name} muted={muted} />
      <div className="person-cell__text">
        {to
          ? <Link to={to} className="person-cell__name person-cell__link">{name}</Link>
          : <span className="person-cell__name">{name}</span>}
        {sub && <span className="person-cell__sub tabular">{sub}</span>}
      </div>
    </div>
  );
}
