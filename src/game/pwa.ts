import { useEffect, useState } from "react";

/**
 * The game as an app: the service worker (offline launch) and the install
 * prompt. Android Chrome hands us a `beforeinstallprompt` event that can be
 * replayed from our own button; iOS has no such API, so there the start card
 * explains the Share → Add to Home Screen route instead.
 */

type InstallPrompt = Event & {
  prompt: () => Promise<void>;
  userChoice: Promise<{ outcome: "accepted" | "dismissed" }>;
};

export function registerServiceWorker() {
  // dev builds have no stable asset names, and a worker there only gets in the way
  if (!import.meta.env.PROD || !("serviceWorker" in navigator)) return;
  const go = () => navigator.serviceWorker.register("/sw.js").catch(() => {});
  if (document.readyState === "complete") go();
  else window.addEventListener("load", go, { once: true });
}

export function isStandalone(): boolean {
  try {
    return (
      window.matchMedia("(display-mode: fullscreen)").matches ||
      window.matchMedia("(display-mode: standalone)").matches ||
      (navigator as Navigator & { standalone?: boolean }).standalone === true
    );
  } catch {
    return false;
  }
}

function isIOS(): boolean {
  const ua = navigator.userAgent;
  // iPadOS reports itself as a Mac, but a Mac has no touch points
  return /iPhone|iPad|iPod/.test(ua) || (/Macintosh/.test(ua) && navigator.maxTouchPoints > 1);
}

export function useInstall() {
  const [prompt, setPrompt] = useState<InstallPrompt | null>(null);
  const [standalone, setStandalone] = useState(true);
  const [ios, setIos] = useState(false);

  useEffect(() => {
    setStandalone(isStandalone());
    setIos(isIOS());
    const onPrompt = (e: Event) => {
      e.preventDefault();
      setPrompt(e as InstallPrompt);
    };
    const onInstalled = () => {
      setPrompt(null);
      setStandalone(true);
    };
    window.addEventListener("beforeinstallprompt", onPrompt);
    window.addEventListener("appinstalled", onInstalled);
    return () => {
      window.removeEventListener("beforeinstallprompt", onPrompt);
      window.removeEventListener("appinstalled", onInstalled);
    };
  }, []);

  const install = async () => {
    if (!prompt) return;
    await prompt.prompt();
    await prompt.userChoice.catch(() => null);
    setPrompt(null);
  };

  return {
    /** Android/desktop Chrome: our button can trigger the real install dialog */
    canInstall: !standalone && prompt !== null,
    /** iOS Safari in a tab: only manual Add to Home Screen exists */
    showIosHint: !standalone && ios,
    install,
  };
}
