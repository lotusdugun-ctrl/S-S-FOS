/**
 * The heavy painting, off the main thread: building a cloud's volume, tinting
 * it for the hour, and working out the distant mountain. Each of these used
 * to hold a frame for tens of milliseconds — the stutters at the start of a
 * climb and as the light changed. Here they cost the game nothing.
 */
import {
  buildCloudData,
  tintPixels,
  type CloudData,
  type CloudKind,
  type CloudLight,
} from "./clouds";
import { computeMountain, type MountainSpec } from "./mountain";

type Msg =
  | { id: number; op: "cloud"; seed: number; kind: CloudKind; w: number }
  | { id: number; op: "tint"; seed: number; light: CloudLight }
  | { id: number; op: "mountain"; spec: MountainSpec; px: number };

const clouds = new Map<number, CloudData>();
const post = (msg: unknown, transfer: Transferable[] = []) =>
  (self as unknown as { postMessage(m: unknown, t: Transferable[]): void }).postMessage(
    msg,
    transfer,
  );

self.onmessage = (e: MessageEvent<Msg>) => {
  const m = e.data;
  try {
    if (m.op === "cloud") {
      const d = buildCloudData(m.seed, m.kind, m.w);
      clouds.set(m.seed, d);
      post({ id: m.id, w: d.w, h: d.h });
    } else if (m.op === "tint") {
      const d = clouds.get(m.seed);
      if (!d) return post({ id: m.id, missing: true });
      const px = tintPixels(d, m.light);
      post({ id: m.id, px }, [px.buffer]);
    } else if (m.op === "mountain") {
      const r = computeMountain(m.spec, m.px);
      post({ id: m.id, ...r }, [r.A.buffer, r.B.buffer]);
    }
  } catch (err) {
    post({ id: m.id, error: String(err) });
  }
};
