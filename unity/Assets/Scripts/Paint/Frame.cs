using System;
using System.Collections.Generic;

namespace Sisifos.Paint
{
    /// <summary>How a triangle's colour is found.</summary>
    public enum PaintKind
    {
        Solid = 0,
        Linear = 1,
        Radial = 2,
        Image = 3,
        Pattern = 4,
        /// <summary>The image's alpha, painted in the vertex colour (shadows).</summary>
        Mask = 5,
    }

    /// <summary>
    /// One vertex as the GPU sees it. Positions are in the target's device pixels, y down.
    /// </summary>
    public struct Vert
    {
        public float x, y;
        /// <summary>Straight colour; for gradients and images only the alpha (globalAlpha) is used.</summary>
        public float r, g, b, a;
        /// <summary>Gradient-space position, or texture coordinate (0..1, y down).</summary>
        public float u, v;
        public float kind, row;
        public float p0, p1, p2, p3, p4, p5;
    }

    public enum StencilMode { None, Push, Pop }

    public enum CmdKind
    {
        Draw,
        Clear,
        Pixels,
        Blur,
        Saturation,
        Text,
        Alloc,
    }

    public sealed class TextRun
    {
        public string text;
        public string font;
        public double x, y;
        public TextAlign align;
        public TextBaseline baseline;
        public Col color;
        public Mat2D m;
        public double maxWidth = double.NaN;
        public bool stroke;
        public double lineWidth;
    }

    public struct Cmd
    {
        public CmdKind kind;
        public Surface target;
        public Composite blend;
        public Surface texture;
        public bool repeat;
        public int first, count;     // index range
        public int clip;             // stencil level the draw is tested against
        public StencilMode stencil;
        // clear / pixels / blur / saturation
        public int rx, ry, rw, rh;
        public byte[] pixels;
        public float radius;
        public Col color;
        public TextRun text;
    }

    /// <summary>
    /// Everything drawn in one frame, in order: one vertex and index stream shared by every
    /// surface, a command list, and the gradients those commands paint with.
    /// </summary>
    public sealed class Frame
    {
        public Vert[] verts = new Vert[1 << 14];
        public int vertCount;
        public int[] indices = new int[1 << 15];
        public int indexCount;
        public readonly List<Cmd> cmds = new List<Cmd>(1024);
        public readonly List<Gradient> gradients = new List<Gradient>(128);
        public int id;

        public void Reset(int frameId)
        {
            id = frameId;
            vertCount = 0;
            indexCount = 0;
            cmds.Clear();
            gradients.Clear();
        }

        public int AddVert(in Vert v)
        {
            if (vertCount == verts.Length) Array.Resize(ref verts, verts.Length * 2);
            verts[vertCount] = v;
            return vertCount++;
        }

        public void AddIndex(int i)
        {
            if (indexCount == indices.Length) Array.Resize(ref indices, indices.Length * 2);
            indices[indexCount++] = i;
        }

        public int RowFor(Gradient g)
        {
            if (g.atlasFrame == id && g.atlasRow >= 0 && g.atlasRow < gradients.Count && ReferenceEquals(gradients[g.atlasRow], g)
                && g.atlasVersion == g.version)
                return g.atlasRow;
            g.atlasFrame = id;
            g.atlasRow = gradients.Count;
            g.atlasVersion = g.version;
            gradients.Add(g);
            return g.atlasRow;
        }

        /// <summary>Append a draw over indices [first, first+count), merging with the last if it matches.</summary>
        public void Draw(Surface target, Composite blend, Surface tex, bool repeat, int first, int count, int clip)
        {
            if (count <= 0) return;
            int n = cmds.Count;
            if (n > 0)
            {
                var last = cmds[n - 1];
                if (last.kind == CmdKind.Draw && last.stencil == StencilMode.None && last.target == target
                    && last.blend == blend && last.texture == tex && last.repeat == repeat && last.clip == clip
                    && last.first + last.count == first)
                {
                    last.count += count;
                    cmds[n - 1] = last;
                    return;
                }
            }
            cmds.Add(new Cmd
            {
                kind = CmdKind.Draw, target = target, blend = blend, texture = tex, repeat = repeat,
                first = first, count = count, clip = clip,
            });
        }

        public void Add(in Cmd c) { cmds.Add(c); }
    }

    /// <summary>Executes a frame's commands on some device (the GPU in Unity, a CPU rasteriser in tests).</summary>
    public interface IBackend
    {
        void Execute(Frame f);
        /// <summary>Width of a text run in CSS pixels, for measureText.</summary>
        double MeasureText(string text, string font);
        /// <summary>A surface is gone; free whatever holds its pixels.</summary>
        void Release(Surface s);
        /// <summary>
        /// The surface's pixels as 8-bit straight-alpha RGBA, top row first (getImageData), after
        /// everything flushed so far. Used to share a picture (share cards); slow, never per frame.
        /// </summary>
        byte[] ReadPixels(Surface s, out int w, out int h);
    }

    /// <summary>The drawing system: the frame being built and where it goes.</summary>
    public static class Gfx
    {
        public static IBackend backend;
        public static readonly Frame frame = new Frame();
        static int frameId = 1;

        public static int FrameId { get { return frameId; } }

        /// <summary>Send everything drawn since the last flush to the device.</summary>
        public static void Flush()
        {
            if (frame.cmds.Count > 0 && backend != null) backend.Execute(frame);
            frame.Reset(++frameId);
        }

        public static double MeasureText(string text, string font)
        {
            if (backend != null) return backend.MeasureText(text, font);
            return FontSpec.Parse(font).size * 0.5 * (text == null ? 0 : text.Length);
        }
    }
}
