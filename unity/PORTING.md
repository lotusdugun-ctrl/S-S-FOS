# Porting the web build (TypeScript) to C# — rules for everyone

The Unity build must look, sound and play **exactly** like the web build. The web code in
`/home/claude/sisifos/src` is the specification. Port line for line; do not "improve",
simplify, re-tune, or drop anything (comments explaining *why* may be shortened but keep the
important ones). If something cannot be ported exactly, say so in a `// PORT:` comment.

## Language
- C# **9** (Unity 6). No file-scoped namespaces, no global usings, no `record struct`,
  no `required`, no raw string literals, no list patterns. `Nullable` is off.
- Pure C#: files under `Assets/Scripts/Paint`, `Game`, `Audio` must **not** use `UnityEngine`.
- Namespace `Sisifos.Game` for game code (`Sisifos.Audio` for audio). `using Sisifos.Paint;`
- Numbers: TS `number` → `double` (use `int` only where the TS value is clearly an integer
  index/count). Keep every constant and formula exactly.
- **Identifiers: keep the TS names exactly** (camelCase fields, methods, locals, enum-like
  strings). A module's free functions/constants go in a `public static class` named after the
  file in PascalCase (`sky.ts` → `Sky`, `figures.ts` → `Figures`, `zeus.ts` → `Zeus`,
  `levels.ts` → `Levels`, `textures.ts` → `Textures`). Exported classes keep their names
  (`MountainPainter`, `ZeusPainter`, `Saga`, `GameAudio`, `SisyphusEngine`). Exported TS types
  become C# classes (or structs when small and value-like) with the **same name and field
  names**. Make everything that is exported `public`; module-private things may be `internal`
  or `private`.
- String unions (`"walk" | "push"`) stay **strings** (compare with `==`), unless it is
  obviously internal to one file. This keeps cross-file calls predictable.
- Optional/nullable object types → class references that can be `null`. `undefined` → `null`
  or a sentinel; `??` and `?.` exist in C#.
- Tuples `[a, b]` → small structs or `ValueTuple` (`(double x, double y)`); `RGB` is the struct
  in `Game/Common.cs` (`new RGB(r,g,b)`, `c[0]`, `RGB.Hex("#rrggbb")`).
- `Map` → `Dictionary`, `Set` → `HashSet`, arrays → `List<T>` or `T[]`, `Float64Array` →
  `double[]`, `Uint8ClampedArray` → `byte[]` (clamp explicitly when writing!).
- Closures (`const f = (..) => ..`) → local functions or lambdas. Avoid allocating lambdas per
  frame in hot paths when a local function works.
- `Math.*` → `System.Math`, **except** use `Js.round` for `Math.round` (JS rounds .5 up),
  `Js.random()` for `Math.random()`, `Js.sign`, `Js.hypot`, `Js.imul`, `Js.urs(x,n)` for
  `x >>> n`, `Js.ToInt32(x)` for `x | 0`, `Js.now()` for `performance.now()`,
  `Js.dateNow()` for `Date.now()`. `%` behaves the same on doubles. Beware integer division:
  `a / b` with ints truncates in C# — cast to double.
- Never format numbers with the current culture: use `CultureInfo.InvariantCulture` or
  `Js.toFixed`. (Turkish devices write decimals with a comma.)

## Drawing: the canvas API
`Sisifos.Paint.Ctx` is a faithful HTML-canvas 2D context (verified pixel-for-pixel against
Chrome). Map 1:1:

| TS | C# |
|---|---|
| `HTMLCanvasElement` / `OffscreenCanvas` / `document.createElement("canvas")` | `Surface` / `new Surface(w, h)` |
| `cv.width = n` (clears) | `cv.width = n` or `cv.SetSize(w, h)` |
| `cv.getContext("2d")` | `cv.GetContext()` (never null) |
| `CanvasRenderingContext2D` | `Ctx` |
| `ctx.fillStyle = "oklch(..)"` (constant string) | `ctx.FillStyle = "oklch(..)";` (strings parse, cached) |
| `` ctx.fillStyle = `rgba(1,2,3,${a})` `` | `ctx.FillStyle = Col.Rgba(1, 2, 3, a);` (**never** string interpolation) |
| `` `oklch(0.5 0.1 40 / ${a})` `` | `Col.Oklch(0.5, 0.1, 40, a)` |
| `` `hsla(${h}, ${s}%, ${l}%, ${a})` `` | `Col.Hsla(h, s, l, a)` |
| `rgb(c, a)` from sky.ts | `Css.rgb(c, a)` (or `Sky.rgb`, which must just call it) |
| `x.toFixed(3)` inside a colour | `Css.fixedNum(x, 3)` (only if the rounding matters) |
| `ctx.beginPath()` etc. | `ctx.BeginPath()`, `MoveTo`, `LineTo`, `QuadraticCurveTo`, `BezierCurveTo`, `Arc`, `Ellipse`, `Rect`, `RoundRect`, `ArcTo`, `ClosePath` |
| `ctx.fill()`, `fill("evenodd")`, `stroke()`, `clip()` | `Fill()`, `Fill("evenodd")`, `Stroke()`, `Clip()` |
| `fillRect/strokeRect/clearRect` | `FillRect/StrokeRect/ClearRect` |
| `save/restore/translate/rotate/scale/transform/setTransform/resetTransform/getTransform` | same, PascalCase; `getTransform()` returns `Mat2D`, `SetTransform(Mat2D)` exists |
| `createLinearGradient/createRadialGradient` + `addColorStop(o, "css" or Col)` | `CreateLinearGradient`/`CreateRadialGradient` + `AddColorStop` |
| `createPattern(cv, "repeat")` | `CreatePattern(surface, "repeat")` |
| `drawImage(cv, ...)` (3, 5 or 9 args) | `DrawImage(surface, ...)` |
| `globalAlpha`, `lineWidth`, `shadowBlur`, `shadowColor`, `shadowOffsetX/Y`, `lineDashOffset`, `miterLimit`, `font`, `filter`, `imageSmoothingEnabled` | PascalCase properties |
| `globalCompositeOperation = "source-atop"` | `GlobalCompositeOperation = Composite.SourceAtop` (also `Lighter`, `SourceIn`, `DestinationOut`, `DestinationIn`, `Copy`, `Saturation`, `Multiply`, `Screen`, `SourceOver`) |
| `lineCap = "round"` / `lineJoin = "round"` | `LineCap = LineCap.Round` / `LineJoin = LineJoin.Round` |
| `textAlign = "center"` / `textBaseline = "middle"` | `TextAlign = TextAlign.Center` / `TextBaseline = TextBaseline.Middle` |
| `setLineDash([4, 2])` | `SetLineDash(new double[] { 4, 2 })`; `SetLineDash(null)` clears |
| `fillText`, `measureText(t).width` | `FillText(t, x, y)`, `MeasureText(t)` (returns the width) |
| `putImageData(new ImageData(px, w, h), x, y)` | `surface.PutPixels(byte[] rgba, x, y, w, h)` (straight alpha, top row first) |
| `CanvasGradient` / `CanvasPattern` type | `Gradient` (`LinearGradient`/`RadialGradient`) / `Pattern` |
| a `string | CanvasGradient` fill parameter | `Style` (implicit from string, `Col`, gradient, pattern) |

There is no getImageData: nothing in the game reads pixels back.

Heavy work the web build sent to the paint worker (`paintRequest`) → `Work.Run(() => job(), result => ...)`.
The result arrives on the main thread at `Work.Pump()` (the engine calls it every frame). Keep
the same fallback behaviour as the TS when the worker was unavailable, but in C# the "worker"
is always available.

## Checking your work
`cd /home/claude/sisifos/unity/Headless && dotnet build -c Release -o /tmp/claude-0/hl-<you>`
compiles everything under Paint/Game/Audio plus the harness (offline, `nuget.config` clears the
sources). It must build with **0 errors** before you finish. Other people are adding files at
the same time; if their unfinished file breaks the build, do not edit it — build with your own
copy of `Headless.csproj` that only includes Paint, `Game/Common.cs` and your files (put that
copy under `/tmp/claude-0/`, with `<Compile Include="/home/claude/sisifos/unity/Assets/Scripts/..." />`
paths, and a `nuget.config` with `<clear />` beside it).

Visual parity is checked by rendering with the CPU backend (`Headless/CpuBackend.cs`, used by
`Headless/Program.cs`) and comparing with the same drawing done by the original TS in Chromium
(Playwright is installed: `require('playwright')`, `chromium.launch()`; the dev server can be
started with `cd /home/claude/sisifos && npx vite --port 5199` if you need the real modules, or
bundle a single TS file with `/opt/npm-tools/node_modules/@esbuild/linux-x64/bin/esbuild src/game/x.ts --bundle --format=iife --global-name=X --outfile=/tmp/claude-0/x.js` run from `/home/claude/sisifos` (tsconfig paths resolve `@/`)).

Scratch files go under `/tmp/claude-0/<your-name>/`. Do not touch git, and do not edit files outside the ones you were given (plus new files you create in your own area).

## Shared helpers already written (use them, do not duplicate)
- `Game/Common.cs`: `RGB`, `Css.rgb/rgba/oklch/hsla/fixedNum`.
- `Game/Data.cs`: `Data.Text("story-text/en")` returns bundled JSON text
  (files in `Assets/Resources/Data/...`); `Store.Get/Set/Remove` replaces `localStorage`.
- `Game/Json.cs`: `Json.Parse` (objects → `Dictionary<string, object>`, arrays →
  `List<object>`, numbers → `double`) and `Json.Stringify`.
- `Paint/Js.cs`: `Js.*` (JS number semantics) and `Work.Run/Pump` (the paint worker).

## Your test harness
Keep it out of the repo: `/tmp/claude-0/<your-name>/` with its own `.csproj` (copy
`Headless/Headless.csproj`, replace the Compile items with absolute paths to Paint/**, the
Game files you need, and `/home/claude/sisifos/unity/Headless/CpuBackend.cs`), a
`nuget.config` with `<clear />`, and your own `Program.cs`. Set `Gfx.backend = new CpuBackend()`,
draw, `Gfx.Flush()`, `backend.SavePng(surface, file)`. For data, set
`Data.loader = p => File.ReadAllText("/home/claude/sisifos/unity/Assets/Resources/Data/" + p + ".json")`.
Look at your PNGs (the Read tool shows images) side by side with the Chromium render.
