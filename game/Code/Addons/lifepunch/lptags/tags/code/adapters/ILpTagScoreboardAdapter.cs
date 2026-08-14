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

/// <summary>Authoritative inputs for one current Scoreboard-row lifetime.</summary>
public readonly record struct LpTagScoreboardRowContext(
    ulong LifetimeToken,
    ulong SteamId,
    string FilteredDisplayName,
    LpTagEffectId CurrentEffectId );

/// <summary>Owns the cosmetic presenter attachment for one Scoreboard row.</summary>
public interface ILpTagScoreboardBinding : IDisposable
{
    /// <summary>Applies an authoritative roster identity update.</summary>
    void UpdateIdentity( ulong steamId, string filteredDisplayName );

    /// <summary>Applies the current effective public effect from the roster owner.</summary>
    void UpdateEffect( LpTagEffectId currentEffectId );

    /// <summary>Applies authoritative board/row visibility.</summary>
    void SetVisible( bool visible );
}

/// <summary>Creates a cosmetic-only binding at the authoritative roster seam.</summary>
/// <remarks>
/// The roster owner supplies the lifetime token and every identity, effect, and
/// visibility update. Bind retains the latest identity/effect, configures the
/// production presenter from <see cref="ILpTagViewerPreferenceSource.Current"/>,
/// and subscribes to <see cref="ILpTagViewerPreferenceSource.Changed"/>. Each
/// viewer change reconfigures with the retained values and preserves the last
/// <see cref="ILpTagScoreboardBinding.SetVisible"/> state; it never fabricates a
/// visibility change. Disposal must unsubscribe and unregister the presenter,
/// and the authoritative owner disposes on row removal or board close.
///
/// Rank, role, staff, status, voice, ping, roster identity, and row lifetime
/// remain outside the Tags presenter and adapter.
/// </remarks>
public interface ILpTagScoreboardAdapter
{
    ILpTagScoreboardBinding Bind(
        in LpTagScoreboardRowContext context,
        ILpTagViewerPreferenceSource viewerPreferenceSource );
}
