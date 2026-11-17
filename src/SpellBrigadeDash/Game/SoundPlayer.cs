namespace SpellBrigadeDash.Game;

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Reuses one of the game's own sounds for the dash. It searches the AudioClips the game has loaded
/// for a whoosh-like name (Sound.ClipNameHints) and plays it through the game's SFX mixer group if
/// one exists, so the in-game SFX volume slider applies.
/// </summary>
internal sealed class SoundPlayer
{
    private AudioSource? _source;
    private AudioClip? _clip;
    private AudioMixerGroup? _mixer;
    private float _nextSearch;

    private string _status = "not searched yet";

    public string Status => FmodSound.Available ? "FMOD: " + FmodSound.Status : _status;

    public void Play(GameObject host, Vector3 position, float now)
    {
        if (!ModConfig.SoundEnabled.Value) return;

        // The Spell Brigade uses FMOD; Unity AudioSources are the fallback for other setups.
        if (FmodSound.Available)
        {
            FmodSound.Play(position);
            return;
        }

        try
        {
            if (_clip == null && now >= _nextSearch)
            {
                _nextSearch = now + 30f; // clips load with levels, so retry occasionally
                Search();
            }

            if (_clip == null) return;
            if (_source == null)
            {
                _source = host.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.spatialBlend = 0f;
                if (_mixer != null) _source.outputAudioMixerGroup = _mixer;
            }

            _source.PlayOneShot(_clip, ModConfig.SoundVolume.Value);
        }
        catch (Exception ex)
        {
            ModLog.ErrorThrottled("sound", ex, now, 60f);
        }
    }

    public static List<string> AllClipNames()
    {
        try
        {
            return Resources.FindObjectsOfTypeAll<AudioClip>().Where(c => c != null).Select(c => $"{c.name} ({c.length:0.00}s)").Distinct().OrderBy(n => n).ToList();
        }
        catch (Exception ex)
        {
            return new List<string> { $"(failed: {ex.Message})" };
        }
    }

    public static List<string> AllMixerGroups()
    {
        try
        {
            return Resources.FindObjectsOfTypeAll<AudioMixerGroup>().Where(g => g != null).Select(g => g.name).Distinct().ToList();
        }
        catch (Exception ex)
        {
            return new List<string> { $"(failed: {ex.Message})" };
        }
    }

    private void Search()
    {
        ModLog.Step("sound: searching AudioClips");
        var clips = Resources.FindObjectsOfTypeAll<AudioClip>().Where(c => c != null).ToList();
        if (clips.Count == 0)
        {
            _status = "no AudioClips loaded (the game may use FMOD/Wwise; see discovery report)";
            return;
        }

        var exact = ModConfig.SoundClipName.Value.Trim();
        if (exact.Length > 0)
        {
            _clip = clips.FirstOrDefault(c => string.Equals(c.name, exact, StringComparison.OrdinalIgnoreCase));
            if (_clip == null) ModLog.WarnOnce("clip-exact", $"Sound.ClipName '{exact}' not found among {clips.Count} loaded clips.");
        }

        if (_clip == null)
        {
            foreach (var hint in ModConfig.SplitList(ModConfig.SoundClipHints.Value.ToLowerInvariant()))
            {
                _clip = clips.Where(c => c.name.ToLowerInvariant().Contains(hint) && c.length > 0.05f && c.length < 1.5f)
                    .OrderBy(c => c.length)
                    .FirstOrDefault();
                if (_clip != null) break;
            }
        }

        try
        {
            _mixer = Resources.FindObjectsOfTypeAll<AudioMixerGroup>()
                .FirstOrDefault(g => g != null && (g.name.ToLowerInvariant().Contains("sfx") || g.name.ToLowerInvariant().Contains("effect") || g.name.ToLowerInvariant().Contains("sound")));
        }
        catch
        {
            _mixer = null;
        }

        _status = _clip != null
            ? $"using '{_clip.name}' ({_clip.length:0.00}s) via mixer '{_mixer?.name ?? "none"}'"
            : $"no matching clip among {clips.Count} loaded clips";
        ModLog.Info($"Dash sound: {_status}");
    }
}
