/**
 * Zeus.
 *
 * He used to be a small robed figure standing beside the stone at the summit —
 * the same size as the man, which is the one thing a god should not be. Now he
 * is a colossus: a bearded, broad-chested torso rising out of a storm bank in
 * the sky over the peak, the thunderbolt drawn back in one fist, then hurled.
 *
 * Drawing a body this detailed every frame would be expensive, so each pose is
 * painted once into an offscreen sprite (body, muscle modelling, hair, beard,
 * himation, laurel, and a true rim light) and the frame only blits it. Only
 * the eyes, the bolt and the clouds — the things that move — are live.
 *
 * Design space: x right, y down, the waist at (0, 0), the raised fist near
 * y = -270. Everything is scaled by `k` when the sprite is built.
 */

export type ZeusPose = "raise" | "hurl";

type Pt = [number, number];

/** a limb as a tapered capsule with a muscle belly, like the man's, but bigger */
function limb(
  c: CanvasRenderingContext2D,
  a: Pt,
  b: Pt,
  wa: number,
  wb: number,
  belly: number,
  at = 0.45,
) {
  const [ax, ay] = a;
  const [bx, by] = b;
  const dx = bx - ax;
  const dy = by - ay;
  const len = Math.hypot(dx, dy) || 1;
  const nx = -dy / len;
  const ny = dx / len;
  const mx = ax + dx * at;
  const my = ay + dy * at;
  const wm = wa + (wb - wa) * at;
  const side = (sg: number, extra: number) => {
    const p0x = ax + nx * wa * sg;
    const p0y = ay + ny * wa * sg;
    const p2x = bx + nx * wb * sg;
    const p2y = by + ny * wb * sg;
    const qx = mx + nx * (wm + extra) * sg;
    const qy = my + ny * (wm + extra) * sg;
    return { p0x, p0y, p2x, p2y, cx: 2 * qx - 0.5 * (p0x + p2x), cy: 2 * qy - 0.5 * (p0y + p2y) };
  };
  const f = side(1, Math.max(0, belly));
  const k = side(-1, Math.max(0, -belly));
  c.beginPath();
  c.moveTo(f.p0x, f.p0y);
  c.quadraticCurveTo(f.cx, f.cy, f.p2x, f.p2y);
  c.lineTo(k.p2x, k.p2y);
  c.quadraticCurveTo(k.cx, k.cy, k.p0x, k.p0y);
  c.closePath();
  c.fill();
  c.beginPath();
  c.arc(ax, ay, wa, 0, Math.PI * 2);
  c.arc(bx, by, wb, 0, Math.PI * 2);
  c.fill();
}

/** joint positions for each pose, in design units */
function rig(pose: ZeusPose) {
  // his right arm (screen left) carries the bolt; his left rests on the hip
  return pose === "raise"
    ? {
        rSh: [-70, -142] as Pt,
        rEl: [-118, -196] as Pt,
        rHand: [-104, -262] as Pt,
        lSh: [70, -142] as Pt,
        lEl: [104, -84] as Pt,
        lHand: [70, -40] as Pt,
        lean: 0,
      }
    : {
        rSh: [-70, -140] as Pt,
        rEl: [-128, -114] as Pt,
        rHand: [-182, -84] as Pt,
        lSh: [70, -144] as Pt,
        lEl: [108, -98] as Pt,
        lHand: [96, -52] as Pt,
        lean: -6,
      };
}

/** the hand that holds the bolt, in design units, for each pose */
export function zeusHand(pose: ZeusPose): Pt {
  return rig(pose).rHand;
}

/**
 * Every solid part of him, filled with whatever `paint` returns for it. The
 * same call paints the shaded body and, with a flat colour, the silhouette the
 * rim light is cut from — so the rim follows the real outline by construction.
 */
function paintBody(
  c: CanvasRenderingContext2D,
  pose: ZeusPose,
  paint: (part: string) => string | CanvasGradient | CanvasPattern,
) {
  const r = rig(pose);
  const L = r.lean;

  // hair falling behind the neck to the shoulders
  c.fillStyle = paint("hairBack");
  c.beginPath();
  c.moveTo(-26 + L, -222);
  c.quadraticCurveTo(-40 + L, -186, -34 + L, -156);
  c.lineTo(34 + L, -156);
  c.quadraticCurveTo(40 + L, -186, 26 + L, -222);
  c.closePath();
  c.fill();

  // himation behind his left shoulder, hanging down his back
  c.fillStyle = paint("clothBack");
  c.beginPath();
  c.moveTo(50, -164);
  c.quadraticCurveTo(100, -160, 98, -110);
  c.quadraticCurveTo(104, -40, 92, 30);
  c.lineTo(50, 30);
  c.closePath();
  c.fill();

  // the arm behind the body when it is drawn back (raise): painted first
  c.fillStyle = paint("armFar");
  if (pose === "raise") {
    limb(c, r.rSh, r.rEl, 22, 17, -7);
    limb(c, r.rEl, r.rHand, 17, 11, 6, 0.3);
  }

  // torso: traps out to the shoulder, lats sweeping in to the waist
  c.fillStyle = paint("torso");
  c.beginPath();
  c.moveTo(-18 + L, -168);
  c.quadraticCurveTo(-50 + L, -160, -78 + L, -142);
  c.quadraticCurveTo(-88 + L, -116, -72, -96);
  c.quadraticCurveTo(-56, -60, -46, -24);
  c.quadraticCurveTo(-48, 0, -50, 30);
  c.lineTo(50, 30);
  c.quadraticCurveTo(48, 0, 46, -24);
  c.quadraticCurveTo(56, -60, 72, -96);
  c.quadraticCurveTo(88 + L, -116, 78 + L, -142);
  c.quadraticCurveTo(50 + L, -160, 18 + L, -168);
  c.closePath();
  c.fill();

  // deltoids: caps of muscle over the joints, wider than the arm
  c.fillStyle = paint("delt");
  c.beginPath();
  c.ellipse(r.rSh[0] - 4 + L, r.rSh[1] + 6, 24, 27, -0.35, 0, Math.PI * 2);
  c.fill();
  c.beginPath();
  c.ellipse(r.lSh[0] + 4 + L, r.lSh[1] + 6, 24, 27, 0.35, 0, Math.PI * 2);
  c.fill();

  // left arm, resting on the hip (or bracing for the throw)
  c.fillStyle = paint("armNear");
  limb(c, r.lSh, r.lEl, 22, 17, 7);
  limb(c, r.lEl, r.lHand, 17, 12, -6, 0.3);
  c.beginPath();
  c.ellipse(r.lHand[0], r.lHand[1], 13, 11, 0.4, 0, Math.PI * 2);
  c.fill();

  // the throwing arm, out in front of him once the bolt has gone
  if (pose === "hurl") {
    c.fillStyle = paint("armNear");
    limb(c, r.rSh, r.rEl, 23, 17, 8);
    limb(c, r.rEl, r.rHand, 17, 11, -6, 0.3);
    // open hand, fingers spread after the release
    c.save();
    c.translate(r.rHand[0], r.rHand[1]);
    c.rotate(Math.atan2(r.rHand[1] - r.rEl[1], r.rHand[0] - r.rEl[0]));
    c.beginPath();
    c.ellipse(6, 0, 14, 11, 0, 0, Math.PI * 2);
    c.fill();
    for (let i = -2; i <= 2; i++) {
      c.beginPath();
      c.ellipse(20, i * 5.2, 9, 2.8, i * 0.22, 0, Math.PI * 2);
      c.fill();
    }
    c.restore();
  } else {
    // fist closed around the bolt: the back of the hand, four fingers wrapped
    // round the shaft with their knuckles stacked, and the thumb over them
    c.fillStyle = paint("armFar");
    const [fx, fy] = r.rHand;
    c.beginPath();
    c.ellipse(fx - 3, fy + 2, 11, 13, 0.15, 0, Math.PI * 2);
    c.fill();
    for (let i = 0; i < 4; i++) {
      c.beginPath();
      c.ellipse(fx + 7, fy - 9 + i * 6, 5.5, 3.6, 0.2, 0, Math.PI * 2);
      c.fill();
    }
    c.beginPath();
    c.ellipse(fx + 2, fy - 12, 6, 3.4, -0.6, 0, Math.PI * 2);
    c.fill();
  }

  // gold armbands high on both upper arms and cuffs at the wrists
  c.fillStyle = paint("gold");
  const band = (a: Pt, b: Pt, at: number, wdt: number, thick: number) => {
    const x = a[0] + (b[0] - a[0]) * at;
    const y = a[1] + (b[1] - a[1]) * at;
    const ang = Math.atan2(b[1] - a[1], b[0] - a[0]);
    c.save();
    c.translate(x, y);
    c.rotate(ang);
    c.beginPath();
    c.rect(-thick / 2, -wdt, thick, wdt * 2);
    c.fill();
    c.restore();
  };
  band(r.lSh, r.lEl, 0.42, 20, 6);
  band(r.lEl, r.lHand, 0.78, 13, 7);
  if (pose === "hurl") {
    band(r.rSh, r.rEl, 0.42, 21, 6);
    band(r.rEl, r.rHand, 0.78, 13, 7);
  } else {
    band(r.rSh, r.rEl, 0.42, 20, 6);
    band(r.rEl, r.rHand, 0.75, 12, 7);
  }

  // himation: over the left shoulder, down across the left side of the chest
  // and gathered at the right hip, so the right pectoral and the abdomen show
  c.fillStyle = paint("cloth");
  c.beginPath();
  c.moveTo(34 + L, -168);
  c.quadraticCurveTo(72 + L, -174, 96 + L, -150);
  c.quadraticCurveTo(110, -100, 98, -46);
  c.quadraticCurveTo(92, -6, 94, 34);
  c.lineTo(-66, 34);
  c.quadraticCurveTo(-60, 12, -48, 2);
  c.quadraticCurveTo(-10, -18, 26, -46);
  c.quadraticCurveTo(52, -78, 50, -116);
  c.quadraticCurveTo(46, -146, 34 + L, -168);
  c.closePath();
  c.fill();

  // neck
  c.fillStyle = paint("torso");
  c.beginPath();
  c.moveTo(-16 + L, -186);
  c.lineTo(16 + L, -186);
  c.lineTo(24 + L, -156);
  c.lineTo(-24 + L, -156);
  c.closePath();
  c.fill();

  // head: a heavy skull with a high forehead
  c.fillStyle = paint("head");
  c.beginPath();
  c.ellipse(-2 + L, -207, 21, 27, -0.05, 0, Math.PI * 2);
  c.fill();
  // ears, just showing at the sides
  c.beginPath();
  c.ellipse(-21 + L, -204, 4, 7, 0.1, 0, Math.PI * 2);
  c.ellipse(17 + L, -204, 4, 7, -0.1, 0, Math.PI * 2);
  c.fill();

  // beard: from the cheekbones down to a point on the chest, in heavy curls
  c.fillStyle = paint("beard");
  c.beginPath();
  c.moveTo(-21 + L, -200);
  c.quadraticCurveTo(-30 + L, -168, -18 + L, -146);
  c.quadraticCurveTo(-8 + L, -128, -2 + L, -120);
  c.quadraticCurveTo(5 + L, -128, 14 + L, -146);
  c.quadraticCurveTo(26 + L, -168, 17 + L, -200);
  c.quadraticCurveTo(12 + L, -186, -2 + L, -186);
  c.quadraticCurveTo(-16 + L, -186, -21 + L, -200);
  c.closePath();
  c.fill();
  const curls: Array<[number, number, number]> = [
    [-22, -172, 8],
    [-16, -156, 8],
    [-6, -142, 8],
    [6, -144, 8],
    [14, -158, 8],
    [19, -174, 7],
    [-2, -128, 7],
  ];
  for (const [x, y, rr] of curls) {
    c.beginPath();
    c.arc(x + L, y, rr, 0, Math.PI * 2);
    c.fill();
  }
  // moustache, sweeping down at the corners of the mouth
  c.beginPath();
  c.moveTo(-17 + L, -184);
  c.quadraticCurveTo(-10 + L, -194, -2 + L, -191);
  c.quadraticCurveTo(6 + L, -194, 13 + L, -184);
  c.quadraticCurveTo(4 + L, -186, -2 + L, -186);
  c.quadraticCurveTo(-8 + L, -186, -17 + L, -184);
  c.fill();

  // hair: curls over the crown, framing the forehead and the temples
  c.fillStyle = paint("hair");
  const crown: Array<[number, number, number]> = [
    [-20, -226, 9],
    [-10, -233, 10],
    [2, -235, 10],
    [13, -230, 9],
    [-24, -214, 7],
    [19, -216, 7],
  ];
  for (const [x, y, rr] of crown) {
    c.beginPath();
    c.arc(x + L, y, rr, 0, Math.PI * 2);
    c.fill();
  }
  // long locks falling from the temples down onto the shoulders, wavy
  for (const side of [-1, 1]) {
    for (let k = 0; k < 3; k++) {
      const x0 = side * (20 + k * 3) + L;
      c.beginPath();
      c.moveTo(x0 - side * 4, -206 + k * 2);
      c.bezierCurveTo(
        x0 + side * 8,
        -190 + k * 3,
        x0 - side * 2,
        -176,
        x0 + side * (10 + k * 2),
        -158 + k * 4,
      );
      c.bezierCurveTo(x0 + side * 4, -164, x0 + side * 5, -184, x0 - side * 1, -204 + k * 2);
      c.closePath();
      c.fill();
    }
  }
}

/** muscle modelling, face and cloth folds, painted over the body with source-atop */
function model(c: CanvasRenderingContext2D, pose: ZeusPose) {
  const r = rig(pose);
  const L = r.lean;
  const soft = (x: number, y: number, rx: number, ry: number, color: string, rot = 0) => {
    c.save();
    c.translate(x, y);
    c.rotate(rot);
    c.scale(1, ry / rx);
    const g = c.createRadialGradient(0, 0, 0, 0, 0, rx);
    g.addColorStop(0, color);
    g.addColorStop(1, "rgba(0,0,0,0)");
    c.fillStyle = g;
    c.beginPath();
    c.arc(0, 0, rx, 0, Math.PI * 2);
    c.fill();
    c.restore();
  };
  const groove = (pts: Pt[], w: number, alpha: number) => {
    c.strokeStyle = `oklch(0.2 0.04 300 / ${alpha})`;
    c.lineWidth = w;
    c.lineCap = "round";
    c.beginPath();
    c.moveTo(pts[0]![0], pts[0]![1]);
    if (pts.length === 3) c.quadraticCurveTo(pts[1]![0], pts[1]![1], pts[2]![0], pts[2]![1]);
    else for (const p of pts.slice(1)) c.lineTo(p[0], p[1]);
    c.stroke();
  };
  const hi = "oklch(0.92 0.06 80 / 0.32)";
  const hiSoft = "oklch(0.92 0.06 80 / 0.18)";
  const lo = "oklch(0.18 0.04 300 / 0.35)";

  // ---- chest: the right pectoral as a mass, the left under the cloth
  soft(-30 + L, -128, 30, 18, hi, 0.2);
  groove(
    [
      [-62 + L, -112],
      [-34 + L, -98],
      [-6 + L, -110],
    ],
    4,
    0.22,
  );
  groove(
    [
      [-4 + L, -150],
      [-4 + L, -100],
    ],
    3,
    0.25,
  );
  // ---- abdominals: a soft grid of masses, not lines
  for (let row = 0; row < 3; row++) {
    const y = -84 + row * 20;
    soft(-18, y, 11, 8, hiSoft);
    soft(4, y, 10, 8, hiSoft);
    groove(
      [
        [-28, y + 9],
        [-16, y + 12],
        [-4, y + 9],
      ],
      2.5,
      0.13,
    );
  }
  groove(
    [
      [-6, -98],
      [-6, -60],
      [-8, -24],
    ],
    2.5,
    0.14,
  );
  // obliques and serratus: the ribs fanning into the side
  for (let i = 0; i < 3; i++) {
    groove(
      [
        [-60 + i * 2, -92 + i * 11],
        [-50 + i * 2, -88 + i * 11],
        [-42 + i * 2, -94 + i * 11],
      ],
      2.2,
      0.16,
    );
  }
  soft(-52, -60, 12, 28, lo, 0.2);
  // ---- shoulders and arms: lit along the outer, sunward curve
  soft(r.rSh[0] - 4, r.rSh[1] + 4, 20, 22, hi);
  soft(r.lSh[0] + 6, r.lSh[1] + 2, 18, 20, hiSoft);
  const along = (a: Pt, b: Pt, f: number, rx: number, ry: number, col: string) =>
    soft(
      a[0] + (b[0] - a[0]) * f,
      a[1] + (b[1] - a[1]) * f,
      rx,
      ry,
      col,
      Math.atan2(b[1] - a[1], b[0] - a[0]),
    );
  along(r.rSh, r.rEl, 0.5, 18, 9, hi);
  along(r.lSh, r.lEl, 0.5, 16, 8, hiSoft);
  along(r.rEl, r.rHand, 0.3, 14, 7, hiSoft);
  along(r.lEl, r.lHand, 0.3, 12, 6, hiSoft);

  // ---- face
  // the brow overhangs the eyes: a band of shadow, then the lit forehead above it
  soft(-2 + L, -222, 18, 7, hiSoft);
  soft(-12 + L, -206, 8, 5, lo);
  soft(8 + L, -206, 8, 5, lo);
  // nose: a lit ridge and a shadowed side
  c.strokeStyle = "oklch(0.88 0.06 75 / 0.55)";
  c.lineWidth = 3;
  c.lineCap = "round";
  c.beginPath();
  c.moveTo(-3 + L, -212);
  c.lineTo(-4 + L, -196);
  c.stroke();
  soft(2 + L, -198, 4, 6, lo);
  // brows: heavy, drawn together — he is not pleased
  c.strokeStyle = "oklch(0.3 0.02 80)";
  c.lineWidth = 3.4;
  c.beginPath();
  c.moveTo(-19 + L, -214);
  c.quadraticCurveTo(-11 + L, -216, -4 + L, -210);
  c.moveTo(14 + L, -214);
  c.quadraticCurveTo(7 + L, -216, 0 + L, -210);
  c.stroke();
  // eyes: pale, almost lit from inside
  c.fillStyle = "oklch(0.95 0.04 85)";
  for (const ex of [-12, 7]) {
    c.beginPath();
    c.ellipse(ex + L, -205, 3.6, 2, 0, 0, Math.PI * 2);
    c.fill();
  }
  c.fillStyle = "oklch(0.55 0.1 240)";
  for (const ex of [-13, 6]) {
    c.beginPath();
    c.arc(ex + L, -205, 1.4, 0, Math.PI * 2);
    c.fill();
  }
  // cheekbones catching the light
  soft(-14 + L, -196, 7, 4, hiSoft);
  soft(10 + L, -196, 7, 4, hi);

  // ---- hair and beard: highlight flicks so the curls read as curls
  c.strokeStyle = "oklch(0.97 0.02 85 / 0.5)";
  c.lineWidth = 2;
  const flick = (x: number, y: number, rr: number) => {
    c.beginPath();
    c.arc(x + L, y, rr, Math.PI * 1.1, Math.PI * 1.8);
    c.stroke();
  };
  for (const [x, y, rr] of [
    [-20, -226, 6],
    [-10, -233, 7],
    [2, -235, 7],
    [13, -230, 6],
    [-22, -172, 5],
    [-16, -156, 5],
    [-6, -142, 5],
    [6, -144, 5],
    [14, -158, 5],
    [19, -174, 5],
    [-2, -128, 5],
  ] as Array<[number, number, number]>)
    flick(x, y, rr);
  c.strokeStyle = "oklch(0.35 0.02 80 / 0.5)";
  c.lineWidth = 1.6;
  for (const [x, y, rr] of [
    [-16, -152, 6],
    [6, -140, 6],
    [-4, -124, 5],
    [16, -166, 5],
  ] as Array<[number, number, number]>) {
    c.beginPath();
    c.arc(x + L, y, rr, Math.PI * 0.1, Math.PI * 0.8);
    c.stroke();
  }

  // ---- himation: long pulls from the shoulder down to the gathered hip
  const fold = (
    x0: number,
    y0: number,
    cx: number,
    cy: number,
    x1: number,
    y1: number,
    dark: boolean,
  ) => {
    c.strokeStyle = dark ? "oklch(0.3 0.03 300 / 0.45)" : "oklch(0.97 0.03 85 / 0.3)";
    c.lineWidth = dark ? 4 : 2.5;
    c.beginPath();
    c.moveTo(x0, y0);
    c.quadraticCurveTo(cx, cy, x1, y1);
    c.stroke();
  };
  fold(60, -160, 70, -80, 20, -20, true);
  fold(70, -156, 82, -80, 40, -10, false);
  fold(84, -148, 96, -70, 62, 20, true);
  fold(48, -120, 30, -50, -30, 10, false);
  fold(40, -90, 10, -30, -46, 26, true);
  fold(92, -120, 100, -40, 84, 30, false);
  // gold meander border along the cloth's front edge
  c.strokeStyle = "oklch(0.8 0.13 82 / 0.9)";
  c.lineWidth = 4.5;
  c.beginPath();
  c.moveTo(34 + L, -168);
  c.quadraticCurveTo(46, -146, 50, -116);
  c.quadraticCurveTo(52, -78, 26, -46);
  c.quadraticCurveTo(-10, -18, -48, 2);
  c.quadraticCurveTo(-60, 12, -66, 34);
  c.stroke();
  c.strokeStyle = "oklch(0.5 0.1 70 / 0.9)";
  c.lineWidth = 1.4;
  c.setLineDash([4, 5]);
  c.stroke();
  c.setLineDash([]);

  // ---- age and power: lines on the brow, crow's feet, collarbones, forearms
  c.strokeStyle = "oklch(0.25 0.04 40 / 0.35)";
  c.lineWidth = 1.2;
  for (let i = 0; i < 3; i++) {
    c.beginPath();
    c.moveTo(-14 + L, -221 + i * 3.2);
    c.quadraticCurveTo(-2 + L, -224 + i * 3.2, 10 + L, -221 + i * 3.2);
    c.stroke();
  }
  for (const [ex, dir] of [
    [-17, -1],
    [12, 1],
  ] as const) {
    for (let i = 0; i < 2; i++) {
      c.beginPath();
      c.moveTo(ex + L, -206 + i * 2);
      c.lineTo(ex + dir * 4 + L, -207 + i * 3.5);
      c.stroke();
    }
  }
  c.strokeStyle = "oklch(0.2 0.04 300 / 0.3)";
  c.lineWidth = 2.5;
  c.beginPath();
  c.moveTo(-8 + L, -152);
  c.quadraticCurveTo(-34 + L, -146, -62 + L, -150);
  c.stroke();
  c.strokeStyle = "oklch(0.92 0.06 80 / 0.25)";
  c.lineWidth = 2;
  c.beginPath();
  c.moveTo(-8 + L, -155);
  c.quadraticCurveTo(-34 + L, -149, -62 + L, -153);
  c.stroke();
  // forearm flexors: a line down each forearm where the muscle bellies part
  c.strokeStyle = "oklch(0.2 0.04 300 / 0.28)";
  c.lineWidth = 2;
  for (const [a, b] of [
    [r.rEl, r.rHand],
    [r.lEl, r.lHand],
  ] as Array<[Pt, Pt]>) {
    c.beginPath();
    c.moveTo(a[0] + (b[0] - a[0]) * 0.15, a[1] + (b[1] - a[1]) * 0.15);
    c.quadraticCurveTo(
      a[0] + (b[0] - a[0]) * 0.45 + 3,
      a[1] + (b[1] - a[1]) * 0.45,
      a[0] + (b[0] - a[0]) * 0.75,
      a[1] + (b[1] - a[1]) * 0.75,
    );
    c.stroke();
  }
  // beard strands: long wavy lines through the curls
  c.strokeStyle = "oklch(0.4 0.02 80 / 0.35)";
  c.lineWidth = 1.3;
  for (let i = -3; i <= 3; i++) {
    c.beginPath();
    c.moveTo(i * 4 + L, -184);
    c.bezierCurveTo(i * 5 + 3 + L, -170, i * 3 - 3 + L, -150, i * 2 + L, -130 + Math.abs(i) * 3);
    c.stroke();
  }

  // ---- laurel: olive-gold leaves on a band over the brow
  for (let i = 0; i < 11; i++) {
    const a = Math.PI * (1.02 + i * 0.096);
    const x = Math.cos(a) * 24 - 2 + L;
    const y = Math.sin(a) * 20 - 214;
    c.save();
    c.translate(x, y);
    c.rotate(a + Math.PI / 2 + (i % 2 ? 0.55 : -0.55));
    c.fillStyle = i % 2 ? "oklch(0.78 0.13 92)" : "oklch(0.68 0.12 110)";
    c.beginPath();
    c.ellipse(0, -2, 3, 7, 0, 0, Math.PI * 2);
    c.fill();
    c.restore();
  }
}

/** the thunderbolt as Greek art drew it: a spindle of zigzag flame, pointed at both ends */
export function drawKeraunos(
  c: CanvasRenderingContext2D,
  x: number,
  y: number,
  len: number,
  rot: number,
  glow: number,
) {
  c.save();
  c.translate(x, y);
  c.rotate(rot);
  const half = len / 2;
  const zig = (sx: number, amp: number) => {
    c.beginPath();
    c.moveTo(0, -half);
    for (let i = 1; i < 6; i++) {
      const t = i / 6;
      c.lineTo(sx * amp * (i % 2 ? 1 : -0.4) * Math.sin(t * Math.PI), -half + t * len);
    }
    c.lineTo(0, half);
    c.stroke();
  };
  c.lineCap = "round";
  c.lineJoin = "round";
  c.shadowColor = "oklch(0.88 0.15 85)";
  c.shadowBlur = 14 * glow;
  c.strokeStyle = `oklch(0.9 0.13 85 / ${0.5 * glow})`;
  c.lineWidth = len * 0.12;
  zig(1, len * 0.09);
  zig(-1, len * 0.09);
  c.strokeStyle = "oklch(0.98 0.04 95)";
  c.lineWidth = len * 0.045;
  zig(1, len * 0.09);
  zig(-1, len * 0.09);
  c.shadowBlur = 0;
  c.restore();
}

type Sprite = { cv: HTMLCanvasElement; ox: number; oy: number; k: number };

export class ZeusPainter {
  private cache = new Map<string, Sprite>();

  /** the posed body as a sprite, `k` px per design unit; origin = the waist */
  sprite(pose: ZeusPose, k: number): Sprite | null {
    const key = `${pose}:${k.toFixed(3)}`;
    const hit = this.cache.get(key);
    if (hit) return hit;
    if (this.cache.size > 6) this.cache.clear();

    const pad = 24;
    const minX = -230;
    const maxX = 140;
    const minY = -300;
    const maxY = 40;
    const w = Math.ceil((maxX - minX) * k + pad * 2);
    const h = Math.ceil((maxY - minY) * k + pad * 2);
    const ox = -minX * k + pad;
    const oy = -minY * k + pad;
    const mk = () => {
      const cv = document.createElement("canvas");
      cv.width = w;
      cv.height = h;
      return cv;
    };

    // 1. the shaded body
    const cv = mk();
    const c = cv.getContext("2d");
    if (!c) return null;
    c.translate(ox, oy);
    c.scale(k, k);
    // Lit from below by the low sun and the bolt, so the values climb toward
    // the waist, and the shadow side stays the violet of the storm.
    // Bronze lit by the low sun from his right and below; the far side of
    // every form drops into the storm's violet.
    const skin = c.createLinearGradient(-120, -260, 90, 20);
    skin.addColorStop(0, "oklch(0.42 0.05 320)");
    skin.addColorStop(0.45, "oklch(0.52 0.06 40)");
    skin.addColorStop(1, "oklch(0.7 0.08 62)");
    const cloth = c.createLinearGradient(110, -170, -60, 30);
    cloth.addColorStop(0, "oklch(0.6 0.03 290)");
    cloth.addColorStop(0.5, "oklch(0.82 0.035 80)");
    cloth.addColorStop(1, "oklch(0.62 0.04 300)");
    const hair = c.createLinearGradient(-30, -240, 30, -120);
    hair.addColorStop(0, "oklch(0.6 0.015 80)");
    hair.addColorStop(1, "oklch(0.86 0.02 85)");
    const fills: Record<string, string | CanvasGradient> = {
      hairBack: "oklch(0.66 0.015 80)",
      clothBack: "oklch(0.42 0.03 300)",
      armFar: skin,
      torso: skin,
      delt: skin,
      armNear: skin,
      cloth,
      head: skin,
      beard: hair,
      hair,
      gold: "oklch(0.78 0.13 82)",
    };
    paintBody(c, pose, (p) => fills[p] ?? skin);
    c.globalCompositeOperation = "source-atop";
    model(c, pose);
    c.globalCompositeOperation = "source-over";

    // 2. a true rim light: the silhouette minus itself shifted toward the light
    const sil = mk();
    const sc = sil.getContext("2d");
    if (!sc) return null;
    sc.translate(ox, oy);
    sc.scale(k, k);
    paintBody(sc, pose, () => "#fff");
    const rim = (dx: number, dy: number, color: string, blur: number) => {
      const r = mk();
      const rc = r.getContext("2d");
      if (!rc) return;
      rc.drawImage(sil, 0, 0);
      rc.globalCompositeOperation = "source-in";
      rc.fillStyle = color;
      rc.fillRect(0, 0, w, h);
      rc.globalCompositeOperation = "destination-out";
      rc.drawImage(sil, dx, dy);
      c.save();
      c.setTransform(1, 0, 0, 1, 0, 0);
      c.globalCompositeOperation = "source-atop";
      void blur;
      c.drawImage(r, 0, 0);
      c.restore();
    };
    const e = Math.max(1.5, 3.2 * k);
    // the sun, low and to his right: a warm edge along the lower right
    rim(-e, -e * 0.6, "rgba(255, 214, 150, 0.9)", 0);
    // the sky above: a cool, thin edge across the top of every form
    rim(0, e * 0.9, "rgba(214, 224, 255, 0.5)", 0);

    const s: Sprite = { cv, ox, oy, k };
    this.cache.set(key, s);
    return s;
  }

  /**
   * One storm cloud mass: dark, with a lit underside where the bolt and the
   * sun reach it. `alpha` fades the whole bank in and out.
   */
  cloud(
    c: CanvasRenderingContext2D,
    x: number,
    y: number,
    r: number,
    alpha: number,
    flash: number,
  ) {
    const g = c.createRadialGradient(x, y - r * 0.25, r * 0.1, x, y, r);
    g.addColorStop(0, `oklch(0.27 0.03 285 / ${0.92 * alpha})`);
    g.addColorStop(0.7, `oklch(0.3 0.035 290 / ${0.75 * alpha})`);
    g.addColorStop(1, "oklch(0.32 0.04 295 / 0)");
    c.fillStyle = g;
    c.beginPath();
    c.arc(x, y, r, 0, Math.PI * 2);
    c.fill();
    const u = c.createRadialGradient(x, y + r * 0.5, 0, x, y + r * 0.5, r * 0.9);
    u.addColorStop(0, `oklch(0.82 0.1 60 / ${(0.22 + flash * 0.4) * alpha})`);
    u.addColorStop(1, "oklch(0.8 0.1 60 / 0)");
    c.fillStyle = u;
    c.beginPath();
    c.arc(x, y + r * 0.5, r * 0.9, 0, Math.PI * 2);
    c.fill();
  }
}
