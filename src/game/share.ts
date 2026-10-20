/**
 * A saying as a picture to share: a page of parchment, 1080 × 1350 (the shape
 * Instagram and WhatsApp show whole), the words set large in the book's face,
 * the author beneath, the game's name at the foot. On a phone it goes to the
 * system share sheet; on a desktop browser it downloads.
 */
import { isNative } from "./native";

const W = 1080;
const H = 1350;
const SERIF = "'Cormorant Garamond', 'EB Garamond', Georgia, serif";

function wrap(c: CanvasRenderingContext2D, text: string, max: number): string[] {
  // scripts without spaces between words (Chinese) wrap by character
  const cjk = /[\u3000-\u9fff]/.test(text);
  const units = cjk ? Array.from(text) : text.split(/\s+/);
  const lines: string[] = [];
  let line = "";
  for (const u of units) {
    const next = line ? (cjk ? line + u : `${line} ${u}`) : u;
    // never start a line on a comma or a full stop
    const punct = /^[，。；：！？、,.;:!?」』）)]$/.test(u);
    if (c.measureText(next).width > max && line && !punct) {
      lines.push(line);
      line = u;
    } else line = next;
  }
  if (line) lines.push(line);
  return lines;
}

/** draw the card; resolves to a PNG blob */
export async function renderShareCard(opts: {
  text: string;
  author: string;
  chapter: string;
  rtl?: boolean;
}): Promise<Blob> {
  try {
    await Promise.all([
      document.fonts.load(`italic 500 64px ${SERIF}`),
      document.fonts.load(`600 40px ${SERIF}`),
    ]);
  } catch {
    /* the fallback serif will do */
  }
  const cv = document.createElement("canvas");
  cv.width = W;
  cv.height = H;
  const c = cv.getContext("2d")!;

  // night-blue around the page, the game's own first colour
  const bg = c.createLinearGradient(0, 0, 0, H);
  bg.addColorStop(0, "#1d2340");
  bg.addColorStop(1, "#120e14");
  c.fillStyle = bg;
  c.fillRect(0, 0, W, H);

  // the page
  const px = 90;
  const py = 110;
  const pw = W - px * 2;
  const ph = H - py * 2;
  c.save();
  c.shadowColor = "rgba(0,0,0,0.6)";
  c.shadowBlur = 50;
  c.shadowOffsetY = 18;
  const paper = c.createLinearGradient(px, py, px + pw, py + ph);
  paper.addColorStop(0, "#f3e9d1");
  paper.addColorStop(0.55, "#e9dbb9");
  paper.addColorStop(1, "#dcc69a");
  c.fillStyle = paper;
  c.fillRect(px, py, pw, ph);
  c.restore();
  // foxing toward the edges
  const fox = c.createRadialGradient(W / 2, H / 2, pw * 0.35, W / 2, H / 2, pw * 0.85);
  fox.addColorStop(0, "rgba(120,78,30,0)");
  fox.addColorStop(1, "rgba(120,78,30,0.28)");
  c.fillStyle = fox;
  c.fillRect(px, py, pw, ph);
  // a ruled double frame
  c.strokeStyle = "rgba(107,74,34,0.45)";
  c.lineWidth = 2;
  c.strokeRect(px + 28, py + 28, pw - 56, ph - 56);
  c.lineWidth = 1;
  c.strokeRect(px + 38, py + 38, pw - 76, ph - 76);

  c.textAlign = "center";
  c.fillStyle = "#6b4a22";
  c.font = `600 30px ${SERIF}`;
  // letter-spaced capitals for Latin scripts; others are set as they are
  const latin = /^[\p{Script=Latin}\s\d'’-]*$/u.test(opts.chapter);
  c.fillText(
    latin ? opts.chapter.toUpperCase().split("").join("\u2009") : opts.chapter,
    W / 2,
    py + 120,
  );
  // a lozenge on a rule
  c.strokeStyle = "rgba(107,74,34,0.6)";
  c.beginPath();
  c.moveTo(W / 2 - 110, py + 152);
  c.lineTo(W / 2 - 18, py + 152);
  c.moveTo(W / 2 + 18, py + 152);
  c.lineTo(W / 2 + 110, py + 152);
  c.stroke();
  c.fillStyle = "rgba(107,74,34,0.75)";
  c.beginPath();
  c.moveTo(W / 2, py + 144);
  c.lineTo(W / 2 + 8, py + 152);
  c.lineTo(W / 2, py + 160);
  c.lineTo(W / 2 - 8, py + 152);
  c.closePath();
  c.fill();

  // the words: as large as fits
  c.fillStyle = "#2a1c0f";
  if (opts.rtl) c.direction = "rtl";
  let size = 74;
  let lines: string[] = [];
  for (; size >= 40; size -= 4) {
    c.font = `italic 500 ${size}px ${SERIF}`;
    lines = wrap(c, opts.text, pw - 200);
    if (lines.length * size * 1.28 < ph - 480) break;
  }
  const lh = size * 1.28;
  // the words and the name under them, centred together on the page
  const group = lines.length * lh + 70;
  const top = H / 2 - group / 2 + size * 0.8;
  lines.forEach((ln, i) => c.fillText(ln, W / 2, top + i * lh));

  c.direction = "ltr";
  c.fillStyle = "#5a3d1c";
  c.font = `600 34px ${SERIF}`;
  c.fillText(`— ${opts.author}`, W / 2, top + lines.length * lh + 40);

  c.fillStyle = "rgba(107,74,34,0.75)";
  c.font = `600 26px ${SERIF}`;
  c.fillText("S I S Y P H U S", W / 2, py + ph - 70);

  return await new Promise<Blob>((res, rej) =>
    cv.toBlob((b) => (b ? res(b) : rej(new Error("no image"))), "image/png"),
  );
}

function blobToBase64(b: Blob): Promise<string> {
  return new Promise((res, rej) => {
    const r = new FileReader();
    r.onload = () => res(String(r.result).split(",")[1] ?? "");
    r.onerror = () => rej(r.error);
    r.readAsDataURL(b);
  });
}

/** hand the card to the system share sheet, or download it where there is none */
export async function shareCard(blob: Blob, text: string): Promise<void> {
  const name = "sisyphus-wisdom.png";
  if (isNative()) {
    const [{ Filesystem, Directory }, { Share }] = await Promise.all([
      import("@capacitor/filesystem"),
      import("@capacitor/share"),
    ]);
    const data = await blobToBase64(blob);
    const f = await Filesystem.writeFile({ path: name, data, directory: Directory.Cache });
    await Share.share({ files: [f.uri], text });
    return;
  }
  const file = new File([blob], name, { type: "image/png" });
  const nav = navigator as Navigator & { canShare?: (d: ShareData) => boolean };
  if (nav.share && nav.canShare?.({ files: [file] })) {
    try {
      await nav.share({ files: [file], text });
      return;
    } catch (e) {
      if ((e as Error).name === "AbortError") return;
    }
  }
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = name;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 2000);
}

/** a records card in the same parchment: the title, then label · value rows */
export async function renderRecordsCard(opts: {
  title: string;
  rows: Array<[string, string]>;
}): Promise<Blob> {
  try {
    await Promise.all([
      document.fonts.load(`italic 500 40px ${SERIF}`),
      document.fonts.load(`600 40px ${SERIF}`),
    ]);
  } catch {
    /* fallback serif */
  }
  const cv = document.createElement("canvas");
  cv.width = W;
  cv.height = H;
  const c = cv.getContext("2d")!;
  const bg = c.createLinearGradient(0, 0, 0, H);
  bg.addColorStop(0, "#1d2340");
  bg.addColorStop(1, "#120e14");
  c.fillStyle = bg;
  c.fillRect(0, 0, W, H);
  const px = 90;
  const py = 110;
  const pw = W - px * 2;
  const ph = H - py * 2;
  const paper = c.createLinearGradient(px, py, px + pw, py + ph);
  paper.addColorStop(0, "#f3e9d1");
  paper.addColorStop(0.55, "#e9dbb9");
  paper.addColorStop(1, "#dcc69a");
  c.save();
  c.shadowColor = "rgba(0,0,0,0.6)";
  c.shadowBlur = 50;
  c.shadowOffsetY = 18;
  c.fillStyle = paper;
  c.fillRect(px, py, pw, ph);
  c.restore();
  c.strokeStyle = "rgba(107,74,34,0.45)";
  c.lineWidth = 2;
  c.strokeRect(px + 28, py + 28, pw - 56, ph - 56);
  c.textAlign = "center";
  c.fillStyle = "#2a1c0f";
  c.font = `600 64px ${SERIF}`;
  c.fillText("SISYPHUS", W / 2, py + 170);
  c.fillStyle = "#6b4a22";
  c.font = `italic 500 40px ${SERIF}`;
  c.fillText(opts.title, W / 2, py + 230);
  const top = py + 340;
  const step = (ph - 460) / Math.max(1, opts.rows.length);
  opts.rows.forEach(([k, v], i) => {
    const y = top + i * step;
    c.fillStyle = "#6b4a22";
    c.font = `italic 500 34px ${SERIF}`;
    c.fillText(k, W / 2, y);
    c.fillStyle = "#2a1c0f";
    c.font = `600 58px ${SERIF}`;
    c.fillText(v, W / 2, y + 62);
  });
  return await new Promise<Blob>((res, rej) =>
    cv.toBlob((b) => (b ? res(b) : rej(new Error("no image"))), "image/png"),
  );
}
