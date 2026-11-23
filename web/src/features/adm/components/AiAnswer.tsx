import type { ReactNode } from 'react';
import type { AiChart } from '../types';
import { HBars, LineChart } from './BoardKit';
import type { BarItem } from './BoardKit';

/** In đậm **...** trong một dòng; React tự thoát ký tự nên không chèn được HTML. */
function inline(text: string): ReactNode[] {
  return text.split(/(\*\*[^*]+\*\*)/g).filter(Boolean).map((part, i) =>
    part.startsWith('**') && part.endsWith('**') ? <strong key={i}>{part.slice(2, -2)}</strong> : part);
}

const cells = (line: string) => line.trim().replace(/^\||\|$/g, '').split('|').map((c) => c.trim());

/** Dòng phạm vi đầu câu trả lời: "Phạm vi: Toàn trường · 4 tuần qua (27/10–23/11/2026)". */
const SCOPE = /^\s*Phạm vi:\s*/i;
/** Dòng số chính: "Tỷ lệ nộp: **18,2%** dưới ngưỡng 50%". */
const FIGURE = /^([^:*]{1,40}):\s*\*\*([^*]+)\*\*\s*(.*)$/;
/** Dòng cuối "Gợi ý: …" hoặc "Lưu ý: …". */
const NOTE = /^\s*(Gợi ý|Lưu ý):\s*/i;

/**
 * Câu trả lời của MSMS AI theo bố cục lời nhắc hệ thống quy định: dòng phạm vi thành nhãn nhỏ, câu kết luận đầu nổi hơn,
 * các dòng "Tên: **số** ghi chú" thành thẻ số, danh sách đánh số thành bảng xếp hạng, dòng Gợi ý/Lưu ý thành khung nhắc.
 * AI viết lệch bố cục thì vẫn hiện như đoạn văn, gạch đầu dòng, bảng nhỏ; không dùng thư viện markdown.
 */
export function AiText({ text }: { text: string }) {
  const lines = text.replace(/\r/g, '').split('\n');
  const blocks: ReactNode[] = [];
  let i = 0;
  while (i < lines.length && !lines[i].trim()) i++;
  if (i < lines.length && SCOPE.test(lines[i])) {
    const parts = lines[i].replace(SCOPE, '').replace(/\*\*/g, '').split('·').map((p) => p.trim()).filter(Boolean);
    blocks.push(<div key="scope" className="aia-scope">{parts.map((p, k) => <span key={k}>{p}</span>)}</div>);
    i++;
  }
  let lead = true;
  while (i < lines.length) {
    const line = lines[i];
    if (!line.trim()) { i++; continue; }
    if (line.trim().startsWith('|')) {
      const rows: string[][] = [];
      while (i < lines.length && lines[i].trim().startsWith('|')) {
        if (!/^\|?\s*:?-{2,}/.test(lines[i].trim())) rows.push(cells(lines[i]));
        i++;
      }
      const [head, ...body] = rows;
      blocks.push(
        <div key={blocks.length} className="aia-table">
          <table>
            <thead><tr>{head.map((c, k) => <th key={k}>{inline(c)}</th>)}</tr></thead>
            <tbody>{body.map((r, k) => <tr key={k}>{r.map((c, j) => <td key={j}>{inline(c)}</td>)}</tr>)}</tbody>
          </table>
        </div>,
      );
      lead = false;
      continue;
    }
    const bullet = /^\s*[-*•]\s+/;
    const numbered = /^\s*\d+[.)]\s+/;
    if (bullet.test(line) || numbered.test(line)) {
      const ordered = numbered.test(line);
      const pattern = ordered ? numbered : bullet;
      const items: string[] = [];
      while (i < lines.length && pattern.test(lines[i])) { items.push(lines[i].replace(pattern, '')); i++; }
      const figures = ordered ? [] : items.map((it) => FIGURE.exec(it.trim()));
      if (!ordered && items.length <= 6 && figures.every(Boolean)) {
        blocks.push(
          <div key={blocks.length} className="aia-figs">
            {figures.map((m, k) => (
              <div key={k} className="aia-fig">
                <span className="aia-fig__label">{m![1].trim()}</span>
                <strong className="aia-fig__value">{m![2].trim()}</strong>
                {m![3].trim() && <span className="aia-fig__note">{inline(m![3].trim())}</span>}
              </div>
            ))}
          </div>,
        );
      } else if (ordered) {
        blocks.push(<ol key={blocks.length} className="aia-rank">{items.map((it, k) => <li key={k}><span>{inline(it)}</span></li>)}</ol>);
      } else {
        blocks.push(<ul key={blocks.length}>{items.map((it, k) => <li key={k}>{inline(it)}</li>)}</ul>);
      }
      lead = false;
      continue;
    }
    const para: string[] = [];
    while (i < lines.length && lines[i].trim() && !lines[i].trim().startsWith('|') && !bullet.test(lines[i]) && !numbered.test(lines[i])) {
      para.push(lines[i].replace(/^#+\s*/, ''));
      i++;
    }
    const note = NOTE.exec(para[0]);
    if (note) {
      para[0] = para[0].replace(NOTE, '');
      blocks.push(
        <p key={blocks.length} className="aia-note">
          <b>{note[1]}</b>
          {para.flatMap((p, k) => (k ? [<br key={`b${k}`} />, ...inline(p)] : inline(p)))}
        </p>,
      );
    } else {
      blocks.push(<p key={blocks.length} className={lead ? 'aia-lead' : undefined}>{para.flatMap((p, k) => (k ? [<br key={`b${k}`} />, ...inline(p)] : inline(p)))}</p>);
    }
    lead = false;
  }
  return <div className="aia-text">{blocks}</div>;
}

/** Câu người dùng gửi: mỗi "@Tên" đã chọn hiện thành nhãn xanh dương; tên dài dò trước để không cắt nhầm tên ngắn. */
export function MeText({ text, names }: { text: string; names?: string[] }) {
  const tags = [...new Set(names ?? [])].sort((a, b) => b.length - a.length).map((n) => `@${n}`);
  if (tags.length === 0) return <>{text}</>;
  const out: ReactNode[] = [];
  let rest = text;
  while (rest) {
    const hit = tags.map((t) => ({ t, at: rest.indexOf(t) })).filter((x) => x.at >= 0)
      .sort((a, b) => a.at - b.at || b.t.length - a.t.length)[0];
    if (!hit) { out.push(rest); break; }
    if (hit.at > 0) out.push(rest.slice(0, hit.at));
    out.push(<span key={out.length} className="aia-at">{hit.t}</span>);
    rest = rest.slice(hit.at + hit.t.length);
  }
  return <>{out}</>;
}

const fmt = (v: number) => v.toLocaleString('vi-VN', { maximumFractionDigits: 2 });

/**
 * Biểu đồ trong câu trả lời, số do Backend dựng từ kết quả truy vấn. Xu hướng tỷ lệ theo tuần là đường một màu;
 * còn lại là thanh ngang một màu, vượt ngưỡng xấu tô san hô, cùng ngôn ngữ với màn Tình hình.
 */
export function AiChartView({ chart }: { chart: AiChart }) {
  const th = chart.threshold ?? undefined;
  if (chart.kind === 'line' && chart.unit === '%') {
    return (
      <figure className="aia-chart">
        <figcaption>{chart.title}</figcaption>
        <LineChart labels={chart.rows.map((r) => r.label.replace(/^Tuần /, ''))} threshold={th} height={200}
          series={[{ key: 'v', name: chart.title, values: chart.rows.map((r) => r.value), slot: 1 }]} />
      </figure>
    );
  }
  const items: BarItem[] = chart.rows.filter((r) => r.value !== null).map((r, k) => {
    const v = r.value as number;
    const bad = th !== undefined && (chart.higherIsBetter ? v < th : v > th);
    return { key: `${k}-${r.label}`, label: r.label, value: v, display: `${fmt(v)}${chart.unit}`, tone: bad ? 'low' : 'good' };
  });
  return (
    <figure className="aia-chart">
      <figcaption>{chart.title}</figcaption>
      <HBars items={items} max={chart.max ?? undefined} mark={th !== undefined ? { value: th, label: `Ngưỡng ${th}${chart.unit}` } : undefined} />
    </figure>
  );
}
