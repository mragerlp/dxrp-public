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

/// <summary>
/// Authoritative, immutable inputs captured when one received Chat row is created.
/// </summary>
public readonly record struct LpTagChatRowContext(
    ulong LifetimeToken,
    ulong SteamId,
    string FilteredDisplayName,
    LpTagEffectId SnapshotEffectId,
    bool IsSystemMessage );

/// <summary>Owns the cosmetic presenter attachment for one Chat-row lifetime.</summary>
/// <remarks>
/// The binding retains the context identity and effect snapshot for its complete
/// lifetime. There is intentionally no effect-update method: an old row cannot
/// be retroactively restyled. When <see cref="LpTagChatRowContext.IsSystemMessage"/>
/// is true, the effective retained effect is always <see cref="LpTagEffectId.None"/>.
/// </remarks>
public interface ILpTagChatBinding : IDisposable
{
    /// <summary>
    /// Applies authoritative row visibility. False must hide the cosmetic and
    /// unregister its animation without discarding the retained identity/effect.
    /// </summary>
    void SetVisible( bool visible );
}

/// <summary>Creates a cosmetic-only binding at the authoritative Chat receive seam.</summary>
/// <remarks>
/// Bind configures the production presenter from the retained identity/effect and
/// <see cref="ILpTagViewerPreferenceSource.Current"/>, subscribes to
/// <see cref="ILpTagViewerPreferenceSource.Changed"/>, and unsubscribes when the
/// returned binding is disposed. A viewer-preference change reconfigures with the
/// same retained identity/effect and preserves the last authoritative visibility;
/// it never calls <see cref="ILpTagChatBinding.SetVisible"/> or fabricates row state.
/// Disposal is idempotent and must, even while visible, unsubscribe from the
/// viewer source, unregister scheduler/presenter animation, and dispose or
/// release the binding-owned presenter and UI attachment.
/// Repeated disposal must retain no callbacks and must not unregister, release, or dispose binding-owned resources more than once.
///
/// The receive pipeline owns <see cref="LpTagChatRowContext.LifetimeToken"/> and
/// supplies the effect snapshot exactly once. The binding may replace only the
/// visible sender name through <see cref="LpTagNamePresenter"/>. Message content,
/// copying, commands, recipient filtering, blocked-user behavior, channels,
/// gag/mute, moderation, logs, timestamps, and row lifecycle remain untouched.
/// </remarks>
public interface ILpTagChatAdapter
{
    ILpTagChatBinding Bind(
        in LpTagChatRowContext context,
        ILpTagViewerPreferenceSource viewerPreferenceSource );
}
