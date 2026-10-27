# Porting engine.ts (6861 lines) — shared contract for the four people doing it

`src/game/engine.ts` becomes `public partial class SisyphusEngine` (namespace `Sisifos.Game`)
split by **TS line ranges**, one C# file each:

| part | TS lines | C# file |
|---|---|---|
| A | 1–1786 (module types/constants, `strideFor`, `weatherFor`, all of the class from its start through `renderParticles`) | `Game/Engine.cs` |
| B | 1787–3327 (`render`, `cameraMove`, sun/light, sky layer, celestial, moon, `drawSky`, aphorism/quote layout+sprite, clouds, horizon streaks, `painterFor`, `drawMountain*`) | `Game/Engine.Render.cs` |
| C | 3328–5198 (`drawForeground` … `drawBolt`, incl. boulder, birds, eagle, motes, ground detail, outcrops, stones, whispers, vibrance, `npcLight`, `tanLook`, `drawNpcs`, echoes, wedge, stone memory, steles, breath ring, wind, weather, mire, scree, keepsakes, ruins, trees, Zeus) | `Game/Engine.Scene.cs` |
| D | 5199–6861 (`drawFigureLit`, `drawFigure`) | `Game/Engine.Figure.cs` |

Read `unity/PORTING.md` first (general rules, canvas API mapping). Additional rules here,
so the four parts fit together without anyone seeing the others' code:

1. **Every class member is declared by the part whose line range contains its TS
   declaration** (fields are scattered: e.g. `mountainComp` ~3091 is B's, `stoneTex` ~3440 and
   `groundPat`, `eagle`, `npcPos`, `speaker` are C's, `figLayers`/`rimLayer`/`fadeCv`/`figLo`/
   `figCtxs` ~5171 are C's too because they are declared before 5199). Module-level
   constants/types (lines 1–280) are A's; make them `internal const`/`static readonly` members
   of `SisyphusEngine` (or nested types) with the same names so every part can use them
   unqualified. Exported module functions (`strideFor`, `weatherFor`) are `public static`
   members of `SisyphusEngine`.
2. **Types** (so independently written parts agree):
   - TS `number` fields and parameters → `double`, always (cast at the call site when an
     already-ported API wants `int`). `boolean` → `bool`. `string` and string unions → `string`.
   - `number[]` → `List<double>` unless the TS clearly treats it as fixed-length scratch
     (`Float64Array` → `double[]`). Other arrays → `List<T>`. `Map` → `Dictionary`.
   - A field whose type is an inline object type gets a **nested class named after the field
     in PascalCase + `T`**: `zeus: {state, t}` → `class ZeusT { public string state; public double t; }`,
     `heaveFx` → `HeaveFxT`, `manBox` → `ManBoxT`, `quotePlace` → `QuotePlaceT`, `meteor` →
     `MeteorT`, `stars` → `StarsT`, `focus…` etc. Field names inside are the TS ones.
     Named TS types (`Bird`, `Mote`, `Particle`, `EngineState`, `RunStats` (already exists in
     `Game/RunStats.cs` — use it), `Hint`, `Weather`, `Phase` (strings)) keep their names.
   - `HTMLCanvasElement` → `Surface`, `CanvasRenderingContext2D` → `Ctx`, `CanvasPattern` →
     `Pattern`, `CanvasGradient` → `Gradient`.
   - `T | null` reference → `T` (null), `number | null` → `double?`.
   - Callbacks like `onState`, `onSaga`, `onFinale`, `onMire` → `Action<...>` fields
     initialised to no-op lambdas.
3. **Host instead of the DOM** (part A owns this): no `requestAnimationFrame`, `window`, or
   DOM. The engine gets `public double hostW, hostH, hostDpr` (CSS pixels and device pixel
   ratio, set by the Unity host before `resize()`), `public Surface canvas` passed to the
   constructor, and `public void frame(double nowMs)` doing exactly what one iteration of the
   rAF loop in `start()` did (`start()` itself does the setup part and records `last`).
   `Work.Pump()` is called by the host, not the engine. `vibrate` → `Native.vibrate`.
4. Already ported modules and their C# names: `Sky` (Sky.cs: `paintSky`, `skyState`,
   `starField` → `StarField`, `drawMoon`, `rgb`, `SkyState` with nested `SkyMoon`, `SkyLight`,
   `SkyCloud`), `Clouds` (`buildCloud`, `retintCloud`, `applyCloudPixels`, `tintCloud`,
   `CloudShape`, `CloudLight`; note the engine must set `CloudShape.key` itself as the TS does),
   `Textures` (`graniteTexture`, `groundTexture`, `rng`), `Flora` (`oliveSprite`, `cypressSprite`,
   `grassClump`, `TreeSprite`), `MountainGen.computeMountain` + `MountainPainter` +
   `MountainSpec` (Mountain.cs; `Mountain` is the level data class in Levels.cs), `Zeus`
   (`zeusHand`, `drawKeraunos`, `Pt`) + `ZeusPainter` (`sprite()` returns
   `ZeusPainter.Sprite`), `Figures` (`drawPrometheus(ctx,x,y,s,L,eagle,gaze,look)`,
   `drawMerope(ctx,x,y,s,MeropeOpts)`, `drawTantalus`, `drawEmptyRock`, `drawPathFire`,
   `FigLight`, `ProLook`, `TanLook`, `EagleVisit`, `ProGaze`, `MeropeOpts`, `PRO_CHAINED`,
   `TAN_REACHING`), `Npcs.drawItem`, `Levels` (`getLevel`, `LEDGE_RUN`, `levelForCycle`,
   `slopeAt`, `terrainAt`, `Level`, `Cloud`), `Saga` (+`NPC_AT`, `SagaEvent`, `SagaState`),
   `Quotes` (`QUOTE_COUNT`, `quotesFor`, `Quote`), `I18n` (`EPIGRAPHS`, `LEVEL_NAMES`),
   `Save.RunSave`, `Native`, and `Sisifos.Audio.GameAudio`. **Open the C# files to check exact
   signatures** before calling them; when a TS call passed an object literal, look at how the
   C# port represents it.
5. **Compiling your part alone**: the other parts are being written at the same time. Create
   `/tmp/claude-0/eng-<part>/` with your own csproj (all of `Assets/Scripts/Paint/**`, `Game/**`
   except the other engine parts, `Audio/**`) plus a scratch `Stubs.cs` there declaring
   `partial class SisyphusEngine` members from the other ranges that you use (signatures
   derived from the TS by the rules above). Your file must compile with 0 errors against
   those stubs. Never put stubs in the repo. List in your report every member of the other
   parts you stubbed (name + signature), so the integration can check them.
6. Port **exactly** — every formula, constant, branch and comment-worthy subtlety. No
   simplification, no dropped effects. Keep allocation low in per-frame code where it is easy
   (reuse lists), but correctness first.
