using System;
using System.Globalization;

namespace Sisifos.Paint
{
    /// <summary>
    /// An offscreen canvas: pixels that can be drawn into and drawn from. Like a canvas element,
    /// setting a new size clears it.
    /// </summary>
    public sealed class Surface
    {
        int w, h;
        Ctx ctx;
        public readonly bool antialias;
        public readonly string name;
        /// <summary>Whatever the backend keeps for it (a RenderTexture in Unity).</summary>
        public object gpu;
        /// <summary>Bumped on every resize, so a backend knows to reallocate.</summary>
        public int generation;
        /// <summary>The screen: what is drawn here is what is shown.</summary>
        public bool isScreen;

        public Surface(int width, int height, bool antialias = true, string name = null)
        {
            w = Math.Max(1, width);
            h = Math.Max(1, height);
            this.antialias = antialias;
            this.name = name;
            Alloc();
        }

        public int width
        {
            get { return w; }
            set { SetSize(value, h); }
        }

        public int height
        {
            get { return h; }
            set { SetSize(w, value); }
        }

        /// <summary>Resize; as with canvas, this clears it even when the size is unchanged.</summary>
        public void SetSize(int width, int height)
        {
            w = Math.Max(1, width);
            h = Math.Max(1, height);
            if (ctx != null) ctx.ResetState();
            Alloc();
        }

        void Alloc()
        {
            generation++;
            Gfx.frame.Add(new Cmd { kind = CmdKind.Alloc, target = this, rw = w, rh = h });
        }

        public Ctx GetContext()
        {
            if (ctx == null) ctx = new Ctx(this);
            return ctx;
        }

        /// <summary>Upload straight-alpha RGBA bytes (row-major, top row first) into a rectangle.</summary>
        public void PutPixels(byte[] rgba, int x, int y, int pw, int ph)
        {
            if (rgba == null || rgba.Length < pw * ph * 4) throw new ArgumentException("pixels");
            Gfx.frame.Add(new Cmd { kind = CmdKind.Pixels, target = this, pixels = rgba, rx = x, ry = y, rw = pw, rh = ph });
        }

        public void Release()
        {
            if (Gfx.backend != null) Gfx.backend.Release(this);
            gpu = null;
        }

        public override string ToString() { return "Surface(" + (name ?? "") + " " + w + "x" + h + ")"; }
    }

    /// <summary>A parsed CSS font shorthand: style, weight, size, family.</summary>
    public struct FontSpec
    {
        public bool italic;
        public int weight;
        public double size;
        public string family;

        public static FontSpec Parse(string css)
        {
            var f = new FontSpec { weight = 400, size = 10, family = "serif" };
            if (string.IsNullOrEmpty(css)) return f;
            var parts = css.Split(' ');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (p.Length == 0) continue;
                if (p == "italic" || p == "oblique") { f.italic = true; continue; }
                if (p == "bold") { f.weight = 700; continue; }
                if (p == "normal") continue;
                if (p.EndsWith("px"))
                {
                    double.TryParse(p.Substring(0, p.Length - 2), NumberStyles.Float, CultureInfo.InvariantCulture, out f.size);
                    f.family = string.Join(" ", parts, i + 1, parts.Length - i - 1);
                    break;
                }
                if (int.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out var wgt)) f.weight = wgt;
            }
            return f;
        }
    }
}
