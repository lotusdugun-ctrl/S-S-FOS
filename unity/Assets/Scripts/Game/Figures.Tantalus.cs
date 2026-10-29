// Port of src/game/figures.ts: Tantalus, his pool and the fig tree.
using System;
using System.Collections.Generic;
using Sisifos.Paint;

namespace Sisifos.Game
{
    /// <summary>
    /// How Tantalus is, as his own story has left him: reaching (the punishment);
    /// `still` — he has stopped reaching and the water stays; `stand` — on his
    /// feet, the water at his knees; `mud` — the pool sunk away, him on his knees;
    /// `back` — turned away from the path; `crown` — still on his head; `sprout` —
    /// the fig he planted by the path.
    /// </summary>
    public class TanLook
    {
        public bool still;
        public bool stand;
        public bool mud;
        public bool back;
        public bool crown;
        public bool sprout;
    }

    public static partial class Figures
    {
        public static readonly TanLook TAN_REACHING = new TanLook
        {
            still = false,
            stand = false,
            mud = false,
            back = false,
            crown = true,
            sprout = false,
        };

        /// <summary>
        /// Tantalus: once a king, Zeus's own son, the only mortal ever asked to eat at
        /// the gods' table — now starved to the bone and standing chest-deep in black
        /// water under a fig heavy with fruit, a bent gold diadem still on his matted
        /// head. He does the two things he is condemned to do, over and over: he
        /// reaches for the figs and the branch lifts out of his hand; he stoops to
        /// drink and the water sinks away from his mouth, leaving the stones wet.
        /// PORT: null `look` means the TS default, TAN_REACHING.
        /// </summary>
        public static void drawTantalus(Ctx ctx, double x, double y, double s, FigLight L, TanLook look = null)
        {
            if (look == null) look = TAN_REACHING;
            double t = L.t;
            double cyc = (((t * 0.16) % 1) + 1) % 1;
            // reach for the fruit, a pause, stoop for the water, a pause — unless he
            // has stopped wanting: then he is still, and the water stays
            bool calm = look.still || look.stand || look.mud;
            bool crowned = look.crown;
            double reach = calm ? 0 : cyc < 0.42 ? Math.Sin((cyc / 0.42) * Math.PI) : 0;
            double bend = calm ? 0 : cyc >= 0.5 && cyc < 0.92 ? Math.Sin(((cyc - 0.5) / 0.42) * Math.PI) : 0;
            // on his feet the water is only at his knees; it always was
            double up = look.stand && !look.mud ? 24 * s : 0;
            double W = 40 * s;
            double full = y - 8 * s; // where the water stands until he bends to it
            double surf = full + bend * 8 * s;
            double Ws = W * (1 - bend * 0.1);
            // the figs hang just over his head; reaching, his fingers all but touch them
            double by = y - 58 * s - reach * 9 * s - (look.mud ? 14 * s : 0);
            figTree(ctx, x, y, s, L, by);
            if (look.sprout)
            {
                // the seed he pressed into the ash beside the path
                ctx.Save();
                double sx = x - W - 12 * s;
                ctx.StrokeStyle = "#4a5a2a";
                ctx.LineWidth = 0.9 * s;
                ctx.BeginPath();
                ctx.MoveTo(sx, y);
                ctx.QuadraticCurveTo(sx + 1 * s, y - 4 * s, sx + 0.5 * s, y - 7 * s);
                ctx.Stroke();
                ctx.FillStyle = "#5d7a32";
                foreach (var lf in new[] { new[] { -1.6, -6, -0.6 }, new[] { 2, -7.4, 0.5 } })
                {
                    double lx = lf[0], ly = lf[1], a = lf[2];
                    ctx.BeginPath();
                    ctx.Ellipse(sx + lx * s, y + ly * s, 2 * s, 1 * s, a, 0, Math.PI * 2);
                    ctx.Fill();
                }
                ctx.Restore();
            }

            // ---- the basin: wet dark rock where the water stood; the water itself
            ctx.Save();
            ctx.FillStyle = "#151412";
            ctx.BeginPath();
            ctx.Ellipse(x, full, W, 9 * s, 0, Math.PI, Math.PI * 2);
            ctx.Fill();
            ctx.FillStyle = look.mud ? "#1d1610" : "#0a1014";
            ctx.BeginPath();
            ctx.Ellipse(x, surf, Ws, 9 * s * (Ws / W), 0, Math.PI, Math.PI * 2);
            ctx.Fill();
            // the sky in the black water, and the fruit's reflection he can never drink either
            var sky = ctx.CreateLinearGradient(0, surf - 9 * s, 0, surf);
            sky.AddColorStop(0, rgba(mix(L.color, new RGB(90, 120, 150), 0.6), 0));
            sky.AddColorStop(1, rgba(mix(L.color, new RGB(90, 120, 150), 0.6), look.mud ? 0 : 0.24 * (1 - L.dim)));
            ctx.FillStyle = sky;
            ctx.BeginPath();
            ctx.Ellipse(x, surf, Ws - 2 * s, 8 * s * (Ws / W), 0, Math.PI, Math.PI * 2);
            ctx.Fill();
            rimStones(ctx, x, full, W, s, L, true);
            // the wet mark on the stones where the water was a moment ago
            if (bend > 0.05)
            {
                ctx.StrokeStyle = Col.Rgba(10, 14, 16, Css.fixedNum(0.5 * bend, 3));
                ctx.LineWidth = 2.2 * s;
                ctx.BeginPath();
                ctx.Ellipse(x, full + 1 * s, W - 1 * s, 8.4 * s, 0, Math.PI * 1.05, Math.PI * 1.95);
                ctx.Stroke();
            }
            ctx.Restore();

            // ---- him
            double sw = surf - y; // the water line, in his own frame
            var pivot = (x: -2 * s, y: sw + 3 * s - up); // his waist, under the water
            double lean = -bend * 0.5 + reach * 0.06 - (calm ? 0.08 : 0);
            (double x, double y) rot(double px, double py)
            {
                return (
                    x: pivot.x + px * Math.Cos(lean) - py * Math.Sin(lean),
                    y: pivot.y + px * Math.Sin(lean) + py * Math.Cos(lean));
            }
            if (look.back)
            {
                // his back to the path
                ctx.Save();
                ctx.Translate(x * 2, 0);
                ctx.Scale(-1, 1);
            }
            paintLit(
                ctx,
                "tantalus",
                x,
                y,
                new Box(-40 * s, -112 * s - up, 28 * s, 6 * s),
                L,
                s,
                (c) =>
                {
                    RGB SK = new RGB(168, 132, 106);
                    Col skin(double k) { return rgba(mix(SK, new RGB(56, 38, 30), k), 1); }
                    if (up != 0)
                    {
                        // standing: his thighs go down into the water
                        foreach (var dk in new[] { new[] { 3.5, 0.34 }, new[] { -3.5, 0.1 } })
                        {
                            double ddx = dk[0], k = dk[1];
                            var hp = rot(ddx * s, 2 * s);
                            limb(c, hp.x, hp.y, hp.x - 1 * s, sw + 6 * s, 3.6 * s, 2.6 * s, 0.6 * s, 0.4, skin(k));
                        }
                    }

                    // the far arm, hanging slack into the water
                    var shF = rot(7.6 * s, -23 * s);
                    var elF = rot(10 * s, -12 * s);
                    var hdF = rot(8 * s, -2 * s);
                    limb(c, shF.x, shF.y, elF.x, elF.y, 2.2 * s, 1.6 * s, 0.3 * s, 0.5, skin(0.32));
                    limb(c, elF.x, elF.y, hdF.x, hdF.y, 1.6 * s, 1.1 * s, 0.2 * s, 0.5, skin(0.34));

                    // ---- the torso, in its own leaning frame
                    c.Save();
                    c.Translate(pivot.x, pivot.y);
                    c.Rotate(lean);
                    var tg = c.CreateLinearGradient(-10 * s, 0, 10 * s, 0);
                    tg.AddColorStop(0, skin(-0.06));
                    tg.AddColorStop(0.55, skin(0.1));
                    tg.AddColorStop(1, skin(0.42));
                    c.FillStyle = tg;
                    c.BeginPath();
                    c.MoveTo(-5.4 * s, 4 * s);
                    c.QuadraticCurveTo(-5 * s, -6 * s, -7.6 * s, -12 * s); // the flank over the starved belly
                    c.QuadraticCurveTo(-9 * s, -18 * s, -8.6 * s, -21.4 * s); // the ribcage
                    c.QuadraticCurveTo(-10.4 * s, -23 * s, -9.4 * s, -25 * s); // the bony point of the shoulder
                    c.QuadraticCurveTo(-6 * s, -26.6 * s, -2.6 * s, -28 * s); // trapezius, thin
                    c.LineTo(-1.8 * s, -31 * s);
                    c.LineTo(2 * s, -31 * s);
                    c.LineTo(2.6 * s, -28 * s);
                    c.QuadraticCurveTo(6 * s, -26.4 * s, 9.4 * s, -24.6 * s);
                    c.QuadraticCurveTo(10.4 * s, -22 * s, 8.8 * s, -19 * s);
                    c.QuadraticCurveTo(8.6 * s, -10 * s, 6 * s, 4 * s);
                    c.ClosePath();
                    c.Fill();
                    atop(c, () =>
                    {
                        // the belly fallen in under the ribs
                        var bl = c.CreateRadialGradient(0, -6 * s, 0, 0, -6 * s, 7 * s);
                        bl.AddColorStop(0, "rgba(40, 20, 14, 0.38)");
                        bl.AddColorStop(1, "rgba(40, 20, 14, 0)");
                        c.FillStyle = bl;
                        c.FillRect(-8 * s, -14 * s, 16 * s, 16 * s);
                        // the ribs: each one a lit ridge with a shadow under it, curving down to the breastbone
                        for (int i = 0; i < 6; i++)
                        {
                            double ry = -22 * s + i * 2.5 * s;
                            double half = (7.6 - Math.Abs(i - 1.6) * 0.5) * s;
                            for (int side = -1; side <= 1; side += 2)
                            {
                                c.StrokeStyle = "rgba(40, 18, 12, 0.3)";
                                c.LineWidth = 1.1 * s;
                                c.BeginPath();
                                c.MoveTo(side * half, ry - 0.6 * s);
                                c.QuadraticCurveTo(side * half * 0.5, ry + 1.4 * s, side * 0.8 * s, ry + 0.4 * s);
                                c.Stroke();
                                c.StrokeStyle = "rgba(255, 226, 196, 0.18)";
                                c.LineWidth = 0.7 * s;
                                c.BeginPath();
                                c.MoveTo(side * half, ry - 1.5 * s);
                                c.QuadraticCurveTo(side * half * 0.5, ry + 0.5 * s, side * 0.8 * s, ry - 0.5 * s);
                                c.Stroke();
                            }
                        }
                        // the breastbone, the collarbones standing out, the pits above them
                        c.StrokeStyle = "rgba(40, 18, 12, 0.35)";
                        c.LineWidth = 0.8 * s;
                        c.BeginPath();
                        c.MoveTo(0, -26 * s);
                        c.LineTo(0.2 * s, -14 * s);
                        c.Stroke();
                        for (int side = -1; side <= 1; side += 2)
                        {
                            c.StrokeStyle = "rgba(255, 230, 200, 0.3)";
                            c.LineWidth = 1 * s;
                            c.BeginPath();
                            c.MoveTo(side * 1.2 * s, -26.6 * s);
                            c.QuadraticCurveTo(side * 5 * s, -27 * s, side * 8.6 * s, -25.4 * s);
                            c.Stroke();
                            c.FillStyle = "rgba(40, 18, 12, 0.35)";
                            c.BeginPath();
                            c.Ellipse(side * 4.6 * s, -28.2 * s, 2.4 * s, 0.9 * s, side * 0.15, 0, Math.PI * 2);
                            c.Fill();
                        }
                        // cords of the neck
                        c.StrokeStyle = "rgba(40, 18, 12, 0.4)";
                        c.LineWidth = 0.6 * s;
                        c.BeginPath();
                        c.MoveTo(-1.2 * s, -30.6 * s);
                        c.LineTo(-0.4 * s, -27 * s);
                        c.MoveTo(1.4 * s, -30.6 * s);
                        c.LineTo(0.8 * s, -27 * s);
                        c.Stroke();
                    });
                    c.Restore();

                    // ---- the head: gaunt, hair and beard grown long and matted, a bent crown
                    var nk = rot(0, -32 * s);
                    // PORT: `look` (the TS shadows the parameter here) is `lookAng`
                    double lookAng = reach * 0.42 - bend * 0.55 - (calm ? 0.3 : 0); // up at the fruit, down to the water
                    c.Save();
                    c.Translate(nk.x - 0.8 * s, nk.y - 4.6 * s);
                    c.Rotate(lean * 0.6 + lookAng);
                    double r = 6 * s;
                    // the long hair behind, in clumps
                    c.FillStyle = "#2a2622";
                    c.BeginPath();
                    c.MoveTo(-0.4 * r, -1.0 * r);
                    c.BezierCurveTo(0.8 * r, -1.4 * r, 1.4 * r, -0.4 * r, 1.3 * r, 0.6 * r);
                    for (int i = 0; i < 5; i++)
                    {
                        double hx2 = 1.3 * r - i * 0.22 * r;
                        c.LineTo(hx2 + 0.1 * r, (2.6 + (i % 2) * 0.5) * r);
                        c.LineTo(hx2 - 0.1 * r, 1.6 * r);
                    }
                    c.LineTo(0.2 * r, 0.6 * r);
                    c.ClosePath();
                    c.Fill();
                    profile(c, 0, 0, r, skin(0.02), bend * 0.5 + reach * 0.15);
                    face(c, 0, 0, r, pain: 0.85, shut: blink(t, 4.1), open: bend * 0.5 + reach * 0.15);
                    atop(c, () =>
                    {
                        // starvation: the cheek fallen in, the temple hollow, the eye sunk deep
                        softSpot(c, -0.42 * r, 0.42 * r, 0.4 * r, 0.26 * r, 0.3, 36, 18, 12, 0.5);
                        softSpot(c, -0.2 * r, -0.3 * r, 0.28 * r, 0.34 * r, 0, 36, 18, 12, 0.4);
                        softSpot(c, -0.64 * r, -0.08 * r, 0.36 * r, 0.26 * r, 0, 20, 10, 8, 0.5);
                        // the eye, raised to the fruit (and now and then a blink)
                        if (blink(t, 4.1) > 0.5)
                        {
                            c.StrokeStyle = "rgba(40, 20, 14, 0.85)";
                            c.LineWidth = 0.07 * r;
                            c.BeginPath();
                            c.MoveTo(-0.8 * r, -0.08 * r);
                            c.QuadraticCurveTo(-0.68 * r, -0.02 * r, -0.56 * r, -0.08 * r);
                            c.Stroke();
                        }
                        else
                        {
                            c.FillStyle = "rgba(230, 220, 200, 0.95)";
                            c.BeginPath();
                            c.Ellipse(-0.68 * r, -0.08 * r, 0.12 * r, 0.07 * r, 0, 0, Math.PI * 2);
                            c.Fill();
                            c.FillStyle = "#120a08";
                            c.BeginPath();
                            c.Arc(-0.74 * r, -0.12 * r, 0.055 * r, 0, Math.PI * 2);
                            c.Fill();
                        }
                        // cracked lips
                        c.StrokeStyle = "rgba(90, 30, 24, 0.7)";
                        c.LineWidth = 0.05 * r;
                        c.BeginPath();
                        c.MoveTo(-0.97 * r, 0.68 * r);
                        c.LineTo(-0.9 * r, 0.74 * r);
                        c.MoveTo(-0.94 * r, 0.62 * r);
                        c.LineTo(-0.88 * r, 0.6 * r);
                        c.Stroke();
                    });
                    // the beard: long, scraggly, to the breastbone, grey in it
                    beard(c, r, "#221d1a", "#46403a", "rgba(200, 192, 180, 0.26)", 1.7, 1, bend * 0.5 + reach * 0.15);
                    c.FillStyle = "#2e2925";
                    // hair over the crown
                    c.BeginPath();
                    c.MoveTo(-0.84 * r, -0.36 * r);
                    c.BezierCurveTo(-0.7 * r, -1.2 * r, 0.6 * r, -1.36 * r, 0.96 * r, -0.4 * r);
                    c.LineTo(0.5 * r, 0.3 * r);
                    c.BezierCurveTo(0.1 * r, -0.3 * r, -0.4 * r, -0.66 * r, -0.84 * r, -0.36 * r);
                    c.Fill();
                    strands(
                        c,
                        new[]
                        {
                            new[] { -0.6 * r, -0.8 * r, 0.9 * r, 0.2 * r },
                            new[] { -0.2 * r, -1.0 * r, 1.2 * r, 1.2 * r },
                            new[] { 0.3 * r, -1.1 * r, 1.0 * r, 2.4 * r },
                            new[] { -0.8 * r, 1.0 * r, -0.6 * r, 2.2 * r },
                            new[] { -0.4 * r, 1.0 * r, -0.2 * r, 2.3 * r },
                            new[] { 0, 1.0 * r, 0.2 * r, 2.1 * r },
                        },
                        "rgba(190, 180, 168, 0.28)",
                        0.06 * r);
                    // the crown of Sipylos: gold, dented, slipped askew — until he takes it off
                    c.Save();
                    if (!crowned) c.GlobalAlpha = 0;
                    c.Translate(0.05 * r, -0.98 * r);
                    c.Rotate(-0.22);
                    var gold = c.CreateLinearGradient(0, -0.5 * r, 0, 0.2 * r);
                    gold.AddColorStop(0, "#f1d27a");
                    gold.AddColorStop(1, "#8a6420");
                    c.FillStyle = gold;
                    c.BeginPath();
                    c.MoveTo(-0.78 * r, 0.12 * r);
                    for (int i = 0; i < 5; i++)
                    {
                        double px = -0.78 * r + (i + 0.5) * 0.32 * r;
                        c.LineTo(px - 0.1 * r, -0.02 * r);
                        c.LineTo(px, (i == 2 ? -0.5 : i == 3 ? -0.22 : -0.38) * r); // one point bent down
                        c.LineTo(px + 0.1 * r, -0.02 * r);
                    }
                    c.LineTo(0.84 * r, 0.12 * r);
                    c.LineTo(0.84 * r, 0.24 * r);
                    c.LineTo(-0.78 * r, 0.24 * r);
                    c.ClosePath();
                    c.Fill();
                    c.FillStyle = "#7a1b22";
                    c.BeginPath();
                    c.Arc(0, 0.12 * r, 0.07 * r, 0, Math.PI * 2);
                    c.Fill();
                    c.Restore();
                    c.Restore();

                    // ---- the near arm: reaching up for the figs — or down for the water
                    var sh = rot(-8.6 * s, -23.6 * s);
                    // at rest the hand lies on the water; it rises to the fruit, or dips to drink
                    var rest = up != 0 ? (x: -12 * s, y: pivot.y - 1 * s) : (x: -15 * s, y: sw - 1 * s);
                    double e = reach * reach * (3 - 2 * reach);
                    var target =
                        bend > 0.05
                            ? (x: -16 * s, y: sw + 1 * s)
                            : (x: rest.x + (-7 * s - rest.x) * e, y: rest.y + (by - y + 11 * s - rest.y) * e);
                    double L1 = 12.5 * s;
                    double L2 = 12 * s;
                    double dx2 = target.x - sh.x;
                    double dy2 = target.y - sh.y;
                    double d = Js.hypot(dx2, dy2);
                    if (d > L1 + L2 - 0.5 * s)
                    {
                        double k = (L1 + L2 - 0.5 * s) / d;
                        dx2 *= k;
                        dy2 *= k;
                        d = L1 + L2 - 0.5 * s;
                    }
                    var hand = (x: sh.x + dx2, y: sh.y + dy2);
                    // the elbow, bent out away from his body
                    double a = Math.Acos(Math.Max(-1, Math.Min(1, (L1 * L1 + d * d - L2 * L2) / (2 * L1 * d))));
                    double bse = Math.Atan2(dy2, dx2);
                    var el = (x: sh.x + Math.Cos(bse - a) * L1, y: sh.y + Math.Sin(bse - a) * L1);
                    limb(c, sh.x, sh.y, el.x, el.y, 2.3 * s, 1.5 * s, 0.4 * s, 0.4, skin(0.04));
                    limb(c, el.x, el.y, hand.x, hand.y, 1.6 * s, 1.1 * s, 0.5 * s, 0.3, skin(0.08));
                    // the knob of the elbow, the tendons of the forearm
                    c.FillStyle = skin(-0.04);
                    c.BeginPath();
                    c.Arc(el.x, el.y, 1.7 * s, 0, Math.PI * 2);
                    c.Fill();
                    // the hand: a claw of long thin fingers, spread for the fruit
                    double ha = Math.Atan2(hand.y - el.y, hand.x - el.x);
                    c.Save();
                    c.Translate(hand.x, hand.y);
                    c.Rotate(ha);
                    c.FillStyle = skin(0.04);
                    c.BeginPath();
                    c.Ellipse(0.8 * s, 0, 1.6 * s, 1.2 * s, 0, 0, Math.PI * 2);
                    c.Fill();
                    c.StrokeStyle = skin(0.06);
                    c.LineCap = LineCap.Round;
                    c.LineWidth = 0.6 * s;
                    c.BeginPath();
                    for (int i = 0; i < 4; i++)
                    {
                        double fa = (i - 1.5) * 0.26 * (0.6 + reach * 0.6);
                        c.MoveTo(2 * s, (i - 1.5) * 0.5 * s);
                        c.QuadraticCurveTo(
                            2 * s + Math.Cos(fa) * 2 * s,
                            (i - 1.5) * 0.5 * s + Math.Sin(fa) * 2 * s,
                            2 * s + Math.Cos(fa + 0.3) * 3.6 * s,
                            (i - 1.5) * 0.5 * s + Math.Sin(fa + 0.3) * 3.6 * s);
                    }
                    c.MoveTo(1 * s, -1 * s);
                    c.LineTo(2.6 * s, -2.4 * s);
                    c.Stroke();
                    c.Restore();
                });
            if (look.back) ctx.Restore();

            // ---- the near half of the water over his chest, and the near bank
            ctx.Save();
            var water = ctx.CreateLinearGradient(0, surf - 6 * s, 0, surf + 9 * s);
            if (look.mud)
            {
                // the pool has sunk away: black mud
                water.AddColorStop(0, "rgba(44, 34, 24, 0.97)");
                water.AddColorStop(1, "rgba(18, 13, 9, 0.99)");
            }
            else
            {
                water.AddColorStop(0, Col.Rgba(40, 58, 70, Css.fixedNum(0.94 - L.dim * 0.2, 3)));
                water.AddColorStop(1, "rgba(6, 10, 14, 0.99)");
            }
            ctx.FillStyle = water;
            ctx.BeginPath();
            ctx.Ellipse(x, surf, Ws, 9 * s * (Ws / W), 0, 0, Math.PI);
            ctx.Fill();
            // the wet rock between the sunk water and the rim
            if (bend > 0.05)
            {
                ctx.FillStyle = Col.Rgba(22, 20, 18, Css.fixedNum(0.9 * bend, 3));
                ctx.BeginPath();
                ctx.Ellipse(x, full, W, 9 * s, 0, 0, Math.PI);
                ctx.Ellipse(x, surf, Ws, 9 * s * (Ws / W), 0, Math.PI, 0, true);
                ctx.Fill("evenodd");
            }
            // the light along the water's edge
            ctx.StrokeStyle = rgba(L.color, 0.3 * L.strength + 0.08);
            ctx.LineWidth = 0.8 * s;
            ctx.BeginPath();
            ctx.Ellipse(x, surf, Ws, 9 * s * (Ws / W), 0, Math.PI * 1.15, Math.PI * 1.85);
            ctx.Stroke();
            // the crown he took off, on the bottom of the still water
            if (!look.crown && !look.mud)
            {
                ctx.FillStyle = "rgba(220, 180, 90, 0.35)";
                ctx.BeginPath();
                ctx.Ellipse(x + 9 * s, surf + 4 * s, 3 * s, 0.9 * s, 0, 0, Math.PI * 2);
                ctx.Fill();
            }
            // ripples spreading from him, because he never stops trembling — fewer once he is still
            int nRipples = look.mud ? 0 : calm ? 1 : 3;
            for (int i = 0; i < nRipples; i++)
            {
                double k = (((t * 0.35 + i / 3.0) % 1) + 1) % 1;
                ctx.StrokeStyle = Col.Rgba(170, 190, 200, Css.fixedNum(0.3 * (1 - k), 3));
                ctx.LineWidth = 0.6 * s;
                ctx.BeginPath();
                ctx.Ellipse(x - 2 * s, surf + 1 * s, (8 + k * 24) * s, (1.5 + k * 4) * s, 0, 0, Math.PI);
                ctx.Stroke();
            }
            rimStones(ctx, x, full, W, s, L, false);
            // reeds at the edge, dry and broken
            ctx.StrokeStyle = "#3b3320";
            ctx.LineWidth = 0.8 * s;
            ctx.BeginPath();
            for (int i = 0; i < 9; i++)
            {
                double rx = x - W - 2 * s + i * 1.8 * s + (i > 4 ? W * 1.9 : 0);
                double hgt = (10 + hh(i, 21) * 12) * s;
                double lean2 = (hh(i, 22) - 0.5) * 6 * s + Math.Sin(t * 1.3 + i) * 0.8 * s;
                ctx.MoveTo(rx, full + 4 * s);
                ctx.QuadraticCurveTo(rx + lean2 * 0.3, full - hgt * 0.5, rx + lean2, full + 4 * s - hgt);
            }
            ctx.Stroke();
            ctx.Restore();
        }

        /// <summary>the stones that ring the pool; the far ones behind him, the near ones in front</summary>
        static void rimStones(Ctx ctx, double x, double surf, double W, double s, FigLight L, bool far)
        {
            const int n = 16;
            for (int i = 0; i < n; i++)
            {
                double a = far ? Math.PI + ((double)i / (n - 1)) * Math.PI : ((double)i / (n - 1)) * Math.PI;
                double sx = x + Math.Cos(a) * (W + 2 * s);
                double sy = surf + Math.Sin(a) * (9 * s + 2 * s) + (far ? 0 : 2 * s);
                double rw = (3.6 + hh(i, far ? 31 : 41) * 3.4) * s * (far ? 0.85 : 1.15);
                double rh = rw * (0.55 + hh(i, 51) * 0.2);
                var g = ctx.CreateLinearGradient(sx, sy - rh, sx, sy + rh);
                double lit = 0.4 + 0.6 * L.strength * (1 - L.dim);
                g.AddColorStop(0, Col.Rgb(Js.ToInt32(70 + 50 * lit), Js.ToInt32(62 + 40 * lit), Js.ToInt32(54 + 30 * lit)));
                g.AddColorStop(1, "#1d1714");
                ctx.FillStyle = g;
                ctx.BeginPath();
                ctx.Ellipse(sx, sy, rw, rh, (hh(i, 61) - 0.5) * 0.6, 0, Math.PI * 2);
                ctx.Fill();
                // wet dark line where the stone meets the water
                ctx.StrokeStyle = "rgba(6, 8, 10, 0.5)";
                ctx.LineWidth = 0.6 * s;
                ctx.BeginPath();
                ctx.Ellipse(
                    sx,
                    sy,
                    rw,
                    rh,
                    0,
                    far ? 0.2 : Math.PI + 0.2,
                    far ? Math.PI - 0.2 : Math.PI * 2 - 0.2);
                ctx.Stroke();
            }
        }

        static readonly double[] figVeins = { -1.26, -0.63, 0, 0.63, 1.26 };

        /// <summary>one fig leaf: palmate, five rounded lobes, deep sinuses, veins</summary>
        static void figLeaf(Ctx ctx, double x, double y, double r, double a, Style col, Style vein)
        {
            ctx.Save();
            ctx.Translate(x, y);
            ctx.Rotate(a);
            ctx.FillStyle = col;
            ctx.BeginPath();
            const int N = 40;
            for (int i = 0; i <= N; i++)
            {
                double th = -Math.PI * 0.95 + ((double)i / N) * Math.PI * 1.9;
                // five lobes around the leaf, the middle one longest
                double lobe = Math.Pow(Math.Abs(Math.Cos(th * 2.5)), 0.6);
                double mid = 1 - (Math.Abs(th) / Math.PI) * 0.35;
                double rr = r * (0.35 + 0.65 * lobe) * mid;
                double px = Math.Cos(th) * rr + r * 0.25;
                double py = Math.Sin(th) * rr * 0.9;
                if (i != 0) ctx.LineTo(px, py);
                else ctx.MoveTo(px, py);
            }
            ctx.ClosePath();
            ctx.Fill();
            ctx.StrokeStyle = vein;
            ctx.LineWidth = Math.Max(0.3, r * 0.06);
            ctx.BeginPath();
            foreach (double th in figVeins)
            {
                ctx.MoveTo(r * 0.1, 0);
                ctx.LineTo(r * 0.25 + Math.Cos(th) * r * 0.75, Math.Sin(th) * r * 0.68);
            }
            ctx.Stroke();
            ctx.Restore();
        }

        static readonly double[][] figRoots =
        {
            new double[] { -6, 14, 2.4 },
            new double[] { 4, 12, 2 },
            new double[] { 10, 10, 1.6 },
            new double[] { -14, 10, 1.4 },
        };
        static readonly double[][] figTrunk =
        {
            new double[] { 44, 2, 6.5 },
            new double[] { 47, -14, 5.6 },
            new double[] { 44, -30, 5 },
            new double[] { 37, -44, 4.4 },
            new double[] { 31, -56, 3.8 },
            new double[] { 24, -68, 3.2 },
            new double[] { 18, -80, 2.4 },
        };
        static readonly string[] figGreens = { "#1b2a17", "#24381d", "#2f4a24", "#3d5c2c" };
        static readonly double[][] figFruit =
        {
            new double[] { -10, 4 },
            new double[] { -4, 3 },
            new double[] { -7, 9 },
            new double[] { -14, 8 },
            new double[] { -1, 8 },
        };

        /// <summary>
        /// The fig over the pool: a gnarled, leaning trunk with its bark, roots
        /// gripping the bank, a crown of broad lobed leaves lit from the sun's side,
        /// and one long branch bowed down over him with the fruit — the branch that
        /// lifts every time he reaches. `by` is where that branch's fruit hangs.
        /// `part`: "still" | "moving".
        /// </summary>
        static void figTreePart(Ctx ctx, double x, double y, double s, FigLight L, double by, string part)
        {
            bool still = part == "still";
            double t = L.t;
            ctx.Save();
            ctx.LineCap = LineCap.Round;
            ctx.LineJoin = LineJoin.Round;
            const string bark = "#2e231d";
            if (still)
            {
                // roots over the bank
                ctx.StrokeStyle = bark;
                foreach (var rt in figRoots)
                {
                    double dx = rt[0], len = rt[1], w = rt[2];
                    ctx.LineWidth = w * s;
                    ctx.BeginPath();
                    ctx.MoveTo(x + 44 * s, y - 2 * s);
                    ctx.QuadraticCurveTo(
                        x + (44 + dx * 0.5) * s,
                        y + 1 * s,
                        x + (44 + dx + (dx < 0 ? -len : len) * 0.4) * s,
                        y + 4 * s);
                    ctx.Stroke();
                }
                // the trunk: a tapering, twisting form, filled, not a stroke
                var tr = figTrunk;
                var left = new List<(double, double)>();
                var right = new List<(double, double)>();
                for (int i = 0; i < tr.Length; i++)
                {
                    double px = tr[i][0], py = tr[i][1], w = tr[i][2];
                    var q = tr[Math.Min(tr.Length - 1, i + 1)];
                    var o = tr[Math.Max(0, i - 1)];
                    double nx = -(q[1] - o[1]);
                    double ny = q[0] - o[0];
                    double m = Js.hypot(nx, ny);
                    if (m == 0 || double.IsNaN(m)) m = 1;
                    nx /= m;
                    ny /= m;
                    left.Add((x + (px + nx * w) * s, y + (py + ny * w) * s));
                    right.Add((x + (px - nx * w) * s, y + (py - ny * w) * s));
                }
                var tg = ctx.CreateLinearGradient(x + 36 * s, 0, x + 52 * s, 0);
                bool litL = L.lx < 0;
                Col lc = Col.Rgb(Js.ToInt32(78 + 40 * L.strength), Js.ToInt32(62 + 28 * L.strength), Js.ToInt32(50 + 16 * L.strength));
                tg.AddColorStop(0, litL ? lc : (Col)"#1d1612");
                tg.AddColorStop(1, litL ? (Col)"#1d1612" : lc);
                ctx.FillStyle = tg;
                ctx.BeginPath();
                for (int i = 0; i < left.Count; i++)
                {
                    var (px, py) = left[i];
                    if (i != 0) ctx.LineTo(px, py); else ctx.MoveTo(px, py);
                }
                for (int i = right.Count - 1; i >= 0; i--) ctx.LineTo(right[i].Item1, right[i].Item2);
                ctx.ClosePath();
                ctx.Fill();
                // bark: long twisting fissures and a knot
                ctx.StrokeStyle = "rgba(10, 6, 4, 0.55)";
                ctx.LineWidth = 0.6 * s;
                ctx.BeginPath();
                for (int k = -2; k <= 2; k++)
                {
                    for (int i = 0; i < tr.Length; i++)
                    {
                        double px = tr[i][0], py = tr[i][1], w = tr[i][2];
                        double xx = x + (px + (k / 2.5) * w * 0.8 + Math.Sin(i * 1.7 + k) * 0.6) * s;
                        double yy = y + py * s;
                        if (i != 0) ctx.LineTo(xx, yy);
                        else ctx.MoveTo(xx, yy);
                    }
                }
                ctx.Stroke();
                ctx.FillStyle = "rgba(10, 6, 4, 0.6)";
                ctx.BeginPath();
                ctx.Ellipse(x + 44 * s, y - 26 * s, 1.6 * s, 2.4 * s, 0.3, 0, Math.PI * 2);
                ctx.Fill();
            }
            // the branches
            void branch(double[][] pts, double w0, double w1)
            {
                for (int i = 0; i < pts.Length - 1; i++)
                {
                    ctx.StrokeStyle = bark;
                    ctx.LineWidth = (w0 + (w1 - w0) * ((double)i / (pts.Length - 1))) * s;
                    ctx.BeginPath();
                    ctx.MoveTo(pts[i][0], pts[i][1]);
                    ctx.LineTo(pts[i + 1][0], pts[i + 1][1]);
                    ctx.Stroke();
                }
            }
            const double sway = 0;
            if (still)
            {
                branch(
                    new[]
                    {
                        new[] { x + 37 * s, y - 44 * s },
                        new[] { x + 50 * s, y - 58 * s },
                        new[] { x + 58 * s + sway, y - 72 * s },
                        new[] { x + 62 * s + sway, y - 84 * s },
                    },
                    3,
                    1.2);
                branch(
                    new[]
                    {
                        new[] { x + 24 * s, y - 68 * s },
                        new[] { x + 30 * s, y - 84 * s },
                        new[] { x + 38 * s + sway, y - 94 * s },
                    },
                    2.4,
                    1);
            }
            // the one that bows over him, lifted when he reaches
            double tipX = x - 12 * s;
            if (!still)
                branch(
                    new[]
                    {
                        new[] { x + 22 * s, y - 72 * s },
                        new[] { x + 12 * s, (y - 78 * s + by) / 2 - 6 * s },
                        new[] { x + 0 * s, by - 6 * s },
                        new[] { tipX, by },
                    },
                    2.4,
                    0.9);
            // the crown: leaf clusters, shaded at the back, lit on the sun's side
            var greens = figGreens;
            double[][] clusters =
            {
                new double[] { 62, -88, 16 },
                new double[] { 40, -98, 14 },
                new double[] { 22, -90, 12 },
                new double[] { 52, -72, 11 },
                new double[] { 8, (by - y) / s - 10, 10 },
                new double[] { -10, (by - y) / s - 6, 8 },
            };
            for (int pass = 0; pass < 2; pass++)
            {
                for (int ci = 0; ci < clusters.Length; ci++)
                {
                    double cx = clusters[ci][0], cy = clusters[ci][1];
                    int n = (int)clusters[ci][2];
                    // the high crown is still and kept; the leaves on the low branch move with it
                    if ((ci < 4) != still) continue;
                    for (int i = 0; i < n; i++)
                    {
                        double a = hh(ci, i) * Math.PI * 2;
                        double d = Math.Sqrt(hh(ci + 7, i)) * (6 + n * 0.5) * s;
                        double lx = x + cx * s + Math.Cos(a) * d * 1.2 + (cy < -80 ? sway : 0);
                        double ly = y + cy * s + Math.Sin(a) * d * 0.7;
                        // the back pass is darker; leaves facing the light are brighter
                        double facing = -(Math.Cos(a) * L.lx + Math.Sin(a) * L.ly);
                        int tone =
                            pass == 0 ? 0 : (int)Math.Max(1, Math.Min(3, Js.round(1.5 + facing * 1.5 * L.strength)));
                        double r = (4.2 + hh(ci + 3, i) * 2.4) * s * (pass != 0 ? 1 : 1.15);
                        figLeaf(
                            ctx,
                            lx,
                            ly,
                            r,
                            a + hh(ci, i + 9) - 0.5 + Math.Sin(t * 1.1 + i) * 0.05,
                            greens[tone],
                            "rgba(170, 200, 120, 0.22)");
                    }
                }
            }
            if (!still)
            {
                // the figs, ripe to splitting, hung under the low branch
                foreach (var f in figFruit)
                {
                    double fx = f[0], fy = f[1];
                    double cx = x + fx * s;
                    double cy = by + fy * s;
                    var g = ctx.CreateRadialGradient(cx - 1.2 * s, cy - 1.2 * s, 0, cx, cy, 3.4 * s);
                    g.AddColorStop(0, "#c46a8c");
                    g.AddColorStop(0.55, "#7a2648");
                    g.AddColorStop(1, "#38102a");
                    ctx.FillStyle = g;
                    ctx.BeginPath();
                    // pear-shaped, narrowing to the stalk
                    ctx.MoveTo(cx, cy - 3.6 * s);
                    ctx.BezierCurveTo(cx + 2.8 * s, cy - 2 * s, cx + 3.4 * s, cy + 2.6 * s, cx, cy + 3 * s);
                    ctx.BezierCurveTo(cx - 3.4 * s, cy + 2.6 * s, cx - 2.8 * s, cy - 2 * s, cx, cy - 3.6 * s);
                    ctx.Fill();
                    ctx.FillStyle = "rgba(255, 220, 230, 0.35)";
                    ctx.BeginPath();
                    ctx.Ellipse(cx - 1 * s, cy - 0.6 * s, 0.7 * s, 1.1 * s, -0.4, 0, Math.PI * 2);
                    ctx.Fill();
                }
            }
            ctx.Restore();
        }

        static Surface treeCacheCv;
        static string treeCacheKey = "";

        /// <summary>
        /// The fig over the pool. Its trunk and high crown — a hundred-odd lobed
        /// leaves — do not move, so they are painted once into a picture and only
        /// painted again when the light on them changes; the low branch, its leaves
        /// and the fruit, which lift every time he reaches, are drawn live.
        /// </summary>
        static void figTree(Ctx ctx, double x, double y, double s, FigLight L, double by)
        {
            var box = new Box(-40 * s, -122 * s, 92 * s, 12 * s);
            string key = Js.toFixed(s, 3) + "|" + (L.lx < 0 ? 1 : 0) + "|" + Js.str(Js.round(L.strength * 10) + 0.0) + "|" +
                Js.str(Js.round(L.dim * 10) + 0.0) + "|" + Js.str(Js.round(L.lx * 4) + 0.0);
            int W = (int)Math.Ceiling((box.r - box.l) * R);
            int H = (int)Math.Ceiling((box.b - box.t) * R);
            if (treeCacheKey != key || treeCacheCv == null)
            {
                if (treeCacheCv == null) treeCacheCv = new Surface(W, H, true, "fig:tree");
                var cv = treeCacheCv;
                cv.SetSize(W, H);
                var c = cv.GetContext();
                c.SetTransform(R, 0, 0, R, -box.l * R, -box.t * R);
                figTreePart(c, 0, 0, s, L, by, "still");
                treeCacheKey = key;
            }
            ctx.DrawImage(treeCacheCv, x + box.l, y + box.t, box.r - box.l, box.b - box.t);
            figTreePart(ctx, x, y, s, L, by, "moving");
        }

        /// <summary>
        /// PORT: test hook (not in the TS): forget the cached tree picture and the scratch canvases,
        /// so a render starts from the same state as a fresh page.
        /// </summary>
        public static void ResetCaches()
        {
            treeCacheCv = null;
            treeCacheKey = "";
            pool.Clear();
        }
    }
}
