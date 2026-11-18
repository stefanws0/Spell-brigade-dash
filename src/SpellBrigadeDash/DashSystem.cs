namespace SpellBrigadeDash;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SpellBrigadeDash.Controls;
using SpellBrigadeDash.Core;
using SpellBrigadeDash.Discovery;
using SpellBrigadeDash.Fx;
using SpellBrigadeDash.Game;
using SpellBrigadeDash.Motion;
using UnityEngine;

/// <summary>
/// The mod's main loop, called by <see cref="DashRunner"/> every frame.
///
/// Update:     input → find wizard → recharge → start dash → watch other players (co-op i-frames)
/// LateUpdate: move the dashing wizard (after the game's own movement) → effects → bar
/// </summary>
internal sealed class DashSystem
{
    private const float FlameSpacingFactor = 0.22f; // distance between sprite flames, relative to the wizard's size
    private const float GameFlameSpacingFactor = 0.42f; // the game's fire is fuller, so fewer of them along the path
    private const float SparkleSpacingFactor = 0.16f; // distance between sparkles, relative to the wizard's size
    private const float StreakInterval = 0.018f;

    private readonly DashInput _input = new();
    private readonly DashProgress _progress = new();
    private readonly DashCharges _charges = new(0, 2f);
    private readonly FxManager _fx = new();
    private readonly GameFireTrail _gameFire = new();
    private readonly BlazeDamage _blaze = new();
    private readonly DashBar _bar = new();
    private readonly RemoteDashDetector _remote = new();
    private readonly List<RemoteEmitter> _remoteEmitters = new();
    private readonly HashSet<long> _hookedRemotes = new();

    private IReadOnlyList<DashCardDef> _cards = Array.Empty<DashCardDef>();
    private DashBaseStats _stats = new();
    private Color _ghostStart;
    private Color _ghostEnd;
    private Color _dustColor;
    private Color _sparkleStart;
    private Color _sparkleEnd;
    private Color _streakColor;
    private bool _initialized;
    private ActiveDash? _active;
    private float _bufferedUntil = -1f;
    private float _discoveryAt = -1f;
    private float _hooksAt = -1f;
    private bool _hooksInstalled;
    private string? _runKey;
    private float _nextRunCheck;
    private int _remoteDashes;
    private int _localDashes;
    private readonly System.Random _rng = new();

    public static DashSystem Instance { get; } = new();

    public PlayerLocator Locator { get; } = new();
    public SoundPlayer Sound { get; } = new();

    // =========================================================================== frame callbacks

    public void Update()
    {
        var now = Time.time;
        var dt = Time.deltaTime;
        try
        {
            if (!_initialized) Init();
            Invulnerability.Now = now;

            ModLog.StepOnce("poll", "first frame: reading input");
            _input.Poll();
            if (ModConfig.DebugHotkeys.Value) HandleHotkeys();

            CheckRun(Time.unscaledTime);
            ModLog.StepOnce("locate", "first frame: looking for the wizard");
            Locator.Update(Time.unscaledTime);
            var p = Locator.Local;
            if (p != null && p.IsDestroyed) p = null;
            if (p != null) TrackMovement(p, dt);
            if (p != null && !_hooksInstalled && _hooksAt > 0f && Time.unscaledTime >= _hooksAt) InstallHooks(p);

            SyncCharges();
            var refilled = _charges.Tick(dt);
            if (refilled > 0) _bar.OnRefilled(_charges, refilled, now);

            if (p != null)
            {
                if (_input.DashPressed) OnDashPressed(p, now);
                else if (_active == null && _bufferedUntil >= now)
                {
                    _bufferedUntil = -1f;
                    OnDashPressed(p, now);
                }
            }

            UpdateRemotes(now);
            Invulnerability.Prune();
            CardBridge.Update(_cards, _progress, Time.unscaledTime);
            StatPanelRows.Update(_stats, _progress, Time.unscaledTime);
            DamageReference.Tick(Time.unscaledTime);

            if (_discoveryAt > 0f && Time.unscaledTime >= _discoveryAt)
            {
                _discoveryAt = -1f;
                ModLog.Step("discovery report: writing");
                DiscoveryReport.Write(this, "run started");
                ModLog.Step("discovery report: done");
            }
        }
        catch (Exception ex)
        {
            ModLog.ErrorThrottled("DashSystem.Update", ex, Time.unscaledTime);
        }
    }

    public void LateUpdate()
    {
        var now = Time.time;
        var dt = Time.deltaTime;
        try
        {
            var p = Locator.Local;
            if (p != null && p.IsDestroyed) p = null;

            if (_active != null)
            {
                if (p == null) _active = null;
                else StepDash(p, now, dt);
            }

            ModLog.StepOnce("fx", "first frame: effects + bar");
            _fx.Update(dt);
            _gameFire.Update(now);
            _blaze.Update(now);
            var allowed = p != null && Time.timeScale > 0f && !Locator.IsDead(p);
            _bar.Update(p, _charges, allowed, now, Time.unscaledDeltaTime);
            ModLog.StepOnce("frame-ok", "first frame completed");
        }
        catch (Exception ex)
        {
            ModLog.ErrorThrottled("DashSystem.LateUpdate", ex, Time.unscaledTime);
        }
    }

    // =========================================================================== setup / run start

    private void Init()
    {
        ModLog.Step("Init: start");
        _initialized = true;
        _stats = ModConfig.BaseStats();
        _cards = DashCards.Build(ModConfig.CardSettings());
        _ghostStart = FxAssets.ParseColor(ModConfig.AfterimageColor.Value, new Color(0.94f, 0.78f, 1f, 0.85f));
        _ghostEnd = FxAssets.ParseColor(ModConfig.AfterimageFadeColor.Value, new Color(0.42f, 0.17f, 1f, 0f));
        _dustColor = FxAssets.ParseColor(ModConfig.DustColor.Value, new Color(0.89f, 0.84f, 0.95f, 0.94f));
        _sparkleStart = FxAssets.ParseColor(ModConfig.SparkleColor.Value, new Color(1f, 0.88f, 1f, 1f));
        _sparkleEnd = FxAssets.ParseColor(ModConfig.SparkleFadeColor.Value, new Color(0.61f, 0.36f, 1f, 1f));
        _streakColor = FxAssets.ParseColor(ModConfig.StreakColor.Value, new Color(0.82f, 0.66f, 1f, 0.75f));
        Locator.LocalChanged += OnNewLocalPlayer;
        CardBridge.Picked = ApplyCard;
        // Nothing risky here: Harmony patches wait until a run has started (see InstallHooks).
        ModLog.Step("Init: done");
    }

    /// <summary>
    /// Harmony patches (i-frame damage hooks, optional level-up tracing). They run a few seconds into a run instead
    /// of at game start, and every patch leaves a breadcrumb in SpellBrigadeDash.log first.
    /// </summary>
    private void InstallHooks(PlayerInfo p)
    {
        _hooksInstalled = true;
        if (ModConfig.SafeMode.Value)
        {
            ModLog.Info("SafeMode: skipping i-frame hooks and tracing.");
            return;
        }

        if (ModConfig.SoundEnabled.Value)
        {
            ModLog.Step("sound: preparing FMOD event");
            FmodSound.Prepare();
        }

        ModLog.Step("hooks: start");
        DamageHooks.HookConfigured();
        DamageHooks.HookFor(p);
        CardBridge.InstallPickHook();
        MethodTracer.Install();
        ModLog.Step("hooks: done");
    }

    /// <summary>
    /// A new local wizard object: a run started, or you were revived (the game spawns a fresh wizard). Clears per-wizard
    /// state only; the run's dash cards are reset by <see cref="CheckRun"/> when the game's run changes.
    /// </summary>
    private void OnNewLocalPlayer(PlayerInfo p)
    {
        _active = null;
        _bufferedUntil = -1f;
        _fx.Clear();
        _gameFire.Clear();
        _blaze.Clear();
        _remote.Clear();
        _remoteEmitters.Clear();
        SyncCharges();
        _charges.RefillAll();
        ModLog.Info($"Wizard spawned; dash cards this run: {_progress}.");

        DamageHooks.SetLocal(p);
        if (!_hooksInstalled) _hooksAt = Time.unscaledTime + Math.Max(0f, ModConfig.HookDelaySeconds.Value);
        else if (!ModConfig.SafeMode.Value) DamageHooks.HookFor(p);

        if (ModConfig.DiscoveryEnabled.Value) _discoveryAt = Time.unscaledTime + Math.Max(0f, ModConfig.HookDelaySeconds.Value) + 3f;
    }

    /// <summary>Reset the dash cards when the game starts a new run (not when a new wizard object appears).</summary>
    private void CheckRun(float now)
    {
        if (now < _nextRunCheck) return;
        _nextRunCheck = now + 0.5f;
        var key = GameRun.Key();
        if (key == null || key == _runKey) return;
        _runKey = key;

        _stats = ModConfig.BaseStats();
        _active = null;
        _blaze.Clear();
        _progress.Reset();
        if (ModConfig.StartRunWithDash.Value) _progress.Apply(DashCardId.Dash);
        SyncCharges();
        _charges.RefillAll();
        ModLog.Info($"New run ({GameRun.Status}). Dash {(_progress.Unlocked ? "unlocked (Debug.StartRunWithDash)" : "locked until the Dash card is picked")}.");
    }

    private void SyncCharges()
    {
        var max = DashTuning.MaxCharges(_stats, _progress);
        if (_charges.Max != max)
        {
            var firstUnlock = _charges.Max == 0 && max > 0;
            _charges.SetMax(max);
            if (firstUnlock) _charges.RefillAll();
        }

        _charges.RechargeSeconds = DashTuning.RechargeSeconds(_stats, _progress);
    }

    // =========================================================================== dashing

    private void OnDashPressed(PlayerInfo p, float now)
    {
        if (Time.timeScale <= 0f || !_progress.Unlocked) return;
        if (Locator.IsDead(p)) return;

        if (_active != null)
        {
            // Pressed during a dash: queue the next one if this one is about to end.
            var timeLeft = _active.Remaining / Math.Max(0.01f, _active.Speed);
            if (timeLeft <= ModConfig.InputBuffer.Value) _bufferedUntil = now + timeLeft + 0.05f;
            return;
        }

        if (!_charges.TryConsume())
        {
            _bar.OnEmptyPress(now);
            return;
        }

        StartDash(p, now);
    }

    private void StartDash(PlayerInfo p, float now)
    {
        ModLog.StepOnce("first-dash", "first dash");
        var dir = ResolveDirection(p);
        var distance = DashTuning.Distance(_stats, _progress);
        var duration = DashTuning.Duration(_stats);
        _active = new ActiveDash
        {
            Dir = dir,
            Remaining = distance,
            Speed = distance / duration,
            MaxTime = duration * 2f + 0.05f,
            FlameSpacing = Math.Max(0.05f, p.VisualSize * (_gameFire.Usable ? GameFlameSpacingFactor : FlameSpacingFactor)),
            FlameRadius = DashTuning.Blazing(_progress) ? _blaze.FlameRadius(p, now) : 0f,
            SparkleSpacing = Math.Max(0.05f, p.VisualSize * SparkleSpacingFactor),
        };
        _localDashes++;

        var iframes = DashTuning.InvulnerableSeconds(_stats);
        Invulnerability.Grant(p, iframes);
        NetBridge.SendDash(iframes);

        // The price of an early dash: a share of max HP (never the last point) until Ethereal Step.
        var hpTaken = HealthBridge.ApplyCost(p, DashTuning.HealthCostFraction(_stats, _progress));
        if (hpTaken > 0f) ModLog.Debug($"Dash cost {hpTaken:0.#} HP.");

        _bar.OnDash(now);
        if (ModConfig.DustPuff.Value) _fx.SpawnPuff(p, p.Feet, dir, _dustColor, 1f);
        if (ModConfig.Afterimages.Value) _fx.SpawnAfterimage(p, _ghostStart, _ghostEnd, ModConfig.AfterimageLifetime.Value);
        _active.NextGhost = ModConfig.AfterimageInterval.Value;
        Sound.Play(_fx.Root, p.Feet, now);
        if (DashTuning.Blazing(_progress) && ModConfig.SoundEnabled.Value) FmodSound.PlayEvent(ModConfig.BlazingFmodEvent.Value, p.Feet);
        GameFx.TeleportBurst(p);
        GameFx.SpeedLines(p, _fx.Root, true);
    }

    /// <summary>Roughly the middle of the wizard's body (for streaks and sparkles).</summary>
    private static Vector3 BodyCenter(PlayerInfo p)
        => p.Feet + (p.Is2D ? new Vector3(0f, p.VisualSize * 0.45f, 0f) : Vector3.up * p.VisualSize * 0.45f);

    private static Vector3 RandomInBody(PlayerInfo p, System.Random rng)
    {
        var h = p.VisualSize * (0.1f + 0.8f * (float)rng.NextDouble());
        return p.Feet + (p.Is2D ? new Vector3(0f, h, 0f) : Vector3.up * h);
    }

    private void StepDash(PlayerInfo p, float now, float dt)
    {
        var a = _active!;
        if (dt <= 0f) return; // paused
        a.Elapsed += dt;

        var step = Math.Min(a.Speed * dt, a.Remaining);
        var moved = step > 0f ? DashMotor.Move(p, a.Dir, step) : 0f;
        a.Remaining -= step;
        a.Travelled += moved;

        if (ModConfig.Afterimages.Value && a.Elapsed >= a.NextGhost)
        {
            _fx.SpawnAfterimage(p, _ghostStart, _ghostEnd, ModConfig.AfterimageLifetime.Value);
            a.NextGhost = a.Elapsed + Math.Max(0.01f, ModConfig.AfterimageInterval.Value);
        }

        if (ModConfig.SpeedStreaks.Value && a.Elapsed >= a.NextStreak)
        {
            var center = BodyCenter(p);
            _fx.SpawnStreak(p, center, a.Dir, _streakColor);
            _fx.SpawnStreak(p, center, a.Dir, _streakColor);
            a.NextStreak = a.Elapsed + StreakInterval;
        }

        if (ModConfig.Sparkles.Value)
        {
            while (a.Travelled >= a.NextSparkleAt)
            {
                var back = a.Travelled - a.NextSparkleAt;
                _fx.SpawnSparkle(p, RandomInBody(p, _rng) - a.Dir * back, _sparkleStart, _sparkleEnd);
                a.NextSparkleAt += a.SparkleSpacing;
            }
        }

        if (DashTuning.Blazing(_progress))
        {
            var feet = p.Feet;
            var life = ModConfig.BlazingTrailSeconds.Value;
            while (a.Travelled >= a.NextFlameAt)
            {
                var back = a.Travelled - a.NextFlameAt;
                var spot = feet - a.Dir * back;
                if (!_gameFire.TrySpawn(p, spot, _fx.Root.transform, life, now, _blaze.SizeMultiplier)) _fx.SpawnFlame(p, spot, life);
                _blaze.AddFlame(spot, a.FlameRadius, now + life);
                a.NextFlameAt += a.FlameSpacing;
            }
        }

        var blocked = step > 0.0001f && moved < step * 0.3f;
        if (a.Remaining <= 0.0001f || blocked || a.Elapsed >= a.MaxTime) EndDash(p);
    }

    private void EndDash(PlayerInfo p)
    {
        var a = _active!;
        _active = null;
        GameFx.SpeedLines(p, _fx.Root, false);
        if (ModConfig.DustPuffOnLanding.Value) _fx.SpawnPuff(p, p.Feet, a.Dir, _dustColor, 0.6f);
        if (ModConfig.Sparkles.Value) _fx.SpawnSparkleBurst(p, BodyCenter(p), _sparkleStart, _sparkleEnd, 8);
    }

    /// <summary>Movement input → world direction; standing still → last movement / facing direction.</summary>
    private Vector3 ResolveDirection(PlayerInfo p)
    {
        var input = _input.Move;
        if (input.sqrMagnitude > 0.04f)
        {
            input.Normalize();
            if (p.Is2D) return new Vector3(input.x, input.y, 0f);

            var fwd = Vector3.forward;
            var right = Vector3.right;
            var cam = Camera.main;
            if (cam != null)
            {
                var ct = cam.transform;
                fwd = ct.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 1e-4f)
                {
                    fwd = ct.up; // camera looking straight down
                    fwd.y = 0f;
                }

                fwd.Normalize();
                right = ct.right;
                right.y = 0f;
                right.Normalize();
            }

            return (fwd * input.y + right * input.x).normalized;
        }

        if (p.LastMoveDir.sqrMagnitude > 0.01f) return p.LastMoveDir;

        if (p.Is2D)
        {
            var sr = p.MainRenderer?.TryCast<SpriteRenderer>();
            var sx = sr != null && sr.flipX ? -1f : 1f;
            if (p.RootT.lossyScale.x < 0f) sx = -sx;
            return new Vector3(sx, 0f, 0f);
        }

        var f = p.RootT.forward;
        f.y = 0f;
        return f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
    }

    private void TrackMovement(PlayerInfo p, float dt)
    {
        var pos = p.RootT.position;
        if (p.HasLastPos && dt > 0f && _active == null)
        {
            var delta = pos - p.LastPos;
            if (p.Is2D) delta.z = 0f;
            else delta.y = 0f;
            var speed = delta.magnitude / dt;
            if (speed > 0.3f)
            {
                p.LastMoveDir = delta.normalized;
                if (speed < _stats.Distance / DashTuning.Duration(_stats) * 0.5f) p.MaxWalkSpeed = Math.Max(p.MaxWalkSpeed, speed);
            }
        }

        p.LastPos = pos;
        p.HasLastPos = true;
    }

    // =========================================================================== other players

    private void UpdateRemotes(float now)
    {
        var wantIFrames = ModConfig.RemoteIFrames.Value;
        var wantGhosts = ModConfig.RemoteAfterimages.Value;
        if (!wantIFrames && !wantGhosts) return;

        var baseSpeed = _stats.Distance / DashTuning.Duration(_stats);
        _remote.SpeedThreshold = baseSpeed * ModConfig.RemoteDashSpeedFactor.Value;
        _remote.MaxJump = _stats.Distance * 4f;

        foreach (var r in Locator.Remotes)
        {
            if (r.IsDestroyed) continue;
            if (_hooksInstalled && _hookedRemotes.Add(r.Id)) DamageHooks.HookFor(r);

            var f = r.RootT.position;
            if (!_remote.Observe(r.Id, new System.Numerics.Vector3(f.x, f.y, f.z), now)) continue;

            _remoteDashes++;
            if (wantIFrames) Invulnerability.Grant(r, DashTuning.InvulnerableSeconds(_stats) + 0.1f);
            if (wantGhosts) _remoteEmitters.Add(new RemoteEmitter(r, now + DashTuning.Duration(_stats)));
        }

        for (var i = _remoteEmitters.Count - 1; i >= 0; i--)
        {
            var e = _remoteEmitters[i];
            if (e.Player.IsDestroyed || now > e.Until)
            {
                _remoteEmitters.RemoveAt(i);
                continue;
            }

            if (now < e.Next) continue;
            _fx.SpawnAfterimage(e.Player, _ghostStart, _ghostEnd, ModConfig.AfterimageLifetime.Value);
            e.Next = now + Math.Max(0.02f, ModConfig.AfterimageInterval.Value);
        }

        _remote.Prune(now);
    }

    // =========================================================================== debug / cards

    private void HandleHotkeys()
    {
        if (_input.DebugPressed(DebugKey.Report))
        {
            ModLog.Step("discovery report (hotkey): writing");
            DiscoveryReport.Write(this, "hotkey");
            ModLog.Step("discovery report (hotkey): done");
        }
        if (_input.DebugPressed(DebugKey.ToggleDash))
        {
            if (_progress.Unlocked) _progress.Lock();
            else _progress.Apply(DashCardId.Dash);
            ModLog.Info($"[debug] Dash {(_progress.Unlocked ? "unlocked" : "locked")}.");
        }

        if (_input.DebugPressed(DebugKey.ExtraCharge)) ApplyCard(DashCardId.ExtraCharge);
        if (_input.DebugPressed(DebugKey.QuickRecovery)) ApplyCard(DashCardId.QuickRecovery);
        if (_input.DebugPressed(DebugKey.LongStride)) ApplyCard(DashCardId.LongStride);
        if (_input.DebugPressed(DebugKey.BlazingDash)) ApplyCard(DashCardId.BlazingDash);
        if (_input.DebugPressed(DebugKey.EtherealStep)) ApplyCard(DashCardId.EtherealStep);
    }

    /// <summary>Apply a dash card to the local player (debug hotkeys now, the level-up screen later).</summary>
    public void ApplyCard(DashCardId id)
    {
        var def = _cards.FirstOrDefault(c => c.Id == id);
        if (def == null) return;
        if (id != DashCardId.Dash && !_progress.Unlocked) _progress.Apply(DashCardId.Dash);
        if (!DashCards.IsAvailable(def, _progress))
        {
            ModLog.Info($"[cards] {def.Title} is at its limit ({_progress.Stacks(id)}/{def.MaxStacks}).");
            return;
        }

        _progress.Apply(id);
        SyncCharges();
        ModLog.Info($"[cards] {def.Title} ({def.Rarity}) applied. Run upgrades: {_progress}. " +
                    $"Charges {_charges.Max}, recharge {_charges.RechargeSeconds:0.00}s, distance {DashTuning.Distance(_stats, _progress):0.00}.");
    }

    public void AppendStatus(StringBuilder sb)
    {
        sb.AppendLine($"Input backend: {_input.BackendName}");
        sb.AppendLine($"Run upgrades: {_progress}   unlocked: {_progress.Unlocked}");
        sb.AppendLine($"Charges: {_charges.Full}/{_charges.Max}  progress {_charges.Progress:0.00}  recharge {_charges.RechargeSeconds:0.00}s");
        sb.AppendLine($"Dash: distance {DashTuning.Distance(_stats, _progress):0.00}  duration {DashTuning.Duration(_stats):0.000}s  i-frames {DashTuning.InvulnerableSeconds(_stats):0.000}s");
        sb.AppendLine($"Dashes this session: yours {_localDashes}, other players' detected {_remoteDashes}");
        sb.AppendLine($"Run: {GameRun.Status}");
        sb.AppendLine($"Card bridge: {CardBridge.Status}");
        sb.AppendLine($"Stats panel rows: {StatPanelRows.Status}");
        foreach (var line in CardBridge.Log) sb.AppendLine("  " + line.Replace("\n", "\n  "));
        sb.AppendLine("Card catalogue:");
        foreach (var c in _cards)
            sb.AppendLine($"  {c.Title,-16} {c.Rarity,-10} max {c.MaxStacks}  taken {_progress.Stacks(c.Id)}  offerable now: {DashCards.IsAvailable(c, _progress)}");
        sb.AppendLine($"Dash sound: {Sound.Status}");
        sb.AppendLine($"Game effects: {GameFx.Status}");
        sb.AppendLine($"Blazing Dash trail: {_gameFire.Status}");
        sb.AppendLine($"Blazing Dash damage: {_blaze.Status}");
        sb.AppendLine($"Wall checks: {DashMotor.Describe()}");
        sb.AppendLine($"Dash HP cost: {DashTuning.HealthCostFraction(_stats, _progress) * 100f:0.#}% of max HP; health: {HealthBridge.Status}");
    }

    // =========================================================================== types

    private sealed class ActiveDash
    {
        public Vector3 Dir;
        public float Remaining;
        public float Speed;
        public float Elapsed;
        public float MaxTime;
        public float NextGhost;
        public float Travelled;
        public float NextFlameAt;
        public float FlameSpacing;
        public float NextSparkleAt;
        public float SparkleSpacing;
        public float FlameRadius;
        public float NextStreak;
    }

    private sealed class RemoteEmitter
    {
        public RemoteEmitter(PlayerInfo player, float until)
        {
            Player = player;
            Until = until;
        }

        public PlayerInfo Player { get; }
        public float Until { get; }
        public float Next { get; set; }
    }
}
