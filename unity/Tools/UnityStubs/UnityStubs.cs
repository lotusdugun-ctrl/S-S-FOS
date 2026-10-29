// Compile stubs: only the Unity APIs Sisifos.App uses, signatures copied from UnityCsReference
// (master, Oct 2026). Bodies are dummies. Not shipped.
#pragma warning disable 0067, 0649, 0169
using System;
using System.Collections.Generic;
using UnityEngine.Rendering;

namespace UnityEngine
{
    [Flags] public enum HideFlags { None = 0, HideInHierarchy = 1, HideInInspector = 2, DontSaveInEditor = 4, NotEditable = 8, DontSaveInBuild = 16, DontUnloadUnusedAsset = 32, DontSave = 52, HideAndDontSave = 61 }
    public partial class Object
    {
        public string name { get; set; }
        public HideFlags hideFlags { get; set; }
        public static void Destroy(Object obj) { }
        public static void DontDestroyOnLoad(Object target) { }
        public static implicit operator bool(Object exists) { return true; }
    }
    public partial class Component : Object { public Transform transform => null; public GameObject gameObject => null; }
    public partial class Behaviour : Component { public bool enabled { get; set; } }
    public partial class MonoBehaviour : Behaviour { }
    public partial class Transform : Component { public void SetParent(Transform parent, bool worldPositionStays) { } }
    public sealed partial class GameObject : Object
    {
        public GameObject(string name) { }
        public T AddComponent<T>() where T : Component { return null; }
        public Transform transform => null;
    }
    public partial struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; } }
    public partial struct Vector3 { public float x, y, z; public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; } public static Vector3 zero => default; }
    public partial struct Vector4 { public float x, y, z, w; public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; } }
    public partial struct Color { public float r, g, b, a; public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; } }
    public partial struct Rect { public float xMin => 0; public float xMax => 0; public float yMin => 0; public float yMax => 0; }
    public partial struct Bounds { public Bounds(Vector3 center, Vector3 size) { } }
    public partial struct Matrix4x4
    {
        public static Matrix4x4 identity => default;
        public static Matrix4x4 Ortho(float left, float right, float bottom, float top, float zNear, float zFar) { return default; }
    }
    public enum FilterMode { Point = 0, Bilinear = 1, Trilinear = 2 }
    public enum TextureWrapMode { Repeat = 0, Clamp = 1, Mirror = 2, MirrorOnce = 3 }
    public enum TextureFormat { Alpha8 = 1, RGBA32 = 4, RGBAHalf = 17 }
    public enum RenderTextureFormat { ARGB32 = 0, Depth = 1, Default = 7 }
    public enum RenderTextureReadWrite { Default = 0, Linear = 1, sRGB = 2 }
    public enum MeshTopology { Triangles = 0, Quads = 2, Lines = 3, LineStrip = 4, Points = 5 }
    public enum CameraClearFlags { Skybox = 1, Color = 2, SolidColor = 2, Depth = 3, Nothing = 4 }
    public enum ScreenOrientation { Portrait = 1, PortraitUpsideDown = 2, LandscapeLeft = 3, LandscapeRight = 4, AutoRotation = 5 }
    public enum KeyCode { None = 0, Backspace = 8, Tab = 9, Return = 13, Escape = 27, Space = 32, A = 97, B = 98, D = 100, S = 115, W = 119, UpArrow = 273, DownArrow = 274, RightArrow = 275, LeftArrow = 276, KeypadEnter = 271 } // UI1 added values
    public enum FontStyle { Normal = 0, Bold = 1, Italic = 2, BoldAndItalic = 3 }
    public enum RuntimeInitializeLoadType { AfterSceneLoad = 0, BeforeSceneLoad = 1 }
    [AttributeUsage(AttributeTargets.Method)] public partial class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType loadType) { } }
    [AttributeUsage(AttributeTargets.Class)] public partial class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int order) { } }
    public static partial class SleepTimeout { public const int NeverSleep = -1; public const int SystemSetting = -2; }

    public partial class Texture : Object
    {
        public virtual int width { get; set; }
        public virtual int height { get; set; }
        public FilterMode filterMode { get; set; }
        public TextureWrapMode wrapMode { get; set; }
    }
    public sealed partial class Texture2D : Texture
    {
        public Texture2D(int width, int height, TextureFormat textureFormat, bool mipChain, bool linear) { }
        public void SetPixel(int x, int y, Color color) { }
        public void Apply(bool updateMipmaps, bool makeNoLongerReadable) { }
        public void LoadRawTextureData(IntPtr data, int size) { }
        public void LoadRawTextureData(byte[] data) { }
        public void SetPixelData<T>(T[] data, int mipLevel, int sourceDataStartIndex = 0) { }
    }
    public partial struct RenderTextureDescriptor
    {
        public RenderTextureDescriptor(int width, int height, RenderTextureFormat colorFormat, int depthBufferBits, int mipCount, RenderTextureReadWrite readWrite) { this = default; }
        public int msaaSamples { get; set; }
        public bool useMipMap { get; set; }
        public bool autoGenerateMips { get; set; }
    }
    public partial class RenderTexture : Texture
    {
        public RenderTexture(RenderTextureDescriptor desc) { }
        public int antiAliasing { get; set; }
        public bool Create() { return true; }
        public void Release() { }
    }
    public sealed partial class Shader : Object { public static int PropertyToID(string name) { return 0; } public bool isSupported => true; }
    public partial class Material : Object
    {
        public void SetVector(int nameID, Vector4 value) { }
        public Material(Shader shader) { }
        public void SetFloat(int nameID, float value) { }
        public Texture mainTexture { get; set; }
    }
    public sealed partial class MaterialPropertyBlock
    {
        public void Clear() { }
        public void SetFloat(int nameID, float value) { }
        public void SetTexture(int nameID, Texture value) { }
    }
    public sealed partial class Mesh : Object
    {
        public Mesh() { }
        public void MarkDynamic() { }
        public IndexFormat indexFormat { get; set; }
        public int subMeshCount { get; set; }
        public Bounds bounds { get; set; }
        public void SetSubMesh(int index, SubMeshDescriptor desc, MeshUpdateFlags flags = MeshUpdateFlags.Default) { }
        public void SetVertexBufferParams(int vertexCount, params VertexAttributeDescriptor[] attributes) { }
        public void SetVertexBufferData<T>(T[] data, int dataStart, int meshBufferStart, int count, int stream = 0, MeshUpdateFlags flags = MeshUpdateFlags.Default) where T : struct { }
        public void SetIndexBufferParams(int indexCount, IndexFormat format) { }
        public void SetIndexBufferData<T>(T[] data, int dataStart, int meshBufferStart, int count, MeshUpdateFlags flags = MeshUpdateFlags.Default) where T : struct { }
        public void SetSubMeshes(SubMeshDescriptor[] desc, int start, int count, MeshUpdateFlags flags = MeshUpdateFlags.Default) { }
    }
    public sealed partial class TextAsset : Object { public string text => null; }
    public static partial class Resources
    {
        public static T Load<T>(string path) where T : Object { return null; }
        public static void UnloadAsset(Object assetToUnload) { }
    }
    public static partial class Graphics { public static void ExecuteCommandBuffer(CommandBuffer buffer) { } }
    public static partial class Debug { public static void LogException(Exception exception) { } }
    public static partial class Application
    {
        public static int targetFrameRate { get; set; }
        public static string streamingAssetsPath => null;
    }
    public partial class PlayerPrefs
    {
        public static void SetString(string key, string value) { }
        public static string GetString(string key) { return null; }
        public static bool HasKey(string key) { return false; }
        public static void DeleteKey(string key) { }
        public static void Save() { }
    }
    public static partial class QualitySettings { public static int vSyncCount { get; set; } }
    public partial struct RefreshRate { public uint numerator; public uint denominator; public double value => 0; }
    public partial struct Resolution { public RefreshRate refreshRateRatio { get; set; } }
    public static partial class Screen
    {
        public static int width => 0;
        public static int height => 0;
        public static float dpi => 0;
        public static Rect safeArea => default;
        public static int sleepTimeout { get; set; }
        public static ScreenOrientation orientation { get; set; }
        public static Resolution currentResolution => default;
    }
    public static partial class Input
    {
        public static bool multiTouchEnabled { get; set; }
        public static bool GetKeyDown(KeyCode key) { return false; }
    }
    public sealed partial class Camera : Behaviour
    {
        public static Camera main => null;
        public CameraClearFlags clearFlags { get; set; }
        public Color backgroundColor { get; set; }
        public int cullingMask { get; set; }
        public bool orthographic { get; set; }
        public bool allowHDR { get; set; }
        public bool allowMSAA { get; set; }
        public bool useOcclusionCulling { get; set; }
        public void AddCommandBuffer(CameraEvent evt, CommandBuffer buffer) { }
        public void RemoveCommandBuffer(CameraEvent evt, CommandBuffer buffer) { }
    }
    public partial struct CharacterInfo
    {
        public int advance => 0;
        public int minX => 0; public int maxX => 0; public int minY => 0; public int maxY => 0;
        public Vector2 uvBottomLeft => default; public Vector2 uvBottomRight => default;
        public Vector2 uvTopRight => default; public Vector2 uvTopLeft => default;
    }
    public sealed partial class Font : Object
    {
        public static event Action<Font> textureRebuilt;
        public Material material { get; set; }
        public int ascent => 0;
        public int lineHeight => 0;
        public int fontSize => 0;
        public static Font CreateDynamicFontFromOSFont(string[] fontnames, int size) { return null; }
        public void RequestCharactersInTexture(string characters, int size, FontStyle style) { }
        public bool GetCharacterInfo(char ch, out CharacterInfo info, int size, FontStyle style) { info = default; return false; }
    }
    public partial class AndroidJavaObject : IDisposable
    {
        public AndroidJavaObject(string className, params object[] args) { }
        public void Dispose() { }
        public void Call(string methodName, params object[] args) { }
        public ReturnType Call<ReturnType>(string methodName, params object[] args) { return default; }
        public FieldType Get<FieldType>(string fieldName) { return default; }
        public FieldType GetStatic<FieldType>(string fieldName) { return default; }
        public ReturnType CallStatic<ReturnType>(string methodName, params object[] args) { return default; }
    }
    public partial class AndroidJavaClass : AndroidJavaObject { public AndroidJavaClass(string className) : base(className) { } }
    public static partial class AndroidJNI { public static int AttachCurrentThread() { return 0; } }
}

namespace UnityEngine.Rendering
{
    public enum IndexFormat { UInt16 = 0, UInt32 = 1 }
    [Flags] public enum MeshUpdateFlags { Default = 0, DontValidateIndices = 1, DontResetBoneBounds = 2, DontNotifyMeshUsers = 4, DontRecalculateBounds = 8 }
    public enum VertexAttribute { Position = 0, Normal, Tangent, Color, TexCoord0, TexCoord1, TexCoord2, TexCoord3, TexCoord4, TexCoord5, TexCoord6, TexCoord7, BlendWeight, BlendIndices }
    public enum VertexAttributeFormat { Float32 = 0, Float16, UNorm8, SNorm8, UNorm16, SNorm16, UInt8, SInt8, UInt16, SInt16, UInt32, SInt32 }
    public partial struct VertexAttributeDescriptor
    {
        public VertexAttributeDescriptor(VertexAttribute attribute = VertexAttribute.Position, VertexAttributeFormat format = VertexAttributeFormat.Float32, int dimension = 3, int stream = 0) { }
    }
    public partial struct SubMeshDescriptor
    {
        public SubMeshDescriptor(int indexStart, int indexCount, MeshTopology topology = MeshTopology.Triangles) { this = default; }
        public int baseVertex { get; set; }
        public int firstVertex { get; set; }
        public int vertexCount { get; set; }
    }
    public enum BlendMode { Zero = 0, One = 1, DstColor = 2, SrcColor = 3, OneMinusDstColor = 4, SrcAlpha = 5, OneMinusSrcColor = 6, DstAlpha = 7, OneMinusDstAlpha = 8, SrcAlphaSaturate = 9, OneMinusSrcAlpha = 10 }
    public enum CompareFunction { Disabled = 0, Never = 1, Less = 2, Equal = 3, LessEqual = 4, Greater = 5, NotEqual = 6, GreaterEqual = 7, Always = 8 }
    public enum StencilOp { Keep = 0, Zero = 1, Replace = 2, IncrementSaturate = 3, DecrementSaturate = 4, Invert = 5, IncrementWrap = 6, DecrementWrap = 7 }
    [Flags] public enum ColorWriteMask { Alpha = 1, Blue = 2, Green = 4, Red = 8, All = 15 }
    [Flags] public enum RTClearFlags { None = 0, Color = 1, Depth = 2, Stencil = 4, All = 7 }
    public enum CameraEvent { AfterEverything = 21 }
    public enum BuiltinRenderTextureType { CameraTarget = 2 }
    public partial struct RenderTargetIdentifier
    {
        public RenderTargetIdentifier(BuiltinRenderTextureType type) { }
        public RenderTargetIdentifier(int nameID) { }
        public RenderTargetIdentifier(Texture tex) { }
        public static implicit operator RenderTargetIdentifier(BuiltinRenderTextureType type) { return default; }
        public static implicit operator RenderTargetIdentifier(int nameID) { return default; }
        public static implicit operator RenderTargetIdentifier(Texture tex) { return default; }
    }
    public partial class CommandBuffer : IDisposable
    {
        public string name { get; set; }
        public void Dispose() { }
        public void Clear() { }
        public void Release() { }
        public void SetRenderTarget(RenderTargetIdentifier rt) { }
        public void SetViewProjectionMatrices(Matrix4x4 view, Matrix4x4 proj) { }
        public void SetGlobalVector(int nameID, Vector4 value) { }
        public void SetGlobalTexture(int nameID, RenderTargetIdentifier value) { }
        public void ClearRenderTarget(RTClearFlags clearFlags, Color backgroundColor, float depth = 1.0f, uint stencil = 0) { }
        public void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int submeshIndex, int shaderPass, MaterialPropertyBlock properties) { }
        public void ResolveAntiAliasedSurface(RenderTexture rt, RenderTexture target = null) { }
        public void GetTemporaryRT(int nameID, int width, int height, int depthBuffer, FilterMode filter, RenderTextureFormat format, RenderTextureReadWrite readWrite) { }
        public void ReleaseTemporaryRT(int nameID) { }
        public void Blit(Texture source, RenderTargetIdentifier dest) { }
        public void Blit(Texture source, RenderTargetIdentifier dest, Material mat, int pass) { }
        public void Blit(RenderTargetIdentifier source, RenderTargetIdentifier dest) { }
        public void Blit(RenderTargetIdentifier source, RenderTargetIdentifier dest, Material mat, int pass) { }
    }
}
