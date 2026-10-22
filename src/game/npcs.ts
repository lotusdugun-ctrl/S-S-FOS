/**
 * Small things carried on the hill. The figures themselves live in figures.ts.
 */
/** a little thing in his hand on the way down */
export function drawItem(
  ctx: CanvasRenderingContext2D,
  x: number,
  y: number,
  s: number,
  item: string,
  t: number,
) {
  ctx.save();
  switch (item) {
    case "feather":
      ctx.fillStyle = "oklch(0.35 0.03 60)";
      ctx.beginPath();
      ctx.ellipse(x, y - 4 * s, 1.6 * s, 5 * s, 0.4, 0, Math.PI * 2);
      ctx.fill();
      break;
    case "thyme":
    case "poppy":
      ctx.strokeStyle = "oklch(0.45 0.1 140)";
      ctx.lineWidth = 0.8 * s;
      ctx.beginPath();
      ctx.moveTo(x, y);
      ctx.lineTo(x - 1 * s, y - 7 * s);
      ctx.stroke();
      ctx.fillStyle = item === "poppy" ? "oklch(0.58 0.22 28)" : "oklch(0.7 0.08 310)";
      ctx.beginPath();
      ctx.arc(x - 1 * s, y - 8 * s, (item === "poppy" ? 2.4 : 1.4) * s, 0, Math.PI * 2);
      ctx.fill();
      break;
    case "snow":
      ctx.fillStyle = "rgba(240, 246, 255, 0.95)";
      ctx.beginPath();
      ctx.arc(x, y - 2 * s, 2.6 * s, 0, Math.PI * 2);
      ctx.fill();
      break;
    case "lethe": {
      // the water of forgetting, cupped in his hands: still, faintly cold-lit
      const g = ctx.createRadialGradient(x, y - 2 * s, 0, x, y - 2 * s, 9 * s);
      const a = 0.45 + Math.sin(t * 2) * 0.12;
      g.addColorStop(0, `rgba(200, 225, 255, ${a})`);
      g.addColorStop(1, "rgba(160, 190, 240, 0)");
      ctx.fillStyle = g;
      ctx.beginPath();
      ctx.arc(x, y - 2 * s, 9 * s, 0, Math.PI * 2);
      ctx.fill();
      break;
    }
    case "light":
    case "understanding": {
      const g = ctx.createRadialGradient(x, y - 2 * s, 0, x, y - 2 * s, 8 * s);
      const a = 0.5 + Math.sin(t * 3) * 0.15;
      g.addColorStop(0, `rgba(255, 236, 190, ${a})`);
      g.addColorStop(1, "rgba(255, 220, 160, 0)");
      ctx.fillStyle = g;
      ctx.beginPath();
      ctx.arc(x, y - 2 * s, 8 * s, 0, Math.PI * 2);
      ctx.fill();
      break;
    }
    case "stone":
    case "pebble":
    case "shard":
      ctx.fillStyle = "oklch(0.4 0.01 60)";
      ctx.beginPath();
      ctx.ellipse(x, y - 1.6 * s, 2.4 * s, 1.8 * s, 0.3, 0, Math.PI * 2);
      ctx.fill();
      break;
    default:
      // words, an answer, a look at the sea: nothing in the hand
      break;
  }
  ctx.restore();
}
