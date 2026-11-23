import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import type { ChangeEvent, FormEvent, KeyboardEvent } from 'react';
import { getErrorMessage } from '@/app/apiClient';
import { AiAvatar, AiBubble, AiDots, TypedText } from '@/features/lrn/components/MsmsAi';
import { admApi } from '../api';
import type { AiChart, AiChatTurn, AiPerson } from '../types';
import { AiChartView, AiText, MeText } from './AiAnswer';
import './ai-assistant.css';

/** text là chữ hiển thị; wire là bản gửi lại server ở các câu sau (tên người chọn bằng @ đã đổi thành mã). */
interface Message { id: number; from: 'ai' | 'me'; text: string; wire: string; names?: string[]; charts?: AiChart[]; steps?: string[]; failed?: boolean }

const GREETING = 'Chào thầy cô! Mình là MSMS AI. Thầy cô có thể hỏi mình để báo cáo nhanh, so sánh hay xếp hạng số liệu chuyên cần, bài tập, giáo viên vắng và tải dạy.';
const HINT = 'Cần báo cáo, phân tích hãy hỏi mình nhé!';
const SEEN_KEY = 'msms-ai-assistant-hint';
/** Cách đáy trang ngần này (px) thì coi là đang ở cuối trang. */
const END_ZONE = 60;
/** Số lượt hỏi đáp gần nhất gửi kèm (khớp Ai:ChatHistoryTurns ở server, server cắt thêm nếu cần). */
const HISTORY_TURNS = 20;

/** Đoạn "@tên" người dùng đang gõ ngay trước con trỏ. */
const MENTION = /(^|\s)@([^@\n]{0,40})$/;

/**
 * MSMS AI trên màn Tình hình của Hiệu trưởng và Hiệu phó: avatar nổi góc dưới phải, bong bóng câu chào cố định hiện một lần mỗi phiên rồi thu lại.
 * Bấm vào thì màn chat chiếm trọn vùng nội dung (bảng ẩn đi nhưng giữ nguyên, đóng lại thì về đúng chỗ đang xem).
 * Hội thoại chỉ nằm trong tab; gõ @ để chọn học sinh, giáo viên trong phạm vi (gửi đi mã, không gửi tên).
 */
export function AiAssistant({ suggestions }: { suggestions: string[] }) {
  const [open, setOpen] = useState(false);
  const [peek, setPeek] = useState(() => !sessionStorage.getItem(SEEN_KEY));
  const [atEnd, setAtEnd] = useState(false);
  const [messages, setMessages] = useState<Message[]>([]);
  const [draft, setDraft] = useState('');
  const [thinking, setThinking] = useState(false);
  const [mentions, setMentions] = useState<AiPerson[]>([]);
  const [picker, setPicker] = useState<{ query: string; people: AiPerson[]; active: number; loaded: boolean } | null>(null);
  const input = useRef<HTMLTextAreaElement>(null);
  const mirror = useRef<HTMLDivElement>(null);
  const list = useRef<HTMLDivElement>(null);
  const nextId = useRef(1);
  const boardScroll = useRef(0);

  // Bong bóng tự thu lại sau vài giây, không nói liên tục khi người dùng đang đọc số liệu.
  useEffect(() => {
    if (!peek) return;
    sessionStorage.setItem(SEEN_KEY, '1');
    const t = setTimeout(() => setPeek(false), 6000);
    return () => clearTimeout(t);
  }, [peek]);

  // Ở cuối trang có khoảng trống bên trái avatar: hiện lại bong bóng câu chào; cuộn lên thì thu vào để không che số liệu.
  // Theo dõi cả đổi cỡ trang vì bảng tải số liệu xong thì trang dài ra mà không có sự kiện cuộn.
  useEffect(() => {
    if (open) return;
    const check = () => setAtEnd(window.innerHeight + window.scrollY >= document.documentElement.scrollHeight - END_ZONE);
    check();
    const size = new ResizeObserver(check);
    size.observe(document.body);
    window.addEventListener('scroll', check, { passive: true });
    window.addEventListener('resize', check);
    return () => {
      size.disconnect();
      window.removeEventListener('scroll', check);
      window.removeEventListener('resize', check);
    };
  }, [open]);

  // Mở: ẩn bảng bằng class trên body (ai-assistant.css), nhớ vị trí cuộn để đóng lại thì về đúng chỗ. Esc đóng.
  useEffect(() => {
    if (!open) return;
    boardScroll.current = window.scrollY;
    document.body.classList.add('ai-chat-open');
    window.scrollTo({ top: 0 });
    input.current?.focus();
    const onKey = (e: globalThis.KeyboardEvent) => { if (e.key === 'Escape') setOpen(false); };
    window.addEventListener('keydown', onKey);
    return () => {
      window.removeEventListener('keydown', onKey);
      document.body.classList.remove('ai-chat-open');
      window.scrollTo({ top: boardScroll.current });
    };
  }, [open]);

  useEffect(() => { list.current?.scrollTo({ top: list.current.scrollHeight, behavior: 'smooth' }); }, [messages, thinking]);

  // Lớp tô màu sau ô nhập cuộn theo ô nhập khi câu dài hơn khung.
  const syncMirror = () => { if (mirror.current && input.current) mirror.current.scrollTop = input.current.scrollTop; };
  useLayoutEffect(syncMirror, [draft]);

  // Gợi ý người theo chữ gõ sau @ (chờ người dùng ngừng gõ một chút rồi mới hỏi server).
  const query = picker?.query ?? null;
  useEffect(() => {
    if (query === null || query.trim().length < 2) return;
    let live = true;
    const t = setTimeout(() => {
      admApi.aiPeople(query.trim()).then((people) => {
        if (live) setPicker((p) => (p && p.query === query ? { ...p, people, active: 0, loaded: true } : p));
      }).catch(() => {
        if (live) setPicker((p) => (p && p.query === query ? { ...p, people: [], loaded: true } : p));
      });
    }, 200);
    return () => { live = false; clearTimeout(t); };
  }, [query]);

  const ask = async (display: string, wire: string, names?: string[]) => {
    const q = wire.trim();
    if (!q || thinking) return;
    const history: AiChatTurn[] = messages.filter((m) => !m.failed)
      .map((m) => ({ role: m.from === 'me' ? 'user' as const : 'ai' as const, text: m.wire }))
      .slice(-HISTORY_TURNS * 2);
    setMessages((m) => [...m, { id: nextId.current++, from: 'me', text: display.trim(), wire: q, names }]);
    setDraft('');
    setMentions([]);
    setPicker(null);
    setThinking(true);
    try {
      const a = await admApi.aiChat(q, history);
      setMessages((m) => [...m, { id: nextId.current++, from: 'ai', text: a.text, wire: a.historyText, charts: a.charts, steps: a.steps }]);
    } catch (e) {
      setMessages((m) => [...m, { id: nextId.current++, from: 'ai', text: getErrorMessage(e), wire: '', failed: true }]);
    } finally {
      setThinking(false);
    }
  };

  /** Bản gửi đi: mỗi "@Tên" đã chọn đổi thành mã; tên dài đổi trước để không cắt nhầm tên ngắn hơn. */
  const toWire = (text: string) => [...mentions].sort((a, b) => b.name.length - a.name.length)
    .reduce((t, p) => t.split(`@${p.name}`).join(p.code), text);

  const submit = (e?: FormEvent) => { e?.preventDefault(); void ask(draft, toWire(draft), mentions.map((p) => p.name)); };

  const onChange = (e: ChangeEvent<HTMLTextAreaElement>) => {
    setDraft(e.target.value);
    const m = MENTION.exec(e.target.value.slice(0, e.target.selectionStart));
    const same = picker?.query === m?.[2];
    setPicker(m ? { query: m[2], people: same ? picker!.people : [], active: 0, loaded: same ? picker!.loaded : false } : null);
  };

  const choose = (person: AiPerson) => {
    const el = input.current;
    const caret = el?.selectionStart ?? draft.length;
    const before = draft.slice(0, caret).replace(MENTION, (_all, lead: string) => `${lead}@${person.name} `);
    setDraft(before + draft.slice(caret));
    setMentions((m) => (m.some((x) => x.code === person.code) ? m : [...m, person]));
    setPicker(null);
    requestAnimationFrame(() => { el?.focus(); el?.setSelectionRange(before.length, before.length); });
  };

  const onKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (picker && picker.people.length > 0) {
      if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
        e.preventDefault();
        const step = e.key === 'ArrowDown' ? 1 : -1;
        setPicker({ ...picker, active: (picker.active + step + picker.people.length) % picker.people.length });
        return;
      }
      if (e.key === 'Enter' || e.key === 'Tab') { e.preventDefault(); choose(picker.people[picker.active]); return; }
    }
    if (picker && e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); setPicker(null); return; }
    if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); submit(); }
  };

  const openChat = () => { setPeek(false); setOpen(true); };
  const last = messages[messages.length - 1];
  const mood = thinking ? 'thinking' : last?.failed ? 'warn' : 'idle';

  if (!open) {
    const talking = peek || atEnd;
    return (
      <div className="aia-launcher">
        {talking && (
          <button type="button" className="aia-peek" onClick={openChat}>
            <AiBubble tail="right"><TypedText text={HINT} /></AiBubble>
          </button>
        )}
        <button type="button" className="aia-fab" onClick={openChat} aria-label="Mở MSMS AI để hỏi về số liệu">
          <AiAvatar mood={talking ? 'happy' : 'idle'} size={84} />
        </button>
      </div>
    );
  }

  return (
    <section className="aia-view" aria-label="MSMS AI">
      <header className="aia-head">
        <AiAvatar mood={mood} size={44} />
        <div>
          <strong>MSMS AI</strong>
          <span>{thinking ? 'Đang xem số liệu…' : 'Hỏi về số liệu trong phạm vi của bạn'}</span>
        </div>
        <button type="button" className="aia-back" onClick={() => setOpen(false)}>Quay lại bảng <kbd className="kbd">Esc</kbd></button>
      </header>

      <div className="aia-body" ref={list} aria-live="polite">
        <div className="aia-col">
          {messages.length === 0 ? (
            <div className="aia-welcome">
              <AiAvatar mood="happy" size={96} />
              <AiBubble tail="none"><TypedText text={GREETING} /></AiBubble>
              {suggestions.length > 0 && (
                <div className="aia-suggest">
                  <span>Thử hỏi</span>
                  <div>{suggestions.map((s) => <button key={s} type="button" onClick={() => void ask(s, s)}>{s}</button>)}</div>
                </div>
              )}
            </div>
          ) : (
            messages.map((m) => (
              <div key={m.id} className={`aia-msg aia-msg--${m.from}${m.charts?.length ? ' aia-msg--wide' : ''}`}>
                {m.from === 'me' ? <p><MeText text={m.text} names={m.names} /></p> : (
                  <AiBubble tail="none" tone={m.failed ? 'warn' : 'plain'}>
                    <AiText text={m.text} />
                    {m.charts?.map((c, k) => <AiChartView key={k} chart={c} />)}
                    {m.steps && m.steps.length > 0 && <small className="aia-steps">Đã xem: {m.steps.join(' · ')}</small>}
                  </AiBubble>
                )}
              </div>
            ))
          )}
          {thinking && <div className="aia-msg aia-msg--ai"><AiBubble tail="none"><AiDots /></AiBubble></div>}
        </div>
      </div>

      <form className="aia-input" onSubmit={submit}>
        <div className="aia-col">
          {picker && (picker.people.length > 0 ? (
            <ul className="aia-mention" role="listbox" aria-label="Chọn người">
              {picker.people.map((p, k) => (
                <li key={p.code} role="option" aria-selected={k === picker.active}>
                  <button type="button" className={k === picker.active ? 'is-active' : undefined}
                    onMouseDown={(e) => { e.preventDefault(); choose(p); }}>
                    <strong>{p.name}</strong><span>{p.detail}</span>
                  </button>
                </li>
              ))}
            </ul>
          ) : (
            // Gõ @ là thấy ngay lời nhắc, không im lặng chờ đủ chữ.
            <p className="aia-mention aia-mention--hint" role="status">
              {picker.query.trim().length < 2 ? 'Gõ tên học sinh hoặc giáo viên (ít nhất 2 chữ, không cần dấu)'
                : picker.loaded ? 'Không thấy ai khớp trong phạm vi của thầy cô.' : 'Đang tìm…'}
            </p>
          ))}
          {/* Textarea không tô màu được từng đoạn: lớp phía sau vẽ lại đúng chữ đang gõ, @tên đã chọn tô xanh; chữ của textarea trong suốt, chỉ còn con trỏ. */}
          <div className="aia-field">
            <div className="aia-field__mirror" ref={mirror} aria-hidden="true">
              <MeText text={draft.endsWith('\n') ? `${draft} ` : draft} names={mentions.map((p) => p.name)} />
            </div>
            <textarea ref={input} rows={2} value={draft} onChange={onChange} onKeyDown={onKeyDown} onBlur={() => setPicker(null)} onScroll={syncMirror}
              placeholder="Hỏi MSMS AI, ví dụ: tuần này phân hiệu nào có nhiều học sinh nghỉ nhất? Gõ @ để chọn học sinh, giáo viên."
              aria-label="Câu hỏi cho MSMS AI" />
          </div>
          <button type="submit" disabled={!draft.trim() || thinking}>Gửi</button>
        </div>      </form>
    </section>
  );
}
