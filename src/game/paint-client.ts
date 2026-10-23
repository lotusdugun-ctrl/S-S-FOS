/**
 * The main thread's side of paint.worker: one worker for the whole game,
 * started on first use, every request a promise. Where workers are missing
 * or the worker fails, `available` turns false and callers do the work
 * themselves the old way.
 */
let worker: Worker | null = null;
let broken = false;
let nextId = 1;
const waiting = new Map<number, (v: Record<string, unknown> | null) => void>();

function start(): Worker | null {
  if (broken) return null;
  if (worker) return worker;
  if (typeof Worker === "undefined" || typeof window === "undefined") {
    broken = true;
    return null;
  }
  try {
    worker = new Worker(new URL("./paint.worker.ts", import.meta.url), { type: "module" });
  } catch {
    broken = true;
    return null;
  }
  worker.onmessage = (e: MessageEvent<Record<string, unknown> & { id: number }>) => {
    const done = waiting.get(e.data.id);
    waiting.delete(e.data.id);
    done?.(e.data["error"] || e.data["missing"] ? null : e.data);
  };
  worker.onerror = () => {
    // the worker could not load or crashed: everyone waiting gets nothing,
    // and from now on the work is done on the main thread
    broken = true;
    worker?.terminate();
    worker = null;
    for (const f of waiting.values()) f(null);
    waiting.clear();
  };
  return worker;
}

export const paintWorkerAvailable = () => !broken && start() !== null;

export function paintRequest<T extends Record<string, unknown>>(
  msg: Record<string, unknown>,
): Promise<T | null> {
  const w = start();
  if (!w) return Promise.resolve(null);
  const id = nextId++;
  return new Promise((res) => {
    waiting.set(id, res as (v: Record<string, unknown> | null) => void);
    w.postMessage({ ...msg, id });
  });
}
