using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Sisifos.Paint;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sisifos.App
{
    /// <summary>
    /// Executes a <see cref="Frame"/> on the GPU, with the semantics of the reference rasteriser
    /// (unity/Headless/CpuBackend.cs): premultiplied colour, the same composite equations, stencil
    /// clips, blur, saturation. Every Surface is a RenderTexture (with 4× MSAA when the surface
    /// is antialiased, and a stencil buffer for clips).
    ///
    /// Per frame: the frame's vertices are copied once into one dynamic Mesh (plus the quads the
    /// backend adds for clears, pixel uploads, saturation and text), each command becomes a
    /// submesh, and the whole command list is recorded into one CommandBuffer and executed with
    /// Graphics.ExecuteCommandBuffer. Nothing is allocated per frame once the buffers have grown.
    /// </summary>
    public sealed class UnityBackend : IBackend, IDisposable
    {
        // ------------------------------------------------------------ vertex format

        [StructLayout(LayoutKind.Sequential)]
        struct GVert
        {
            public float x, y, z;
            public float r, g, b, a;
            public float u, v, kind, row;
            public float p0, p1, p2, p3;
            public float p4, p5;
        }

        static readonly VertexAttributeDescriptor[] layout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 2),
        };

        const MeshUpdateFlags Fast = MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers
            | MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontResetBoneBounds;

        // backend-only paint kinds (see SisifosPaint.shader)
        const float KindPixels = 6, KindSaturation = 7, KindGlyph = 8;

        // ------------------------------------------------------------ surfaces

        /// <summary>What a Surface holds on the GPU (Surface.gpu).</summary>
        sealed class GS
        {
            public RenderTexture rt;
            public int w, h;
            public bool aa;
            /// <summary>Frame in which the stencil was last cleared.</summary>
            public int stencilFrame = -1;
            /// <summary>Drawn into since the last MSAA resolve.</summary>
            public bool dirty;
            public string name;
        }

        struct Entry
        {
            public WeakReference<Surface> surface;
            public GS gs;
        }

        // ------------------------------------------------------------ state

        readonly CommandBuffer cb = new CommandBuffer { name = "Sisifos.Paint" };
        readonly Mesh mesh;
        readonly Shader paintShader, blurShader;
        readonly Material blurMat;
        readonly Dictionary<int, Material> materials = new Dictionary<int, Material>();
        readonly MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        readonly Texture2D transparent;
        readonly List<Entry> registry = new List<Entry>();
        readonly List<UnityEngine.Object> doomed = new List<UnityEngine.Object>();
        readonly GradientLuts luts = new GradientLuts(GradW);
        readonly TextEngine text;

        const int GradW = 512;
        Texture2D gradTex;
        ushort[] gradData;
        ushort[][] gradRows = new ushort[0][];

        GVert[] gv = new GVert[1 << 14];
        int gvn;
        int[] gi = new int[1 << 15];
        int gin;
        SubMeshDescriptor[] subs = new SubMeshDescriptor[256];
        int subn;
        int[] cmdSub = new int[1024];
        Texture[] cmdTex = new Texture[1024];
        int meshVerts, meshIndices;

        int frameNo;
        GS cur;

        static readonly int idMainTex = Shader.PropertyToID("_MainTex");
        static readonly int idRepeat = Shader.PropertyToID("_Repeat");
        static readonly int idGradTex = Shader.PropertyToID("_GradTex");
        static readonly int idGradSize = Shader.PropertyToID("_GradSize");
        static readonly int idTargetSize = Shader.PropertyToID("_TargetSize");
        static readonly int idDstTex = Shader.PropertyToID("_DstTex");
        static readonly int idBlur = Shader.PropertyToID("_BlurParams");
        static readonly int idTmpA = Shader.PropertyToID("_SisifosTmpA");
        static readonly int idSrcBlend = Shader.PropertyToID("_SrcBlend");
        static readonly int idDstBlend = Shader.PropertyToID("_DstBlend");
        static readonly int idSrcBlendA = Shader.PropertyToID("_SrcBlendA");
        static readonly int idDstBlendA = Shader.PropertyToID("_DstBlendA");
        static readonly int idStencilRef = Shader.PropertyToID("_StencilRef");
        static readonly int idStencilComp = Shader.PropertyToID("_StencilComp");
        static readonly int idStencilPass = Shader.PropertyToID("_StencilPass");
        static readonly int idColorMask = Shader.PropertyToID("_ColorMask");
        static readonly int idDstMode = Shader.PropertyToID("_DstMode");

        public UnityBackend()
        {
            paintShader = Resources.Load<Shader>("Shaders/SisifosPaint");
            blurShader = Resources.Load<Shader>("Shaders/SisifosBlur");
            if (paintShader == null || blurShader == null) throw new InvalidOperationException("Sisifos shaders missing from Resources/Shaders");
            blurMat = new Material(blurShader) { hideFlags = HideFlags.HideAndDontSave };
            mesh = new Mesh { name = "Sisifos.Frame", hideFlags = HideFlags.HideAndDontSave };
            mesh.MarkDynamic();
            mesh.indexFormat = IndexFormat.UInt32;
            transparent = new Texture2D(1, 1, TextureFormat.RGBA32, false, true) { hideFlags = HideFlags.HideAndDontSave };
            transparent.SetPixel(0, 0, new Color(0, 0, 0, 0));
            transparent.Apply(false, false);
            text = TextEngine.Create();
        }

        public void Dispose()
        {
            foreach (var e in registry) if (e.gs.rt != null) Kill(e.gs.rt);
            registry.Clear();
            foreach (var m in materials.Values) Kill(m);
            materials.Clear();
            Kill(blurMat); Kill(mesh); Kill(transparent);
            if (gradTex != null) Kill(gradTex);
            text.Dispose();
            cb.Release();
        }

        static void Kill(UnityEngine.Object o)
        {
            if (o == null) return;
            if (o is RenderTexture rt) rt.Release();
            UnityEngine.Object.Destroy(o);
        }

        /// <summary>The render texture showing a surface (null before it is first drawn).</summary>
        public RenderTexture TextureOf(Surface s)
        {
            var g = s?.gpu as GS;
            return g?.rt;
        }

        public double MeasureText(string t, string font) { return text.Measure(t, font); }

        /// <summary>
        /// getImageData for a whole surface: resolve, copy through a plain texture, read back
        /// (the GPU waits), flip to top-row-first and un-premultiply. Call after Gfx.Flush().
        /// </summary>
        public byte[] ReadPixels(Surface s, out int w, out int h)
        {
            w = Math.Max(1, s.width);
            h = Math.Max(1, s.height);
            var o = new byte[w * h * 4];
            var g = s.gpu as GS;
            if (g == null || g.rt == null) return o;
            if (g.dirty)
            {
                Resolve(g);
                Graphics.ExecuteCommandBuffer(cb);
                cb.Clear();
            }
            var tmp = RenderTexture.GetTemporary(g.w, g.h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(g.rt, tmp);
            var prev = RenderTexture.active;
            RenderTexture.active = tmp;
            var tex = new Texture2D(g.w, g.h, TextureFormat.RGBA32, false, true);
            tex.ReadPixels(new Rect(0, 0, g.w, g.h), 0, 0, false);
            tex.Apply(false, false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(tmp);
            var raw = tex.GetRawTextureData();
            Kill(tex);
            int rw = Math.Min(w, g.w), rh = Math.Min(h, g.h);
            for (int y = 0; y < rh; y++)
            {
                // texture rows run bottom-up; the canvas's top row is the texture's last
                int src = (g.h - 1 - y) * g.w * 4, dst = y * w * 4;
                for (int x = 0; x < rw; x++, src += 4, dst += 4)
                {
                    int a = raw[src + 3];
                    o[dst + 3] = (byte)a;
                    if (a == 0) continue;
                    o[dst] = (byte)Math.Min(255, (raw[src] * 255 + a / 2) / a);
                    o[dst + 1] = (byte)Math.Min(255, (raw[src + 1] * 255 + a / 2) / a);
                    o[dst + 2] = (byte)Math.Min(255, (raw[src + 2] * 255 + a / 2) / a);
                }
            }
            return o;
        }

        public void Release(Surface s)
        {
            var g = s.gpu as GS;
            if (g == null) return;
            if (g.rt != null) Kill(g.rt);
            g.rt = null;
            for (int i = registry.Count - 1; i >= 0; i--)
                if (registry[i].gs == g) { registry.RemoveAt(i); break; }
        }

        // ------------------------------------------------------------ frame

        public void Execute(Frame f)
        {
            frameNo++;
            var cmds = f.cmds;
            int n = cmds.Count;
            if (cmdSub.Length < n) { cmdSub = new int[n * 2]; cmdTex = new Texture[n * 2]; }

            // 1. geometry: the frame's own vertices, then whatever the backend adds
            gvn = 0; gin = 0; subn = 0;
            Grow(ref gv, f.vertCount);
            for (int i = 0; i < f.vertCount; i++)
            {
                ref readonly Vert s = ref f.verts[i];
                ref GVert d = ref gv[i];
                d.x = s.x; d.y = s.y; d.z = 0;
                d.r = s.r; d.g = s.g; d.b = s.b; d.a = s.a;
                d.u = s.u; d.v = s.v; d.kind = s.kind; d.row = s.row;
                d.p0 = s.p0; d.p1 = s.p1; d.p2 = s.p2; d.p3 = s.p3; d.p4 = s.p4; d.p5 = s.p5;
            }
            gvn = f.vertCount;
            Grow(ref gi, f.indexCount);
            Array.Copy(f.indices, gi, f.indexCount);
            gin = f.indexCount;

            text.BeginFrame(f, frameNo);
            for (int ci = 0; ci < n; ci++)
            {
                var c = cmds[ci];
                cmdSub[ci] = -1;
                cmdTex[ci] = null;
                switch (c.kind)
                {
                    case CmdKind.Draw:
                        cmdSub[ci] = AddSub(c.first, c.count);
                        break;
                    case CmdKind.Clear:
                    {
                        int s = gin;
                        Quad(c.rx, c.ry, c.rx + c.rw, c.ry + c.rh, 0, 0, 0, 0, 0, 0, 0, 0, 0);
                        cmdSub[ci] = AddSub(s, 6);
                        break;
                    }
                    case CmdKind.Pixels:
                    {
                        int s = gin;
                        Quad(c.rx, c.ry, c.rx + c.rw, c.ry + c.rh, 1, 1, 1, 1, 0, 0, 1, 1, KindPixels);
                        cmdSub[ci] = AddSub(s, 6);
                        cmdTex[ci] = Upload(c);
                        break;
                    }
                    case CmdKind.Saturation:
                    {
                        int s = gin;
                        var col = c.color;
                        Quad(c.rx, c.ry, c.rx + c.rw, c.ry + c.rh, col.r, col.g, col.b, col.a, 0, 0, 0, 0, KindSaturation);
                        cmdSub[ci] = AddSub(s, 6);
                        break;
                    }
                    case CmdKind.Text:
                    {
                        int s = gin;
                        var tex = text.Emit(c.text, this);
                        if (tex != null && gin > s)
                        {
                            cmdSub[ci] = AddSub(s, gin - s);
                            cmdTex[ci] = tex;
                        }
                        break;
                    }
                }
            }
            for (int i = 0; i < subn; i++) subs[i].vertexCount = gvn;
            UploadMesh();
            UploadGradients(f);

            // 2. commands
            cb.Clear();
            cur = null;
            if (gradTex != null)
            {
                cb.SetGlobalTexture(idGradTex, gradTex);
                cb.SetGlobalVector(idGradSize, new Vector4(GradW, gradTex.height, 0, 0));
            }
            for (int ci = 0; ci < n; ci++)
            {
                var c = cmds[ci];
                switch (c.kind)
                {
                    case CmdKind.Alloc: Alloc(c); break;
                    case CmdKind.Clear: Clear(c, cmdSub[ci]); break;
                    case CmdKind.Pixels:
                    {
                        var g = Get(c.target);
                        Bind(g);
                        Draw(g, cmdSub[ci], Mat(Composite.Copy, StencilMode.None, 0), cmdTex[ci], false);
                        break;
                    }
                    case CmdKind.Draw: DrawCmd(c, cmdSub[ci]); break;
                    case CmdKind.Blur: Blur(Get(c.target), c.radius); break;
                    case CmdKind.Saturation: Saturation(c, cmdSub[ci]); break;
                    case CmdKind.Text:
                        if (cmdSub[ci] >= 0) Paint(Get(c.target), cmdSub[ci], c.blend, c.clip, cmdTex[ci], false);
                        break;
                }
            }
            // the screen is shown by the camera after this: resolve it now
            foreach (var e in registry)
            {
                if (e.gs.rt != null && e.gs.dirty && e.surface.TryGetTarget(out var s) && s.isScreen) Resolve(e.gs);
            }
            Graphics.ExecuteCommandBuffer(cb);
            cb.Clear();

            for (int i = 0; i < doomed.Count; i++) Kill(doomed[i]);
            doomed.Clear();
            for (int i = 0; i < n; i++) cmdTex[i] = null;
            text.EndFrame(frameNo);
            if ((frameNo & 63) == 0)
            {
                Sweep();
                luts.Trim(frameNo, 300);
            }
        }

        static void Grow<T>(ref T[] a, int need)
        {
            if (a.Length >= need) return;
            int n = a.Length;
            while (n < need) n *= 2;
            Array.Resize(ref a, n);
        }

        int AddSub(int first, int count)
        {
            if (count <= 0) return -1;
            Grow(ref subs, subn + 1);
            subs[subn] = new SubMeshDescriptor(first, count, MeshTopology.Triangles) { baseVertex = 0, firstVertex = 0 };
            return subn++;
        }

        /// <summary>An axis-aligned quad in target pixels (two triangles appended to the index stream).</summary>
        void Quad(float x0, float y0, float x1, float y1, float r, float g, float b, float a,
            float u0, float v0, float u1, float v1, float kind)
        {
            QuadAt(x0, y0, x1, y0, x1, y1, x0, y1, r, g, b, a, u0, v0, u1, v1, kind);
        }

        /// <summary>A quad with arbitrary corners (top-left, top-right, bottom-right, bottom-left).</summary>
        internal void QuadAt(float ax, float ay, float bx, float by, float cx, float cy, float dx, float dy,
            float r, float g, float b, float a, float u0, float v0, float u1, float v1, float kind)
        {
            Grow(ref gv, gvn + 4);
            Grow(ref gi, gin + 6);
            int k = gvn;
            gv[k] = new GVert { x = ax, y = ay, r = r, g = g, b = b, a = a, u = u0, v = v0, kind = kind };
            gv[k + 1] = new GVert { x = bx, y = by, r = r, g = g, b = b, a = a, u = u1, v = v0, kind = kind };
            gv[k + 2] = new GVert { x = cx, y = cy, r = r, g = g, b = b, a = a, u = u1, v = v1, kind = kind };
            gv[k + 3] = new GVert { x = dx, y = dy, r = r, g = g, b = b, a = a, u = u0, v = v1, kind = kind };
            gvn += 4;
            gi[gin++] = k; gi[gin++] = k + 1; gi[gin++] = k + 2;
            gi[gin++] = k; gi[gin++] = k + 2; gi[gin++] = k + 3;
        }

        /// <summary>A glyph quad for text engines, in the glyph kind.</summary>
        internal void GlyphQuad(float ax, float ay, float bx, float by, float cx, float cy, float dx, float dy,
            Col col, float u0, float v0, float u1, float v1)
        {
            QuadAt(ax, ay, bx, by, cx, cy, dx, dy, col.r, col.g, col.b, col.a, u0, v0, u1, v1, KindGlyph);
        }

        /// <summary>Glyph quad with explicit per-corner uvs (for font atlases that store glyphs rotated).</summary>
        internal void GlyphQuadUV(float ax, float ay, float bx, float by, float cx, float cy, float dx, float dy, Col col,
            Vector2 ta, Vector2 tb, Vector2 tc, Vector2 td)
        {
            Grow(ref gv, gvn + 4);
            Grow(ref gi, gin + 6);
            int k = gvn;
            gv[k] = new GVert { x = ax, y = ay, r = col.r, g = col.g, b = col.b, a = col.a, u = ta.x, v = ta.y, kind = KindGlyph };
            gv[k + 1] = new GVert { x = bx, y = by, r = col.r, g = col.g, b = col.b, a = col.a, u = tb.x, v = tb.y, kind = KindGlyph };
            gv[k + 2] = new GVert { x = cx, y = cy, r = col.r, g = col.g, b = col.b, a = col.a, u = tc.x, v = tc.y, kind = KindGlyph };
            gv[k + 3] = new GVert { x = dx, y = dy, r = col.r, g = col.g, b = col.b, a = col.a, u = td.x, v = td.y, kind = KindGlyph };
            gvn += 4;
            gi[gin++] = k; gi[gin++] = k + 1; gi[gin++] = k + 2;
            gi[gin++] = k; gi[gin++] = k + 2; gi[gin++] = k + 3;
        }

        internal int FrameNo { get { return frameNo; } }

        void UploadMesh()
        {
            if (gvn == 0 || gin == 0)
            {
                mesh.subMeshCount = 1;
                mesh.SetSubMesh(0, new SubMeshDescriptor(0, 0), Fast);
                return;
            }
            if (gvn > meshVerts)
            {
                meshVerts = Math.Max(gvn, meshVerts * 2);
                mesh.SetVertexBufferParams(meshVerts, layout);
            }
            if (gin > meshIndices)
            {
                meshIndices = Math.Max(gin, meshIndices * 2);
                // keep old submeshes from pointing past a buffer being resized
                mesh.subMeshCount = 1;
                mesh.SetSubMesh(0, new SubMeshDescriptor(0, 0), Fast);
                mesh.SetIndexBufferParams(meshIndices, IndexFormat.UInt32);
            }
            mesh.SetVertexBufferData(gv, 0, 0, gvn, 0, Fast);
            mesh.SetIndexBufferData(gi, 0, 0, gin, Fast);
            if (subn > 0) mesh.SetSubMeshes(subs, 0, subn, Fast);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(1e7f, 1e7f, 10));
        }

        void UploadGradients(Frame f)
        {
            int rows = f.gradients.Count;
            if (rows == 0) return;
            int h = 16;
            while (h < rows) h *= 2;
            bool changed = false;
            if (gradTex == null || gradTex.height < h)
            {
                if (gradTex != null) doomed.Add(gradTex);
                gradTex = new Texture2D(GradW, h, TextureFormat.RGBAHalf, false, true)
                {
                    hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                    name = "Sisifos.Gradients",
                };
                gradData = new ushort[GradW * h * 4];
                gradRows = new ushort[h][];
                changed = true;
            }
            for (int i = 0; i < rows; i++)
            {
                var row = luts.Row(f.gradients[i], frameNo);
                if (!ReferenceEquals(gradRows[i], row))
                {
                    gradRows[i] = row;
                    Array.Copy(row, 0, gradData, i * GradW * 4, GradW * 4);
                    changed = true;
                }
            }
            if (changed)
            {
                gradTex.SetPixelData(gradData, 0, 0);
                gradTex.Apply(false, false);
            }
        }

        Texture2D Upload(in Cmd c)
        {
            var t = new Texture2D(c.rw, c.rh, TextureFormat.RGBA32, false, true)
            {
                hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            };
            // top row first, as the canvas wrote it; the shader samples it without a flip
            var h = GCHandle.Alloc(c.pixels, GCHandleType.Pinned);
            try { t.LoadRawTextureData(h.AddrOfPinnedObject(), c.rw * c.rh * 4); }
            finally { h.Free(); }
            t.Apply(false, true);
            doomed.Add(t);
            return t;
        }

        // ------------------------------------------------------------ targets

        GS Get(Surface s)
        {
            var g = s.gpu as GS;
            if (g == null)
            {
                g = new GS { aa = s.antialias, name = s.name };
                s.gpu = g;
                registry.Add(new Entry { surface = new WeakReference<Surface>(s), gs = g });
            }
            if (g.rt == null)
            {
                Create(g, s.width, s.height);
                Bind(g);
                cb.ClearRenderTarget(RTClearFlags.All, new Color(0, 0, 0, 0), 1f, 0);
                g.stencilFrame = frameNo;
            }
            return g;
        }

        void Create(GS g, int w, int h)
        {
            var d = new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 24, 1, RenderTextureReadWrite.Linear)
            {
                msaaSamples = g.aa ? 4 : 1,
                useMipMap = false,
                autoGenerateMips = false,
            };
            var rt = new RenderTexture(d)
            {
                name = "Sisifos." + (g.name ?? "surface"),
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                antiAliasing = d.msaaSamples,
                // resolved explicitly (Resolve) before the surface is read
                autoResolve = false,
            };
            rt.Create();
            g.rt = rt;
            g.w = w;
            g.h = h;
            g.dirty = false;
        }

        void Alloc(in Cmd c)
        {
            var s = c.target;
            var g = s.gpu as GS;
            if (g == null)
            {
                g = new GS { aa = s.antialias, name = s.name };
                s.gpu = g;
                registry.Add(new Entry { surface = new WeakReference<Surface>(s), gs = g });
            }
            int w = Math.Max(1, c.rw), h = Math.Max(1, c.rh);
            if (g.rt != null && (g.w != w || g.h != h))
            {
                // commands already recorded may still read the old texture: free it after the frame
                doomed.Add(g.rt);
                if (cur == g) cur = null;
                g.rt = null;
            }
            if (g.rt == null) Create(g, w, h);
            Bind(g);
            cb.ClearRenderTarget(RTClearFlags.All, new Color(0, 0, 0, 0), 1f, 0);
            g.stencilFrame = frameNo;
            g.dirty = true;
        }

        void Bind(GS g)
        {
            if (cur == g) return;
            cb.SetRenderTarget(g.rt);
            // y down, pixel units; Unity flips this for render textures where it must
            cb.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.Ortho(0, g.w, g.h, 0, -1, 1));
            cb.SetGlobalVector(idTargetSize, new Vector4(g.w, g.h, 1f / g.w, 1f / g.h));
            cur = g;
        }

        /// <summary>Make the texture side of an MSAA target current before it is read.</summary>
        void Resolve(GS g)
        {
            if (!g.dirty) return;
            g.dirty = false;
            if (g.aa) cb.ResolveAntiAliasedSurface(g.rt);
        }

        /// <summary>The stencil starts each frame at zero (as the reference clears it at frame end).</summary>
        void EnsureStencil(GS g)
        {
            if (g.stencilFrame == frameNo) return;
            Bind(g);
            cb.ClearRenderTarget(RTClearFlags.Stencil, new Color(0, 0, 0, 0), 1f, 0);
            g.stencilFrame = frameNo;
        }

        void Sweep()
        {
            for (int i = registry.Count - 1; i >= 0; i--)
            {
                var e = registry[i];
                if (e.surface.TryGetTarget(out _)) continue;
                if (e.gs.rt != null) Kill(e.gs.rt);
                e.gs.rt = null;
                registry.RemoveAt(i);
            }
        }

        // ------------------------------------------------------------ commands

        void Clear(in Cmd c, int sub)
        {
            var g = Get(c.target);
            Bind(g);
            g.dirty = true;
            if (c.rx <= 0 && c.ry <= 0 && c.rx + c.rw >= g.w && c.ry + c.rh >= g.h)
            {
                cb.ClearRenderTarget(RTClearFlags.Color, new Color(0, 0, 0, 0), 1f, 0);
                return;
            }
            if (sub >= 0) Draw(g, sub, Mat(Composite.Copy, StencilMode.None, 0), null, false);
        }

        void DrawCmd(in Cmd c, int sub)
        {
            if (sub < 0) return;
            var g = Get(c.target);
            Texture tex = null;
            if (c.texture != null)
            {
                var src = Get(c.texture);
                Resolve(src);
                tex = src.rt;
            }
            if (c.stencil != StencilMode.None)
            {
                EnsureStencil(g);
                Bind(g);
                Draw(g, sub, Mat(Composite.SourceOver, c.stencil, c.clip), null, false);
                return;
            }
            Paint(g, sub, c.blend, c.clip, tex, c.repeat);
        }

        /// <summary>Paint a submesh with a composite mode, tested against the clip level.</summary>
        void Paint(GS g, int sub, Composite blend, int clip, Texture tex, bool repeat)
        {
            if (clip > 0) EnsureStencil(g);
            bool dst = blend == Composite.Multiply;
            if (dst) CopyTarget(g);
            Bind(g);
            Draw(g, sub, Mat(blend, StencilMode.None, clip), tex, repeat);
            if (dst) cb.ReleaseTemporaryRT(idTmpA);
        }

        void Draw(GS g, int sub, Material m, Texture tex, bool repeat)
        {
            mpb.Clear();
            mpb.SetTexture(idMainTex, tex != null ? tex : transparent);
            mpb.SetFloat(idRepeat, repeat ? 1f : 0f);
            cb.DrawMesh(mesh, Matrix4x4.identity, m, sub, 0, mpb);
            g.dirty = true;
        }

        /// <summary>A copy of the target in _DstTex, for the modes that read what they paint over.</summary>
        void CopyTarget(GS g)
        {
            Resolve(g);
            cb.GetTemporaryRT(idTmpA, g.w, g.h, 0, FilterMode.Point, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            cb.Blit(g.rt, new RenderTargetIdentifier(idTmpA));
            cb.SetGlobalTexture(idDstTex, new RenderTargetIdentifier(idTmpA));
            cur = null; // Blit changed the target and the matrices
        }

        void Saturation(in Cmd c, int sub)
        {
            if (sub < 0) return;
            var g = Get(c.target);
            CopyTarget(g);
            Bind(g);
            Draw(g, sub, Mat(Composite.Copy, StencilMode.None, 0), null, false);
            cb.ReleaseTemporaryRT(idTmpA);
        }

        /// <summary>Gaussian blur of the whole surface, sigma = radius device pixels (two passes).</summary>
        void Blur(GS g, float sigma)
        {
            if (sigma < 0.3f) return;
            Resolve(g);
            int taps = Math.Min(160, (int)Math.Ceiling(sigma * 3));
            cb.GetTemporaryRT(idTmpA, g.w, g.h, 0, FilterMode.Bilinear, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var tmp = new RenderTargetIdentifier(idTmpA);
            cb.SetGlobalVector(idBlur, new Vector4(1f / g.w, 0, sigma, taps));
            cb.Blit(g.rt, tmp, blurMat, 0);
            cb.SetGlobalVector(idBlur, new Vector4(0, 1f / g.h, sigma, taps));
            cb.Blit(tmp, new RenderTargetIdentifier(g.rt), blurMat, 0);
            cb.ReleaseTemporaryRT(idTmpA);
            g.dirty = true;
            cur = null;
        }

        // ------------------------------------------------------------ materials

        Material Mat(Composite blend, StencilMode stencil, int clip)
        {
            if (blend == Composite.Saturation) blend = Composite.SourceOver;
            int key = (int)blend | ((int)stencil << 4) | (Math.Min(clip, 255) << 8);
            if (materials.TryGetValue(key, out var m)) return m;
            m = new Material(paintShader) { hideFlags = HideFlags.HideAndDontSave, name = "Sisifos." + blend + "." + stencil + "." + clip };
            BlendMode s = BlendMode.One, d = BlendMode.OneMinusSrcAlpha, sa = BlendMode.One, da = BlendMode.OneMinusSrcAlpha;
            switch (blend)
            {
                case Composite.SourceOver: break;
                case Composite.Lighter: s = d = sa = da = BlendMode.One; break;
                case Composite.SourceAtop: s = BlendMode.DstAlpha; d = BlendMode.OneMinusSrcAlpha; sa = BlendMode.Zero; da = BlendMode.One; break;
                case Composite.SourceIn: s = sa = BlendMode.DstAlpha; d = da = BlendMode.Zero; break;
                case Composite.DestinationOut: s = sa = BlendMode.Zero; d = da = BlendMode.OneMinusSrcAlpha; break;
                case Composite.DestinationIn: s = sa = BlendMode.Zero; d = da = BlendMode.SrcAlpha; break;
                case Composite.Copy: s = sa = BlendMode.One; d = da = BlendMode.Zero; break;
                case Composite.Screen: s = BlendMode.OneMinusDstColor; d = BlendMode.One; sa = BlendMode.OneMinusDstAlpha; da = BlendMode.One; break;
                case Composite.Multiply: s = sa = BlendMode.One; d = da = BlendMode.Zero; break; // computed in the shader
            }
            m.SetFloat(idSrcBlend, (float)s);
            m.SetFloat(idDstBlend, (float)d);
            m.SetFloat(idSrcBlendA, (float)sa);
            m.SetFloat(idDstBlendA, (float)da);
            m.SetFloat(idDstMode, blend == Composite.Multiply ? 1f : 0f);
            switch (stencil)
            {
                case StencilMode.Push:
                    m.SetFloat(idStencilRef, clip);
                    m.SetFloat(idStencilComp, (float)CompareFunction.Equal);
                    m.SetFloat(idStencilPass, (float)StencilOp.IncrementSaturate);
                    m.SetFloat(idColorMask, 0);
                    break;
                case StencilMode.Pop:
                    m.SetFloat(idStencilRef, clip);
                    m.SetFloat(idStencilComp, (float)CompareFunction.Equal);
                    m.SetFloat(idStencilPass, (float)StencilOp.DecrementSaturate);
                    m.SetFloat(idColorMask, 0);
                    break;
                default:
                    m.SetFloat(idStencilRef, clip);
                    m.SetFloat(idStencilComp, clip > 0 ? (float)CompareFunction.Equal : (float)CompareFunction.Always);
                    m.SetFloat(idStencilPass, (float)StencilOp.Keep);
                    m.SetFloat(idColorMask, (float)ColorWriteMask.All);
                    break;
            }
            materials[key] = m;
            return m;
        }
    }
}
