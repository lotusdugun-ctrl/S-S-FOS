// Ported from src/game/engine.ts, lines 1787-3327 (part B): the frame, the camera, the sky,
// the aphorism, the clouds and the distant mountain.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Sisifos.Paint;

namespace Sisifos.Game
{
    public partial class SisyphusEngine
    {
        // the world-to-screen mappings handed to the scene's passes; they read only fields,
        // so one pair serves every frame (no closures allocated per frame)
        private Func<double, double> toScreenXFn;
        private Func<double, double> toScreenYFn;

        private static readonly string[] BLOOM_PAL =
        {
            "oklch(0.6 0.22 28)",
            "oklch(0.86 0.15 95)",
            "oklch(0.95 0.02 90)",
            "oklch(0.62 0.14 310)",
        };

        /// <summary>JS `a || b` for numbers: 0 and NaN are falsy</summary>
        private static double orNum(double a, double b)
        {
            return a != 0 && !double.IsNaN(a) ? a : b;
        }

        // ---------- rendering ----------
        private void render()
        {
            Ctx ctx = this.ctx;
            Level L = this.level;
            double w = this.w, h = this.h;
            // no clear: the sky is opaque and covers the frame before anything else lands

            this.scale = Math.Min(1, w / 900) * 0.92 + 0.28;
            // what was on the ridges last frame may be heard now (Engine.Soundscape)
            this.hearSwap();
            this.soundscape();
            this.groundY = h * 0.78;
            // a jolt is a quick, damped sway — not noise, which jitters every frame
            this.shakeY = this.shake * Math.Sin(this.t * 31) * 4.5;

            // camera keeps the action (boulder or kick midpoint) slightly left of centre,
            // smoothed so the rejoin of character and boulder doesn't jump
            // through a throw it eases toward the midpoint of him and the stone, and
            // back once he has caught up — switched at once, the frame lurched back
            // and then forward, and the whole sky seemed to hop
            this.kickBlend +=
                ((this.kickT >= 0 ? 1 : 0) - this.kickBlend) * Math.Min(1, this.frameDt * 2.5);
            double camTargetX =
                this.phase == "descent"
                    ? this.walkX + 40
                    : this.x - ((this.x - this.charX) / 2) * this.kickBlend;
            double desiredCamX = camTargetX - (w * 0.38) / this.scale;
            // in a scene the camera takes in whoever is speaking too, even one standing
            // off the edge of the frame: it moves to between the two of them, unhurried
            double? sw = this.cutscene ? this.speakerWorldX() : null;
            // Half way between them was not enough: with the man far off, the one speaking
            // stood at the very edge and the push-in on their face found only sky or hill.
            // The frame now goes to the speaker; the push-in is on their face anyway.
            if (sw.HasValue) desiredCamX = sw.Value - (w * 0.5) / this.scale;
            this.camX += (desiredCamX - this.camX) * Math.Min(1, this.frameDt * (this.cutscene ? 2.5 : 12));
            // a camera held by hand, not mounted: a slow, small drift that never settles
            double driftX =
                (Math.Sin(this.t * 0.31) * 1.6 + Math.Sin(this.t * 0.73 + 1.1) * 0.7) / this.scale;
            this.camX += driftX;
            this.shakeY += Math.Sin(this.t * 0.27 + 2) * 1.1 + Math.Sin(this.t * 0.61) * 0.5;
            this.camY = Levels.terrainAt(L, this.camX);
            // The height the frame is held at is the hill's at its left edge, so on a slope
            // whoever stood mid-frame stood high: Prometheus on his rock had his face above
            // the top of the picture, and a zoom cannot show past it. While one of them
            // speaks the frame comes up the hill to their feet, unhurried.
            this.camLift += ((sw.HasValue ? 1 : 0) - this.camLift) * Math.Min(1, this.frameDt * 2.2);
            if (sw.HasValue) this.camLiftTo = Levels.terrainAt(L, sw.Value);
            // ...but only so far: lifted a long way on a steep slope, the far ridges sank under
            // the horizon and the plain behind them showed as a flat band (at most an eighth
            // of the frame; the zoom's pan does the rest)
            if (this.camLift > 0.001)
            {
                double most = h * 0.125 / (this.scale * 0.55);
                double d = Math.Max(-most, Math.Min(most, this.camLiftTo - this.camY));
                this.camY += d * this.camLift;
            }

            if (toScreenXFn == null) toScreenXFn = wx => (wx - this.camX) * this.scale;
            if (toScreenYFn == null)
                toScreenYFn = wy => this.groundY - (wy - this.camY) * this.scale * 0.55 + this.shakeY;
            Func<double, double> toScreenX = toScreenXFn;
            Func<double, double> toScreenY = toScreenYFn;

            // the camera move for this frame (see `cinematic`), applied as one transform
            // over everything in the world; the interface on top is drawn without it
            double z = this.cameraMove();
            double zfx = this.focusX;
            double zfy = this.focusY;
            double dpr = this.dpr;
            // A zoom about a point keeps that point where it is on screen, so a scene's
            // speaker standing at the edge stayed at the edge, small. In a scene the
            // camera now also pans, bringing whoever speaks toward the middle — as far
            // as the picture allows, never showing past its edges.
            double tx = zfx * (1 - z) + this.frameOffX, ty = zfy * (1 - z) + this.frameOffY;
            tx = Math.Max(w * (1 - z), Math.Min(0, tx));
            ty = Math.Max(h * (1 - z), Math.Min(0, ty));
            void zoomOn(Ctx c)
            {
                c.SetTransform(dpr * z, 0, 0, dpr * z, dpr * (tx + this.shakeX), dpr * ty);
            }
            void zoomOff(Ctx c) { c.SetTransform(dpr, 0, 0, dpr, 0, 0); }
            bool zoomed = z != 1 || this.shakeX != 0;
            // the sky takes the camera's slow moves but not the jolts of the stone:
            // shaken with the hill, the clouds read as hopping
            if (z != 1)
                this.ctx.SetTransform(dpr * z, 0, 0, dpr * z, dpr * tx, dpr * ty);

            this.st = Sky.skyState(this.hour, this.moonPhase());
            this.drawSky();
            this.drawCelestial();
            // the moon behind the clouds: they are solid enough now to pass in front of it
            this.drawMoonNow();
            this.drawClouds();
            this.drawOvercast();
            this.drawPegasus();
            if (zoomed) zoomOn(this.ctx);

            /*
             * The landscape goes into its own layer, so that the hour can grade it —
             * night blue, dawn rose, the flat light of noon — without grading the sky,
             * which is already the colour it should be. On slow devices there is no
             * layer and the grade goes straight over the frame.
             */
            Ctx mainCtx = this.ctx;
            Ctx land = null;
            if (!this.lowQuality)
            {
                int pw = (int)Math.Ceiling(w * this.dpr);
                int ph = (int)Math.Ceiling(h * this.dpr);
                Surface lc = this.landLayer ?? new Surface(pw, ph, true, "land");
                if (lc.width != pw || lc.height != ph)
                {
                    lc.SetSize(pw, ph);
                }
                this.landLayer = lc;
                land = lc.GetContext();
                if (land != null)
                {
                    land.SetTransform(1, 0, 0, 1, 0, 0);
                    land.GlobalCompositeOperation = Composite.SourceOver;
                    land.GlobalAlpha = 1;
                    land.ClearRect(0, 0, pw, ph);
                    land.SetTransform(this.dpr, 0, 0, this.dpr, 0, 0);
                    if (zoomed) zoomOn(land);
                    this.ctx = land;
                    ctx = land;
                }
            }
            this.drawMountain();
            this.renderBirds();
            this.drawMotes();

            // Parallax ridges. These are landforms in front of the horizon, so they have
            // to stay under it: at RIDGE_RELIEF 0.4 the nearest rank sampled terrain
            // 2100 units ahead and crested at 0.35h, two hundred pixels above the sun,
            // burying it for most of the climb. No amount of recolouring fixes a mass
            // that is simply standing in front of the light.
            double horizonY = this.st.horizon * h;
            int toneCount = System.Linq.Enumerable.Count(RIDGE_TONES);
            Func<double, double> lifeRidge1 = null;
            for (int i = 0; i < L.ridges.Length; i++)
            {
                var (amp, freq, off) = L.ridges[i];
                double p = 0.15 + i * 0.16;
                ctx.BeginPath();
                ctx.MoveTo(0, h);
                for (double sx0 = 0; sx0 <= w; sx0 += 8)
                {
                    double wx = this.camX * p + sx0 / this.scale + off;
                    double relief =
                        (Levels.terrainAt(L, wx) * 0.5 + Math.Sin(wx * freq) * amp - this.camY * p) *
                        this.scale *
                        RIDGE_RELIEF;
                    double raw = this.groundY - relief - 18 * i;
                    double floor = horizonY + (4 + 6 * i) * this.scale;
                    // Anything that would breach the skyline gets eased back under it rather
                    // than sheared off at it — a hard clamp would leave a ruled flat line
                    // along the crests wherever the wave went over.
                    double y = raw >= floor ? raw : floor - (floor - raw) * 0.25;
                    ctx.LineTo(sx0, y);
                }
                ctx.LineTo(w, h);
                ctx.ClosePath();
                // Atmospheric perspective: the air between you and a ridge drains its
                // warmth and lifts it. Nearest the sun that haze is gold, not blue.
                ctx.FillStyle = i < toneCount ? RIDGE_TONES[i] : RIDGE_TONES[toneCount - 1];
                ctx.Fill();
                // the next two ridges carry the people who live here
                if (i == 1 || i == 2)
                {
                    int ri2 = i;
                    double lifeY(double sx0)
                    {
                        double wx = this.camX * p + sx0 / this.scale + off;
                        double relief =
                            (Levels.terrainAt(L, wx) * 0.5 + Math.Sin(wx * freq) * amp - this.camY * p) *
                            this.scale *
                            RIDGE_RELIEF;
                        double raw = this.groundY - relief - 18 * ri2;
                        double floor = horizonY + (4 + 6 * ri2) * this.scale;
                        return raw >= floor ? raw : floor - (floor - raw) * 0.25;
                    }
                    if (i == 2) this.drawRidgeLife(ctx, lifeY, p, i);
                    else lifeRidge1 = lifeY;
                }
                // the two far ridges carry figures from the old stories
                if (i <= 1)
                {
                    int ri = i;
                    double ridgeY(double sx0)
                    {
                        double wx = this.camX * p + sx0 / this.scale + off;
                        double relief =
                            (Levels.terrainAt(L, wx) * 0.5 + Math.Sin(wx * freq) * amp - this.camY * p) *
                            this.scale *
                            RIDGE_RELIEF;
                        double raw = this.groundY - relief - 18 * ri;
                        double floor = horizonY + (4 + 6 * ri) * this.scale;
                        return raw >= floor ? raw : floor - (floor - raw) * 0.25;
                    }
                    this.drawLandmark(ctx, ridgeY, p, off, ri);
                    if (i == 1 && lifeRidge1 != null) this.drawRidgeLife(ctx, lifeRidge1, p, 1);
                }
            }

            // Main terrain. The same curve used to be resolved three times over — once
            // for the fill, once for the worn trail, once for the ridgeline — at four
            // pixels a step, so a wide window was paying for fifteen hundred evaluations
            // of a power and two sines to draw one line three times. Sampled once here.
            int tn = (int)Math.Floor((w + 4) / 4) + 2;
            if (this.terrainYs == null || this.terrainYs.Length != tn) this.terrainYs = new double[tn];
            double[] tys = this.terrainYs;
            double tsx(int i) { return Math.Min(-2 + i * 4, w + 2); }
            for (int i = 0; i < tn; i++)
            {
                tys[i] = toScreenY(Levels.terrainAt(L, this.camX + tsx(i) / this.scale));
            }

            ctx.BeginPath();
            ctx.MoveTo(-2, h + 2);
            for (int i = 0; i < tn; i++) ctx.LineTo(tsx(i), tys[i]);
            ctx.LineTo(w + 2, h + 2);
            ctx.ClosePath();
            var groundG = ctx.CreateLinearGradient(0, this.groundY - 120, 0, h);
            // the face goes cool and dark toward us: lit by the sky, not the sun
            groundG.AddColorStop(0, "oklch(0.36 0.05 54)");
            groundG.AddColorStop(0.45, "oklch(0.28 0.042 48)");
            groundG.AddColorStop(1, "oklch(0.17 0.03 300)");
            ctx.FillStyle = groundG;
            ctx.Fill();

            // the fill is one flat wash over the biggest area in the frame; this is what
            // turns it into rock. Clipped to the same path, so nothing can spill upward
            // into the sky.
            ctx.Save();
            ctx.Clip();
            this.drawGroundDetail(toScreenX, toScreenY);
            ctx.Restore();

            // lighter dirt trail worn by the endless rolling boulder
            ctx.StrokeStyle = "oklch(0.4 0.05 58 / 0.35)";
            ctx.LineWidth = 24 * this.scale;
            ctx.LineCap = LineCap.Round;
            ctx.BeginPath();
            for (int i = 0; i < tn; i++)
            {
                if (i == 0) ctx.MoveTo(tsx(i), tys[i] - 1);
                else ctx.LineTo(tsx(i), tys[i] - 1);
            }
            ctx.Stroke();

            // sunlit ridgeline
            ctx.StrokeStyle = "oklch(0.66 0.06 62 / 0.7)";
            ctx.LineWidth = 2;
            ctx.BeginPath();
            for (int i = 0; i < tn; i++)
            {
                if (i == 0) ctx.MoveTo(tsx(i), tys[i]);
                else ctx.LineTo(tsx(i), tys[i]);
            }
            ctx.Stroke();

            /*
             * Outcrops go on last of the three, and unclipped.
             *
             * Their whole job is to break the skyline — the high-contrast edge the eye
             * reads the hill's shape from — so clipping them to the ground would sand
             * them flat against it. And drawn before the trail, as they were at first,
             * the twenty-four-pixel trail stroke painted straight over the top of them
             * and they simply vanished.
             */
            this.drawOutcrops(toScreenX, toScreenY);

            // dry grass along the skyline, leaning with the wind, seed heads lit
            // from behind
            double grassStep = this.lowQuality ? 36 : 18;
            for (
                double wx = Math.Floor(this.camX / grassStep) * grassStep;
                wx < this.camX + w / this.scale + grassStep;
                wx += grassStep)
            {
                double h1 = this.hash(wx * 1.31);
                if (h1 < 0.38) continue;
                double gwx = wx + (h1 - 0.5) * grassStep;
                Flora.grassClump(
                    ctx,
                    toScreenX(gwx),
                    toScreenY(Levels.terrainAt(L, gwx)) + 1,
                    (5 + h1 * 7) * this.scale,
                    Math.Floor(wx),
                    this.windAt(gwx),
                    "oklch(0.2 0.03 60 / 0.9)",
                    "rgba(255, 210, 140, 0.75)");
                // spring: when he has climbed well for a while the hill flowers
                if (this.bloom > 0.04)
                {
                    double h2 = this.hash(wx * 2.77 + 0.5);
                    if (h2 < this.bloom * 0.75)
                    {
                        double fx = toScreenX(gwx) + (h2 - 0.3) * 6 * this.scale;
                        double fy = toScreenY(Levels.terrainAt(L, gwx)) - (4 + h1 * 6) * this.scale;
                        string[] pal = BLOOM_PAL;
                        ctx.FillStyle = pal[(int)Math.Floor(this.hash(wx * 5.3) * pal.Length)];
                        double r = (1.3 + h2 * 1.4) * this.scale * Math.Min(1, this.bloom * 1.6);
                        ctx.BeginPath();
                        ctx.Arc(fx, fy, r, 0, Math.PI * 2);
                        ctx.Fill();
                    }
                }
            }

            // summit marker
            double sumX = toScreenX(L.length);
            if (sumX > -60 && sumX < w + 60)
            {
                double sy = toScreenY(Levels.terrainAt(L, L.length));
                ctx.StrokeStyle = "oklch(0.78 0.11 70 / 0.65)";
                ctx.LineWidth = 2;
                ctx.BeginPath();
                ctx.MoveTo(sumX, sy);
                ctx.LineTo(sumX, sy - 70 * this.scale);
                ctx.Stroke();
            }

            // Mediterranean vegetation + scattered stones on the trail
            this.drawStones(toScreenX, toScreenY);
            this.drawOliveTrees(toScreenX, toScreenY);
            this.drawSteles(toScreenX, toScreenY);
            this.drawEchoes(toScreenX, toScreenY);
            this.drawNpcs(toScreenX, toScreenY);
            this.drawWedge(toScreenX, toScreenY);

            // the storm and the god come in over everything behind the man
            this.drawZeus(toScreenX, toScreenY);

            // boulder + figure
            double R = 34 * this.scale;
            double sx = this.x + this.creep;
            double bx = toScreenX(sx);
            double by = toScreenY(Levels.terrainAt(L, sx)) - this.hop * R * 1.4;
            double ang = Levels.slopeAt(L, this.x) * 0.5;

            // once the stone is gone he stays where it left him, at the top
            bool left = this.phase == "rolling" || this.phase == "descent";
            double fwx =
                this.phase == "descent"
                    ? this.walkX
                    : left
                        ? this.summitX
                        : this.kickT >= 0
                            ? this.charX
                            : this.setT < SET_TIME
                                ? // stepping back from the stone into his stance
                                  this.setFrom +
                                  (sx - 64 - this.setFrom) *
                                      (1 - Math.Pow(1 - Math.Min(1, this.setT / SET_TIME), 2.2))
                                : sx - 64;
            // long shadows thrown toward us by the low sun behind the hill
            if (this.zeus.state == "gone")
            {
                this.drawLongShadow(
                    toScreenX(fwx),
                    toScreenY(Levels.terrainAt(L, fwx)),
                    9 * this.scale,
                    120 * this.scale);
                this.drawLongShadow(bx, by, R * 0.9, 170 * this.scale);
            }
            // figure stands downhill of the boulder, feet on the real terrain
            double fxs = toScreenX(fwx);
            // in the mire his feet sink
            double fys = toScreenY(Levels.terrainAt(L, fwx)) + (this.inMire(fwx + 40) ? 3.5 * this.scale : 0);
            // what the camera holds on next frame: him, a little above his shoulders,
            // and at the summit high enough to keep the god in the frame
            this.focusTX = fxs + (this.phase == "summit" ? 60 * this.scale : 20 * this.scale);
            this.focusTY =
                this.phase == "summit"
                    ? this.epiphany
                        ? fys - 92 * this.scale
                        : fys - 150 * this.scale
                    : fys - 70 * this.scale;
            if (this.epiphany && this.phase == "summit") this.focusTX = fxs + 8 * this.scale;
            // a scene: frame the two of them; the ending: his face
            NpcPosT other = null;
            if (!string.IsNullOrEmpty(this.speaker) && this.speaker != "sis")
                this.npcFace.TryGetValue(this.speaker, out other);
            NpcPosT lastOther = other;
            if (lastOther == null) this.npcPos.TryGetValue("merope", out lastOther);
            if (this.cutscene && lastOther != null)
            {
                // in a scene the camera goes to whoever is speaking: to her (or him) while
                // they talk, back to the man when he answers, and wide on the two of them
                // while the story tells it
                if (other != null)
                {
                    this.focusTX = other.x;
                    this.focusTY = other.y;
                }
                else if (this.speaker == "sis")
                {
                    this.focusTX = fxs;
                    this.focusTY = fys - 80 * this.scale;
                }
                else
                {
                    this.focusTX = (fxs + lastOther.x) / 2;
                    this.focusTY = Math.Min(fys - 70 * this.scale, lastOther.y);
                }
            }
            if (this.finaleOn)
            {
                this.focusTX = fxs + 10 * this.scale;
                this.focusTY = fys - 88 * this.scale;
            }
            void drawHim()
            {
                this.drawFigureLit(fxs, fys, this.scale, left ? Levels.slopeAt(L, fwx) * 0.5 : ang, bx, by - R, R);
            }
            // at the foot of the hill he turns from the way down to the stone: the
            // figure narrows through the turn, still facing down, and opens out
            // facing up the hill — a turn, not a cut
            // At the stone he turns round where it hides him — behind it, at its
            // middle — and steps back from it into his stance. (Squeezing the figure
            // flat through the turn read as a cardboard cut-out spinning on a pin, and
            // a dissolve between the two poses as a double exposure.)
            // once the stone is rolling back it passes in front of him, on the near
            // side of the path, instead of through his body
            if (left) drawHim();
            this.drawContactShadow(bx, by, R, ang);
            this.drawBoulder(bx, by - R, R, ang);
            this.drawWhispers(bx, by - R, R);
            if (!left) drawHim();
            // what she asked for, in his hand on the way down
            string item = this.saga.s.carrying;
            if (!string.IsNullOrEmpty(item) && this.phase == "descent")
            {
                Npcs.drawItem(ctx, fxs - 8 * this.scale, fys - 28 * this.scale, this.scale, item, this.t);
            }
            this.headY = fys - 84 * this.scale;
            this.headX = fxs;
            // the man and his stone on screen, so the saying in the sky keeps off them
            // PORT: the TS assigned a fresh object each frame; the same values are written
            // into the one box (nothing holds on to the old one)
            if (this.manBox == null) this.manBox = new ManBoxT();
            this.manBox.l = Math.Min(fxs - 46 * this.scale, bx - R);
            this.manBox.r = Math.Max(fxs + 46 * this.scale, bx + R);
            this.manBox.top = Math.Min(this.headY, by - 2 * R);
            this.drawBolt(bx, by - R);
            this.renderParticles();
            this.drawCreatures(toScreenX, toScreenY);
            this.drawSeason();
            this.drawForeground();

            // the hour's grade over the landscape, then the landscape over the sky
            SkyState st = this.st;
            if (land != null && this.landLayer != null)
            {
                /*
                 * The hour's grade, spring's warmth, winter's frost and the wet of rain
                 * are all flat colours laid over the land, one after another. Laid one
                 * after another they cost a full-screen pass each; composed here into
                 * the single colour they add up to, they cost one.
                 */
                // "over", applied in order, on premultiplied colour
                double r = 0;
                double g = 0;
                double b = 0;
                double a = 0;
                int tints = 0;
                void tint(double tr, double tg, double tb, double ta)
                {
                    tints++;
                    r = tr * ta + r * (1 - ta);
                    g = tg * ta + g * (1 - ta);
                    b = tb * ta + b * (1 - ta);
                    a = ta + a * (1 - ta);
                }
                if (st.gradeA > 0.005) tint(st.grade[0], st.grade[1], st.grade[2], st.gradeA);
                if (this.bloom > 0.02) tint(255, 196, 120, 0.07 * this.bloom);
                if (this.saga.winter && (this.weather == "rain" || this.weather == "storm"))
                    tint(222, 232, 248, 0.1 * this.wxFade);
                // wet ground is darker and cooler — and deeper in colour, not greyer
                double wet =
                    (this.weather == "storm" ? 0.16 : this.weather == "rain" ? 0.1 : 0) * this.wxFade;
                if (wet > 0) tint(24, 46, 58, wet);
                if (tints > 0)
                {
                    if (a > 0.003)
                    {
                        land.Save();
                        land.GlobalCompositeOperation = Composite.SourceAtop;
                        land.FillStyle = Col.Rgba(Js.round(r / a), Js.round(g / a), Js.round(b / a), Css.fixedNum(a, 3));
                        land.FillRect(-w, -h, w * 3, h * 3);
                        land.Restore();
                    }
                }
                this.ctx = mainCtx;
                ctx = mainCtx;
                if (zoomed) zoomOff(ctx);
                ctx.DrawImage(this.landLayer, 0, 0, w, h);
                this.vibrance(ctx);
            }
            else if (st.gradeA > 0.005)
            {
                ctx.FillStyle = Css.rgb(st.grade, st.gradeA * 0.85);
                ctx.FillRect(0, 0, w, h);
            }
            if (zoomed) zoomOff(ctx);
            this.drawWeather();
            // Tantalus's thirst: the edges of the world dry out and shimmer
            if (this.saga.s.burden == "tan" && this.phase == "playing")
            {
                double a = 0.16 + Math.Sin(this.t * 1.7) * 0.05;
                var hv = ctx.CreateRadialGradient(w / 2, h / 2, h * 0.35, w / 2, h / 2, h * 0.9);
                hv.AddColorStop(0, "rgba(200, 120, 40, 0)");
                hv.AddColorStop(1, Col.Rgba(200, 120, 40, Css.fixedNum(a, 3)));
                ctx.FillStyle = hv;
                ctx.FillRect(0, 0, w, h);
            }
            this.drawAphorism();
            // the ring is interface, but it has to sit over him where the camera put him
            double ringY = fys - 112 * this.scale;
            if (!this.cutscene && !this.finaleOn)
                this.drawBreathRing(tx + fxs * z + this.shakeX, ty + ringY * z);

            // vignette: the scene sinks into the level's own fog colour, not into black.
            // Black would drain the gold straight back out of the corners.
            var fog = ctx.CreateRadialGradient(w / 2, h * 0.55, h * 0.2, w / 2, h * 0.55, h * 0.95);
            fog.AddColorStop(0, "rgba(0,0,0,0)");
            fog.AddColorStop(1, L.fog);
            ctx.Save();
            ctx.GlobalAlpha = 0.24;
            ctx.FillStyle = fog;
            ctx.FillRect(0, 0, w, h);
            ctx.Restore();

            // lightning flash
            if (this.flash > 0)
            {
                ctx.FillStyle = Col.Rgba(255, 250, 235, this.flash * 0.55);
                ctx.FillRect(0, 0, w, h);
            }
            // the last frame of the old climb, dissolving into the new one
            if (this.fadeCv != null && this.fadeT < CROSSFADE)
            {
                double k = this.fadeT / CROSSFADE;
                ctx.Save();
                ctx.SetTransform(1, 0, 0, 1, 0, 0);
                ctx.GlobalAlpha = 1 - k * k * (3 - 2 * k);
                ctx.DrawImage(this.fadeCv, 0, 0);
                ctx.Restore();
            }
            this.camX -= driftX;
        }

        /// <summary>
        /// Where the camera wants to be this frame, eased toward smoothly: returns the
        /// zoom, and moves the focus point it zooms about.
        /// </summary>
        private double cameraMove()
        {
            double dt = this.frameDt;
            double w = this.w, h = this.h;
            if (this.focusX < 0)
            {
                this.focusX = orNum(this.focusTX, w * 0.4);
                this.focusY = orNum(this.focusTY, h * 0.6);
            }
            // how badly he is losing: pushing and getting nowhere, slipping, or spent
            double strain = 0;
            if (this.phase == "playing" && this.kickT < 0)
            {
                double stuck =
                    Math.Max(0, Math.Min(1, this.input)) * (1 - Math.Min(1, Math.Abs(this.vx) / 95));
                double spent = this.calm ? 0 : Math.Max(0, Math.Min(1, (0.4 - this.breath) / 0.4));
                strain = Math.Max(Math.Max(stuck * 0.75, this.slipT > 0 ? 1 : 0), spent * 0.8);
            }
            // the push-in is slow: a camera creeping closer, never a snap
            this.strainS += (strain - this.strainS) * Math.Min(1, dt * 0.9);
            double target = this.finaleOn
                ? 1 + 1.4 * Math.Min(1, this.finaleT / 4)
                : this.cutscene
                    ? (string.IsNullOrEmpty(this.speaker) || this.speaker == "narr" ? 1.45 : 1.75)
                    : this.phase == "playing"
                        ? 1 + 0.15 * this.strainS
                        : this.phase == "summit"
                            ? this.epiphany
                                ? 1 +
                                  1.5 *
                                      Math.Min(1, Math.Max(0, (this.phaseT - 0.6) / 3)) *
                                      Math.Max(0, Math.Min(1, (EPIPHANY_STRIKE - 0.4 - this.phaseT) / 1.2))
                                : 1 + 0.17 * Math.Min(1, this.phaseT / 1.4)
                            : this.phase == "rolling"
                                ? 1 + 0.12 * Math.Max(0, 1 - this.phaseT / 1.6)
                                : 1;
            double k = Math.Min(1, dt * (this.phase == "summit" ? 1.6 : 1.1));
            this.zoom += (target - this.zoom) * k;
            this.kickWant *= Math.Exp(-dt * 2.2);
            this.zoomKick += (this.kickWant - this.zoomKick) * Math.Min(1, dt * 7);
            this.focusX += (this.focusTX - this.focusX) * Math.Min(1, dt * 3);
            // in a scene, the speaker brought toward the middle of the frame (see render)
            bool speaking = this.cutscene && !string.IsNullOrEmpty(this.speaker) && this.speaker != "narr";
            double wantOffX = speaking ? (w * 0.5 - this.focusX) : 0;
            // the caption takes the lower part of the screen in a scene: the face goes to
            // the middle of the picture that is left above it
            double wantOffY = speaking ? (h * 0.36 - this.focusY) : 0;
            this.frameOffX += (wantOffX - this.frameOffX) * Math.Min(1, dt * 2.2);
            this.frameOffY += (wantOffY - this.frameOffY) * Math.Min(1, dt * 2.2);
            this.focusY += (this.focusTY - this.focusY) * Math.Min(1, dt * 3);
            this.focusX = Math.Max(0, Math.Min(w, this.focusX));
            this.focusY = Math.Max(0, Math.Min(h, this.focusY));
            // the stone's impacts shake the frame sideways as well as up and down
            this.shakeX = this.cinematic ? this.shake * Math.Sin(this.t * 27 + 1.3) * 4 : 0;
            if (!this.cinematic) return 1;
            return Math.Max(1, this.zoom + this.zoomKick);
        }

        /// <summary>the sun in screen space</summary>
        private (double x, double y) sunScreen()
        {
            return (this.w * this.st.sunX, this.h * this.st.sunY);
        }

        /// <summary>what lightScreen() returns</summary>
        public struct LightScreenT
        {
            public double x, y;
            public RGB color;
            public double strength, elev;
        }

        /// <summary>
        /// The light on the scene, in screen space: the sun, or by night the moon.
        /// Shadows, rim lights and the lit band on the hill all take their direction,
        /// colour and strength from this.
        /// </summary>
        private LightScreenT lightScreen()
        {
            var l = this.st.light;
            return new LightScreenT { x = this.w * l.x, y = this.h * l.y, color = l.color, strength = l.strength, elev = l.elev };
        }

        /// <summary>
        /// The sky's gradient, glow, afterglow and disc depend only on the hour and
        /// the viewport. Painted into an offscreen canvas and only repainted when the
        /// hour has moved on enough to show, or the window changes.
        /// </summary>
        /// <summary>
        /// The sky for the minute of the day the hour is in, and for the next, painted
        /// once each. The hour runs on continuously but the sky used to be repainted only
        /// as each minute came round, so on the way down — sunset, a few minutes of it a
        /// second — the colours jumped in steps; now the next minute's sky is blended in
        /// over this one's as the minute passes (drawSky). A repaint reuses its surface
        /// instead of allocating a new one each time.
        /// </summary>
        private (Surface a, Surface b, double f) skyLayers()
        {
            double w = this.w, h = this.h;
            string bas = Js.str(w) + "x" + Js.str(h) + "@" + Js.str(this.dpr) + ":" + Js.toFixed(this.scale, 2) + ":";
            double mins = ((this.st.hour % 24) + 24) % 24 * 60;
            int m = (int)Math.Floor(mins);
            double f = mins - m;
            string keyA = bas + m;
            string keyB = bas + ((m + 1) % 1440);
            if (this.skyCacheKey != keyA)
            {
                if (this.skyNextKey == keyA && this.skyNext != null)
                {
                    var t = this.skyCache;
                    this.skyCache = this.skyNext;
                    this.skyNext = t;
                    this.skyNextKey = "";
                }
                else this.skyCache = this.paintSkyAt(this.skyCache, m / 60.0);
                this.skyCacheKey = keyA;
            }
            if (this.skyNextKey != keyB)
            {
                this.skyNext = this.paintSkyAt(this.skyNext, ((m + 1) % 1440) / 60.0);
                this.skyNextKey = keyB;
            }
            return (this.skyCache, this.skyNext, f);
        }

        /// <summary>the sky at an hour, into `cv` (made, or resized only when the size changed)</summary>
        private Surface paintSkyAt(Surface cv, double hour)
        {
            double w = this.w, h = this.h;
            // the sky is all gradient: three-quarter resolution loses nothing and saves a lot
            double res = Math.Min(1, this.dpr) * 0.75;
            int cw = (int)Math.Max(1, Math.Ceiling(w * res));
            int ch = (int)Math.Max(1, Math.Ceiling(h * res));
            if (cv == null) cv = new Surface(cw, ch, true, "sky");
            else if (cv.width != cw || cv.height != ch) cv.SetSize(cw, ch);
            Ctx c = cv.GetContext();
            if (c == null) return cv;
            c.ResetState();
            c.ClearRect(0, 0, cw, ch);
            c.SetTransform(res, 0, 0, res, 0, 0);
            Sky.paintSky(c, w, h, Sky.skyState(hour, this.moonPhase()), this.scale);
            return cv;
        }

        /*
         * The sky is now the gradient and the disc, and nothing else. The god-rays and
         * the heat shimmer were the last two things the sun was throwing across it —
         * and the shimmer was four horizontal strokes running the full width, which is
         * a ruled line however faintly you draw it.
         */
        /// <summary>
        /// Stars, the Milky Way, the moon, and now and then a meteor — all only as
        /// dark as the sky really is. The brightest stars twinkle; the faint ones
        /// just sit there, as they do.
        /// </summary>
        private void drawCelestial()
        {
            Ctx ctx = this.ctx;
            double w = this.w, h = this.h;
            SkyState st = this.st;
            if (st.stars > 0.01)
            {
                string key = Js.str(Js.round(w)) + "x" + Js.str(Js.round(h)) + "@" + Js.str(this.dpr);
                if (this.stars == null || this.starsKey != key)
                {
                    StarField sf = Sky.starField(w, h, Math.Min(2, this.dpr));
                    this.stars = new StarsT { cv = sf.cv, bright = sf.bright };
                    this.starsKey = key;
                }
                ctx.Save();
                ctx.GlobalAlpha = st.stars;
                ctx.DrawImage(this.stars.cv, 0, 0, w, h);
                ctx.GlobalCompositeOperation = Composite.Lighter;
                for (int i = 0; i < this.stars.bright.Count; i += 1)
                {
                    var (x, y, mag) = this.stars.bright[i];
                    double tw = 0.5 + 0.5 * Math.Sin(this.t * (2 + (i % 7)) + i * 1.7);
                    ctx.GlobalAlpha = st.stars * mag * tw * 0.6;
                    ctx.FillStyle = "rgb(235, 240, 255)";
                    ctx.BeginPath();
                    ctx.Arc(x, y, 0.6 + mag * 1.1, 0, Math.PI * 2);
                    ctx.Fill();
                }
                ctx.Restore();

                // a meteor every so often on a dark night
                this.meteorT -= this.frameDt;
                if (this.meteor == null && this.meteorT <= 0 && st.stars > 0.7)
                {
                    this.meteorT = 12 + Js.random() * 20;
                    var nm = new MeteorT();
                    nm.x = w * (0.2 + Js.random() * 0.6);
                    nm.y = h * (0.05 + Js.random() * 0.2);
                    nm.vx = (Js.random() < 0.5 ? -1 : 1) * w * (0.5 + Js.random() * 0.3);
                    nm.vy = h * (0.25 + Js.random() * 0.2);
                    nm.t = 0;
                    this.meteor = nm;
                }
                var m = this.meteor;
                if (m != null)
                {
                    m.t += this.frameDt;
                    const double life = 0.7;
                    double k = m.t / life;
                    if (k >= 1) this.meteor = null;
                    else
                    {
                        double x = m.x + m.vx * m.t;
                        double y = m.y + m.vy * m.t;
                        var g = ctx.CreateLinearGradient(x, y, x - m.vx * 0.12, y - m.vy * 0.12);
                        double a = Math.Sin(k * Math.PI) * st.stars;
                        g.AddColorStop(0, Col.Rgba(255, 255, 255, Css.fixedNum(a, 3)));
                        g.AddColorStop(1, "rgba(200, 220, 255, 0)");
                        ctx.StrokeStyle = g;
                        ctx.LineWidth = 1.4;
                        ctx.LineCap = LineCap.Round;
                        ctx.BeginPath();
                        ctx.MoveTo(x, y);
                        ctx.LineTo(x - m.vx * 0.12, y - m.vy * 0.12);
                        ctx.Stroke();
                    }
                }
            }
        }

        /// <summary>the moon: drawn before the cloud bank, which can pass in front of it</summary>
        private void drawMoonNow()
        {
            SkyState st = this.st;
            if (st.moon.vis > 0.01)
            {
                Sky.drawMoon(
                    this.ctx,
                    st.moon.x * this.w,
                    st.moon.y * this.h,
                    Math.Max(9, 13 * this.scale),
                    this.moonPhase(),
                    st.moon.vis);
            }
        }

        private void drawSky()
        {
            Ctx ctx = this.ctx;
            double w = this.w, h = this.h;
            var (cached, next, f) = this.skyLayers();
            if (cached != null)
            {
                ctx.DrawImage(cached, 0, 0, w, h);
                if (next != null && f > 0.004)
                {
                    ctx.Save();
                    ctx.GlobalAlpha = f;
                    ctx.DrawImage(next, 0, 0, w, h);
                    ctx.Restore();
                }
            }
            else
            {
                // nothing else clears the frame, so the fallback has to cover it
                var sky = this.level.sky;
                ctx.FillStyle = sky != null && sky.Length > 0 && sky[0].Item2 != null ? sky[0].Item2 : "#0a0c10";
                ctx.FillRect(0, 0, w, h);
            }
        }

        /// <summary>the current cycle's aphorism, written into the sky to the left of the sun</summary>
        /// <summary>off while the new page is being shown as its own moment over the scene</summary>
        public bool showAphorism = true;
        /// <summary>accessibility: a larger, heavier breath ring that does not rely on colour</summary>
        public bool boldRing = false;
        /// <summary>the player's own saying, written into the sky every seventh summit</summary>
        public Quote ownQuote = null;
        /// <summary>chapters of the book completed, whose keepsakes stand on the hills</summary>
        public List<double> chaptersDone = new List<double>();

        private void drawAphorism()
        {
            if (!this.showAphorism || this.finaleOn || this.cutscene) return;
            // no aphorism in the background until the first cycle is completed
            if (this.phase == "playing" && this.cycles == 0) return;
            // the continue card sits right where the block is written; it comes back
            // the moment the next climb starts
            if (this.phase == "summit") return;
            Quote q = this.currentQuote;
            if (q == null) return;
            Ctx ctx = this.ctx;
            double w = this.w, h = this.h;
            double s = this.scale;
            double cy = Math.Min(h * 0.58, h * 0.4);
            const double topMin = 44;
            /*
             * The place is chosen once and then held, so it is chosen against the
             * whole climb, not this frame: the camera keeps him at the same distance
             * from the left edge, but how high he stands on the screen, and where the
             * hill's skyline runs, change with the slope as it goes up. Both are
             * worked out over every stretch of this hill.
             */
            QuoteSafeT safe = this.quoteSafety();
            double boxL = safe.l, boxR = safe.r;
            double above = safe.top - 18;
            double baseFs = Math.Max(22, Math.Min(Math.Min(44, w * 0.045), h * 0.065));
            // the hill's skyline under a span of the screen: the saying must stay above it
            double skyline(double l, double r) { return Math.Min(safe.skyline(l, r) - 12, h * 0.7); }
            // where its top goes at this place, or null if it cannot go there
            double? topAt(double plFs, double plMaxW, double plCx)
            {
                QuoteLayoutsT L0 = this.layoutQuote(q.text, q.author, plFs, plMaxW);
                double l = plCx - L0.w / 2;
                double r = plCx + L0.w / 2;
                if (l < 4 || r > w - 4) return null;
                double lim = skyline(l, r);
                // over him only if it clears his head
                if (r > boxL - 24 * s && l < boxR + 24 * s) lim = Math.Min(lim, above);
                double top = cy - L0.anchorY;
                if (top + L0.h > lim) top = lim - L0.h;
                return top >= topMin ? top : (double?)null;
            }
            /*
             * Where to write it. Its home is the upper-left sky; a long saying used to
             * come down over the man because there was no room for it there. It looks
             * for open sky — at home first, then nearby — at its full size, wider,
             * then smaller, never over him or down on the hill. The place is found
             * once for each saying and then never moves: a saying that slid about the
             * sky as he climbed was worse than one that crowded him.
             */
            string placeFor = q.text + "\0" + Js.str(Js.round(w)) + "x" + Js.str(Js.round(h)) + "\0" + this.level.id;
            QuotePlaceT pl = this.quotePlaceFor == placeFor ? this.quotePlace : null;
            if (pl == null)
            {
                double[] anchors = { w * 0.22, w * 0.14, w * 0.32, w * 0.44 };
                double[] ks = { 0.4, 0.56, 0.74 };
                for (double fs = baseFs; fs >= 13; fs = Math.Floor(fs * 0.9))
                {
                    foreach (double k in ks)
                    {
                        double maxW = Js.round(w * k);
                        QuoteLayoutsT L0 = this.layoutQuote(q.text, q.author, Js.round(fs), maxW);
                        foreach (double a0 in anchors)
                        {
                            double cx = Math.Max(L0.w / 2 + 6, Math.Min(w - L0.w / 2 - 6, a0));
                            double candFs = Js.round(fs);
                            double? tp = topAt(candFs, maxW, cx);
                            if (tp != null)
                            {
                                pl = new QuotePlaceT { fs = candFs, maxW = maxW, cx = cx, top = tp.Value };
                                goto placed;
                            }
                        }
                    }
                }
            placed:
                // too long for any open sky: smallest, as high as it goes
                if (pl == null) pl = new QuotePlaceT { fs = 13, maxW = Js.round(w * 0.56), cx = w * 0.3, top = topMin };
                this.quotePlace = pl;
                this.quotePlaceFor = placeFor;
            }
            Surface sprite = this.quoteSprite(q.text, q.author, pl.fs, pl.maxW);
            if (sprite == null) return;
            // On a small screen a long saying may have nowhere to go that he never
            // reaches. It still does not move: where he comes up under it, it fades
            // out, and comes back when he has passed.
            var mb = this.manBox;
            double ql = pl.cx - this.quoteW / 2;
            bool under =
                ql + this.quoteW > mb.l - 20 * this.scale &&
                ql < mb.r + 20 * this.scale &&
                pl.top + this.quoteH > mb.top - 28 * this.scale;
            this.quoteFade += ((under ? 0 : 1) - this.quoteFade) * Math.Min(1, this.frameDt * 5);
            if (this.quoteFade < 0.02) return;
            ctx.Save();
            ctx.GlobalAlpha = this.quoteFade;
            ctx.DrawImage(
                sprite,
                Js.round(pl.cx - this.quoteW / 2),
                Js.round(pl.top),
                this.quoteW,
                this.quoteH);
            ctx.Restore();
        }

        /// <summary>
        /// Over the whole of this hill: the highest his head (or the top of his
        /// stone) ever comes on the screen, the span of screen the two of them keep
        /// to, and the hill's highest skyline under any span — found by walking the
        /// camera up the slope the way the climb does.
        /// </summary>
        private QuoteSafeT quoteSafety()
        {
            double w = this.w, h = this.h;
            double s = this.scale;
            Level Lv = this.level;
            string key = Lv.id + "|" + Js.str(Js.round(w)) + "x" + Js.str(Js.round(h)) + "|" + Js.toFixed(s, 3);
            if (this.quoteSafe != null && this.quoteSafe.key == key) return this.quoteSafe;
            double lead = (w * 0.38) / s;
            double gy = h * 0.78;
            double R = 34 * s;
            double yOf(double wx, double cam) { return gy - (Levels.terrainAt(Lv, wx) - Levels.terrainAt(Lv, cam)) * s * 0.55; }
            var cams = new List<double>();
            for (double c = -lead; c <= Lv.length + 40; c += 24) cams.Add(c);
            double top = double.PositiveInfinity;
            foreach (double c in cams)
            {
                double x = c + lead;
                top = Math.Min(top, Math.Min(yOf(x - 64, c) - 92 * s, yOf(x, c) - 2.3 * R));
            }
            // the skyline per 16px column: its highest point over the whole climb
            int cols = (int)Math.Ceiling(w / 16) + 1;
            // a Float32Array: every value is rounded to single precision as it is stored
            var sky = new float[cols];
            for (int i = 0; i < cols; i++) sky[i] = float.PositiveInfinity;
            foreach (double c in cams)
                for (int i = 0; i < cols; i++) sky[i] = (float)Math.Min(sky[i], yOf(c + (i * 16) / s, c));
            var outv = new QuoteSafeT
            {
                key = key,
                top = top,
                l = w * 0.38 - (64 + 46 + 40) * s,
                r = w * 0.38 + R + 10 * s,
                skyline = (l, r) =>
                {
                    double m = double.PositiveInfinity;
                    for (
                        int i = (int)Math.Max(0, Math.Floor(l / 16));
                        i <= Math.Min(cols - 1, Math.Ceiling(r / 16));
                        i++)
                        m = Math.Min(m, sky[i]);
                    return m;
                },
            };
            this.quoteSafe = outv;
            return outv;
        }

        /// <summary>how big the saying comes out at a size and a width, without painting it</summary>
        private QuoteLayoutsT layoutQuote(string text, string author, double fs, double maxW)
        {
            string key = text + "\0" + author + "\0" + Js.str(fs) + "\0" + Js.str(maxW);
            QuoteLayoutsT hit;
            if (this.quoteLayouts.TryGetValue(key, out hit) && hit != null) return hit;
            var lines = this.wrapQuote(text, author, fs, maxW);
            double lineH = Js.round(fs * 1.35);
            double pad = Math.Ceiling(fs * 0.6);
            double anchorY = pad + lineH / 2;
            double authorY = anchorY + lines.lines.Count * lineH + lineH * 0.4;
            var outv = new QuoteLayoutsT
            {
                w = Math.Max(1, Math.Ceiling(lines.widest) + pad * 2),
                h = Math.Max(1, Math.Ceiling(authorY + fs + pad)),
                anchorY = anchorY,
            };
            if (this.quoteLayouts.Count > 40) this.quoteLayouts.Clear();
            this.quoteLayouts[key] = outv;
            return outv;
        }

        private static string quoteFont(double fs)
        {
            return "italic " + Js.str(fs) + "px Georgia, 'Times New Roman', serif";
        }

        /// <summary>Array.from(text): the string split into code points (not UTF-16 units)</summary>
        private static List<string> codePoints(string text)
        {
            var o = new List<string>();
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    o.Add(text.Substring(i, 2));
                    i++;
                }
                else o.Add(text.Substring(i, 1));
            }
            return o;
        }

        /// <summary>word wrap; scripts without spaces (CJK) wrap per character</summary>
        private (List<string> lines, double widest) wrapQuote(string text, string author, double fs, double maxW)
        {
            Ctx m = this.ctx;
            m.Save();
            m.Font = quoteFont(fs);
            bool spaced = text.Contains(" ");
            IList<string> words = spaced ? (IList<string>)text.Split(' ') : codePoints(text);
            var lines = new List<string>();
            string cur = "";
            foreach (string word in words)
            {
                string test = cur != "" ? cur + (spaced ? " " : "") + word : word;
                if (cur != "" && m.MeasureText(test) > maxW)
                {
                    lines.Add(cur);
                    cur = word;
                }
                else
                {
                    cur = test;
                }
            }
            if (cur != "") lines.Add(cur);
            double widest = 0;
            foreach (string line in lines) widest = Math.Max(widest, m.MeasureText(line));
            m.Font = quoteFont(Js.round(fs * 0.72));
            widest = Math.Max(widest, m.MeasureText(author));
            m.Restore();
            return (lines, widest);
        }

        /// <summary>
        /// The aphorism, rendered once and then blitted.
        ///
        /// It used to be laid out from scratch on every frame — a `measureText` per word
        /// to find the wrap, then a `fillText` per line under a shadow blur, which is one
        /// of the slowest things a 2D context does. None of it changes between summits:
        /// the words only change when the quote or the language does, and the layout only
        /// when the window resizes.
        /// </summary>
        private Surface quoteSprite(string text, string author, double fs, double maxW)
        {
            double w = this.w, h = this.h;
            double lineH = Js.round(fs * 1.35);
            double dpr = this.dpr;
            // dark ink on a bright sky; by night, pale lettering like starlight
            bool pale = this.st.elev < -2;
            string key = text + "\0" + author + "\0" + Js.str(Js.round(w)) + "x" + Js.str(Js.round(h)) + "@" +
                Js.toFixed(dpr, 2) + ":" + (pale ? "true" : "false") + ":" + Js.str(fs) + ":" + Js.str(maxW);
            if (this.quoteCacheKey == key && this.quoteCache != null) return this.quoteCache;

            string bodyFont = quoteFont(fs);
            string authorFont = quoteFont(Js.round(fs * 0.72));

            // measured on the live context, which already has the fonts loaded
            var wrapped = this.wrapQuote(text, author, fs, maxW);
            List<string> lines = wrapped.lines;
            double widest = wrapped.widest;

            // room for the halo, and for whatever a serif italic hangs past its metrics
            double pad = Math.Ceiling(fs * 0.6);
            double anchorY = pad + lineH / 2;
            double authorY = anchorY + lines.Count * lineH + lineH * 0.4;
            // Painted at the screen's real pixel density. It used to be painted at one
            // canvas pixel per CSS pixel and then stretched onto a 2x or 3x phone
            // screen, which is exactly why the lettering looked out of focus.
            double cssW = Math.Max(1, Math.Ceiling(widest) + pad * 2);
            double cssH = Math.Max(1, Math.Ceiling(authorY + fs + pad));
            int pw = (int)Math.Ceiling(cssW * dpr);
            int ph = (int)Math.Ceiling(cssH * dpr);
            Surface cv = this.quoteCache;
            if (cv == null) cv = new Surface(pw, ph, true, "quote");
            else cv.SetSize(pw, ph);
            this.quoteW = cssW;
            this.quoteH = cssH;
            Ctx c = cv.GetContext();
            if (c == null) return null;
            c.SetTransform(1, 0, 0, 1, 0, 0);
            c.ClearRect(0, 0, cv.width, cv.height);
            c.SetTransform(dpr, 0, 0, dpr, 0, 0);
            c.TextAlign = TextAlign.Center;
            c.TextBaseline = TextBaseline.Middle;
            double mid = cssW / 2;

            // Pale text on a pale sky is why this could not be read: nothing in this
            // frame is darker than L 0.6, so the only value left to write with is a dark
            // Ink, not light: held translucent so it still reads as written into the sky
            // rather than printed onto it, and haloed rather than drop-shadowed, because
            // once the letters are the dark thing the separation has to come from
            // something brighter than they are. Dropping the block to 0.4h buys about
            // four hundredths of sky lightness to sit against, which is most of what
            // makes dark lettering work on a ramp this low.
            //
            // The halo is its own pass now, and the letters go down on top of it with
            // no blur at all. Drawn in one pass with a shadow, the blur was applied to
            // the glyphs themselves and softened every edge.
            void paintLines()
            {
                c.Font = bodyFont;
                for (int i = 0; i < lines.Count; i++) c.FillText(lines[i], mid, anchorY + i * lineH);
                c.Font = authorFont;
                c.FillText(author, mid, authorY);
            }
            c.Save();
            c.ShadowColor = pale ? "rgba(0, 0, 10, 0.6)" : "oklch(0.92 0.06 78 / 0.55)";
            c.ShadowBlur = 7 * dpr;
            c.FillStyle = pale ? "rgba(0, 0, 10, 0.3)" : "oklch(0.92 0.06 78 / 0.35)";
            paintLines();
            c.Restore();
            c.Font = bodyFont;
            c.FillStyle = pale ? "rgba(235, 238, 250, 0.92)" : "oklch(0.14 0.03 40 / 0.94)";
            for (int i = 0; i < lines.Count; i++) c.FillText(lines[i], mid, anchorY + i * lineH);
            c.Font = authorFont;
            c.FillStyle = pale ? "rgba(210, 215, 235, 0.8)" : "oklch(0.2 0.04 42 / 0.85)";
            c.FillText(author, mid, authorY);

            this.quoteCache = cv;
            this.quoteCacheKey = key;
            // the caller places the block by its centre, as it did when it drew the
            // lines itself — so the anchor is the middle of the stack, not of line one
            this.quoteAnchorY = anchorY + ((lines.Count - 1) * lineH) / 2;
            return cv;
        }

        /// <summary>
        /// Three parallax layers of cumulus drifting above the sun, lit from beneath —
        /// held in a half-resolution layer of their own and repainted only when they
        /// have moved. See CLOUD_RES: this is where the frame time was going.
        /// </summary>
        private void drawClouds()
        {
            double w = this.w, h = this.h;
            // a little finer on capable devices, so the softening pass below can stay
            // small on screen; the slow path keeps the old half resolution
            this.cloudRes = this.lowQuality ? CLOUD_RES : 0.75;
            string key = Js.str(Math.Ceiling(w)) + "x" + Js.str(Math.Ceiling(h)) + "@" + Js.str(this.cloudRes) + ":" + this.level.id;
            Surface cv = this.cloudCache;
            bool stale = false;

            if (cv == null || this.cloudCacheKey != key)
            {
                int cw = (int)Math.Max(1, Math.Ceiling(w * this.cloudRes));
                int ch = (int)Math.Max(1, Math.Ceiling(h * this.cloudRes));
                if (cv == null) cv = new Surface(cw, ch, true, "clouds");
                else cv.SetSize(cw, ch);
                this.cloudCache = cv;
                this.cloudCacheKey = key;
                stale = true;
            }
            else
            {
                // the front rank has the strongest parallax, so it is the one that decides
                // whether the bank has drifted far enough to be worth repainting
                double drift = Math.Abs(this.camX - this.cloudCacheCamX) * 0.085 * this.scale;
                // while the camera swings — the throw, the stone rolling back, the
                // summit — they are painted every frame, so they glide rather than step
                bool swinging = this.kickT >= 0 || this.phase != "playing";
                stale =
                    swinging ||
                    this.t - this.cloudCacheT > CLOUD_MAX_AGE ||
                    drift > CLOUD_MAX_DRIFT ||
                    Math.Abs(this.st.hour - this.cloudCacheHour) > 0.02;
            }

            if (stale)
            {
                Ctx c = cv.GetContext();
                if (c != null)
                {
                    c.SetTransform(1, 0, 0, 1, 0, 0);
                    c.ClearRect(0, 0, cv.width, cv.height);
                    // painted in CSS pixels and scaled down on the way in, so every
                    // coordinate below stays the one the scene reasons in
                    c.SetTransform(this.cloudRes, 0, 0, this.cloudRes, 0, 0);
                    this.paintClouds(c);
                    // (The layer used to get a blur pass here, to melt the dither of
                    // dozens of stacked gradients. The clouds are sprites now, with no
                    // gradients to dither, and that full-screen blur, sixteen times a
                    // second, was one of the most expensive things in the frame.)
                    this.cloudCacheT = this.t;
                    this.cloudCacheHour = this.st.hour;
                    this.cloudCacheCamX = this.camX;
                }
            }

            this.ctx.DrawImage(cv, 0, 0, w, h);
            this.drawHorizonStreaks();
        }

        private sealed class CloudLayer
        {
            public double parallax;
            public double speed;
            public double alpha;
            public double minW;
            public double maxW;
            public double count;
            public double yTop;
            public double ySpan;
            /// <summary>0 = far haze, 1 = the rank nearest the camera</summary>
            public double depth;
        }

        // back layer slowest & most translucent, front layer fastest & most solid
        private static readonly CloudLayer[] CLOUD_LAYERS =
        {
            new CloudLayer { parallax = 0.018, speed = 6, alpha = 0.42, minW = 0.26, maxW = 0.44, count = 9, yTop = 0.04, ySpan = 0.22, depth = 0 },
            new CloudLayer { parallax = 0.045, speed = 12, alpha = 0.64, minW = 0.38, maxW = 0.6, count = 7, yTop = 0.09, ySpan = 0.26, depth = 0.5 },
            new CloudLayer { parallax = 0.085, speed = 20, alpha = 0.9, minW = 0.55, maxW = 0.85, count = 5, yTop = 0.15, ySpan = 0.3, depth = 1 },
        };

        private void paintClouds(Ctx ctx)
        {
            double w = this.w, h = this.h;
            double t = this.t;
            double seed = 0;
            // building a volume is the costly part: a few per paint, nearest first
            this.cloudBuildBudget = 1;
            // while a new climb dissolves in, every cloud catches up with its new light
            this.cloudBudget = this.fadeT < CROSSFADE ? 12 : this.phase == "descent" ? 5 : 3;
            foreach (CloudLayer L in CLOUD_LAYERS)
            {
                for (int i = 0; i < L.count; i++)
                {
                    double k = seed++;
                    double cw = w * (L.minW + this.hash(k * 3.1) * (L.maxW - L.minW));
                    // the drawn width: sheets far off spread wide, a near cumulus is a heap
                    double dw = cw * (L.depth < 0.25 ? 0.95 : L.depth < 0.75 ? 0.62 : 0.55);
                    double wrap = w + dw + 120;
                    double off = this.hash(k * 5.3) * wrap;
                    double x =
                        ((((off - this.camX * L.parallax + t * L.speed) % wrap) + wrap) % wrap) - dw / 2 - 60;
                    double y = h * (L.yTop + this.hash(k * 7.7) * L.ySpan) + Math.Sin(t * 0.09 + k * 2.1) * 4;
                    this.drawVolumeCloud(ctx, x, y, dw, L.alpha, k, L.depth);
                }
            }
        }

        private CloudShape buildCloudShape(double seed, double depth)
        {
            double px = (this.lowQuality ? 0.6 : 1) * (depth < 0.25 ? 300 : depth < 0.75 ? 340 : 400);
            CloudShape shape = Clouds.buildCloud(seed + 1, depth < 0.25 ? "strato" : "cumulus", px);
            this.cloudShapes[seed] = shape;
            return shape;
        }

        /// <summary>clouds being built, and tints being worked out, in the paint worker</summary>
        private HashSet<double> cloudPending = new HashSet<double>();
        private HashSet<double> cloudTinting = new HashSet<double>();
        /// <summary>
        /// PORT: the paint worker's own store of built volumes (paint.worker.ts `clouds`),
        /// keyed like it by seed + 1. Written and read only by the worker jobs.
        /// </summary>
        private readonly ConcurrentDictionary<double, CloudData> cloudWork = new ConcurrentDictionary<double, CloudData>();

        /// <summary>PORT: paint-client's paintWorkerAvailable(); in C# the worker (Work.Run) always is</summary>
        private static bool paintWorkerAvailable() { return true; }

        /// <summary>
        /// Ask the worker for a cloud's volume. Building one took 40-80 ms on the
        /// main thread — a visible hitch every few frames at the start of a climb.
        /// </summary>
        private void requestCloud(double seed, double depth)
        {
            if (this.cloudShapes.ContainsKey(seed) || this.cloudPending.Contains(seed)) return;
            this.cloudPending.Add(seed);
            double px = (this.lowQuality ? 0.6 : 1) * (depth < 0.25 ? 300 : depth < 0.75 ? 340 : 400);
            double wseed = seed + 1;
            string kind = depth < 0.25 ? "strato" : "cumulus";
            var store = this.cloudWork;
            Work.Run(() =>
            {
                CloudData d = Clouds.buildCloudData(wseed, kind, px);
                store[wseed] = d;
                return d;
            }, r =>
            {
                this.cloudPending.Remove(seed);
                if (r == null) return; // no worker: the next frame builds it here instead
                var canvas = new Surface(r.w, r.h, true, "cloud");
                var none = new float[0];
                this.cloudShapes[seed] = new CloudShape
                {
                    w = r.w,
                    h = r.h,
                    dens = none,
                    top = none,
                    bot = none,
                    canvas = canvas,
                    key = "",
                };
            });
        }

        /// <summary>
        /// Build the cloud volumes in the background, one per tick, while the start
        /// card is still up — so the first climb never waits on them. The ranks are
        /// 9 far, 7 middle, 5 near (see paintClouds); the near ones go first.
        /// </summary>
        private void prebuildClouds()
        {
            var order = new List<double>();
            for (int i = 0; i < 21; i++) order.Add(20 - i);
            // with a worker, all of them at once: they are built there, in turn
            if (paintWorkerAvailable())
            {
                foreach (double seed in order) this.requestCloud(seed, seed < 9 ? 0 : seed < 16 ? 0.5 : 1);
                return;
            }
            // PORT: the no-worker fallback (one volume per idle callback, starting after a
            // 200 ms timeout) is unreachable here, since Work.Run is always available; the
            // per-paint build budget in drawVolumeCloud remains as the last resort.
        }

        /// <summary>one cloud from its built volume, coloured for the hour (see clouds.ts)</summary>
        private void drawVolumeCloud(Ctx ctx, double x, double y, double dw, double alpha, double seed, double depth)
        {
            double day = Math.Max(0, Math.Min(1, (this.st.elev - 10) / 10));
            // fair-weather days: the heaps are scattered, not a ceiling
            bool keep = this.hash(seed * 1.7 + 0.3) < 0.55;
            double show = keep ? 1 : 1 - day;
            // Cloud is opaque: a thinner sky means fewer clouds, not see-through ones
            // (stars showing through a bank gave it away). Cover decides how many of
            // the bank are out; the far ranks keep a little haze.
            double outK = Math.Max(
                0,
                Math.Min(1, (this.st.cover * 1.3 - this.hash(seed * 2.9 + 0.7)) / 0.15));
            double solid = 0.9 + depth * 0.1 + (alpha - 0.64) * 0.1;
            double a = outK * solid * show * (1 - day) + (keep ? 0.97 * day : 0);
            if (a < 0.01) return;
            bool offload = paintWorkerAvailable();
            CloudShape shape;
            this.cloudShapes.TryGetValue(seed, out shape);
            if (shape == null)
            {
                if (offload)
                {
                    this.requestCloud(seed, depth);
                    return;
                }
                if (this.cloudBuildBudget <= 0 || this.cloudPending.Contains(seed)) return;
                this.cloudBuildBudget--;
                shape = this.buildCloudShape(seed, depth);
            }
            double lit = Math.Max(0.12, 1 - Math.Abs(x - this.sunScreen().x) / (this.w * 0.8));
            double litQ = Js.round(lit * 4) / 4;
            string key = Js.str(Js.round(this.st.elev * 2)) + "|" + Js.str(litQ);
            if (shape.key != key && shape.dens.Length == 0)
            {
                // built in the worker: the worker colours it too, and the new picture
                // fades in over the old when it comes back
                if (!this.cloudTinting.Contains(seed))
                {
                    this.cloudTinting.Add(seed);
                    SkyCloud pal = this.st.cloud;
                    CloudShape target = shape;
                    var light = new CloudLight
                    {
                        day = day,
                        lit = litQ,
                        crown = pal.crown,
                        crownA = pal.crownA,
                        mid = pal.mid,
                        belly = pal.belly,
                        bellyA = pal.bellyA * (0.6 + depth * 0.4),
                    };
                    double wseed = seed + 1;
                    var store = this.cloudWork;
                    Work.Run(() =>
                    {
                        CloudData d;
                        if (!store.TryGetValue(wseed, out d) || d == null) return null; // "missing"
                        return Clouds.tintPixels(d, light);
                    }, px =>
                    {
                        this.cloudTinting.Remove(seed);
                        if (px != null) Clouds.applyCloudPixels(target, px, key, this.t);
                        // the worker is gone (and with it this volume): build it again here
                        else
                        {
                            CloudShape cur;
                            if (this.cloudShapes.TryGetValue(seed, out cur) && cur == target) this.cloudShapes.Remove(seed);
                        }
                    });
                }
                if (string.IsNullOrEmpty(shape.key)) return;
            }
            else if (shape.key != key && (this.cloudBudget > 0 || string.IsNullOrEmpty(shape.key)))
            {
                this.cloudBudget--;
                SkyCloud pal = this.st.cloud;
                // the far ranks fade toward the sky colour: haze eats their contrast
                Clouds.retintCloud(
                    shape,
                    new CloudLight
                    {
                        day = day,
                        lit = litQ,
                        crown = pal.crown,
                        crownA = pal.crownA,
                        mid = pal.mid,
                        belly = pal.belly,
                        bellyA = pal.bellyA * (0.6 + depth * 0.4),
                    },
                    this.t);
                shape.key = key;
            }
            double dh = (dw * shape.h) / shape.w;
            // the base sits a little below the cloud's height line
            ctx.Save();
            // the old tint underneath, the new one fading in over it: no steps (and a
            // cloud's first picture fades in from nothing)
            double f = shape.swapAt != null ? (this.t - shape.swapAt.Value) / 1.4 : 1;
            if (f < 1 && shape.prev != null)
            {
                ctx.GlobalAlpha = Math.Min(1, a);
                ctx.DrawImage(shape.prev, x - dw / 2, y - dh * 0.6, dw, dh);
                ctx.GlobalAlpha = Math.Min(1, a) * Math.Max(0, f);
            }
            else ctx.GlobalAlpha = Math.Min(1, a) * Math.Max(0, Math.Min(1, f));
            ctx.DrawImage(shape.canvas, x - dw / 2, y - dh * 0.6, dw, dh);
            ctx.Restore();
        }

        /// <summary>
        /// Thin cloud stripes stacked just over the sun. They belong to the cloud pass,
        /// not to the sky pass — drawn there the cumulus above simply painted over them.
        /// </summary>
        private void drawHorizonStreaks()
        {
            Ctx ctx = this.ctx;
            var (sunX, sunY) = this.sunScreen();
            double s = this.scale;
            double t = this.t;
            // Eight of these used to sit stacked directly over the disc at even spacing
            // and a uniform thickness, which stopped reading as cloud and started reading
            // as a smear. Five now, spread further apart, each a different thickness and
            // length, and none of them parked on the sun itself — that is what makes a
            // thin cirrus band look like weather rather than like blur.
            double low = this.st.elev > -7 && this.st.elev < 14 ? 1 - Math.Abs(this.st.elev - 3) / 11 : 0;
            if (low <= 0.02) return;
            ctx.Save();
            for (int i = 0; i < 5; i++)
            {
                double sy = sunY - 132 * s + i * 27 * s + Math.Sin(t * 0.05 + i * 1.3) * 4;
                double len = (150 + this.hash(i * 9.1) * 210) * s;
                double thick = (1.4 + this.hash(i * 4.3) * 2.1) * s;
                // the bands nearest the disc catch the most light
                double heat = Math.Max(0.2, 1 - Math.Abs(sy - sunY) / (190 * s));
                ctx.FillStyle = Css.rgb(this.st.glow, 0.2 * heat * low);
                ctx.BeginPath();
                ctx.Ellipse(
                    // a slow lateral breath; a one-way drift would walk them off the sun forever
                    sunX + len * 0.25 + Math.Sin(t * 0.03 + i * 0.8) * 26 * s,
                    sy,
                    len * 0.5,
                    thick,
                    (this.hash(i + 4) - 0.5) * 0.24,
                    0,
                    Math.PI * 2);
                ctx.Fill();
            }
            ctx.Restore();
        }

        /// <summary>the painted mountains, one per hill, built a few rows a frame</summary>
        private Dictionary<string, MountainPainter> mountains = new Dictionary<string, MountainPainter>();

        private static readonly Dictionary<string, RGB> MOUNTAIN_ROCK = new Dictionary<string, RGB>
        {
            { "tartarus", new RGB(128, 106, 92) },
            { "scree", new RGB(112, 110, 116) },
            { "windy", new RGB(116, 120, 130) },
            { "ruins", new RGB(132, 102, 88) },
        };

        /// <summary>
        /// The painter for a hill's mountain. It is placed and sized so that it
        /// rises into view as he nears the summit: the main peak comes to stand at
        /// about two thirds of the way across the frame when he is at the top.
        /// </summary>
        private MountainPainter painterFor(Level level)
        {
            Mountain mt = level.mountain;
            if (mt == null) return null;
            string id = level.id;
            MountainPainter mp;
            if (this.mountains.TryGetValue(id, out mp) && mp != null) return mp;
            Dictionary<string, RGB> ROCK = MOUNTAIN_ROCK;
            double seed = 7;
            for (int i = 0; i < id.Length; i++) seed = (seed * 31 + id[i]) % 100003;
            double width = mt.width * 0.8;
            double height = mt.height * 0.84;
            double camAtTop = level.length - 460;
            double x = 0.72 * (1280 / 1.2) + camAtTop * mt.parallax + width * 0.16;
            RGB rock;
            if (!ROCK.TryGetValue(id, out rock)) rock = new RGB(120, 108, 98);
            mp = new MountainPainter(
                new MountainSpec { x = x, height = height, width = width, snow = true, seed = seed, rock = rock },
                this.lowQuality ? 1.15 : 1.45);
            this.mountains[id] = mp;
            return mp;
        }
        private Dictionary<string, double> mountainReadyAt = new Dictionary<string, double>();
        /// <summary>the mountain with its light and air composed in, and what it was composed for</summary>
        private Surface mountainComp = null;
        private string mountainCompKey = "";
        private MountainPainter mountainCompFor = null;

        private void drawMountain()
        {
            MountainPainter mp = this.painterFor(this.level);
            if (mp == null) return;
            if (!mp.ready)
            {
                // built a few rows a frame; until then the sky simply has no mountain
                if (mp.step(this.lowQuality ? 2 : 3)) this.mountainReadyAt[this.level.id] = this.t;
                return;
            }
            // while this one stands finished, make the next hill's in the background
            Level nextLevel = Levels.getLevel((int)(this.levelIndex + 1));
            MountainPainter next = this.painterFor(nextLevel);
            if (next != null && !next.ready) next.step(1.5);
            // keep only this hill's and the next one's: they are big pictures
            if (this.mountains.Count > 2)
                foreach (string k in new List<string>(this.mountains.Keys))
                    if (k != this.level.id && k != nextLevel.id) this.mountains.Remove(k);
            double at;
            if (!this.mountainReadyAt.TryGetValue(this.level.id, out at)) at = -10;
            double fade = Math.Min(1, (this.t - at) / 1.5);
            if (fade < 1)
            {
                this.ctx.Save();
                this.ctx.GlobalAlpha = fade;
                this.drawMountainPainted(mp);
                this.ctx.Restore();
            }
            else this.drawMountainPainted(mp);
        }

        private void drawMountainPainted(MountainPainter mp)
        {
            Mountain mt = this.level.mountain;
            Ctx ctx = this.ctx;
            double w = this.w, h = this.h;
            double p = mt.parallax;
            double farX(double wx) { return (wx - this.camX * p) * this.scale; }
            // The hill itself, rising to the right, hid the mountain until he was nearly at
            // the top. It now stands higher in the sky lower down the slope and settles to
            // its old place as he climbs: in sight from about half way, as a goal should be.
            double prog = Math.Max(0, Math.Min(1, this.camX / Math.Max(1, this.level.length - 460)));
            double lift = (1 - prog * prog) * this.h * 0.2;
            double farY(double wy) { return this.groundY - (wy - this.camY * p) * this.scale * 0.5 + this.shakeY * 0.4 - lift; }
            double sx = farX(mp.x0);
            double sy = farY(mp.top);
            double sw = mp.W * mp.kx * this.scale;
            double sh = mp.H * mp.ky * this.scale * 0.5;
            if (sx > w || sx + sw < 0) return;
            var sun = this.lightScreen();
            // which side the sun is on, eased: the two lightings cross-fade
            double mcx = farX(mp.spec.x - mp.spec.width * 0.16);
            double u = Math.Max(0, Math.Min(1, (sun.x - mcx) / (w * 0.9) + 0.5));
            double k = u * u * (3 - 2 * u);
            // a low sun lights its far side: the face toward us sinks into shadow
            double back = 1 - Math.Max(0, Math.Min(1, (sun.elev - 3) / 22));
            // under a grey sky it goes grey and dim with distance and rain
            double wetK =
                (this.weather == "storm" ? 0.5 : this.weather == "rain" ? 0.38 : 0) * this.wxFade;
            RGB hz = this.st.ramp[3];
            /*
             * The two lightings, the shadow of a low sun, the grey of rain and the
             * air of distance are all composed once into one picture at the sprite's
             * own size, and only composed again when one of them has moved enough to
             * see. Done on screen every frame they were six full-height passes, and
             * the mountain had become the most expensive thing in the frame.
             */
            string q(double v, double n) { return Js.str(Js.round(v * n)); }
            string key = q(k, 40) + "|" + q(back, 40) + "|" + q(wetK, 30) + "|" + q(hz[0], 1.0 / 6) + "|" +
                q(hz[1], 1.0 / 6) + "|" + q(hz[2], 1.0 / 6);
            // at the sprite's own size: at half size it was stretched to twice that on screen,
            // and all the ridges and gullies went soft (low quality keeps the half)
            double cres = this.lowQuality ? 0.5 : 1;
            int cw = (int)Math.Ceiling(mp.W * cres);
            int ch = (int)Math.Ceiling(mp.H * cres);
            if (this.mountainComp == null) this.mountainComp = new Surface(cw, ch, true, "mountain");
            Surface comp = this.mountainComp;
            if (this.mountainCompKey != key || this.mountainCompFor != mp)
            {
                // at half the sprite's size: it is drawn soft and far off anyway, and a
                // quarter of the pixels makes each re-composition a quarter of the cost
                if (comp.width != cw || comp.height != ch)
                {
                    comp.SetSize(cw, ch);
                }
                Ctx c = comp.GetContext();
                if (c != null)
                {
                    c.GlobalCompositeOperation = Composite.Copy;
                    c.GlobalAlpha = 1;
                    c.DrawImage(mp.a, 0, 0, cw, ch);
                    c.GlobalCompositeOperation = Composite.SourceOver;
                    if (k > 0.01)
                    {
                        c.GlobalAlpha = k;
                        c.DrawImage(mp.b, 0, 0, cw, ch);
                        c.GlobalAlpha = 1;
                    }
                    c.GlobalCompositeOperation = Composite.SourceAtop;
                    if (back > 0.01)
                    {
                        c.GlobalAlpha = 0.48 * back;
                        c.FillStyle = mt.color;
                        c.FillRect(0, 0, cw, ch);
                        c.GlobalAlpha = 1;
                    }
                    if (wetK > 0.01)
                    {
                        c.FillStyle = Col.Rgba(92, 100, 116, Css.fixedNum(wetK, 3));
                        c.FillRect(0, 0, cw, ch);
                    }
                    var air = c.CreateLinearGradient(0, 0, 0, ch);
                    // the air of distance, lighter than it was: it had washed the mountain
                    // out to a pale shape with hardly a ridge or a gully left in it
                    air.AddColorStop(0, Css.rgb(hz, 0.1));
                    air.AddColorStop(1, Css.rgb(hz, 0.38));
                    c.FillStyle = air;
                    c.FillRect(0, 0, cw, ch);
                    c.GlobalCompositeOperation = Composite.SourceOver;
                }
                this.mountainCompKey = key;
                this.mountainCompFor = mp;
            }
            ctx.Save();
            ctx.ImageSmoothingEnabled = true;
            // PORT: imageSmoothingQuality = "low" has no Ctx equivalent (bilinear is used)
            ctx.DrawImage(comp, sx, sy, sw, sh);
            // under the crest the painting stops; below it, the mass goes on in its own colour
            double below = h - sy - sh + 3;
            if (below > 0)
            {
                ctx.FillStyle = mt.color;
                ctx.FillRect(sx, sy + sh - 1, sw, below);
                if (back > 0.01 || wetK > 0.01)
                {
                    ctx.FillStyle = Col.Rgba(92, 100, 116, Css.fixedNum(Math.Max(wetK, 0), 3));
                    ctx.FillRect(sx, sy + sh - 1, sw, below);
                }
                ctx.FillStyle = Css.rgb(hz, 0.38);
                ctx.FillRect(sx, sy + sh - 1, sw, below);
            }
            int step = (int)Math.Max(1, Js.round(4 / (mp.kx * this.scale)));

            // the river catching the light, the water moving down it
            double day = Math.Max(0, Math.Min(1, (sun.elev + 2) / 12));
            if (mp.river.Count > 1 && day > 0.02 && !this.lowQuality)
            {
                ctx.Save();
                ctx.LineCap = LineCap.Round;
                ctx.SetLineDash(new double[] { 2 * this.scale, 7 * this.scale });
                ctx.LineDashOffset = -this.t * 14 * this.scale;
                ctx.StrokeStyle = Col.Rgba(235, 244, 255, Css.fixedNum(0.55 * day, 3));
                ctx.LineWidth = 1.1 * this.scale;
                ctx.BeginPath();
                for (int i = 0; i < mp.river.Count; i++)
                {
                    var (rx, ry) = mp.river[i];
                    if (i != 0) ctx.LineTo(farX(rx), farY(ry));
                    else ctx.MoveTo(farX(rx), farY(ry));
                }
                ctx.Stroke();
                ctx.Restore();
            }

            // the summit's weather
            double peakX = mp.spec.x - mp.spec.width * 0.16;
            double px = farX(peakX);
            double py = farY(mp.crestAt(peakX));
            double s = this.scale;
            bool wet = this.weather == "rain" || this.weather == "storm";
            ctx.Save();
            if (wet)
            {
                // a cloud sits on the summit and the rain hangs under it in grey curtains
                double k2 = this.wxFade;
                double dark = this.weather == "storm" ? 0.75 : 0.6;
                for (int i = 0; i < 5; i++)
                {
                    double cx = px + (i - 2) * 70 * s + Math.Sin(this.t * 0.05 + i) * 10 * s;
                    double cy = py - 6 * s + (i % 2) * 10 * s;
                    double r = (70 + (i % 3) * 22) * s;
                    var g = ctx.CreateRadialGradient(cx, cy, 0, cx, cy, r);
                    g.AddColorStop(0, Col.Rgba(70, 76, 90, Css.fixedNum(dark * k2, 3)));
                    g.AddColorStop(1, "rgba(70, 76, 90, 0)");
                    ctx.FillStyle = g;
                    ctx.FillRect(cx - r, cy - r, r * 2, r * 2);
                }
                var veil = ctx.CreateLinearGradient(0, py, 0, py + 200 * s);
                veil.AddColorStop(0, Col.Rgba(96, 104, 120, Css.fixedNum(0.4 * k2, 3)));
                veil.AddColorStop(1, "rgba(96, 104, 120, 0)");
                ctx.StrokeStyle = veil;
                ctx.LineWidth = 1 * s;
                ctx.BeginPath();
                for (int i = 0; i < 46; i++)
                {
                    double x0 = px - 170 * s + i * 7.5 * s + ((this.t * 30 * s) % (7.5 * s));
                    ctx.MoveTo(x0, py);
                    ctx.LineTo(x0 - 40 * s, py + (130 + (i % 5) * 16) * s);
                }
                ctx.Stroke();
            }
            else if (!this.lowQuality)
            {
                // spindrift: snow torn off the summit by the wind, a long pale plume
                double day2 = 0.35 + 0.65 * day;
                for (int i = 0; i < 4; i++)
                {
                    double ph = (((this.t * 0.04 + i * 0.25) % 1) + 1) % 1;
                    double cx = px + (20 + ph * 160) * s;
                    double cy = py - (4 + ph * 18) * s;
                    double r = (16 + ph * 50) * s;
                    double a = Math.Sin(ph * Math.PI) * 0.16 * day2;
                    var g = ctx.CreateRadialGradient(cx, cy, 0, cx, cy, r);
                    g.AddColorStop(0, Col.Rgba(248, 250, 255, Css.fixedNum(a, 3)));
                    g.AddColorStop(1, "rgba(248, 250, 255, 0)");
                    ctx.FillStyle = g;
                    ctx.Save();
                    ctx.Translate(cx, cy);
                    ctx.Scale(1.8, 0.55);
                    ctx.Translate(-cx, -cy);
                    ctx.FillRect(cx - r, cy - r, r * 2, r * 2);
                    ctx.Restore();
                }
            }
            ctx.Restore();

            // a hairline of light along the crest
            ctx.StrokeStyle = Col.Rgba(255, 226, 190, Css.fixedNum(0.18 + 0.2 * back, 3));
            ctx.LineWidth = 1.1;
            ctx.BeginPath();
            for (int i = 0; i < mp.W; i += step)
            {
                double X = farX(mp.x0 + i * mp.kx);
                double Y = farY(mp.crest[i]);
                if (i != 0) ctx.LineTo(X, Y);
                else ctx.MoveTo(X, Y);
            }
            ctx.Stroke();
            ctx.Restore();
        }
    }
}
