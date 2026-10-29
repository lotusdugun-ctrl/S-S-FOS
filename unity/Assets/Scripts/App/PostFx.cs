using System;
using Sisifos.Game;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sisifos.App
{
    /// <summary>
    /// The finished frame on its way to the screen (SisifosPost.shader): bloom, the light
    /// shafts of a low sun, drifting mist, rain in three depths and film grain, steered by
    /// the engine's postFx() each frame. Recorded once into the present command buffer and
    /// rebuilt only when the frame's texture or size changes; the numbers that move are
    /// material properties, read when the buffer runs. Off in low quality, when the frame
    /// goes to the screen as it was drawn — and then the engine strokes the rain itself.
    /// </summary>
    public sealed class PostFx : IDisposable
    {
        readonly Material mat;
        readonly PostFxState st = new PostFxState();
        RenderTexture builtFor;
        int builtW, builtH;
        bool builtOn;

        static readonly int idA = Shader.PropertyToID("_SisifosPostA");
        static readonly int idB = Shader.PropertyToID("_SisifosPostB");
        static readonly int idC = Shader.PropertyToID("_SisifosPostC");
        static readonly int idBloom = Shader.PropertyToID("_BloomTex");
        static readonly int idRays = Shader.PropertyToID("_RaysTex");
        static readonly int idTexel = Shader.PropertyToID("_PostTexel");
        static readonly int idSun = Shader.PropertyToID("_Sun");
        static readonly int idSunColor = Shader.PropertyToID("_SunColor");
        static readonly int idFog = Shader.PropertyToID("_Fog");
        static readonly int idRain = Shader.PropertyToID("_Rain");
        static readonly int idScreen = Shader.PropertyToID("_Screen");

        public static PostFx Create()
        {
            var sh = Resources.Load<Shader>("Shaders/SisifosPost");
            if (sh == null || !sh.isSupported) return null;
            return new PostFx(sh);
        }

        PostFx(Shader sh)
        {
            mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        }

        public void Dispose()
        {
            if (mat != null) UnityEngine.Object.Destroy(mat);
        }

        /// <summary>
        /// Once a frame, after the engine has drawn: refresh the numbers and, if the frame's
        /// texture, the screen's size or on/off changed, record the present buffer again.
        /// </summary>
        public void Present(CommandBuffer cb, RenderTexture rt, SisyphusEngine engine)
        {
            if (engine != null) engine.postFx(st);
            else st.on = false;
            int w = Math.Max(1, Screen.width), h = Math.Max(1, Screen.height);
            bool on = st.on && rt != null;
            if (rt != builtFor || w != builtW || h != builtH || on != builtOn)
            {
                builtFor = rt;
                builtW = w;
                builtH = h;
                builtOn = on;
                Record(cb, rt, w, h, on);
            }
            mat.SetVector(idScreen, new Vector4(w, h, (float)st.horizon, 1.18f));
            if (!on) return;
            int qw = Math.Max(1, w / 4), qh = Math.Max(1, h / 4);
            mat.SetVector(idTexel, new Vector4(1f / qw, 1f / qh, 0, 0));
            // the sun in uv: y up
            mat.SetVector(idSun, new Vector4((float)st.sunX, (float)(1 - st.sunY), (float)st.sunVis, (float)st.bloom));
            mat.SetVector(idSunColor, new Vector4((float)st.sunR, (float)st.sunG, (float)st.sunB, 1));
            mat.SetVector(idFog, new Vector4((float)st.fogR, (float)st.fogG, (float)st.fogB, (float)st.fog));
            // the clock wraps so the shader's float keeps its precision
            mat.SetVector(idRain, new Vector4((float)st.rain, (float)st.rainLean, (float)(st.t % 600.0), (float)st.grain));
            // w: the brightness lift (a gamma; 1 = as drawn)
            mat.SetVector(idScreen, new Vector4(w, h, (float)st.horizon, 1.18f));
        }

        void Record(CommandBuffer cb, RenderTexture rt, int w, int h, bool on)
        {
            cb.Clear();
            if (rt == null) return;
            var target = new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget);
            if (!on)
            {
                // no effects, but the same brightness
                cb.Blit(rt, target, mat, 5);
                return;
            }
            int qw = Math.Max(1, w / 4), qh = Math.Max(1, h / 4);
            cb.GetTemporaryRT(idA, qw, qh, 0, FilterMode.Bilinear, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            cb.GetTemporaryRT(idB, qw, qh, 0, FilterMode.Bilinear, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            cb.GetTemporaryRT(idC, qw, qh, 0, FilterMode.Bilinear, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            // the bright parts, at a quarter of the size
            cb.Blit(rt, idA, mat, 0);
            // the shafts, from the bright parts before they are blurred (the ridges stay sharp in them)
            cb.Blit(idA, idC, mat, 3);
            // the glow
            cb.Blit(idA, idB, mat, 1);
            cb.Blit(idB, idA, mat, 2);
            cb.Blit(idA, idB, mat, 1);
            cb.Blit(idB, idA, mat, 2);
            cb.SetGlobalTexture(idBloom, idA);
            cb.SetGlobalTexture(idRays, idC);
            cb.Blit(rt, target, mat, 4);
            cb.ReleaseTemporaryRT(idA);
            cb.ReleaseTemporaryRT(idB);
            cb.ReleaseTemporaryRT(idC);
        }
    }
}
