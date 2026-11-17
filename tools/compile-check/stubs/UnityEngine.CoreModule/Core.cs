// COMPILE-CHECK STUB ONLY - approximates the BepInEx-generated interop API. Not the real assembly.
using System;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace UnityEngine
{
    public enum HideFlags { None = 0, HideAndDontSave = 61 }

    public class Object : Il2CppSystem.Object
    {
        public Object(IntPtr pointer) : base(pointer) { }
        public Object() : base(IntPtr.Zero) { }
        public string name { get; set; }
        public HideFlags hideFlags { get; set; }
        public int GetInstanceID() => 0;
        public static void Destroy(Object obj) { }
        public static void DontDestroyOnLoad(Object target) { }
        public static Il2CppReferenceArray<Object> FindObjectsOfType(Il2CppSystem.Type type) => null;
        public static bool operator ==(Object x, Object y) => ReferenceEquals(x, y);
        public static bool operator !=(Object x, Object y) => !ReferenceEquals(x, y);
        public static implicit operator bool(Object exists) => exists is not null;
    }

    public class Component : Object
    {
        public Component(IntPtr pointer) : base(pointer) { }
        public GameObject gameObject => null;
        public Transform transform => null;
        public T GetComponent<T>() => default;
        public T GetComponentInChildren<T>(bool includeInactive) => default;
        public Il2CppArrayBase<T> GetComponentsInChildren<T>(bool includeInactive) => null;
    }

    public class Behaviour : Component
    {
        public Behaviour(IntPtr pointer) : base(pointer) { }
        public bool enabled { get; set; }
        public bool isActiveAndEnabled => false;
    }

    public class MonoBehaviour : Behaviour
    {
        public MonoBehaviour(IntPtr pointer) : base(pointer) { }
    }

    public sealed class GameObject : Object
    {
        public GameObject(IntPtr pointer) : base(pointer) { }
        public GameObject(string name) : base(IntPtr.Zero) { }
        public Transform transform => null;
        public int layer { get; set; }
        public bool activeSelf => false;
        public bool activeInHierarchy => false;
        public void SetActive(bool value) { }
        public T GetComponent<T>() => default;
        public Il2CppArrayBase<T> GetComponents<T>() => null;
        public T GetComponentInChildren<T>(bool includeInactive) => default;
        public Il2CppArrayBase<T> GetComponentsInChildren<T>(bool includeInactive) => null;
        public T AddComponent<T>() where T : Component => default;
    }

    public class Transform : Component
    {
        public Transform(IntPtr pointer) : base(pointer) { }
        public Vector3 position { get; set; }
        public Quaternion rotation { get; set; }
        public Vector3 localScale { get; set; }
        public Vector3 lossyScale => default;
        public Vector3 eulerAngles { get; set; }
        public Vector3 forward => default;
        public Vector3 right => default;
        public Vector3 up => default;
        public Transform parent { get; set; }
        public int childCount => 0;
        public Transform GetChild(int index) => null;
        public void SetParent(Transform parent, bool worldPositionStays) { }
    }

    public sealed class RectTransform : Transform
    {
        public RectTransform(IntPtr pointer) : base(pointer) { }
        public Vector2 anchorMin { get; set; }
        public Vector2 anchorMax { get; set; }
        public Vector2 pivot { get; set; }
        public Vector2 sizeDelta { get; set; }
        public Vector2 anchoredPosition { get; set; }
        public Rect rect => default;
    }

    public class Renderer : Component
    {
        public Renderer(IntPtr pointer) : base(pointer) { }
        public bool enabled { get; set; }
        public bool isVisible => false;
        public Bounds bounds => default;
        public int sortingLayerID { get; set; }
        public int sortingOrder { get; set; }
        public Material material { get; set; }
        public Material sharedMaterial { get; set; }
    }

    public sealed class SpriteRenderer : Renderer
    {
        public SpriteRenderer(IntPtr pointer) : base(pointer) { }
        public Sprite sprite { get; set; }
        public bool flipX { get; set; }
        public bool flipY { get; set; }
        public Color color { get; set; }
    }

    public sealed class MeshRenderer : Renderer
    {
        public MeshRenderer(IntPtr pointer) : base(pointer) { }
    }

    public sealed class SkinnedMeshRenderer : Renderer
    {
        public SkinnedMeshRenderer(IntPtr pointer) : base(pointer) { }
        public Mesh sharedMesh { get; set; }
        public void BakeMesh(Mesh mesh) { }
    }

    public sealed class MeshFilter : Component
    {
        public MeshFilter(IntPtr pointer) : base(pointer) { }
        public Mesh sharedMesh { get; set; }
        public Mesh mesh { get; set; }
    }

    public sealed class Mesh : Object
    {
        public Mesh(IntPtr pointer) : base(pointer) { }
        public Mesh() : base(IntPtr.Zero) { }
    }

    public class Material : Object
    {
        public Material(IntPtr pointer) : base(pointer) { }
        public Material(Shader shader) : base(IntPtr.Zero) { }
        public Color color { get; set; }
    }

    public sealed class Shader : Object
    {
        public Shader(IntPtr pointer) : base(pointer) { }
        public static Shader Find(string name) => null;
    }

    public enum SpriteMeshType { FullRect, Tight }

    public sealed class Sprite : Object
    {
        public Sprite(IntPtr pointer) : base(pointer) { }
        public Vector4 border => default;
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit) => null;
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit, uint extrude, SpriteMeshType meshType, Vector4 border) => null;
    }

    public enum TextureFormat { RGBA32 = 4 }
    public enum TextureWrapMode { Repeat, Clamp }
    public enum FilterMode { Point, Bilinear, Trilinear }

    public class Texture : Object
    {
        public Texture(IntPtr pointer) : base(pointer) { }
        public TextureWrapMode wrapMode { get; set; }
        public FilterMode filterMode { get; set; }
    }

    public sealed class Texture2D : Texture
    {
        public Texture2D(IntPtr pointer) : base(pointer) { }
        public Texture2D(int width, int height, TextureFormat textureFormat, bool mipChain) : base(IntPtr.Zero) { }
        public void SetPixel(int x, int y, Color color) { }
        public void Apply() { }
    }

    public class ScriptableObject : Object
    {
        public ScriptableObject(IntPtr pointer) : base(pointer) { }
    }

    public sealed class Camera : Behaviour
    {
        public Camera(IntPtr pointer) : base(pointer) { }
        public static Camera main => null;
        public bool orthographic { get; set; }
        public float orthographicSize { get; set; }
        public float fieldOfView { get; set; }
        public Vector3 WorldToScreenPoint(Vector3 position) => default;
        public Vector3 WorldToViewportPoint(Vector3 position) => default;
    }

    public static class Time
    {
        public static float time => 0;
        public static float deltaTime => 0;
        public static float unscaledTime => 0;
        public static float unscaledDeltaTime => 0;
        public static float timeScale { get; set; }
    }

    public static class Screen
    {
        public static int width => 0;
        public static int height => 0;
    }

    public static class Application
    {
        public static string unityVersion => null;
        public static string productName => null;
        public static string version => null;
    }

    public static class Resources
    {
        public static Il2CppArrayBase<T> FindObjectsOfTypeAll<T>() where T : Object => null;
    }

    public struct LayerMask
    {
        public static string LayerToName(int layer) => null;
    }

    public enum KeyCode
    {
        None = 0, Space = 32, A = 97, D = 100, S = 115, W = 119, UpArrow = 273, DownArrow = 274, RightArrow = 275, LeftArrow = 276,
        F5 = 286, F6 = 287, F7 = 288, F8 = 289, F9 = 290, F10 = 291, LeftShift = 304, JoystickButton4 = 334,
    }

    // Legacy input (UnityEngine.InputLegacyModule)
    public static class Input
    {
        public static bool GetKey(KeyCode key) => false;
        public static bool GetKeyDown(KeyCode key) => false;
        public static float GetAxisRaw(string axisName) => 0;
    }

    // UI module
    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }

    public sealed class Canvas : Behaviour
    {
        public Canvas(IntPtr pointer) : base(pointer) { }
        public RenderMode renderMode { get; set; }
        public int sortingOrder { get; set; }
    }

    public sealed class CanvasGroup : Behaviour
    {
        public CanvasGroup(IntPtr pointer) : base(pointer) { }
        public float alpha { get; set; }
        public bool interactable { get; set; }
        public bool blocksRaycasts { get; set; }
    }

    // Audio module
    public sealed class AudioClip : Object
    {
        public AudioClip(IntPtr pointer) : base(pointer) { }
        public float length => 0;
    }

    public sealed class AudioSource : Behaviour
    {
        public AudioSource(IntPtr pointer) : base(pointer) { }
        public bool playOnAwake { get; set; }
        public float spatialBlend { get; set; }
        public Audio.AudioMixerGroup outputAudioMixerGroup { get; set; }
        public void PlayOneShot(AudioClip clip, float volumeScale) { }
    }

    // Physics module
    public enum QueryTriggerInteraction { UseGlobal, Ignore, Collide }

    [Flags]
    public enum CollisionFlags { None = 0, Sides = 1, Above = 2, Below = 4 }

    public class Collider : Component
    {
        public Collider(IntPtr pointer) : base(pointer) { }
        public bool enabled { get; set; }
        public bool isTrigger { get; set; }
        public Bounds bounds => default;
    }

    public sealed class CharacterController : Collider
    {
        public CharacterController(IntPtr pointer) : base(pointer) { }
        public float radius { get; set; }
        public CollisionFlags Move(Vector3 motion) => default;
    }

    public sealed class Rigidbody : Component
    {
        public Rigidbody(IntPtr pointer) : base(pointer) { }
        public Vector3 position { get; set; }
    }

    public struct RaycastHit
    {
        public Vector3 m_Point;
        public float m_Distance;
        public float distance => m_Distance;
        public Collider collider => null;
    }

    public static class Physics
    {
        public static bool GetIgnoreLayerCollision(int layer1, int layer2) => false;
        public static Il2CppStructArray<RaycastHit> SphereCastAll(Vector3 origin, float radius, Vector3 direction, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction) => null;
    }

    // Physics2D module
    public class Collider2D : Behaviour
    {
        public Collider2D(IntPtr pointer) : base(pointer) { }
        public bool isTrigger { get; set; }
        public Bounds bounds => default;
    }

    public sealed class Rigidbody2D : Component
    {
        public Rigidbody2D(IntPtr pointer) : base(pointer) { }
        public Vector2 position { get; set; }
    }

    public struct RaycastHit2D
    {
        public Vector2 m_Point;
        public float m_Distance;
        public float distance => m_Distance;
        public Collider2D collider => null;
    }

    public static class Physics2D
    {
        public static int GetLayerCollisionMask(int layer) => 0;
        public static Il2CppStructArray<RaycastHit2D> CircleCastAll(Vector2 origin, float radius, Vector2 direction, float distance, int layerMask) => null;
    }
}

namespace UnityEngine.Audio
{
    public class AudioMixerGroup : UnityEngine.Object
    {
        public AudioMixerGroup(IntPtr pointer) : base(pointer) { }
    }
}

namespace UnityEngine.SceneManagement
{
    public struct Scene
    {
        public string name => null;
    }

    public static class SceneManager
    {
        public static Scene GetActiveScene() => default;
    }
}

namespace UnityEngine.UI
{
    public class Graphic : MonoBehaviour
    {
        public Graphic(IntPtr pointer) : base(pointer) { }
        public UnityEngine.Color color { get; set; }
        public bool raycastTarget { get; set; }
    }

    public class MaskableGraphic : Graphic
    {
        public MaskableGraphic(IntPtr pointer) : base(pointer) { }
    }

    public class Image : MaskableGraphic
    {
        public Image(IntPtr pointer) : base(pointer) { }
        public UnityEngine.Sprite sprite { get; set; }
        public Type type { get; set; }

        public enum Type { Simple, Sliced, Tiled, Filled }
    }
}
