import { useEffect, useRef, useState } from "react";
import { SisyphusEngine, type EngineState } from "@/game/engine";

export function SisyphusGame() {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const engineRef = useRef<SisyphusEngine | null>(null);
  const [state, setState] = useState<EngineState>({
    phase: "playing",
    progress: 0,
    levelName: "",
    epigraph: "",
    cycles: 0,
  });
  const [started, setStarted] = useState(false);
  const [muted, setMuted] = useState(false);
  const joystickBaseRef = useRef<HTMLDivElement>(null);
  const joystickKnobRef = useRef<HTMLDivElement>(null);
  const joyActive = useRef(false);
  const JOY_RADIUS = 36;

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const engine = new SisyphusEngine(canvas);
    engineRef.current = engine;
    engine.onState = setState;
    engine.start();

    const onResize = () => engine.resize();
    window.addEventListener("resize", onResize);

    // --- keyboard
    const keys = new Set<string>();
    const apply = () => {
      const right =
        keys.has("ArrowRight") ||
        keys.has("KeyD") ||
        keys.has("Space") ||
        keys.has("KeyW") ||
        keys.has("ArrowUp");
      const left = keys.has("ArrowLeft") || keys.has("KeyA");
      engine.input = right ? 1 : left ? -0.6 : 0;
    };
    const down = (e: KeyboardEvent) => {
      keys.add(e.code);
      if (
        ["Space", "ArrowRight", "ArrowLeft", "ArrowUp", "Tab"].includes(e.code)
      )
        e.preventDefault();
      if (e.code === "Tab") engine.kick();
      apply();
    };
    const up = (e: KeyboardEvent) => {
      keys.delete(e.code);
      apply();
    };
    window.addEventListener("keydown", down);
    window.addEventListener("keyup", up);

    // --- pointer (mouse + touch drag)
    let pointerId: number | null = null;
    let startX = 0;
    const pdown = (e: PointerEvent) => {
      pointerId = e.pointerId;
      startX = e.clientX;
      canvas.setPointerCapture(e.pointerId);
      engine.input = 0.65;
    };
    const pmove = (e: PointerEvent) => {
      if (pointerId !== e.pointerId) return;
      const dx = (e.clientX - startX) / 90;
      engine.input = Math.max(-1, Math.min(1, 0.5 + dx));
    };
    const pend = (e: PointerEvent) => {
      if (pointerId !== e.pointerId) return;
      pointerId = null;
      engine.input = 0;
    };
    canvas.addEventListener("pointerdown", pdown);
    canvas.addEventListener("pointermove", pmove);
    canvas.addEventListener("pointerup", pend);
    canvas.addEventListener("pointercancel", pend);

    return () => {
      window.removeEventListener("resize", onResize);
      window.removeEventListener("keydown", down);
      window.removeEventListener("keyup", up);
      canvas.removeEventListener("pointerdown", pdown);
      canvas.removeEventListener("pointermove", pmove);
      canvas.removeEventListener("pointerup", pend);
      canvas.removeEventListener("pointercancel", pend);
      engine.stop();
    };
  }, []);

  const begin = () => {
    engineRef.current?.audio.start();
    engineRef.current?.audio.resume();
    setStarted(true);
  };

  const toggleMute = () => {
    const m = !muted;
    setMuted(m);
    engineRef.current?.audio.setMuted(m);
  };

  const joySet = (clientX: number, clientY: number) => {
    const base = joystickBaseRef.current;
    const engine = engineRef.current;
    if (!base || !engine) return;
    const rect = base.getBoundingClientRect();
    let dx = clientX - (rect.left + rect.width / 2);
    let dy = clientY - (rect.top + rect.height / 2);
    const dist = Math.hypot(dx, dy);
    if (dist > JOY_RADIUS) {
      dx = (dx / dist) * JOY_RADIUS;
      dy = (dy / dist) * JOY_RADIUS;
    }
    if (joystickKnobRef.current) {
      joystickKnobRef.current.style.transform = `translate(${dx}px, ${dy}px)`;
    }
    engine.input = dx / JOY_RADIUS;
  };

  const joyDown = (e: React.PointerEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.currentTarget.setPointerCapture(e.pointerId);
    joyActive.current = true;
    joySet(e.clientX, e.clientY);
  };
  const joyMove = (e: React.PointerEvent<HTMLDivElement>) => {
    if (!joyActive.current) return;
    joySet(e.clientX, e.clientY);
  };
  const joyUp = () => {
    joyActive.current = false;
    if (engineRef.current) engineRef.current.input = 0;
    if (joystickKnobRef.current) {
      joystickKnobRef.current.style.transform = "translate(0px, 0px)";
    }
  };

  return (
    <div className="relative h-dvh w-full overflow-hidden bg-background select-none">
      <canvas ref={canvasRef} className="h-full w-full touch-none" />

      {/* HUD */}
      <div className="pointer-events-none absolute inset-x-0 top-0 flex items-start justify-between p-4 sm:p-6">
        <div>
          <p className="text-[0.7rem] tracking-[0.35em] text-muted-foreground uppercase">
            {state.levelName}
          </p>
          <div className="mt-2 h-px w-32 bg-border sm:w-48">
            <div
              className="h-px bg-accent transition-[width] duration-150"
              style={{ width: `${state.progress * 100}%` }}
            />
          </div>
        </div>
        <button
          onClick={toggleMute}
          className="pointer-events-auto text-[0.7rem] tracking-[0.25em] text-muted-foreground uppercase transition-colors hover:text-foreground"
        >
          {muted ? "ses kapalı" : "ses açık"}
        </button>
      </div>

      <p className="pointer-events-none absolute bottom-5 left-1/2 -translate-x-1/2 text-[0.65rem] tracking-[0.3em] text-muted-foreground/70 uppercase">
        döngü {Math.min(state.cycles + 1, 50)} / 50
      </p>

      {/* Key shortcuts */}
      {(state.phase === "playing" || state.phase === "rolling") && (
        <div className="pointer-events-none absolute bottom-5 left-4 hidden flex-col gap-1.5 text-[0.6rem] tracking-[0.2em] text-muted-foreground/80 uppercase sm:flex">
          <div className="flex items-center gap-1.5">
            <kbd className="rounded border border-border bg-background/40 px-1.5 py-0.5 font-mono text-foreground/90">
              →
            </kbd>
            <kbd className="rounded border border-border bg-background/40 px-1.5 py-0.5 font-mono text-foreground/90">
              D
            </kbd>
            <kbd className="rounded border border-border bg-background/40 px-1.5 py-0.5 font-mono text-foreground/90">
              Boşluk
            </kbd>
            <span>ilerlet</span>
          </div>
          <div className="flex items-center gap-1.5">
            <kbd className="rounded border border-border bg-background/40 px-1.5 py-0.5 font-mono text-foreground/90">
              ←
            </kbd>
            <kbd className="rounded border border-border bg-background/40 px-1.5 py-0.5 font-mono text-foreground/90">
              A
            </kbd>
            <span>geri it</span>
          </div>
          <div className="flex items-center gap-1.5">
            <kbd className="rounded border border-border bg-background/40 px-1.5 py-0.5 font-mono text-foreground/90">
              Tab
            </kbd>
            <span>kayayı fırlat</span>
          </div>
        </div>
      )}

      {/* Mobile joystick */}
      {(state.phase === "playing" || state.phase === "rolling") && (
        <div
          ref={joystickBaseRef}
          onPointerDown={joyDown}
          onPointerMove={joyMove}
          onPointerUp={joyUp}
          onPointerCancel={joyUp}
          className="absolute bottom-6 left-4 flex h-24 w-24 touch-none items-center justify-center rounded-full border border-border/60 bg-background/30 backdrop-blur-sm select-none sm:hidden"
        >
          <div
            ref={joystickKnobRef}
            className="h-10 w-10 rounded-full border border-border bg-accent/70"
            style={{ willChange: "transform" }}
          />
        </div>
      )}

      {/* Mobile throw button */}
      {(state.phase === "playing" || state.phase === "rolling") && (
        <button
          onPointerDown={(e) => {
            e.preventDefault();
            engineRef.current?.kick();
          }}
          className="absolute right-4 bottom-6 flex h-16 w-16 touch-none items-center justify-center rounded-full border border-border bg-background/50 text-[0.65rem] tracking-[0.15em] text-foreground uppercase backdrop-blur-sm select-none active:scale-95 active:bg-accent sm:hidden"
        >
          fırlat
        </button>
      )}

      {/* Summit cinematic */}
      {state.phase === "summit" && (
        <div className="pointer-events-none absolute inset-0 flex items-center justify-center bg-background/30 backdrop-blur-[1px]">
          <p className="animate-fade-in max-w-md px-8 text-center font-serif text-xl leading-relaxed text-foreground/90 italic sm:text-2xl">
            {state.epigraph}
          </p>
        </div>
      )}

      {/* Final: the twentieth summit */}
      {state.phase === "done" && (
        <div className="absolute inset-0 flex flex-col items-center justify-center gap-5 bg-background/60 px-6 text-center">
          <p className="text-xs tracking-[0.35em] text-muted-foreground uppercase">
            50 / 50 döngü tamamlandı
          </p>
          <p className="max-w-lg font-serif text-2xl leading-relaxed text-foreground/95 italic sm:text-4xl">
            {state.epigraph}
          </p>
          <button
            onClick={() => engineRef.current?.restart()}
            className="mt-4 rounded-full border border-border px-8 py-3 text-xs tracking-[0.35em] text-foreground uppercase transition-colors hover:bg-accent"
          >
            Yeniden Başla
          </button>
        </div>
      )}

      {/* Continue */}
      {state.phase === "restart" && (
        <div className="absolute inset-0 flex flex-col items-center justify-center gap-6 bg-background/70">
          <button
            onClick={() => engineRef.current?.restart()}
            className="animate-scale-in text-4xl font-light tracking-[0.3em] text-foreground uppercase transition-opacity hover:opacity-70 sm:text-6xl"
          >
            Devam Et
          </button>
          <p className="text-xs tracking-[0.3em] text-muted-foreground uppercase">
            taş yine aşağıda
          </p>
        </div>
      )}

      {/* Start overlay */}
      {!started && (
        <div className="absolute inset-0 flex flex-col items-center justify-center gap-5 bg-background/85 px-6 text-center">
          <h1 className="text-5xl font-light tracking-[0.4em] text-foreground uppercase sm:text-7xl">
            Sisyphus
          </h1>
          <p className="max-w-sm text-sm leading-relaxed text-muted-foreground">
            Kayayı zirveye taşı. Masaüstünde{" "}
            <span className="text-foreground">boşluk / → / D</span> tuşlarıyla it,{" "}
            <span className="text-foreground">Tab</span> ile kayayı fırlat ve peşinden
            koş. Mobilde sol alttaki joystikle it, sağ alttaki{" "}
            <span className="text-foreground">Fırlat</span> tuşuyla kayayı yukarı
            fırlat.
          </p>
          <button
            onClick={begin}
            className="mt-2 rounded-full border border-border px-8 py-3 text-xs tracking-[0.35em] text-foreground uppercase transition-colors hover:bg-accent"
          >
            Başla
          </button>
        </div>
      )}
    </div>
  );
}
