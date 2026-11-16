using System;
using System.Collections.Generic;
using SpellBrigadeDash.Game;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace SpellBrigadeDash.Fx;

/// <summary>
/// Spawns and animates every world-space effect: afterimages, dust puffs and the Blazing Dash trail.
/// Everything is animated from here each frame (no extra MonoBehaviours), and each effect
/// destroys its own objects when it ends.
/// </summary>
internal sealed class FxManager
{
    private const int MaxItems = 160;
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

    // ------------------------------------------------------------------------------- afterimages

    public void SpawnAfterimage(PlayerInfo p, Color tint, float life)
    {
        if (p.Is2D) SpawnSpriteGhost(p, tint, life);
        else SpawnMeshGhost(p, tint, life);
    }

    private void SpawnSpriteGhost(PlayerInfo p, Color tint, float life)
    {
        var ghost = new GameObject("SBD_Afterimage");
        ghost.transform.SetParent(Root.transform, false);
        var item = new SpriteGhost(ghost, tint, life);
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

            sr.color = tint;
            item.Renderers.Add(sr);
        }

        if (item.Renderers.Count == 0)
        {
            Object.Destroy(ghost);
            return;
        }

        Add(item);
    }

    private void SpawnMeshGhost(PlayerInfo p, Color tint, float life)
    {
        var shader = FxAssets.GhostShader3D;
        if (shader == null)
        {
            ModLog.WarnOnce("no-ghost-shader", "No usable shader for 3D afterimages; afterimages disabled.");
            return;
        }

        var ghost = new GameObject("SBD_Afterimage");
        ghost.transform.SetParent(Root.transform, false);
        var mat = new Material(shader) { color = tint };
        var item = new MeshGhost(ghost, mat, tint, life);

        foreach (var smr in p.Root.GetComponentsInChildren<SkinnedMeshRenderer>(false))
        {
            if (smr == null || !smr.enabled) continue;
            var mesh = new Mesh();
            smr.BakeMesh(mesh);
            item.Meshes.Add(mesh);
            AddMeshPart(ghost, smr.transform, mesh, mat);
        }

        foreach (var mr in p.Root.GetComponentsInChildren<MeshRenderer>(false))
        {
            if (mr == null || !mr.enabled) continue;
            var mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            AddMeshPart(ghost, mr.transform, mf.sharedMesh, mat);
        }

        if (ghost.transform.childCount == 0)
        {
            item.Destroy();
            return;
        }

        Add(item);
    }

    private static void AddMeshPart(GameObject ghost, Transform src, Mesh mesh, Material mat)
    {
        var part = new GameObject("part");
        part.transform.SetParent(ghost.transform, false);
        part.transform.position = src.position;
        part.transform.rotation = src.rotation;
        part.transform.localScale = src.lossyScale;
        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = part.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
    }

    // ------------------------------------------------------------------------------- dust & fire

    public void SpawnPuff(PlayerInfo p, Vector3 position, Vector3 dashDir, Color color, float strength)
    {
        var size = p.VisualSize;
        var count = Math.Max(3, (int)(8 * strength));
        var side = p.Is2D ? new Vector3(-dashDir.y, dashDir.x, 0f) : Vector3.Cross(Vector3.up, dashDir).normalized;
        for (var i = 0; i < count; i++)
        {
            var back = -dashDir * Range(0.5f, 1.6f) + side * Range(-1.3f, 1.3f);
            if (!p.Is2D) back += Vector3.up * Range(0.1f, 0.6f);
            var vel = back * size * 1.4f * strength;
            var start = position + side * Range(-0.15f, 0.15f) * size;
            var part = MakeParticle(p, start, color, -2);
            Add(new Particle(part.go, part.sr, vel, size * 0.22f * strength, size * 0.55f * strength, color, Range(0.28f, 0.42f), !p.Is2D, drag: 4f));
        }
    }

    public void SpawnFlame(PlayerInfo p, Vector3 position, float life)
    {
        var size = p.VisualSize;
        var color = new Color(1f, Range(0.45f, 0.7f), 0.15f, 0.85f);
        var part = MakeParticle(p, position, color, -1);
        var rise = p.Is2D ? new Vector3(0f, 0.15f, 0f) : new Vector3(0f, 0.35f, 0f);
        Add(new Flame(part.go, part.sr, rise * size, size * Range(0.35f, 0.5f), color, life + Range(-0.15f, 0.15f), !p.Is2D, (float)_rng.NextDouble() * 10f));
    }

    private (GameObject go, SpriteRenderer sr) MakeParticle(PlayerInfo p, Vector3 pos, Color color, int orderOffset)
    {
        var go = new GameObject("SBD_Particle");
        go.transform.SetParent(Root.transform, false);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = FxAssets.SoftCircle;
        sr.color = color;
        var main = p.MainRenderer?.TryCast<SpriteRenderer>();
        if (main != null)
        {
            sr.sortingLayerID = main.sortingLayerID;
            sr.sortingOrder = main.sortingOrder + orderOffset;
        }

        return (go, sr);
    }

    private float Range(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

    // ------------------------------------------------------------------------------- item types

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
        private readonly Color _tint;
        public readonly List<SpriteRenderer> Renderers = new();
        public readonly List<Material> Materials = new();

        public SpriteGhost(GameObject go, Color tint, float life)
        {
            _go = go;
            _tint = tint;
            Life = life;
        }

        public override void Tick(float t, float dt, Camera? cam)
        {
            var a = _tint.a * (1f - t) * (1f - t);
            var c = FxAssets.WithAlpha(_tint, a);
            foreach (var r in Renderers) r.color = c;
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
        private readonly Color _tint;
        public readonly List<Mesh> Meshes = new();

        public MeshGhost(GameObject go, Material mat, Color tint, float life)
        {
            _go = go;
            _mat = mat;
            _tint = tint;
            Life = life;
        }

        public override void Tick(float t, float dt, Camera? cam) => _mat.color = FxAssets.WithAlpha(_tint, _tint.a * (1f - t) * (1f - t));

        public override void Destroy()
        {
            foreach (var m in Meshes) SafeDestroy(m);
            SafeDestroy(_mat);
            SafeDestroy(_go);
        }
    }

    private class Particle : FxItem
    {
        protected readonly GameObject Go;
        protected readonly SpriteRenderer Sr;
        protected readonly Color Color;
        protected readonly bool Billboard;
        private readonly float _startSize;
        private readonly float _endSize;
        private readonly float _drag;
        private Vector3 _vel;

        public Particle(GameObject go, SpriteRenderer sr, Vector3 vel, float startSize, float endSize, Color color, float life, bool billboard, float drag)
        {
            Go = go;
            Sr = sr;
            _vel = vel;
            _startSize = startSize;
            _endSize = endSize;
            Color = color;
            Life = life;
            Billboard = billboard;
            _drag = drag;
            go.transform.localScale = Vector3.one * startSize;
        }

        public override void Tick(float t, float dt, Camera? cam)
        {
            var tr = Go.transform;
            tr.position += _vel * dt;
            _vel *= Math.Max(0f, 1f - _drag * dt);
            tr.localScale = Vector3.one * Mathf.Lerp(_startSize, _endSize, 1f - (1f - t) * (1f - t));
            if (Billboard && cam != null) tr.rotation = cam.transform.rotation;
            Sr.color = FxAssets.WithAlpha(Color, Color.a * (1f - t) * (1f - t));
        }

        public override void Destroy() => SafeDestroy(Go);
    }

    private sealed class Flame : Particle
    {
        private readonly float _size;
        private readonly float _seed;

        public Flame(GameObject go, SpriteRenderer sr, Vector3 rise, float size, Color color, float life, bool billboard, float seed)
            : base(go, sr, rise, size, size, color, life, billboard, 0f)
        {
            _size = size;
            _seed = seed;
        }

        public override void Tick(float t, float dt, Camera? cam)
        {
            base.Tick(t, dt, cam);
            var flicker = 0.85f + 0.15f * Mathf.Sin((Age + _seed) * 25f);
            var fade = t < 0.75f ? 1f : 1f - (t - 0.75f) / 0.25f;
            Go.transform.localScale = Vector3.one * (_size * flicker * (0.6f + 0.4f * fade));
            var c = Color.Lerp(Color, new Color(0.9f, 0.2f, 0.1f, Color.a), t);
            Sr.color = FxAssets.WithAlpha(c, Color.a * fade);
        }
    }

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
}
