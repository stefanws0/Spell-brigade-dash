namespace SpellBrigadeDash;

using System;
using UnityEngine;

/// <summary>
/// The only MonoBehaviour this mod injects into the IL2CPP runtime. It just forwards Unity's frame
/// callbacks to <see cref="DashSystem"/>, which is plain C#.
///
/// IL2CPP note: injected MonoBehaviours MUST have this (IntPtr) constructor. Unity creates the native
/// object and the interop layer wraps it with this constructor.
/// </summary>
public class DashRunner : MonoBehaviour
{
    public DashRunner(IntPtr ptr) : base(ptr)
    {
    }

    public void Update() => DashSystem.Instance.Update();

    public void LateUpdate() => DashSystem.Instance.LateUpdate();

    /// <summary>Marks a normal quit in SpellBrigadeDash.log, so the next start can tell a crash from a clean exit.</summary>
    public void OnApplicationQuit() => ModLog.Step(ModLog.CleanExit);
}
