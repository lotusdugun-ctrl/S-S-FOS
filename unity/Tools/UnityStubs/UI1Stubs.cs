// Compile stubs for the UI1 port (SisyphusGame/GameExtras on UI Toolkit). Signatures copied from
// UnityCsReference branch 6000.0 (Modules/UIElements, TextCoreTextEngine, Runtime/Export).
// Bodies are dummies. The UI2 agent adds its own members in UI2Stubs.cs (partial types; grep both
// files before adding a member).
#pragma warning disable 0067, 0649, 0169, 0108, 0114
using System;
using System.Collections.Generic;
using UnityEngine.TextCore.Text;

namespace UnityEngine
{
    public partial struct Color
    {
        public static Color white => new Color(1, 1, 1, 1); public static Color black => new Color(0, 0, 0, 1); public static Color clear => default;
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; this.a = 1; }
        public static Color operator *(Color a, float b) { return a; }
        public static bool operator ==(Color lhs, Color rhs) { return lhs.r == rhs.r && lhs.g == rhs.g && lhs.b == rhs.b && lhs.a == rhs.a; }
        public static bool operator !=(Color lhs, Color rhs) { return !(lhs == rhs); }
        public override bool Equals(object other) { return false; }
        public override int GetHashCode() { return 0; }
        public static Color Lerp(Color a, Color b, float t) { return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t); }
    }
    public partial struct Color32 { public byte r, g, b, a; public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; } }
    public partial struct Vector2
    {
        public static Vector2 zero => default;
        public float magnitude => 0;
        public static Vector2 operator +(Vector2 a, Vector2 b) { return a; }
        public static Vector2 operator -(Vector2 a, Vector2 b) { return a; }
        public static Vector2 operator *(Vector2 a, float d) { return a; }
        public static implicit operator Vector2(Vector3 v) { return default; }
        public static implicit operator Vector3(Vector2 v) { return default; }
    }
    public partial struct Vector3
    {
        public static Vector3 operator -(Vector3 a, Vector3 b) { return a; }
        public static Vector3 operator +(Vector3 a, Vector3 b) { return a; }
    }
    public partial struct Rect
    {
        public Rect(float x, float y, float width, float height) { }
        public float x { get => 0; set { } }
        public float y { get => 0; set { } }
        public float width { get => 0; set { } }
        public float height { get => 0; set { } }
        public Vector2 position { get => default; set { } }
        public Vector2 center { get => default; set { } }
        public Vector2 size { get => default; set { } }
        public bool Contains(Vector2 point) { return false; }
        public bool Contains(Vector3 point) { return false; }
    }
    public static partial class Mathf
    {
        public static float Max(float a, float b) { return a; }
        public static float Min(float a, float b) { return a; }
        public static float Clamp01(float value) { return value; }
        public static float Clamp(float value, float min, float max) { return value; }
        public static int RoundToInt(float f) { return 0; }
        public static float Round(float f) { return f; }
        public static float Abs(float f) { return f; }
        public static float Lerp(float a, float b, float t) { return a; }
        public const float PI = 3.14159274F;
    }
    public partial class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject { return null; }
    }
    public static partial class Time
    {
        public static double realtimeSinceStartupAsDouble => 0;
        public static float realtimeSinceStartup => 0;
        public static float unscaledDeltaTime => 0;
        public static float deltaTime => 0;
        public static int frameCount => 0;
    }
    public static partial class Application
    {
        public static void OpenURL(string url) { }
        public static string temporaryCachePath => null;
        public static string persistentDataPath => null;
        public static SystemLanguage systemLanguage => default;
        public static bool isMobilePlatform => false;
        public static RuntimePlatform platform => default;
        public static bool isFocused => true;
        public static event Action<bool> focusChanged;
        public static void Quit() { }
    }
    public enum RuntimePlatform { OSXEditor = 0, OSXPlayer = 1, WindowsPlayer = 2, WindowsEditor = 7, IPhonePlayer = 8, Android = 11, LinuxPlayer = 13, LinuxEditor = 16 }
    public enum SystemLanguage { Afrikaans = 0, Arabic = 1, Basque = 2, Belarusian = 3, Bulgarian = 4, Catalan = 5, Chinese = 6, Czech = 7, Danish = 8, Dutch = 9, English = 10, Estonian = 11, Faroese = 12, Finnish = 13, French = 14, German = 15, Greek = 16, Hebrew = 17, Icelandic = 19, Indonesian = 20, Italian = 21, Japanese = 22, Korean = 23, Latvian = 24, Lithuanian = 25, Norwegian = 26, Polish = 27, Portuguese = 28, Romanian = 29, Russian = 30, SerboCroatian = 31, Slovak = 32, Slovenian = 33, Spanish = 34, Swedish = 35, Thai = 36, Turkish = 37, Ukrainian = 38, Vietnamese = 39, ChineseSimplified = 40, ChineseTraditional = 41, Hindi = 42, Unknown = 43 }
    public partial class GUIUtility { public static string systemCopyBuffer { get; set; } }
    public static partial class Debug
    {
        public static void Log(object message) { }
        public static void LogWarning(object message) { }
        public static void LogError(object message) { }
    }
    public enum DeviceType { Unknown = 0, Handheld = 1, Console = 2, Desktop = 3 }
    public sealed partial class SystemInfo
    {
        public static DeviceType deviceType => default;
        public static bool supportsVibration => false;
        public static string operatingSystem => null;
    }
    public static partial class Screen
    {
        public static bool autorotateToPortrait { get; set; }
        public static bool autorotateToPortraitUpsideDown { get; set; }
        public static bool autorotateToLandscapeLeft { get; set; }
        public static bool autorotateToLandscapeRight { get; set; }
    }
    public enum TouchScreenKeyboardType { Default = 0, ASCIICapable = 1 }
    public partial class TouchScreenKeyboard
    {
        public static bool hideInput { get; set; }
        public static bool isSupported => false;
    }
    public static partial class Input
    {
        public static bool GetKey(KeyCode key) { return false; }
        public static bool GetKeyUp(KeyCode key) { return false; }
        public static bool touchSupported => false;
        public static bool mousePresent => false;
    }
    public sealed partial class Texture2D : Texture
    {
        public Texture2D(int width, int height) { }
        public Texture2D(int width, int height, TextureFormat textureFormat, bool mipChain) { }
        public void ReadPixels(Rect source, int destX, int destY, bool recalculateMipMaps) { }
        public void ReadPixels(Rect source, int destX, int destY) { }
        public byte[] GetRawTextureData() { return null; }
        public void Apply() { }
    }
    public static partial class ImageConversion
    {
        public static byte[] EncodeToPNG(this Texture2D tex) { return null; }
        public static byte[] EncodeArrayToPNG(byte[] array, Experimental.Rendering.GraphicsFormat format, uint width, uint height, uint rowBytes = 0) { return null; }
    }
    public partial class RenderTexture : Texture
    {
        public static RenderTexture active { get; set; }
    }
    public static partial class Resources
    {
        public static Object[] FindObjectsOfTypeAll(Type type) { return null; }
    }
    public sealed partial class Font : Object
    {
        public Font() { }
        public static string[] GetPathsToOSFonts() { return null; }
        public static string[] GetOSInstalledFontNames() { return null; }
    }
    public partial class Object
    {
        public static T FindObjectOfType<T>() where T : Object { return null; }
    }
    public sealed partial class GameObject : Object
    {
        public T GetComponent<T>() { return default; }
    }
    public partial class Component : Object
    {
        public T GetComponent<T>() { return default; }
    }
    public partial class Transform : Component { }
    public sealed partial class AudioClip : Object
    {
        public AudioDataLoadState loadState => default;
        public bool LoadAudioData() { return true; }
        public float length => 0;
    }
    public enum AudioDataLoadState { Unloaded = 0, Loading = 1, Loaded = 2, Failed = 3 }
    public sealed partial class AudioSource : Behaviour
    {
        public AudioClip clip { get; set; }
        public bool loop { get; set; }
        public bool playOnAwake { get; set; }
        public float volume { get; set; }
        public float spatialBlend { get; set; }
        public bool isPlaying => false;
        public bool ignoreListenerPause { get; set; }
        public int priority { get; set; }
        public void Play() { }
        public void Pause() { }
        public void UnPause() { }
        public void Stop() { }
    }
    public sealed partial class AudioListener : Behaviour { }
    public sealed partial class AudioSettings
    {
        public static int outputSampleRate => 0;
        public static void GetDSPBufferSize(out int bufferLength, out int numBuffers) { bufferLength = 0; numBuffers = 0; }
        public static event Action<bool> OnAudioConfigurationChanged;
    }
    public enum TextAnchor { UpperLeft = 0, UpperCenter = 1, UpperRight = 2, MiddleLeft = 3, MiddleCenter = 4, MiddleRight = 5, LowerLeft = 6, LowerCenter = 7, LowerRight = 8 }
    public enum TextGeneratorType { Standard = 0, Advanced = 1 }
    public enum ScaleMode { StretchToFill = 0, ScaleAndCrop = 1, ScaleToFit = 2 }
}

namespace UnityEngine.Experimental.Rendering
{
    public enum GraphicsFormat { R8G8B8A8_UNorm = 4, R8G8B8A8_SRGB = 8 }
}

namespace UnityEngine.TextCore.Text
{
    public enum AtlasPopulationMode { Static = 0, Dynamic = 1, DynamicOS = 2 }
    public partial class TextAsset : ScriptableObject { }
    public partial class FontAsset : TextAsset
    {
        public static FontAsset CreateFontAsset(Font font) { return null; }
        public static FontAsset CreateFontAsset(string familyName, string styleName, int pointSize = 90) { return null; }
        public static FontAsset CreateFontAsset(string fontFilePath, int faceIndex, int samplingPointSize, int atlasPadding, UnityEngine.TextCore.LowLevel.GlyphRenderMode renderMode, int atlasWidth, int atlasHeight) { return null; }
        public static FontAsset CreateFontAsset(Font font, int samplingPointSize, int atlasPadding, UnityEngine.TextCore.LowLevel.GlyphRenderMode renderMode, int atlasWidth, int atlasHeight, AtlasPopulationMode atlasPopulationMode = AtlasPopulationMode.Dynamic, bool enableMultiAtlasSupport = true) { return null; }
        public List<FontAsset> fallbackFontAssetTable { get; set; }
        public bool isMultiAtlasTexturesEnabled { get; set; }
        public AtlasPopulationMode atlasPopulationMode { get; set; }
        public Font sourceFontFile => null;
        public bool TryAddCharacters(string characters, bool includeFontFeatures = false) { return true; }
    }
    public partial class TextSettings : ScriptableObject
    {
        public List<FontAsset> fallbackFontAssets { get; set; }
        public FontAsset defaultFontAsset { get; set; }
        public bool matchMaterialPreset { get; set; }
    }
}
namespace UnityEngine.TextCore.LowLevel
{
    [System.Flags] public enum FontFeatureLookupFlags { None = 0x0, IgnoreLigatures = 0x004, IgnoreSpacingAdjustments = 0x100 }
    public struct GlyphPairAdjustmentRecord { public FontFeatureLookupFlags featureLookupFlags { get; set; } }
    public enum GlyphRenderMode { SMOOTH_HINTED = 4121, SMOOTH = 4117, RASTER_HINTED = 4105, RASTER = 4101, SDF = 4134, SDF8 = 8230, SDF16 = 16422, SDF32 = 32806, SDFAA_HINTED = 4169, SDFAA = 4165 }
}

namespace UnityEngine.UIElements
{
    public partial class PanelTextSettings : TextSettings { }
    public partial class StyleSheet : ScriptableObject { }
    public partial class ThemeStyleSheet : StyleSheet { }
    public enum PanelScaleMode { ConstantPixelSize = 0, ConstantPhysicalSize = 1, ScaleWithScreenSize = 2 }
    public enum PanelScreenMatchMode { MatchWidthOrHeight = 0, Shrink = 1, Expand = 2 }
    public partial class PanelSettings : ScriptableObject
    {
        public ThemeStyleSheet themeStyleSheet { get; set; }
        public RenderTexture targetTexture { get; set; }
        public PanelScaleMode scaleMode { get; set; }
        public float scale { get; set; }
        public float referenceDpi { get; set; }
        public float fallbackDpi { get; set; }
        public PanelScreenMatchMode screenMatchMode { get; set; }
        public float match { get; set; }
        public float sortingOrder { get; set; }
        public int targetDisplay { get; set; }
        public bool clearColor { get; set; }
        public Color colorClearValue { get; set; }
        public bool clearDepthStencil { get; set; }
        public PanelTextSettings textSettings;
    }
    public sealed partial class UIDocument : MonoBehaviour
    {
        public PanelSettings panelSettings { get; set; }
        public VisualElement rootVisualElement => null;
        public float sortingOrder { get; set; }
    }

    // ---- style values
    public enum StyleKeyword { Undefined = 0, Null = 1, Auto = 2, None = 3, Initial = 4 }
    public enum LengthUnit { Pixel = 0, Percent = 1 }
    public partial struct Length
    {
        public Length(float value) { }
        public Length(float value, LengthUnit unit) { }
        public static Length Percent(float value) { return default; }
        public static Length Auto() { return default; }
        public static Length None() { return default; }
        public float value { get => 0; set { } }
        public LengthUnit unit { get => default; set { } }
        public static implicit operator Length(float value) { return default; }
    }
    public partial struct StyleLength
    {
        public StyleLength(float v) { }
        public StyleLength(Length v) { }
        public StyleLength(StyleKeyword keyword) { }
        public Length value { get => default; set { } }
        public StyleKeyword keyword { get => default; set { } }
        public static implicit operator StyleLength(StyleKeyword keyword) { return default; }
        public static implicit operator StyleLength(float v) { return default; }
        public static implicit operator StyleLength(Length v) { return default; }
    }
    public partial struct StyleFloat
    {
        public StyleFloat(float v) { }
        public StyleFloat(StyleKeyword keyword) { }
        public float value { get => 0; set { } }
        public static implicit operator StyleFloat(StyleKeyword keyword) { return default; }
        public static implicit operator StyleFloat(float v) { return default; }
    }
    public partial struct StyleInt
    {
        public static implicit operator StyleInt(StyleKeyword keyword) { return default; }
        public static implicit operator StyleInt(int v) { return default; }
    }
    public partial struct StyleColor
    {
        public StyleColor(Color v) { }
        public StyleColor(StyleKeyword keyword) { }
        public Color value { get => default; set { } }
        public static implicit operator StyleColor(StyleKeyword keyword) { return default; }
        public static implicit operator StyleColor(Color v) { return default; }
    }
    public partial struct StyleEnum<T> where T : struct, IConvertible
    {
        public StyleEnum(T v) { }
        public StyleEnum(StyleKeyword keyword) { }
        public T value { get => default; set { } }
        public static implicit operator StyleEnum<T>(StyleKeyword keyword) { return default; }
        public static implicit operator StyleEnum<T>(T v) { return default; }
    }
    public partial struct FontDefinition
    {
        public static FontDefinition FromFont(Font f) { return default; }
        public static FontDefinition FromSDFFont(FontAsset f) { return default; }
        public FontAsset fontAsset { get => null; set { } }
        public Font font { get => null; set { } }
    }
    public partial struct StyleFontDefinition
    {
        public StyleFontDefinition(FontDefinition f) { }
        public StyleFontDefinition(StyleKeyword keyword) { }
        public static implicit operator StyleFontDefinition(StyleKeyword keyword) { return default; }
        public static implicit operator StyleFontDefinition(FontDefinition f) { return default; }
    }
    public partial struct StyleFont
    {
        public static implicit operator StyleFont(StyleKeyword keyword) { return default; }
        public static implicit operator StyleFont(Font v) { return default; }
    }
    public partial struct Background
    {
        public static Background FromTexture2D(Texture2D t) { return default; }
        public static Background FromRenderTexture(RenderTexture rt) { return default; }
        public Texture texture { get => null; set { } }
        public RenderTexture renderTexture { get => null; set { } }
    }
    public partial struct StyleBackground
    {
        public StyleBackground(Background v) { }
        public StyleBackground(StyleKeyword keyword) { }
        public static implicit operator StyleBackground(StyleKeyword keyword) { return default; }
        public static implicit operator StyleBackground(Background v) { return default; }
        public static implicit operator StyleBackground(Texture2D v) { return default; }
    }
    public partial struct Translate
    {
        public Translate(Length x, Length y, float z) { }
        public Translate(Length x, Length y) { }
        public static Translate None() { return default; }
    }
    public partial struct StyleTranslate
    {
        public StyleTranslate(Translate v) { }
        public static implicit operator StyleTranslate(StyleKeyword keyword) { return default; }
        public static implicit operator StyleTranslate(Translate v) { return default; }
    }
    public partial struct Scale
    {
        public Scale(Vector2 scale) { }
        public Scale(Vector3 scale) { }
        public static Scale None() { return default; }
    }
    public partial struct StyleScale
    {
        public StyleScale(Scale v) { }
        public static implicit operator StyleScale(StyleKeyword keyword) { return default; }
        public static implicit operator StyleScale(Scale v) { return default; }
    }
    public enum AngleUnit { Degree = 0, Gradian = 1, Radian = 2, Turn = 3 }
    public partial struct Angle
    {
        public Angle(float value) { }
        public Angle(float value, AngleUnit unit) { }
        public static Angle Degrees(float value) { return default; }
        public static implicit operator Angle(float value) { return default; }
    }
    public partial struct Rotate
    {
        public Rotate(Angle angle) { }
        public static Rotate None() { return default; }
    }
    public partial struct StyleRotate
    {
        public StyleRotate(Rotate v) { }
        public static implicit operator StyleRotate(StyleKeyword keyword) { return default; }
        public static implicit operator StyleRotate(Rotate v) { return default; }
    }
    public partial struct TransformOrigin
    {
        public TransformOrigin(Length x, Length y, float z) { }
        public TransformOrigin(Length x, Length y) { }
        public static TransformOrigin Initial() { return default; }
    }
    public partial struct StyleTransformOrigin
    {
        public StyleTransformOrigin(TransformOrigin v) { }
        public static implicit operator StyleTransformOrigin(StyleKeyword keyword) { return default; }
        public static implicit operator StyleTransformOrigin(TransformOrigin v) { return default; }
    }
    public partial struct TextShadow
    {
        public Vector2 offset;
        public float blurRadius;
        public Color color;
    }
    public partial struct StyleTextShadow
    {
        public StyleTextShadow(TextShadow v) { }
        public static implicit operator StyleTextShadow(StyleKeyword keyword) { return default; }
        public static implicit operator StyleTextShadow(TextShadow v) { return default; }
    }
    public enum Position { Relative = 0, Absolute = 1 }
    public enum FlexDirection { Column = 0, ColumnReverse = 1, Row = 2, RowReverse = 3 }
    public enum Wrap { NoWrap = 0, Wrap = 1, WrapReverse = 2 }
    public enum Align { Auto = 0, FlexStart = 1, Center = 2, FlexEnd = 3, Stretch = 4 }
    public enum Justify { FlexStart = 0, Center = 1, FlexEnd = 2, SpaceBetween = 3, SpaceAround = 4, SpaceEvenly = 5 }
    public enum DisplayStyle { Flex = 0, None = 1 }
    public enum Visibility { Visible = 0, Hidden = 1 }
    public enum Overflow { Visible = 0, Hidden = 1 }
    public enum WhiteSpace { Normal = 0, NoWrap = 1, Pre = 2, PreWrap = 3 }
    public enum TextOverflow { Clip = 0, Ellipsis = 1 }
    public enum OverflowClipBox { PaddingBox = 0, ContentBox = 1 }

    public partial interface IStyle
    {
        StyleEnum<Align> alignContent { get; set; }
        StyleEnum<Align> alignItems { get; set; }
        StyleEnum<Align> alignSelf { get; set; }
        StyleColor backgroundColor { get; set; }
        StyleBackground backgroundImage { get; set; }
        StyleColor borderBottomColor { get; set; }
        StyleLength borderBottomLeftRadius { get; set; }
        StyleLength borderBottomRightRadius { get; set; }
        StyleFloat borderBottomWidth { get; set; }
        StyleColor borderLeftColor { get; set; }
        StyleFloat borderLeftWidth { get; set; }
        StyleColor borderRightColor { get; set; }
        StyleFloat borderRightWidth { get; set; }
        StyleColor borderTopColor { get; set; }
        StyleLength borderTopLeftRadius { get; set; }
        StyleLength borderTopRightRadius { get; set; }
        StyleFloat borderTopWidth { get; set; }
        StyleLength bottom { get; set; }
        StyleColor color { get; set; }
        StyleEnum<DisplayStyle> display { get; set; }
        StyleLength flexBasis { get; set; }
        StyleEnum<FlexDirection> flexDirection { get; set; }
        StyleFloat flexGrow { get; set; }
        StyleFloat flexShrink { get; set; }
        StyleEnum<Wrap> flexWrap { get; set; }
        StyleLength fontSize { get; set; }
        StyleLength height { get; set; }
        StyleEnum<Justify> justifyContent { get; set; }
        StyleLength left { get; set; }
        StyleLength letterSpacing { get; set; }
        StyleLength marginBottom { get; set; }
        StyleLength marginLeft { get; set; }
        StyleLength marginRight { get; set; }
        StyleLength marginTop { get; set; }
        StyleLength maxHeight { get; set; }
        StyleLength maxWidth { get; set; }
        StyleLength minHeight { get; set; }
        StyleLength minWidth { get; set; }
        StyleFloat opacity { get; set; }
        StyleEnum<Overflow> overflow { get; set; }
        StyleLength paddingBottom { get; set; }
        StyleLength paddingLeft { get; set; }
        StyleLength paddingRight { get; set; }
        StyleLength paddingTop { get; set; }
        StyleEnum<Position> position { get; set; }
        StyleLength right { get; set; }
        StyleRotate rotate { get; set; }
        StyleScale scale { get; set; }
        StyleEnum<TextOverflow> textOverflow { get; set; }
        StyleTextShadow textShadow { get; set; }
        StyleLength top { get; set; }
        StyleTransformOrigin transformOrigin { get; set; }
        StyleTranslate translate { get; set; }
        StyleColor unityBackgroundImageTintColor { get; set; }
        StyleFont unityFont { get; set; }
        StyleFontDefinition unityFontDefinition { get; set; }
        StyleEnum<FontStyle> unityFontStyleAndWeight { get; set; }
        StyleEnum<OverflowClipBox> unityOverflowClipBox { get; set; }
        StyleLength unityParagraphSpacing { get; set; }
        StyleEnum<TextAnchor> unityTextAlign { get; set; }
        StyleEnum<TextGeneratorType> unityTextGenerator { get; set; }
        StyleColor unityTextOutlineColor { get; set; }
        StyleFloat unityTextOutlineWidth { get; set; }
        StyleEnum<Visibility> visibility { get; set; }
        StyleEnum<WhiteSpace> whiteSpace { get; set; }
        StyleLength width { get; set; }
        StyleLength wordSpacing { get; set; }
    }
    public partial interface IResolvedStyle
    {
        float width { get; }
        float height { get; }
        float fontSize { get; }
        float opacity { get; }
        Color color { get; }
        float letterSpacing { get; }
        DisplayStyle display { get; }
    }

    // ---- events
    public enum TrickleDown { NoTrickleDown = 0, TrickleDown = 1 }
    public delegate void EventCallback<in TEventType>(TEventType evt);
    public partial interface IEventHandler { }
    public abstract partial class EventBase : IDisposable
    {
        public IEventHandler target { get; set; }
        public IEventHandler currentTarget => null;
        public bool isPropagationStopped => false;
        public void StopPropagation() { }
        public void StopImmediatePropagation() { }
        public void PreventDefault() { }
        public void Dispose() { }
    }
    public abstract partial class EventBase<T> : EventBase where T : EventBase<T>, new() { }
    public partial interface IPointerEvent { }
    public abstract partial class PointerEventBase<T> : EventBase<T>, IPointerEvent where T : PointerEventBase<T>, new()
    {
        public int pointerId { get; protected set; }
        public string pointerType { get; protected set; }
        public bool isPrimary { get; protected set; }
        public int button { get; protected set; }
        public Vector3 position { get; protected set; }
        public Vector3 localPosition { get; protected set; }
        public Vector3 deltaPosition { get; protected set; }
    }
    public sealed partial class PointerDownEvent : PointerEventBase<PointerDownEvent> { }
    public sealed partial class PointerMoveEvent : PointerEventBase<PointerMoveEvent> { }
    public sealed partial class PointerUpEvent : PointerEventBase<PointerUpEvent> { }
    public sealed partial class PointerCancelEvent : PointerEventBase<PointerCancelEvent> { }
    public sealed partial class PointerEnterEvent : PointerEventBase<PointerEnterEvent> { }
    public sealed partial class PointerLeaveEvent : PointerEventBase<PointerLeaveEvent> { }
    public sealed partial class ClickEvent : PointerEventBase<ClickEvent> { }
    public partial class PointerCaptureOutEvent : EventBase<PointerCaptureOutEvent> { }
    public abstract partial class KeyboardEventBase<T> : EventBase<T> where T : KeyboardEventBase<T>, new()
    {
        public char character { get; protected set; }
        public KeyCode keyCode { get; protected set; }
        public EventModifiers modifiers { get; protected set; }
    }

    public partial class KeyDownEvent : KeyboardEventBase<KeyDownEvent> { }
    public partial class KeyUpEvent : KeyboardEventBase<KeyUpEvent> { }
    public partial class GeometryChangedEvent : EventBase<GeometryChangedEvent>
    {
        public Rect oldRect { get; private set; }
        public Rect newRect { get; private set; }
    }
    public partial class AttachToPanelEvent : EventBase<AttachToPanelEvent> { }
    public partial class DetachFromPanelEvent : EventBase<DetachFromPanelEvent> { }
    public partial class FocusInEvent : EventBase<FocusInEvent> { }
    public partial class FocusOutEvent : EventBase<FocusOutEvent> { }
    public partial class ChangeEvent<T> : EventBase<ChangeEvent<T>>
    {
        public T previousValue { get; protected set; }
        public T newValue { get; protected set; }
    }
    public partial class WheelEvent : EventBase<WheelEvent> { public Vector3 delta { get; private set; } }

    public abstract partial class CallbackEventHandler : IEventHandler
    {
        public void RegisterCallback<TEventType>(EventCallback<TEventType> callback, TrickleDown useTrickleDown = TrickleDown.NoTrickleDown) where TEventType : EventBase<TEventType>, new() { }
        public void UnregisterCallback<TEventType>(EventCallback<TEventType> callback, TrickleDown useTrickleDown = TrickleDown.NoTrickleDown) where TEventType : EventBase<TEventType>, new() { }
    }
    public abstract partial class Focusable : CallbackEventHandler
    {
        public virtual bool focusable { get; set; }
        public int tabIndex { get; set; }
        public virtual void Focus() { }
        public void Blur() { }
    }
    public static partial class PointerCaptureHelper
    {
        public static bool HasPointerCapture(this IEventHandler handler, int pointerId) { return false; }
        public static void CapturePointer(this IEventHandler handler, int pointerId) { }
        public static void ReleasePointer(this IEventHandler handler, int pointerId) { }
    }

    // ---- scheduler
    public partial struct TimerState { public long start; public long now; public long deltaTime => 0; }
    public partial interface IVisualElementScheduledItem
    {
        VisualElement element { get; }
        bool isActive { get; }
        void Resume();
        void Pause();
        void ExecuteLater(long delayMs);
        IVisualElementScheduledItem StartingIn(long delayMs);
        IVisualElementScheduledItem Every(long intervalMs);
        IVisualElementScheduledItem Until(Func<bool> stopCondition);
        IVisualElementScheduledItem ForDuration(long durationMs);
    }
    public partial interface IVisualElementScheduler
    {
        IVisualElementScheduledItem Execute(Action<TimerState> timerUpdateEvent);
        IVisualElementScheduledItem Execute(Action timerUpdateEvent);
    }
    public partial interface IPanel { VisualElement visualTree { get; } }

    public enum PickingMode { Position = 0, Ignore = 1 }
    public enum LanguageDirection { Inherit = 0, LTR = 1, RTL = 2 }
    public partial class VisualElement : Focusable
    {
        public VisualElement() { }
        public IStyle style => NullProxy.Style;
        public IResolvedStyle resolvedStyle => null;
        public Rect layout => default;
        public Rect worldBound => default;
        public Rect contentRect => default;
        public Rect localBound => default;
        public string name { get; set; }
        public PickingMode pickingMode { get; set; }
        public bool visible { get; set; }
        public bool enabledSelf => true;
        public bool enabledInHierarchy => true;
        public void SetEnabled(bool value) { }
        public object userData { get; set; }
        public LanguageDirection languageDirection { get; set; }
        public IVisualElementScheduler schedule => null;
        public IPanel panel => null;
        public VisualElement parent => null;
        public virtual VisualElement contentContainer => null;
        public int childCount => 0;
        public VisualElement this[int key] => null;
        public IEnumerable<VisualElement> Children() { return null; }
        public void Add(VisualElement child) { }
        public void Insert(int index, VisualElement element) { }
        public void Remove(VisualElement element) { }
        public void RemoveAt(int index) { }
        public void Clear() { }
        public void RemoveFromHierarchy() { }
        public void BringToFront() { }
        public void SendToBack() { }
        public int IndexOf(VisualElement element) { return 0; }
        public bool Contains(VisualElement child) { return false; }
        public void AddToClassList(string className) { }
        public void RemoveFromClassList(string className) { }
        public bool ClassListContains(string cls) { return false; }
        public void MarkDirtyRepaint() { }
        public Action<MeshGenerationContext> generateVisualContent { get; set; }
        public bool usageHints2 { get; set; }
        public UsageHints usageHints { get; set; }
        public Hierarchy hierarchy => default;
        public partial struct Hierarchy
        {
            public void Add(VisualElement child) { }
            public void Insert(int index, VisualElement child) { }
            public int childCount => 0;
        }
    }
    [Flags] public enum UsageHints { None = 0, DynamicTransform = 1, GroupTransform = 2, MaskContainer = 4, DynamicColor = 8 }
    public partial class MeshGenerationContext { public Painter2D painter2D => null; public VisualElement visualElement => null; }
    public partial class Painter2D { }
    public static partial class VisualElementExtensions
    {
        public static Vector2 WorldToLocal(this VisualElement ele, Vector2 p) { return p; }
        public static Vector2 LocalToWorld(this VisualElement ele, Vector2 p) { return p; }
        public static Rect WorldToLocal(this VisualElement ele, Rect r) { return r; }
        public static Vector2 ChangeCoordinatesTo(this VisualElement src, VisualElement dest, Vector2 point) { return point; }
        public static void StretchToParentSize(this VisualElement elem) { }
    }
    public static partial class UQueryExtensions
    {
        public static VisualElement Q(this VisualElement e, string name = null, string className = null) { return null; }
        public static T Q<T>(this VisualElement e, string name = null, string className = null) where T : VisualElement { return null; }
    }

    // ---- controls
    public partial class TextElement : VisualElement
    {
        public virtual string text { get; set; }
        public bool enableRichText { get; set; }
        public bool parseEscapeSequences { get; set; }
        public bool displayTooltipWhenElided { get; set; }
        public Vector2 MeasureTextSize(string textToMeasure, float width, MeasureMode widthMode, float height, MeasureMode heightMode) { return default; }
        public ITextSelection selection => null;
    }
    public partial interface ITextSelection { bool isSelectable { get; set; } Color cursorColor { get; set; } Color selectionColor { get; set; } }
    public enum MeasureMode { Undefined = 0, Exactly = 1, AtMost = 2 }
    public partial class Label : TextElement
    {
        public Label() { }
        public Label(string text) { }
    }
    public partial class Clickable { public event Action clicked; }
    public partial class Button : TextElement
    {
        public Button() { }
        public Button(Action clickEvent) { }
        public event Action clicked;
        public Clickable clickable { get; set; }
    }
    public partial class Image : VisualElement
    {
        public Texture image { get; set; }
        public ScaleMode scaleMode { get; set; }
        public Color tintColor { get; set; }
    }
    public enum ScrollViewMode { Vertical = 0, Horizontal = 1, VerticalAndHorizontal = 2 }
    public enum ScrollerVisibility { Auto = 0, AlwaysVisible = 1, Hidden = 2 }
    public partial class Scroller : VisualElement { public float value { get; set; } public float highValue { get; set; } }
    public partial class ScrollView : VisualElement
    {
        public enum TouchScrollBehavior { Unrestricted = 0, Elastic = 1, Clamped = 2 }
        public enum NestedInteractionKind { Default = 0, StopScrolling = 1, ForwardScrolling = 2 }
        public ScrollView() { }
        public ScrollView(ScrollViewMode scrollViewMode) { }
        public ScrollViewMode mode { get; set; }
        public Vector2 scrollOffset { get; set; }
        public ScrollerVisibility horizontalScrollerVisibility { get; set; }
        public ScrollerVisibility verticalScrollerVisibility { get; set; }
        public TouchScrollBehavior touchScrollBehavior { get; set; }
        public NestedInteractionKind nestedInteractionKind { get; set; }
        public float elasticity { get; set; }
        public float scrollDecelerationRate { get; set; }
        public float mouseWheelScrollSize { get; set; }
        public Scroller verticalScroller => null;
        public VisualElement contentViewport => null;
        public override VisualElement contentContainer => null;
    }
    public partial interface INotifyValueChanged<T>
    {
        T value { get; set; }
        void SetValueWithoutNotify(T newValue);
    }
    public static partial class INotifyValueChangedExtensions
    {
        public static bool RegisterValueChangedCallback<T>(this INotifyValueChanged<T> control, EventCallback<ChangeEvent<T>> callback) { return true; }
    }
    public partial interface ITextEdition
    {
        int maxLength { get; set; }
        string placeholder { get; set; }
        bool hidePlaceholderOnFocus { get; set; }
        bool isDelayed { get; set; }
        bool hideMobileInput { get; set; }
        TouchScreenKeyboardType keyboardType { get; set; }
    }
    public abstract partial class BaseField<TValueType> : VisualElement, INotifyValueChanged<TValueType>
    {
        public virtual TValueType value { get; set; }
        public virtual void SetValueWithoutNotify(TValueType newValue) { }
        public string label { get; set; }
        public Label labelElement => null;
    }
    public abstract partial class TextInputBaseField<TValueType> : BaseField<TValueType>
    {
        public int maxLength { get; set; }
        public bool isDelayed { get; set; }
        public bool isReadOnly { get; set; }
        public bool hideMobileInput { get; set; }
        public TouchScreenKeyboardType keyboardType { get; set; }
        public string text => null;
        public ITextEdition textEdition => null;
        public bool selectAllOnFocus { get; set; }
        public bool selectAllOnMouseUp { get; set; }
        public ITextSelection textSelection => null;
    }
    public partial class TextField : TextInputBaseField<string>
    {
        public TextField() { }
        public TextField(int maxLength, bool multiline, bool isPasswordField, char maskChar) { }
        public bool multiline { get; set; }
        public override string value { get; set; }
        public static readonly string inputUssClassName = "unity-text-field__input";
    }
}

namespace UnityEngine
{
    public sealed partial class GameObject : Object { public void SetActive(bool value) { } }
    public partial class Object
    {
        public static T Instantiate<T>(T original) where T : Object { return original; }
    }
    public sealed partial class Shader : Object { public static Shader Find(string name) { return null; } }
    public partial class RenderTexture : Texture
    {
        public RenderTexture(int width, int height, int depth, RenderTextureFormat format) { }
    }
    public static partial class Graphics
    {
        public static void Blit(Texture source, RenderTexture dest) { }
        public static void Blit(Texture source, RenderTexture dest, Material mat) { }
    }
}
namespace UnityEngine
{
    public partial class RenderTexture : Texture
    {
        public static RenderTexture GetTemporary(int width, int height, int depthBuffer, RenderTextureFormat format, RenderTextureReadWrite readWrite) { return null; }
        public static void ReleaseTemporary(RenderTexture temp) { }
    }
}
namespace UnityEngine.UIElements
{
    public partial struct StyleEnum<T> where T : struct, IConvertible
    {
        public static bool operator ==(StyleEnum<T> lhs, StyleEnum<T> rhs) { return true; }
        public static bool operator !=(StyleEnum<T> lhs, StyleEnum<T> rhs) { return false; }
        public override bool Equals(object obj) { return false; }
        public override int GetHashCode() { return 0; }
    }
}
namespace UnityEngine
{
    public partial struct Rect
    {
        public static bool operator ==(Rect lhs, Rect rhs) { return true; }
        public static bool operator !=(Rect lhs, Rect rhs) { return false; }
        public override bool Equals(object other) { return false; }
        public override int GetHashCode() { return 0; }
    }
    public sealed partial class Handheld { public static void Vibrate() { } }
}
namespace UnityEngine
{
    public partial class AndroidJavaObject
    {
        public void CallStatic(string methodName, params object[] args) { }
    }
}
namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Class)] public partial class InitializeOnLoadAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed partial class MenuItem : Attribute { public MenuItem(string itemName) { } }
    public sealed partial class EditorApplication
    {
        public delegate void CallbackFunction();
        public static CallbackFunction delayCall;
    }
    public partial class AssetDatabase
    {
        public static T LoadAssetAtPath<T>(string assetPath) where T : UnityEngine.Object { return null; }
        public static void CreateAsset(UnityEngine.Object asset, string path) { }
        public static void SaveAssets() { }
        public static T GetBuiltinExtraResource<T>(string path) where T : UnityEngine.Object { return null; }
    }
    public partial class EditorUtility { public static void SetDirty(UnityEngine.Object target) { } }
}

namespace UnityEngine.UIElements
{
    // stub-only: a do-nothing IStyle so code that sets styles can run in the headless harness
    public class NullProxy : System.Reflection.DispatchProxy
    {
        static IStyle s;
        public static IStyle Style { get { return s ?? (s = System.Reflection.DispatchProxy.Create<IStyle, NullProxy>()); } }
        protected override object Invoke(System.Reflection.MethodInfo m, object[] a)
        {
            return m.ReturnType.IsValueType && m.ReturnType != typeof(void) ? Activator.CreateInstance(m.ReturnType) : null;
        }
    }
}
