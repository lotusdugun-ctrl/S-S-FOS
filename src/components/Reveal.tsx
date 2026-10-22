/**
 * Words that come in one at a time out of a blur. Quoted words are set in
 * gold. `instant` shows the whole line at once (a tap while it is still
 * coming in). Scripts written without spaces come in a character at a time.
 */
export function Reveal({
  text,
  step = 45,
  delay = 0,
  instant = false,
  gold = "#d9b46a",
  className,
  style,
}: {
  text: string;
  step?: number;
  delay?: number;
  instant?: boolean;
  gold?: string;
  className?: string;
  style?: React.CSSProperties;
}) {
  const noSpaces = /[\u3040-\u9fff\uff00-\uffef]/.test(text);
  const parts = noSpaces ? Array.from(text) : text.split(/(\s+)/);
  let quoted = false;
  let n = 0;
  return (
    <span className={className} style={style} dir="auto">
      {parts.map((p, k) => {
        if (!p) return null;
        if (/^\s+$/.test(p)) return <span key={k}>{p}</span>;
        if (/[“「«"]/.test(p)) quoted = true;
        const q = quoted;
        if (/[”」»"]/.test(p) && !/^[“「«"]$/.test(p)) quoted = false;
        const d = delay + n++ * step;
        return (
          <span
            key={k}
            className={instant ? "reveal-done" : "prologue-word"}
            style={{
              animationDelay: instant ? undefined : `${d}ms`,
              color: q ? gold : undefined,
            }}
          >
            {p}
          </span>
        );
      })}
    </span>
  );
}
