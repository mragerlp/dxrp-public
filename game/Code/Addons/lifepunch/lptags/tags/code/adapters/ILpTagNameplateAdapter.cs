// â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
// PROPRIETARY & CONFIDENTIAL â€” Â© 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCHâ„¢ Tags for DXRP" (s&box ident: lifepunch.tags Â· addon ident: lptags) is the sole-owned
// intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
// sublicensing, copying, or reuse by ANY person or entity â€” including DXRP and
// LifePunch staff, contributors, or community â€” EXCEPT the owner (lifepunch.co).
// Third-party material identified by an accompanying notice remains under its stated license.
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
//
// Author account: mrragerlp Â· Public alias (in-game Â· Steam Â· Discord): Bloodwave
// â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
using System;

namespace LifePunch.DXRP.Addons.Tags;

/// <summary>Authoritative host visibility for a player nameplate.</summary>
public enum LpTagHostVisibility : byte
{
    Unknown,
    Hidden,
    Visible
}

/// <summary>Authoritative identity/effect and host/geometric visibility inputs.</summary>
public readonly record struct LpTagNameplateContext(
    ulong LifetimeToken,
    ulong SteamId,
    string FilteredDisplayName,
    LpTagEffectId CurrentEffectId,
    LpTagHostVisibility HostVisibility,
    bool GeometricVisible );

/// <summary>Owns the cosmetic presenter attachment for one nameplate lifetime.</summary>
public interface ILpTagNameplateBinding : IDisposable
{
    /// <summary>Applies an authoritative player-identity update.</summary>
    void UpdateIdentity( ulong steamId, string filteredDisplayName );

    /// <summary>Applies the current effective public effect.</summary>
    void UpdateEffect( LpTagEffectId currentEffectId );

    /// <summary>Applies authoritative host state and current geometric visibility.</summary>
    void SetVisibility( LpTagHostVisibility hostVisibility, bool geometricVisible );
}

/// <summary>Creates a cosmetic-only binding at an authoritative nameplate seam.</summary>
/// <remarks>
/// Binding is permitted only after the host supplies authoritative identity and
/// lifetime. The adapter performs no player, world, entity, prefab, or roster
/// lookup and uses no identity heuristic.
///
/// A cosmetic is visible if and only if host visibility is explicitly
/// <see cref="LpTagHostVisibility.Visible"/> and geometric visibility is true.
/// <see cref="LpTagHostVisibility.Unknown"/> and
/// <see cref="LpTagHostVisibility.Hidden"/> always hide and unregister it,
/// preserving cloak, incognito, fake-disconnect, and equivalent precedence.
///
/// Bind retains the latest identity/effect, configures from
/// <see cref="ILpTagViewerPreferenceSource.Current"/>, subscribes to
/// <see cref="ILpTagViewerPreferenceSource.Changed"/>, and unsubscribes on
/// disposal. A viewer change reconfigures with retained values but never calls
/// <see cref="ILpTagNameplateBinding.SetVisibility"/>, fabricates host visibility,
/// or replaces the last authoritative geometric state.
/// Disposal is idempotent and must, even while visible, unsubscribe from the
/// viewer source, unregister scheduler/presenter animation, and dispose or
/// release the binding-owned presenter and UI attachment.
/// Repeated disposal must retain no callbacks and must not unregister, release, or dispose binding-owned resources more than once.
/// </remarks>
public interface ILpTagNameplateAdapter
{
    ILpTagNameplateBinding Bind(
        in LpTagNameplateContext context,
        ILpTagViewerPreferenceSource viewerPreferenceSource );
}
