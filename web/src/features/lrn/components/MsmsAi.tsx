import { useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import avatar from '../assets/msms-ai.png';
import './msms-ai.css';

/** idle: lơ lửng · thinking: lắc đầu, ba chấm · happy: nhún nhảy · warn: rung báo gấp. */
export type AiMood = 'idle' | 'thinking' | 'happy' | 'warn';
export type AiTone = 'plain' | 'ok' | 'warn' | 'danger';

interface AvatarProps {
  mood?: AiMood;
  size?: number;
  className?: string;
}

/** Linh vật MSMS AI (logo/chatbot.png) đeo thẻ tên: chỉ chuyển động bằng CSS, tôn trọng "giảm chuyển động" của hệ điều hành. */
export function AiAvatar({ mood = 'idle', size = 72, className }: AvatarProps) {
  return (
    <span className={`ai-avatar ai-avatar--${mood}${className ? ` ${className}` : ''}`} style={{ width: size, height: size, fontSize: Math.max(9, Math.round(size * 0.15)) }} aria-hidden>
      <img src={avatar} alt="" draggable={false} />
      <span className="ai-avatar__tag">MSMS AI</span>
    </span>
  );
}

/** Ba chấm nhấp nháy: MSMS AI đang nghĩ. */
export function AiDots() {
  return (
    <span className="ai-dots" aria-label="MSMS AI đang nghĩ">
      <span /><span /><span />
    </span>
  );
}

interface BubbleProps {
  tone?: AiTone;
  /** Đuôi bong bóng chỉ về phía avatar. */
  tail?: 'left' | 'bottom-right' | 'none';
  children: ReactNode;
  className?: string;
}

export function AiBubble({ tone = 'plain', tail = 'left', children, className }: BubbleProps) {
  return <div className={`ai-bubble ai-bubble--${tone} ai-bubble--tail-${tail}${className ? ` ${className}` : ''}`} role="status">{children}</div>;
}

function TypedInner({ text, speed }: { text: string; speed: number }) {
  const reduce = typeof window !== 'undefined' && window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;
  const [shown, setShown] = useState(reduce ? text.length : 0);
  useEffect(() => {
    if (reduce) return;
    const t = setInterval(() => {
      setShown((n) => {
        if (n >= text.length) { clearInterval(t); return n; }
        return n + 1;
      });
    }, speed);
    return () => clearInterval(t);
  }, [text, speed, reduce]);
  return <span aria-label={text}>{text.slice(0, shown)}</span>;
}

/** Gõ chữ từng ký tự khi nội dung đổi, như MSMS AI đang nhắn. Giảm chuyển động thì hiện luôn. */
export function TypedText({ text, speed = 16 }: { text: string; speed?: number }) {
  return <TypedInner key={text} text={text} speed={speed} />;
}

interface Props {
  mood?: AiMood;
  tone?: AiTone;
  size?: number;
  /** Có thể là chữ (gõ dần) hoặc nội dung tự dựng. Bỏ trống thì chỉ có avatar. */
  say?: ReactNode;
  typed?: boolean;
  className?: string;
}

/** Avatar kèm bong bóng chat, dùng ở mọi chỗ MSMS AI xuất hiện. */
export function MsmsAi({ mood = 'idle', tone = 'plain', size = 72, say, typed = true, className }: Props) {
  return (
    <div className={`ai-line${className ? ` ${className}` : ''}`}>
      <AiAvatar mood={mood} size={size} />
      {say !== undefined && say !== null && (
        <AiBubble tone={tone}>
          {mood === 'thinking' && typeof say !== 'string' ? say : typed && typeof say === 'string' ? <TypedText text={say} /> : say}
        </AiBubble>
      )}
    </div>
  );
}
