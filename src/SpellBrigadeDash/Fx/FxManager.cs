namespace SpellBrigadeDash.Fx;

using System;
using System.Collections.Generic;
using SpellBrigadeDash.Game;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

/// <summary>
/// Spawns and animates every world-space effect: afterimages, dust, sparkles, speed streaks and the Blazing
/// Dash fire. Everything is animated from here each frame (no extra MonoBehaviours), and each effect
/// destroys its own objects when it ends.
/// </summary>
internal sealed class FxManager
{
    private const int MaxItems = 260;
    private const float Rad2Deg = 57.29578f;
    private readonly List<FxItem> _items = new();
    private readonly Random _rng = new();
    private GameObject? _root;

    public GameObject Root
    {
        get
        {
            if (_root != null) return _root;
            _root = new GameObject("SpellBrigadeDash_FX");
            Object.DontDestroyOnLoad(_root);
            return _root;
        }
    }

    public void Update(float dt)
    {
        Camera? cam = null;
        try
        {
            cam = Camera.main;
        }
        catch
        {
            // ignore
        }

        for (var i = _items.Count - 1; i >= 0; i--)
        {
            var item = _items[i];
            item.Age += dt;
            var done = item.Age >= item.Life;
            try
            {
                if (!done) item.Tick(item.Age / item.Life, dt, cam);
            }
            catch
            {
                done = true; // its objects were destroyed by a scene change
            }

            if (!done) continue;
            item.Destroy();
            _items.RemoveAt(i);
        }
    }

    public void Clear()
    {
        foreach (var item in _items) item.Destroy();
        _items.Clear();
    }

    private void Add(FxItem item)
    {
        if (_items.Count >= MaxItems)
        {
            _items[0].Destroy();
            _items.RemoveAt(0);
        }

        _items.Add(item);
    }

    // =============================================================================== afterimages

    /// <summary>A copy of the wizard that glows from <paramref name="start"/> to <paramref name="end"/> while fading and swelling slightly.</summary>
    public void SpawnAfterimage(PlayerInfo p, Color start, Color end, float life)
    {
        ModLog.StepOnce("ghost", $"fx: first afterimage ({(p.Is2D ? "sprites" : "meshes")})");
        if (p.Is2D) SpawnSpriteGhost(p, start, end, life);
        else SpawnMeshGhost(p, start, end, life);
    }

    private void SpawnSpriteGhost(PlayerInfo p, Color start, Color end, float life)
    {
        var ghost = new GameObject("SBD_Afterimage");
        ghost.transform.SetParent(Root.transform, false);
        var item = new SpriteGhost(ghost, start, end, life);
        var silhouette = FxAssets.SilhouetteShader2D;

        foreach (var src in p.Root.GetComponentsInChildren<SpriteRenderer>(false))
        {
            if (src == null || !src.enabled || src.sprite == null) continue;
            var part = new GameObject("part");
            part.transform.SetParent(ghost.transform, false);
            var st = src.transform;
            part.transform.position = st.position;
            part.transform.rotation = st.rotation;
            part.transform.localScale = st.lossyScale;

            var sr = part.AddComponent<SpriteRenderer>();
            sr.sprite = src.sprite;
            sr.flipX = src.flipX;
            sr.flipY = src.flipY;
            sr.sortingLayerID = src.sortingLayerID;
            sr.sortingOrder = src.sortingOrder - 1;
            if (silhouette != null)
            {
                var mat = new Material(silhouette);
                sr.material = mat;
                item.Materials.Add(mat);
            }

            sr.color = start;
            item.Parts.Add((part.transform, st.lossyScale, sr));
        }

        if (item.Parts.Count == 0)
        {
            Object.Destroy(ghost);
            return;
        }

        Add(item);
    }

    private void SpawnMeshGhost(PlayerInfo p, Color start, Color end, float life)
    {
        var shader = FxAssets.GhostShader3D;
        if (shader == null)
        {
            ModLog.WarnOnce("no-ghost-shader", "No usable shader for 3D afterimages; afterimages disabled.");
            return;
        }

        var ghost = new GameObject("SBD_Afterimage");
        ghost.transform.SetParent(Root.transform, false);
        var mat = new Material(shader) { color = start };
        var item = new MeshGhost(ghost, mat, start, end, life);

        // Only the character model: the wizard object also holds its spells (e.g. the Sun Mage's Solar Flare circle under
        // PlayerSpells) and effect meshes, which must not be copied into the afterimage.
        var model = ModelRoot(p);
        foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(false))
        {
            if (smr == null || !smr.enabled) continue;
            var mesh = new Mesh();
            smr.BakeMesh(mesh);
            item.Meshes.Add(mesh);
            item.Parts.Add(AddMeshPart(ghost, smr.transform, mesh, mat));
        }

        foreach (var mr in model.GetComponentsInChildren<MeshRenderer>(false))
        {
            if (mr == null || !mr.enabled) continue;
            var mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            item.Parts.Add(AddMeshPart(ghost, mr.transform, mf.sharedMesh, mat));
        }

        if (item.Parts.Count == 0)
        {
            item.Destroy();
            return;
        }

        ModLog.InfoOnce("ghost-model-" + p.Id, $"Afterimages copy the model '{model.name}' ({item.Parts.Count} mesh(es)), not the whole wizard (spells, effects).");
        Add(item);
    }

    /// <summary>
    /// The wizard's character model: the parent of its main (skinned) renderer, which holds the Animator and the armature
    /// (e.g. GameplayPlayer_SunMage_Skin3(Clone)/Visual/SunMage). Falls back to the wizard object if that isn't found.
    /// </summary>
    private static Transform ModelRoot(PlayerInfo p)
    {
        try
        {
            var main = p.MainRenderer;
            if (main == null) return p.RootT;
            var t = main.transform;
            var parent = t.parent;
            for (var up = parent; up != null; up = up.parent)
                if (up.Pointer == p.RootT.Pointer)
                    return parent == p.RootT ? t : parent!; // inside the wizard: the model root (or the renderer itself)
            return p.RootT;
        }
        catch
        {
            return p.RootT;
        }
    }

    private static (Transform, Vector3, SpriteRenderer?) AddMeshPart(GameObject ghost, Transform src, Mesh mesh, Material mat)
    {
        var part = new GameObject("part");
        part.transform.SetParent(ghost.transform, false);
        part.transform.position = src.position;
        part.transform.rotation = src.rotation;
        part.transform.localScale = src.lossyScale;
        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        part.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return (part.transform, src.lossyScale, null);
    }

    // =============================================================================== dust

    /// <summary>A soft dust burst kicked back from the dash direction. Lifted off the ground so it isn't half-hidden.</summary>
    public void SpawnPuff(PlayerInfo p, Vector3 feet, Vector3 dashDir, Color color, float strength)
    {
        ModLog.StepOnce("puff", "fx: first dust puff");
        var size = p.VisualSize;
        var side = Side(p, dashDir);
        var up = p.Is2D ? Vector3.zero : Vector3.up;
        var count = Math.Max(4, (int)(14 * strength));
        for (var i = 0; i < count; i++)
        {
            var vel = (-dashDir * Range(0.4f, 1.6f) + side * Range(-1.6f, 1.6f) + up * Range(0.3f, 1.1f)) * size * 1.6f * strength;
            var pos = feet + side * Range(-0.25f, 0.25f) * size + up * size * Range(0.15f, 0.35f)
                      + (p.Is2D ? new Vector3(0f, size * Range(0.02f, 0.12f), 0f) : Vector3.zero);
            var sr = MakeSprite(p, FxAssets.SoftCircle, pos, -2, glow: false);
            Add(new Particle(sr, vel, drag: 3.2f, accel: Vector3.zero,
                size0: size * Range(0.28f, 0.4f) * strength, size1: size * Range(0.75f, 1.05f) * strength,
                c0: color, c1: FxAssets.WithAlpha(color, color.a * 0.6f), alphaPow: 1.6f,
                twinkle: 0f, spin: Range(-60f, 60f), life: Range(0.45f, 0.7f), billboard: !p.Is2D, seed: Range(0f, 10f)));
        }
    }

    // =============================================================================== sparkles

    /// <summary>One arcane sparkle that drifts up and twinkles out.</summary>
    public void SpawnSparkle(PlayerInfo p, Vector3 pos, Color start, Color end, float scale = 1f)
    {
        ModLog.StepOnce("sparkle", "fx: first sparkle");
        var size = p.VisualSize;
        var drift = p.Is2D
            ? new Vector3(Range(-0.4f, 0.4f), Range(0.2f, 0.7f), 0f)
            : new Vector3(Range(-0.4f, 0.4f), Range(0.4f, 1.0f), Range(-0.4f, 0.4f));
        var sr = MakeSprite(p, FxAssets.Sparkle, pos, -1, glow: true);
        Add(new Particle(sr, drift * size * 0.6f, drag: 1.5f, accel: Vector3.zero,
            size0: size * Range(0.16f, 0.28f) * scale, size1: size * Range(0.04f, 0.08f) * scale,
            c0: start, c1: end, alphaPow: 1.2f,
            twinkle: 0.55f, spin: Range(-120f, 120f), life: Range(0.45f, 0.85f), billboard: !p.Is2D, seed: Range(0f, 10f)));
    }

    /// <summary>A ring of sparkles bursting outward (landing).</summary>
    public void SpawnSparkleBurst(PlayerInfo p, Vector3 center, Color start, Color end, int count)
    {
        var size = p.VisualSize;
        for (var i = 0; i < count; i++)
        {
            var a = (float)(i / (double)count * Math.PI * 2 + Range(0f, 0.5f));
            var dir = p.Is2D ? new Vector3(MathF.Cos(a), MathF.Sin(a), 0f) : new Vector3(MathF.Cos(a), 0.35f, MathF.Sin(a));
            var sr = MakeSprite(p, FxAssets.Sparkle, center + dir * size * 0.15f, -1, glow: true);
            Add(new Particle(sr, dir * size * Range(1.4f, 2.4f), drag: 4f, accel: Vector3.zero,
                size0: size * Range(0.18f, 0.26f), size1: size * 0.05f, c0: start, c1: end, alphaPow: 1.3f,
                twinkle: 0.4f, spin: Range(-180f, 180f), life: Range(0.35f, 0.6f), billboard: !p.Is2D, seed: Range(0f, 10f)));
        }
    }

    // =============================================================================== speed streaks

    /// <summary>A thin light line behind the wizard, aligned with the dash.</summary>
    public void SpawnStreak(PlayerInfo p, Vector3 bodyCenter, Vector3 dashDir, Color color)
    {
        ModLog.StepOnce("streak", "fx: first speed streak");
        var size = p.VisualSize;
        var side = Side(p, dashDir);
        var up = p.Is2D ? new Vector3(0f, 1f, 0f) : Vector3.up;
        var length = size * Range(0.9f, 1.6f);
        var pos = bodyCenter + side * Range(-0.45f, 0.45f) * size + up * Range(-0.35f, 0.35f) * size - dashDir * length * Range(0.4f, 0.8f);
        var sr = MakeSprite(p, FxAssets.Streak, pos, -1, glow: true);
        Add(new Streak(sr, dashDir, p.Is2D, length, size * Range(0.06f, 0.1f), color, Range(0.14f, 0.24f)));
    }

    // =============================================================================== blazing dash fire

    /// <summary>An upright flickering flame (outer flame + hot core) with the odd ember, standing on the ground.</summary>
    public void SpawnFlame(PlayerInfo p, Vector3 feet, float life)
    {
        ModLog.StepOnce("flame", "fx: first flame");
        var size = p.VisualSize;
        var go = new GameObject("SBD_Flame");
        go.transform.SetParent(Root.transform, false);
        go.transform.position = feet + (p.Is2D ? Vector3.zero : Vector3.up * 0.02f);

        var outer = AddChildSprite(go, p, FxAssets.Flame, -1, glow: false);
        var core = AddChildSprite(go, p, FxAssets.Flame, 0, glow: true);
        var height = size * Range(0.45f, 0.7f);
        Add(new Flame(go, outer, core, height, life + Range(-0.2f, 0.2f), !p.Is2D, Range(0f, 10f)));

        if (_rng.NextDouble() < 0.6) SpawnEmber(p, feet + (p.Is2D ? new Vector3(0f, height * 0.4f, 0f) : Vector3.up * height * 0.4f));
    }

    private void SpawnEmber(PlayerInfo p, Vector3 pos)
    {
        var size = p.VisualSize;
        var rise = p.Is2D ? new Vector3(Range(-0.3f, 0.3f), Range(0.8f, 1.4f), 0f) : new Vector3(Range(-0.3f, 0.3f), Range(0.9f, 1.6f), Range(-0.3f, 0.3f));
        var sr = MakeSprite(p, FxAssets.SoftCircle, pos, 1, glow: true);
        Add(new Particle(sr, rise * size, drag: 0.8f, accel: Vector3.zero,
            size0: size * 0.07f, size1: size * 0.02f, c0: new Color(1f, 0.85f, 0.4f, 1f), c1: new Color(1f, 0.3f, 0.1f, 0.8f),
            alphaPow: 1f, twinkle: 0.5f, spin: 0f, life: Range(0.6f, 1.1f), billboard: !p.Is2D, seed: Range(0f, 10f)));
    }

    // =============================================================================== helpers

    private SpriteRenderer MakeSprite(PlayerInfo p, Sprite sprite, Vector3 pos, int orderOffset, bool glow)
    {
        var go = new GameObject("SBD_Particle");
        go.transform.SetParent(Root.transform, false);
        go.transform.position = pos;
        return SetupRenderer(go, p, sprite, orderOffset, glow);
    }

    private static SpriteRenderer AddChildSprite(GameObject parent, PlayerInfo p, Sprite sprite, int orderOffset, bool glow)
    {
        var go = new GameObject("layer");
        go.transform.SetParent(parent.transform, false);
        return SetupRenderer(go, p, sprite, orderOffset, glow);
    }

    private static SpriteRenderer SetupRenderer(GameObject go, PlayerInfo p, Sprite sprite, int orderOffset, bool glow)
    {
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        var glowMat = glow ? FxAssets.Glow : null;
        if (glowMat != null) sr.sharedMaterial = glowMat;
        var main = p.MainRenderer?.TryCast<SpriteRenderer>();
        if (main != null)
        {
            sr.sortingLayerID = main.sortingLayerID;
            sr.sortingOrder = main.sortingOrder + orderOffset;
        }

        return sr;
    }

    private static Vector3 Side(PlayerInfo p, Vector3 dir)
        => p.Is2D ? new Vector3(-dir.y, dir.x, 0f) : Vector3.Cross(Vector3.up, dir).normalized;

    private float Range(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

    private static void SafeDestroy(Object? o)
    {
        try
        {
            if (o != null) Object.Destroy(o);
        }
        catch
        {
            // already gone
        }
    }

    /// <summary>Sprite rotation that faces the camera; in 2D just a spin around Z.</summary>
    private static Quaternion Facing(Camera? cam, bool billboard, float spinDegrees)
    {
        if (billboard && cam != null) return cam.transform.rotation * Quaternion.Euler(0f, 0f, spinDegrees);
        return Quaternion.Euler(0f, 0f, spinDegrees);
    }

    // =============================================================================== item types

    private abstract class FxItem
    {
        public float Age;
        public float Life;
        public abstract void Tick(float t, float dt, Camera? cam);
        public abstract void Destroy();
    }

    private sealed class SpriteGhost : FxItem
    {
        private readonly GameObject _go;
        private readonly Color _start;
        private readonly Color _end;
        public readonly List<(Transform t, Vector3 baseScale, SpriteRenderer sr)> Parts = new();
        public readonly List<Material> Materials = new();

        public SpriteGhost(GameObject go, Color start, Color end, float life)
        {
            _go = go;
            _start = start;
            _end = end;
            Life = life;
        }

        public override void Tick(float t, float dt, Camera? cam)
        {
            var c = FxAssets.WithAlpha(Color.Lerp(_start, _end, t), _start.a * (1f - t) * (1f - t));
            var swell = 1f + 0.1f * t;
            foreach (var (tr, baseScale, sr) in Parts)
            {
                sr.color = c;
                tr.localScale = baseScale * swell;
            }
        }

        public override void Destroy()
        {
            foreach (var m in Materials) SafeDestroy(m);
            SafeDestroy(_go);
        }
    }

    private sealed class MeshGhost : FxItem
    {
        private readonly GameObject _go;
        private readonly Material _mat;
        private readonly Color _start;
        private readonly Color _end;
        public readonly List<Mesh> Meshes = new();
        public readonly List<(Transform t, Vector3 baseScale, SpriteRenderer? sr)> Parts = new();

        public MeshGhost(GameObject go, Material mat, Color start, Color end, float life)
        {
            _go = go;
            _mat = mat;
            _start = start;
            _end = end;
            Life = life;
        }

        public override void Tick(float t, float dt, Camera? cam)
        {
            var c = Color.Lerp(_start, _end, t);
            _mat.color = FxAssets.WithAlpha(c, _start.a * (1f - t) * (1f - t));
            var swell = 1f + 0.06f * t;
            foreach (var (tr, baseScale, _) in Parts) tr.localScale = baseScale * swell;
        }

        public override void Destroy()
        {
            foreach (var m in Meshes) SafeDestroy(m);
            SafeDestroy(_mat);
            SafeDestroy(_go);
        }
    }

    /// <summary>Generic sprite particle: velocity + drag, size curve, colour gradient, fade, optional twinkle and spin.</summary>
    private sealed class Particle : FxItem
    {
        private readonly SpriteRenderer _sr;
        private readonly Transform _t;
        private readonly float _drag;
        private readonly Vector3 _accel;
        private readonly float _size0;
        private readonly float _size1;
        private readonly Color _c0;
        private readonly Color _c1;
        private readonly float _alphaPow;
        private readonly float _twinkle;
        private readonly float _spin;
        private readonly bool _billboard;
        private readonly float _seed;
        private Vector3 _vel;

        public Particle(SpriteRenderer sr, Vector3 vel, float drag, Vector3 accel, float size0, float size1, Color c0, Color c1,
            float alphaPow, float twinkle, float spin, float life, bool billboard, float seed)
        {
            _sr = sr;
            _t = sr.transform;
            _vel = vel;
            _drag = drag;
            _accel = accel;
            _size0 = size0;
            _size1 = size1;
            _c0 = c0;
            _c1 = c1;
            _alphaPow = alphaPow;
            _twinkle = twinkle;
            _spin = spin;
            _billboard = billboard;
            _seed = seed;
            Life = life;
            _t.localScale = Vector3.one * size0;
            _sr.color = c0;
        }

        public override void Tick(float t, float dt, Camera? cam)
        {
            _t.position += _vel * dt;
            _vel = (_vel + _accel * dt) * Math.Max(0f, 1f - _drag * dt);
            var ease = 1f - (1f - t) * (1f - t);
            _t.localScale = Vector3.one * Mathf.Lerp(_size0, _size1, ease);
            _t.rotation = Facing(cam, _billboard, _seed * 36f + _spin * Age);
            var c = Color.Lerp(_c0, _c1, t);
            var twinkle = 1f - _twinkle + _twinkle * (0.5f + 0.5f * Mathf.Sin(Age * 28f + _seed * 7f));
            _sr.color = FxAssets.WithAlpha(c, c.a * MathF.Pow(1f - t, _alphaPow) * twinkle);
        }

        public override void Destroy() => SafeDestroy(_sr.gameObject);
    }

    /// <summary>A stretched light line aligned with the dash direction (facing the camera in 3D).</summary>
    private sealed class Streak : FxItem
    {
        private readonly SpriteRenderer _sr;
        private readonly Transform _t;
        private readonly Vector3 _dir;
        private readonly bool _is2D;
        private readonly float _length;
        private readonly float _thickness;
        private readonly Color _color;

        public Streak(SpriteRenderer sr, Vector3 dir, bool is2D, float length, float thickness, Color color, float life)
        {
            _sr = sr;
            _t = sr.transform;
            _dir = dir;
            _is2D = is2D;
            _length = length;
            _thickness = thickness;
            _color = color;
            Life = life;
            _sr.color = color;
        }

        public override void Tick(float t, float dt, Camera? cam)
        {
            if (_is2D)
            {
                _t.rotation = Quaternion.Euler(0f, 0f, MathF.Atan2(_dir.y, _dir.x) * Rad2Deg);
            }
            else if (cam != null)
            {
                // Lie in the camera's view plane with the sprite's X axis along the dash.
                var fwd = cam.transform.forward;
                var along = _dir - fwd * Vector3.Dot(_dir, fwd);
                if (along.sqrMagnitude > 1e-6f) _t.rotation = Quaternion.LookRotation(fwd, Vector3.Cross(fwd, along.normalized));
            }

            // sprite is 1 x 0.125 units: scale X = length, Y = thickness
            _t.localScale = new Vector3(_length * (1f - 0.4f * t), _thickness / 0.125f * (1f - 0.5f * t), 1f);
            _sr.color = FxAssets.WithAlpha(_color, _color.a * (1f - t));
        }

        public override void Destroy() => SafeDestroy(_sr.gameObject);
    }

    /// <summary>Two-layer flame (orange body + glowing yellow core) that flickers, sways and dies down.</summary>
    private sealed class Flame : FxItem
    {
        private static readonly Color OuterHot = new(1f, 0.55f, 0.12f, 0.95f);
        private static readonly Color OuterCool = new(0.85f, 0.18f, 0.08f, 0.85f);
        private static readonly Color CoreColor = new(1f, 0.93f, 0.6f, 1f);

        private readonly GameObject _go;
        private readonly SpriteRenderer _outer;
        private readonly SpriteRenderer _core;
        private readonly float _height;
        private readonly bool _billboard;
        private readonly float _seed;

        public Flame(GameObject go, SpriteRenderer outer, SpriteRenderer core, float height, float life, bool billboard, float seed)
        {
            _go = go;
            _outer = outer;
            _core = core;
            _height = height;
            Life = life;
            _billboard = billboard;
            _seed = seed;
        }

        public override void Tick(float t, float dt, Camera? cam)
        {
            // grow in quickly, burn, then die down
            var grow = Mathf.Clamp01(Age / 0.12f);
            var die = t < 0.65f ? 1f : 1f - (t - 0.65f) / 0.35f;
            var flicker = 0.82f + 0.18f * Mathf.Sin(Age * 23f + _seed) * Mathf.Sin(Age * 13.7f + _seed * 2f);
            var h = _height * grow * die * flicker;
            var w = _height * 0.55f * grow * (0.9f + 0.1f * Mathf.Sin(Age * 17f + _seed * 3f));
            var sway = 7f * Mathf.Sin(Age * 9f + _seed);

            // sprite is 0.5 x 1 units at scale 1
            _go.transform.rotation = Facing(cam, _billboard, sway);
            _outer.transform.localScale = new Vector3(w * 2f, h, 1f);
            _core.transform.localScale = new Vector3(w * 1.1f, h * 0.6f, 1f);

            var outer = Color.Lerp(OuterHot, OuterCool, t);
            _outer.color = FxAssets.WithAlpha(outer, outer.a * Mathf.Clamp01(die * 1.3f));
            _core.color = FxAssets.WithAlpha(CoreColor, Mathf.Clamp01(die * 1.2f) * (0.75f + 0.25f * flicker));
        }

        public override void Destroy() => SafeDestroy(_go);
    }
}
