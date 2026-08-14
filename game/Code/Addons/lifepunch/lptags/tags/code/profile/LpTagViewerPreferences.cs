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

public readonly record struct LpViewerTagPreferencesV1(
    bool ReduceMotion,
    bool DisableAnimations,
    bool HideDotPlusDecorations );

public interface ILpTagViewerPreferenceSource
{
    LpViewerTagPreferencesV1 Current { get; }
    event Action<LpViewerTagPreferencesV1>? Changed;
}

public interface ILpTagViewerPreferenceEditor : ILpTagViewerPreferenceSource
{
    void SetReduceMotion( bool value );
    void SetDisableAnimations( bool value );
    void SetHideDotPlusDecorations( bool value );
}

internal enum LpTagEffectiveTimingMode : byte
{
    Plain,
    StaticEffect,
    StaticRepresentative,
    Animated
}

internal static partial class LpTagSchedulingRules
{
    public static LpTagEffectiveTimingMode ResolveTimingMode(
        LpTagEffectId effectId,
        int graphemeCount,
        bool requiresWholeNameFallback,
        LpTagSurfacePolicy policy,
        LpViewerTagPreferencesV1 viewerPreferences )
    {
        if ( graphemeCount <= 0
            || !LpTagEffectCatalog.TryGet( effectId, out var definition )
            || !definition.Supports( policy.Surface )
            || definition.Id == LpTagEffectId.None )
        {
            return LpTagEffectiveTimingMode.Plain;
        }

        if ( definition.TimingKind == LpTagTimingKind.Static )
            return LpTagEffectiveTimingMode.StaticEffect;
        if ( viewerPreferences.DisableAnimations )
            return LpTagEffectiveTimingMode.Plain;
        if ( definition.RequiresGraphemeRuns
            && (requiresWholeNameFallback || !policy.AllowPerGrapheme) )
        {
            return LpTagEffectiveTimingMode.Plain;
        }
        if ( definition.RequiresDecorations
            && (viewerPreferences.HideDotPlusDecorations || !policy.AllowDecorations) )
        {
            return LpTagEffectiveTimingMode.Plain;
        }
        if ( viewerPreferences.ReduceMotion )
            return LpTagEffectiveTimingMode.StaticRepresentative;

        return LpTagEffectiveTimingMode.Animated;
    }

    public static bool ShouldSchedule(
        bool visible,
        bool disposed,
        LpTagEffectiveTimingMode timingMode )
    {
        return visible && !disposed && timingMode == LpTagEffectiveTimingMode.Animated;
    }
}
