/*
 * Sisyphus service worker: makes the game installable and lets it start with
 * no connection at all.
 *
 * - The page itself is network-first, so a deploy shows up on the next launch,
 *   and falls back to the last copy it saw when offline.
 * - Built assets have content hashes in their names, so they never change:
 *   cache-first, filled as they are fetched.
 * - The music is a 5 MB file that the <audio> element asks for in byte ranges.
 *   Partial (206) responses cannot be cached, so the whole file is fetched once
 *   in the background and ranges are cut from that copy.
 */
const VERSION = "sisyphus-v6";
const SHELL = ["/", "/manifest.webmanifest", "/icon-192.png", "/icon-512.png", "/favicon.ico"];
const MUSIC = "/music.mp3";

self.addEventListener("install", (event) => {
  event.waitUntil(
    caches
      .open(VERSION)
      .then(async (c) => {
        await c.addAll(SHELL);
        // the page's own scripts and styles, so the first offline launch works
        // even if this install happened after they had already loaded
        const html = await (await c.match("/")).text();
        const assets = [...new Set(html.match(/\/assets\/[^"'\s)]+/g) || [])];
        await Promise.all(assets.map((a) => c.add(a).catch(() => {})));
      })
      .then(() => self.skipWaiting()),
  );
});

self.addEventListener("activate", (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((keys) => Promise.all(keys.filter((k) => k !== VERSION).map((k) => caches.delete(k))))
      .then(() => self.clients.claim()),
  );
});

async function fromCache(req) {
  const c = await caches.open(VERSION);
  return c.match(req, { ignoreSearch: true });
}

async function put(req, res) {
  if (!res || !res.ok || res.status !== 200) return;
  const c = await caches.open(VERSION);
  await c.put(req, res);
}

async function networkFirst(req) {
  try {
    const res = await fetch(req);
    put(req, res.clone());
    return res;
  } catch {
    return (await fromCache(req)) || (await fromCache("/")) || Response.error();
  }
}

async function cacheFirst(req) {
  const hit = await fromCache(req);
  if (hit) return hit;
  const res = await fetch(req);
  put(req, res.clone());
  return res;
}

let musicFill = null;
function fillMusic() {
  if (!musicFill) {
    musicFill = fetch(MUSIC)
      .then((res) => put(MUSIC, res))
      .catch(() => {
        musicFill = null;
      });
  }
  return musicFill;
}

async function music(req) {
  const full = await fromCache(MUSIC);
  if (!full) {
    fillMusic();
    return fetch(req);
  }
  const range = req.headers.get("range");
  if (!range) return full;
  const buf = await full.arrayBuffer();
  const m = /bytes=(\d*)-(\d*)/.exec(range);
  const size = buf.byteLength;
  let start = m && m[1] ? parseInt(m[1], 10) : 0;
  let end = m && m[2] ? parseInt(m[2], 10) : size - 1;
  if (m && !m[1] && m[2]) {
    start = size - parseInt(m[2], 10);
    end = size - 1;
  }
  end = Math.min(end, size - 1);
  return new Response(buf.slice(start, end + 1), {
    status: 206,
    headers: {
      "Content-Type": "audio/mpeg",
      "Content-Range": `bytes ${start}-${end}/${size}`,
      "Content-Length": String(end - start + 1),
      "Accept-Ranges": "bytes",
    },
  });
}

self.addEventListener("fetch", (event) => {
  const req = event.request;
  if (req.method !== "GET") return;
  const url = new URL(req.url);
  if (url.origin !== self.location.origin) return;
  if (url.pathname === MUSIC) return event.respondWith(music(req));
  if (req.mode === "navigate") return event.respondWith(networkFirst(req));
  if (
    url.pathname.startsWith("/assets/") ||
    /\.(png|svg|ico|webmanifest|woff2?)$/.test(url.pathname)
  )
    return event.respondWith(cacheFirst(req));
  // anything else same-origin (server functions, etc.): network, cache as fallback
  event.respondWith(networkFirst(req));
});
