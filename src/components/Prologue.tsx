import "@fontsource/cinzel/500.css";
import "@fontsource/cinzel/700.css";
import "@fontsource/marcellus/400.css";
import { useEffect, useRef, useState } from "react";
import type { LangCode } from "@/i18n";
import { STORY } from "@/game/journey";

/**
 * The opening. Four cards out of the dark, each over a moving picture of its
 * own, and then the name.
 *
 *   I    Corinth at night on its hill over the sea: the king who cheated Death.
 *   II   A chain across the dark, its middle link slowly breaking open.
 *   III  The Pleiades, seven sisters drawn in by a constellation line — and
 *        one of them, Merope, faint.
 *   IV   The hill: a tiny man pushes a stone up it, and it rolls back.
 *
 * The words of each card come in one at a time out of a blur, the quoted
 * words in gold; a thin line under each card fills while it is read. A tap
 * moves on; the pictures cross-fade, never cut.
 */

const TITLE = "'Cinzel', 'Cormorant Garamond', Georgia, serif";
const BODY = "'Marcellus', 'Cormorant Garamond', Georgia, serif";
const CREAM = "#f1e4c4";
const GOLD = "#d9b46a";

/** how long a card stays, by how much there is to read */
const holdFor = (text: string) => Math.min(9500, Math.max(5200, 2600 + text.length * 48));

/** split a card into words (or characters, for scripts written without spaces) */
function tokens(text: string): Array<{ t: string; q: boolean }> {
  const out: Array<{ t: string; q: boolean }> = [];
  const noSpaces = /[\u3040-\u9fff\uff00-\uffef]/.test(text);
  let quoted = false;
  const parts = noSpaces ? Array.from(text) : text.split(/(\s+)/);
  for (const p of parts) {
    if (!p) continue;
    if (/[“「«"]/.test(p)) quoted = true;
    out.push({ t: p, q: quoted });
    if (/[”」»"]/.test(p) && !/^[“「«"]$/.test(p)) quoted = false;
  }
  return out;
}

export function Prologue({ lang, onDone }: { lang: LangCode; onDone: () => void }) {
  const S = STORY[lang];
  const cards = S.prologue;
  const n = cards.length;
  const [i, setI] = useState(0);
  const [leaving, setLeaving] = useState(false);
  const [reduced] = useState(
    () =>
      typeof window !== "undefined" &&
      window.matchMedia?.("(prefers-reduced-motion: reduce)").matches,
  );
  const sceneRef = useRef(0);
  sceneRef.current = Math.min(i, n - 1);

  // a tap and the card's own timer can land together: only one may move on
  const leavingRef = useRef(false);
  const iRef = useRef(i);
  iRef.current = i;
  const next = () => {
    if (leavingRef.current) return;
    if (iRef.current >= n) {
      onDone();
      return;
    }
    leavingRef.current = true;
    setLeaving(true);
    window.setTimeout(
      () => {
        leavingRef.current = false;
        setLeaving(false);
        setI((v) => Math.min(n, v + 1));
      },
      reduced ? 0 : 520,
    );
  };

  useEffect(() => {
    if (i >= n) return;
    const id = window.setTimeout(next, holdFor(cards[i] ?? ""));
    return () => window.clearTimeout(id);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [i, n]);

  const isTitle = i >= n;
  return (
    <div
      className="absolute inset-0 z-[45] overflow-hidden bg-black"
      onClick={next}
      onKeyDown={(e) => {
        if (e.key === " " || e.key === "Enter") next();
      }}
      role="dialog"
      aria-modal="true"
      tabIndex={-1}
    >
      <Backdrop sceneRef={sceneRef} title={isTitle} reduced={reduced} />
      {/* letterbox */}
      <div className="prologue-bar pointer-events-none absolute inset-x-0 top-0 h-[9vh] bg-black" />
      <div className="prologue-bar pointer-events-none absolute inset-x-0 bottom-0 h-[9vh] bg-black" />

      {!isTitle ? (
        <div
          className="absolute inset-x-0 bottom-[14vh] flex flex-col items-center px-6"
          style={{
            opacity: leaving ? 0 : 1,
            filter: leaving ? "blur(6px)" : "none",
            transition: "opacity .5s, filter .5s",
          }}
        >
          <div className="mb-4 flex items-center gap-3" style={{ color: GOLD, fontFamily: TITLE }}>
            <span className="prologue-rule h-px w-10 bg-current opacity-60" />
            <span key={i} className="prologue-num text-[0.8rem] tracking-[0.5em]">
              {["I", "II", "III", "IV", "V", "VI"][i]}
            </span>
            <span className="prologue-rule h-px w-10 bg-current opacity-60" />
          </div>
          <p
            key={i}
            dir="auto"
            className="max-w-3xl text-center text-[1.35rem] leading-[1.65] sm:text-[1.8rem]"
            style={{ fontFamily: BODY, color: CREAM, textShadow: "0 2px 18px rgba(0,0,0,0.9)" }}
          >
            {tokens(cards[i] ?? "").map((w, k) =>
              /^\s+$/.test(w.t) ? (
                <span key={k}>{w.t}</span>
              ) : (
                <span
                  key={k}
                  className={reduced ? "" : "prologue-word"}
                  style={{
                    animationDelay: `${180 + k * (cards[i]!.length > 120 ? 38 : 60)}ms`,
                    color: w.q ? GOLD : undefined,
                    fontStyle: w.q ? "italic" : undefined,
                  }}
                >
                  {w.t}
                </span>
              ),
            )}
          </p>
          {/* the time left on this card */}
          <div className="mt-7 flex gap-2">
            {cards.map((_, k) => (
              <span key={k} className="relative h-[2px] w-8 overflow-hidden rounded bg-white/15">
                <span
                  key={k === i ? `on${i}` : k}
                  className="absolute inset-y-0 left-0 bg-[#d9b46a]"
                  style={
                    k < i
                      ? { width: "100%" }
                      : k === i && !reduced
                        ? {
                            width: "100%",
                            animation: `prologue-fill ${holdFor(cards[i] ?? "")}ms linear both`,
                          }
                        : { width: k === i ? "100%" : "0%" }
                  }
                />
              </span>
            ))}
          </div>
        </div>
      ) : (
        <div className="absolute inset-0 flex flex-col items-center justify-center gap-6 px-6">
          <h1
            className="prologue-title text-[2.6rem] sm:text-[4.4rem]"
            style={{ fontFamily: TITLE, fontWeight: 700, color: CREAM }}
            aria-label="SISYPHUS"
          >
            {Array.from("SISYPHUS").map((ch, k) => (
              <span
                key={k}
                className={reduced ? "" : "prologue-letter"}
                style={{ animationDelay: `${200 + k * 110}ms` }}
              >
                {ch}
              </span>
            ))}
          </h1>
          <span className={`h-px bg-[#d9b46a] ${reduced ? "w-48" : "prologue-line"}`} />
          <button
            onClick={(e) => {
              e.stopPropagation();
              onDone();
            }}
            className="prologue-begin mt-4 rounded-full border px-10 py-3 text-[0.8rem] tracking-[0.45em] uppercase"
            style={{ fontFamily: TITLE, color: CREAM, borderColor: "rgba(217,180,106,0.55)" }}
          >
            {S.begin}
          </button>
        </div>
      )}

      {!isTitle && (
        <button
          onClick={(e) => {
            e.stopPropagation();
            onDone();
          }}
          className="absolute right-5 bottom-[2.6vh] z-10 text-[0.7rem] tracking-[0.35em] uppercase opacity-60 hover:opacity-100"
          style={{ fontFamily: TITLE, color: CREAM }}
        >
          {S.skip} ›
        </button>
      )}
    </div>
  );
}

// ---------------------------------------------------------------------------
//  The moving pictures behind the cards
// ---------------------------------------------------------------------------

type Star = { x: number; y: number; r: number; p: number };

function rand(seed: number) {
  let s = seed >>> 0;
  return () => {
    s = (s * 1664525 + 1013904223) >>> 0;
    return s / 4294967296;
  };
}

function Backdrop({
  sceneRef,
  title,
  reduced,
}: {
  sceneRef: React.MutableRefObject<number>;
  title: boolean;
  reduced: boolean;
}) {
  const cvRef = useRef<HTMLCanvasElement | null>(null);
  const titleRef = useRef(title);
  titleRef.current = title;
  useEffect(() => {
    const cv = cvRef.current;
    if (!cv) return;
    const ctx = cv.getContext("2d");
    if (!ctx) return;
    const R = rand(7);
    const stars: Star[] = Array.from({ length: 220 }, () => ({
      x: R(),
      y: R() * 0.8,
      r: R() * 1.2 + 0.2,
      p: R() * 6,
    }));
    const embers = Array.from({ length: 46 }, () => ({
      x: R(),
      y: R(),
      v: 0.012 + R() * 0.03,
      s: R() * 1.6 + 0.4,
      p: R() * 6,
    }));
    let raf = 0;
    let shown = -1;
    let prev = -1;
    let since = 0;
    const t0 = performance.now();
    const frame = (now: number) => {
      const t = (now - t0) / 1000;
      const dpr = Math.min(2, window.devicePixelRatio || 1);
      const w = cv.clientWidth;
      const h = cv.clientHeight;
      if (cv.width !== Math.round(w * dpr) || cv.height !== Math.round(h * dpr)) {
        cv.width = Math.round(w * dpr);
        cv.height = Math.round(h * dpr);
      }
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
      const want = sceneRef.current;
      if (want !== shown) {
        prev = shown;
        shown = want;
        since = t;
      }
      const k = reduced ? 1 : Math.min(1, (t - since) / 1.6);
      // the sky, deep and cold
      const g = ctx.createLinearGradient(0, 0, 0, h);
      g.addColorStop(0, "#04050a");
      g.addColorStop(0.7, "#0c0f1a");
      g.addColorStop(1, "#160f12");
      ctx.fillStyle = g;
      ctx.fillRect(0, 0, w, h);
      for (const s of stars) {
        const a = 0.35 + 0.45 * Math.sin(t * 1.3 + s.p) ** 2;
        ctx.fillStyle = `rgba(230, 226, 255, ${a.toFixed(3)})`;
        ctx.fillRect(s.x * w, s.y * h, s.r, s.r);
      }
      // a slow push in, as in a film
      const zoom = reduced ? 1 : 1 + Math.min(1, (t - since) / 10) * 0.05;
      const drawScene = (id: number, alpha: number, st: number) => {
        if (id < 0 || alpha <= 0.002) return;
        ctx.save();
        ctx.globalAlpha = alpha;
        ctx.translate(w / 2, h / 2);
        ctx.scale(zoom, zoom);
        ctx.translate(-w / 2, -h / 2);
        SCENES[Math.min(id, SCENES.length - 1)]!(ctx, w, h, st, titleRef.current);
        ctx.restore();
      };
      drawScene(prev, 1 - k, t);
      drawScene(shown, k, t - since);
      // embers drifting up through everything
      for (const e of embers) {
        const y = (((e.y - t * e.v) % 1) + 1) % 1;
        const x = e.x + Math.sin(t * 0.6 + e.p) * 0.01;
        const a = 0.5 * Math.sin(y * Math.PI) * (0.6 + 0.4 * Math.sin(t * 3 + e.p));
        ctx.fillStyle = `rgba(255, 170, 90, ${a.toFixed(3)})`;
        ctx.beginPath();
        ctx.arc(x * w, y * h, e.s, 0, Math.PI * 2);
        ctx.fill();
      }
      // vignette, and the dark at the bottom the words sit on
      const v = ctx.createRadialGradient(
        w / 2,
        h * 0.45,
        h * 0.25,
        w / 2,
        h * 0.5,
        Math.max(w, h) * 0.75,
      );
      v.addColorStop(0, "rgba(0,0,0,0)");
      v.addColorStop(1, "rgba(0,0,0,0.85)");
      ctx.fillStyle = v;
      ctx.fillRect(0, 0, w, h);
      const b = ctx.createLinearGradient(0, h * 0.5, 0, h);
      b.addColorStop(0, "rgba(0,0,0,0)");
      b.addColorStop(1, "rgba(0,0,0,0.7)");
      ctx.fillStyle = b;
      ctx.fillRect(0, h * 0.5, w, h * 0.5);
      raf = requestAnimationFrame(frame);
    };
    raf = requestAnimationFrame(frame);
    return () => cancelAnimationFrame(raf);
  }, [sceneRef, reduced]);
  return <canvas ref={cvRef} className="absolute inset-0 h-full w-full" aria-hidden="true" />;
}

/** a capsule (roundRect is missing before Safari 16) */
function pill(c: CanvasRenderingContext2D, x: number, y: number, w: number, h: number) {
  const r = Math.min(h, w) / 2;
  c.moveTo(x + r, y);
  c.lineTo(x + w - r, y);
  c.arc(x + w - r, y + r, r, -Math.PI / 2, Math.PI / 2);
  c.lineTo(x + r, y + h);
  c.arc(x + r, y + r, r, Math.PI / 2, (Math.PI * 3) / 2);
  c.closePath();
}

type SceneFn = (
  c: CanvasRenderingContext2D,
  w: number,
  h: number,
  t: number,
  title: boolean,
) => void;

/** I — Corinth on its hill over the sea, at night, a few torches still burning */
const corinth: SceneFn = (c, w, h, t) => {
  const hz = h * 0.62;
  // the moon and its road on the water
  const mx = w * 0.74;
  const my = h * 0.2;
  const mg = c.createRadialGradient(mx, my, 0, mx, my, h * 0.22);
  mg.addColorStop(0, "rgba(230, 226, 210, 0.35)");
  mg.addColorStop(1, "rgba(230, 226, 210, 0)");
  c.fillStyle = mg;
  c.fillRect(mx - h * 0.22, my - h * 0.22, h * 0.44, h * 0.44);
  c.fillStyle = "#e8e2cf";
  c.beginPath();
  c.arc(mx, my, h * 0.035, 0, Math.PI * 2);
  c.fill();
  // the sea
  const sea = c.createLinearGradient(0, hz, 0, h);
  sea.addColorStop(0, "#111827");
  sea.addColorStop(1, "#05070c");
  c.fillStyle = sea;
  c.fillRect(0, hz, w, h - hz);
  for (let i = 0; i < 26; i++) {
    const yy = hz + 4 + i * i * 0.5;
    const wob = Math.sin(t * 1.4 + i * 1.7) * 6;
    const len = 10 + i * 3;
    c.fillStyle = `rgba(232, 226, 205, ${(0.5 - i * 0.017).toFixed(3)})`;
    c.fillRect(mx - len / 2 + wob, yy, len, 1.2);
  }
  // the acropolis: a rock, walls, a temple, roofs
  c.fillStyle = "#07080d";
  c.beginPath();
  c.moveTo(0, hz + 10);
  c.lineTo(w * 0.08, hz - h * 0.04);
  c.quadraticCurveTo(w * 0.2, hz - h * 0.2, w * 0.3, hz - h * 0.22);
  c.lineTo(w * 0.42, hz - h * 0.2);
  c.quadraticCurveTo(w * 0.52, hz - h * 0.08, w * 0.62, hz + 6);
  c.lineTo(w * 0.62, hz + 12);
  c.lineTo(0, hz + 12);
  c.fill();
  const top = hz - h * 0.22;
  // the temple: a pediment on columns
  const tx = w * 0.33;
  const tw = w * 0.1;
  c.beginPath();
  c.moveTo(tx - tw / 2, top - h * 0.055);
  c.lineTo(tx, top - h * 0.085);
  c.lineTo(tx + tw / 2, top - h * 0.055);
  c.fill();
  c.fillRect(tx - tw / 2, top - h * 0.056, tw, h * 0.008);
  for (let i = 0; i < 7; i++)
    c.fillRect(tx - tw / 2 + 2 + (i * (tw - 6)) / 6, top - h * 0.05, 3, h * 0.05);
  // houses down the slope, and torches flickering in a few of them
  const R = rand(11);
  for (let i = 0; i < 24; i++) {
    const hx = w * (0.06 + R() * 0.5);
    const slope =
      hx < w * 0.3
        ? (hx / (w * 0.3)) * 0.2
        : Math.max(0, 0.2 - ((hx - w * 0.36) / (w * 0.26)) * 0.2);
    const hy = hz - h * slope + h * 0.02 + R() * h * 0.03;
    const bw = 10 + R() * 18;
    const bh = 8 + R() * 12;
    c.fillStyle = "#07080d";
    c.fillRect(hx, hy - bh, bw, bh + 6);
    if (R() < 0.45) {
      const f = 0.55 + 0.45 * Math.sin(t * (5 + R() * 4) + i);
      c.fillStyle = `rgba(255, 170, 80, ${(0.55 * f).toFixed(3)})`;
      c.fillRect(hx + bw * 0.4, hy - bh * 0.6, 3, 4);
      const gl = c.createRadialGradient(
        hx + bw * 0.4,
        hy - bh * 0.5,
        0,
        hx + bw * 0.4,
        hy - bh * 0.5,
        16,
      );
      gl.addColorStop(0, `rgba(255, 150, 60, ${(0.2 * f).toFixed(3)})`);
      gl.addColorStop(1, "rgba(255, 150, 60, 0)");
      c.fillStyle = gl;
      c.fillRect(hx + bw * 0.4 - 16, hy - bh * 0.5 - 16, 32, 32);
    }
  }
};

/** II — a chain across the dark; its middle link is breaking */
const chain: SceneFn = (c, w, h, t) => {
  const y = h * 0.4 + Math.sin(t * 0.6) * 4;
  const open = Math.min(1, t / 6);
  const gap = open * 26;
  const linkW = Math.max(34, w * 0.045);
  const n = Math.ceil(w / (linkW * 0.78)) + 2;
  const mid = Math.floor(n / 2);
  for (let i = 0; i < n; i++) {
    const side = i < mid ? -1 : i > mid ? 1 : 0;
    const x = (i - mid) * linkW * 0.78 + w / 2 + side * gap;
    const sag = Math.pow((x - w / 2) / (w / 2), 2) * -18 + 18;
    const ly = y + sag + side * open * 6 * Math.abs(i - mid) ** 0.5;
    c.save();
    c.translate(x, ly);
    c.rotate(side * open * 0.08);
    const g = c.createLinearGradient(0, -linkW * 0.3, 0, linkW * 0.3);
    g.addColorStop(0, "#6b6762");
    g.addColorStop(0.5, "#2a2826");
    g.addColorStop(1, "#141312");
    if (i % 2 === 0) {
      c.strokeStyle = g;
      c.lineWidth = linkW * 0.16;
      c.beginPath();
      c.ellipse(0, 0, linkW * 0.5, linkW * 0.26, 0, 0, Math.PI * 2);
      c.stroke();
      c.strokeStyle = "rgba(255, 230, 200, 0.18)";
      c.lineWidth = 1.2;
      c.beginPath();
      c.ellipse(0, -1, linkW * 0.5, linkW * 0.26, 0, Math.PI * 1.15, Math.PI * 1.85);
      c.stroke();
    } else {
      c.fillStyle = g;
      c.beginPath();
      pill(c, -linkW * 0.5, -linkW * 0.07, linkW, linkW * 0.14);
      c.fill();
    }
    c.restore();
  }
  // where it breaks: heat in the iron, and sparks
  const hx = w / 2;
  const heat = c.createRadialGradient(hx, y + 18, 0, hx, y + 18, 70);
  heat.addColorStop(0, `rgba(255, 120, 40, ${(0.35 * (0.6 + 0.4 * Math.sin(t * 9))).toFixed(3)})`);
  heat.addColorStop(1, "rgba(255, 120, 40, 0)");
  c.fillStyle = heat;
  c.fillRect(hx - 70, y + 18 - 70, 140, 140);
  for (let i = 0; i < 24; i++) {
    const p = (((t * 0.9 + i * 0.137) % 1) + 1) % 1;
    const a = i * 2.39;
    const r = p * (60 + (i % 5) * 18);
    const sx = hx + Math.cos(a) * r;
    const sy = y + 18 + Math.sin(a) * r * 0.6 + p * p * 60;
    c.fillStyle = `rgba(255, ${190 - p * 100}, 90, ${(1 - p).toFixed(3)})`;
    c.fillRect(sx, sy, 2, 2);
  }
};

/** III — the seven sisters; one of them faint */
const pleiades: SceneFn = (c, w, h, t) => {
  const cx = w / 2;
  const cy = h * 0.36;
  const S = Math.min(w, h) * 0.32;
  const rot = t * 0.02;
  const pts: Array<[number, number, number]> = [
    [-0.55, -0.1, 1],
    [-0.2, -0.42, 0.9],
    [0.18, -0.3, 1],
    [0.5, -0.05, 0.85],
    [0.28, 0.3, 0.95],
    [-0.12, 0.18, 0.9],
    [-0.42, 0.4, 0.34], // Merope, the faint one
  ];
  const P = pts.map(([x, y, b]) => {
    const xr = x * Math.cos(rot) - y * Math.sin(rot);
    const yr = x * Math.sin(rot) + y * Math.cos(rot);
    return [cx + xr * S, cy + yr * S, b] as const;
  });
  // the line of the constellation, drawn in over the first seconds
  const draw = Math.min(1, t / 3.5);
  c.strokeStyle = "rgba(200, 210, 255, 0.28)";
  c.lineWidth = 1;
  c.beginPath();
  const order = [0, 1, 2, 3, 4, 5, 0, 5, 6];
  const segs = order.length - 1;
  for (let i = 0; i < segs; i++) {
    const k = Math.max(0, Math.min(1, draw * segs - i));
    if (k <= 0) break;
    const a = P[order[i]!]!;
    const b = P[order[i + 1]!]!;
    c.moveTo(a[0], a[1]);
    c.lineTo(a[0] + (b[0] - a[0]) * k, a[1] + (b[1] - a[1]) * k);
  }
  c.stroke();
  P.forEach(([x, y, b], i) => {
    const tw = i === 6 ? 0.5 + 0.5 * Math.sin(t * 2.2) : 0.85 + 0.15 * Math.sin(t * 1.5 + i);
    const r = 26 * b * tw;
    const g = c.createRadialGradient(x, y, 0, x, y, r);
    g.addColorStop(0, `rgba(235, 240, 255, ${(0.95 * b * tw).toFixed(3)})`);
    g.addColorStop(0.2, `rgba(170, 195, 255, ${(0.5 * b * tw).toFixed(3)})`);
    g.addColorStop(1, "rgba(120, 150, 255, 0)");
    c.fillStyle = g;
    c.fillRect(x - r, y - r, r * 2, r * 2);
    // the cross of light on the bright ones
    if (b > 0.5) {
      c.strokeStyle = `rgba(220, 230, 255, ${(0.35 * tw).toFixed(3)})`;
      c.beginPath();
      c.moveTo(x - r * 1.2, y);
      c.lineTo(x + r * 1.2, y);
      c.moveTo(x, y - r * 1.2);
      c.lineTo(x, y + r * 1.2);
      c.stroke();
    }
  });
  // a soft nebula behind them
  const nb = c.createRadialGradient(cx, cy, 0, cx, cy, S * 1.3);
  nb.addColorStop(0, "rgba(90, 110, 200, 0.12)");
  nb.addColorStop(1, "rgba(90, 110, 200, 0)");
  c.fillStyle = nb;
  c.fillRect(cx - S * 1.3, cy - S * 1.3, S * 2.6, S * 2.6);
};

/** IV — the hill, the man, the stone; up, and back down */
const hill: SceneFn = (c, w, h, t) => {
  // a red dawn behind the mountain
  const dawn = c.createRadialGradient(w * 0.7, h * 0.62, 0, w * 0.7, h * 0.62, w * 0.6);
  dawn.addColorStop(0, "rgba(200, 90, 50, 0.45)");
  dawn.addColorStop(0.5, "rgba(120, 40, 50, 0.18)");
  dawn.addColorStop(1, "rgba(60, 20, 40, 0)");
  c.fillStyle = dawn;
  c.fillRect(0, 0, w, h);
  // the slope
  const x0 = -w * 0.05;
  const y0 = h * 0.92;
  const x1 = w * 0.78;
  const y1 = h * 0.3;
  c.fillStyle = "#060508";
  c.beginPath();
  c.moveTo(x0, h);
  c.lineTo(x0, y0);
  c.lineTo(x1, y1);
  c.quadraticCurveTo(x1 + w * 0.06, y1 - h * 0.02, x1 + w * 0.12, y1 + h * 0.04);
  c.lineTo(w * 1.05, h * 0.6);
  c.lineTo(w * 1.05, h);
  c.fill();
  // the climb: eleven seconds up, two and a half down, again
  const cyc = 13.5;
  const ph = (t % cyc) / cyc;
  const up = 11 / cyc;
  let u: number;
  let roll = false;
  if (ph < up) u = 0.12 + (ph / up) * 0.8;
  else {
    roll = true;
    const k = (ph - up) / (1 - up);
    u = 0.92 - k * k * 0.8;
  }
  const along = (k: number) => [x0 + (x1 - x0) * k, y0 + (y1 - y0) * k] as const;
  const ang = Math.atan2(y1 - y0, x1 - x0);
  const R = Math.max(10, h * 0.028);
  const [bx, by] = along(u);
  const nx = Math.sin(ang);
  const ny = -Math.cos(ang);
  // the stone
  c.save();
  c.translate(bx + nx * R, by + ny * R);
  c.rotate((u * Math.hypot(x1 - x0, y1 - y0)) / R);
  c.fillStyle = "#0b0a0c";
  c.beginPath();
  for (let i = 0; i < 9; i++) {
    const a = (i / 9) * Math.PI * 2;
    const rr = R * (0.9 + ((i * 37) % 7) / 50);
    if (i) c.lineTo(Math.cos(a) * rr, Math.sin(a) * rr);
    else c.moveTo(rr, 0);
  }
  c.closePath();
  c.fill();
  c.restore();
  // the man: behind the stone, leaning into it — or standing near the top, watching it go
  const off = (R * 1.5) / Math.hypot(x1 - x0, y1 - y0);
  const man = along(roll ? 0.92 - off : u - off);
  c.save();
  c.translate(man[0], man[1]);
  c.rotate(ang);
  c.fillStyle = "#0b0a0c";
  c.strokeStyle = "#0b0a0c";
  c.lineCap = "round";
  const lean = roll ? -0.35 : 0.75; // forward into the stone, or upright
  const step = roll ? 0 : Math.sin(t * 4) * 0.35;
  const H = R * 2.6;
  c.rotate(-lean * 0.6);
  c.lineWidth = R * 0.32;
  c.beginPath();
  c.moveTo(0, -H * 0.45); // the legs
  c.lineTo(-R * 0.5 + step * R, 0);
  c.moveTo(0, -H * 0.45);
  c.lineTo(R * 0.4 - step * R, 0);
  c.stroke();
  c.lineWidth = R * 0.42;
  c.beginPath();
  c.moveTo(0, -H * 0.45);
  c.lineTo(R * 0.3, -H * 0.9); // the body
  c.stroke();
  c.beginPath();
  c.arc(R * 0.42, -H * 1.05, R * 0.24, 0, Math.PI * 2);
  c.fill();
  c.lineWidth = R * 0.2;
  c.beginPath();
  c.moveTo(R * 0.3, -H * 0.85); // the arms, to the stone
  c.lineTo(roll ? R * 0.4 : R * 1.05, roll ? -H * 0.5 : -H * 0.78);
  c.stroke();
  c.restore();
  // dust where it rolls
  if (roll) {
    for (let i = 0; i < 10; i++) {
      const k = i / 10;
      const [dx, dy] = along(Math.min(0.92, u + k * 0.06));
      c.fillStyle = `rgba(170, 120, 90, ${(0.25 * (1 - k)).toFixed(3)})`;
      c.beginPath();
      c.arc(dx + nx * 2, dy + ny * 2 - k * 6, 3 + k * 8, 0, Math.PI * 2);
      c.fill();
    }
  }
};

const SCENES: SceneFn[] = [corinth, chain, pleiades, hill];

/**
 * The hill in miniature, for the menu: a man pushes his stone up a slope
 * under a red dawn, and it rolls back, and he goes down after it. The same
 * picture the opening ends on, so the menu picks up where the story left off.
 */
export function HillEmblem({ className }: { className?: string }) {
  const ref = useRef<HTMLCanvasElement | null>(null);
  useEffect(() => {
    const cv = ref.current;
    if (!cv) return;
    const ctx = cv.getContext("2d");
    if (!ctx) return;
    const still =
      typeof window !== "undefined" &&
      window.matchMedia?.("(prefers-reduced-motion: reduce)").matches;
    let raf = 0;
    let idle = 0;
    const t0 = performance.now();
    const frame = (now: number) => {
      const dpr = Math.min(2, window.devicePixelRatio || 1);
      const w = cv.clientWidth;
      const h = cv.clientHeight;
      if (w === 0 || h === 0) {
        // hidden (a short screen): look again in a while, do not draw
        if (!still) idle = window.setTimeout(() => (raf = requestAnimationFrame(frame)), 600);
        return;
      }
      if (cv.width !== Math.round(w * dpr) || cv.height !== Math.round(h * dpr)) {
        cv.width = Math.round(w * dpr);
        cv.height = Math.round(h * dpr);
      }
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
      ctx.clearRect(0, 0, w, h);
      // fade the picture out at its edges so it floats on the menu
      // a low sun behind the hill, so the man and his stone stand out against it
      const sun = ctx.createRadialGradient(w * 0.6, h * 0.42, 0, w * 0.6, h * 0.42, w * 0.42);
      sun.addColorStop(0, "rgba(255, 196, 120, 0.85)");
      sun.addColorStop(0.25, "rgba(230, 120, 70, 0.55)");
      sun.addColorStop(1, "rgba(120, 40, 50, 0)");
      ctx.fillStyle = sun;
      ctx.fillRect(0, 0, w, h);
      hill(ctx, w, h, still ? 4 : (now - t0) / 1000, false);
      // the crest of the slope, catching the light
      ctx.strokeStyle = "rgba(240, 200, 130, 0.7)";
      ctx.lineWidth = 1;
      ctx.beginPath();
      ctx.moveTo(-w * 0.05, h * 0.92);
      ctx.lineTo(w * 0.78, h * 0.3);
      ctx.stroke();
      ctx.globalCompositeOperation = "destination-in";
      const m = ctx.createRadialGradient(w / 2, h * 0.6, h * 0.2, w / 2, h * 0.6, w * 0.55);
      m.addColorStop(0, "rgba(0,0,0,1)");
      m.addColorStop(1, "rgba(0,0,0,0)");
      ctx.fillStyle = m;
      ctx.fillRect(0, 0, w, h);
      ctx.globalCompositeOperation = "source-over";
      if (!still) raf = requestAnimationFrame(frame);
    };
    raf = requestAnimationFrame(frame);
    // without motion it is drawn once — and again whenever its size changes
    const ro =
      still && typeof ResizeObserver !== "undefined"
        ? new ResizeObserver(() => (raf = requestAnimationFrame(frame)))
        : null;
    ro?.observe(cv);
    return () => {
      cancelAnimationFrame(raf);
      window.clearTimeout(idle);
      ro?.disconnect();
    };
  }, []);
  return <canvas ref={ref} className={className} aria-hidden="true" />;
}
