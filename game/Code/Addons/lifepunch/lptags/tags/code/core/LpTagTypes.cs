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

public enum LpTagSurface : byte
{
    Chat = 1,
    Scoreboard = 2,
    Nameplate = 3
}

public enum LpTagEffectId : byte
{
    None = 0,
    SolidRed = 1,
    SolidGreen = 2,
    SolidYellow = 3,
    SolidBlue = 4,
    SolidCyan = 5,
    ColorWipe = 6,
    RainbowWave = 7,
    RedScanner = 8,
    Slide = 9,
    BouncingDot = 10,
    BouncingPlus = 11
}

public enum LpTagPaletteRole : byte
{
    Neutral = 0,
    Red = 1,
    Green = 2,
    Yellow = 3,
    Blue = 4,
    Cyan = 5
}

[Flags]
public enum LpTagSurfaceMask : byte
{
    None = 0,
    Chat = 1,
    Scoreboard = 2,
    Nameplate = 4,
    All = Chat | Scoreboard | Nameplate
}

public enum LpTagMotionClass : byte
{
    Static,
    SteppedColor,
    Scanner,
    Translation,
    Decoration
}

public enum LpTagTimingKind : byte
{
    Static,
    FixedStep,
    GraphemeScaledStep,
    DiscreteCurve
}

public enum LpTagReducedMotionFrame : byte
{
    PlainIdentity,
    StaticEffect,
    ColorWipe,
    RainbowWave,
    RedScanner,
    Slide,
    BouncingDot,
    BouncingPlus
}

public readonly record struct LpTagSurfacePolicy(
    LpTagSurface Surface,
    int SlideAmplitudePixels,
    bool AllowDecorations,
    bool AllowPerGrapheme );

public readonly record struct LpTagEffectDefinition(
    LpTagEffectId Id,
    string Key,
    string DisplayLabel,
    LpTagMotionClass MotionClass,
    LpTagTimingKind TimingKind,
    double FixedPeriodMilliseconds,
    double FixedStepMilliseconds,
    LpTagSurfaceMask SupportedSurfaces,
    bool RequiresGraphemeRuns,
    bool RequiresDecorations,
    bool RequiresTransform,
    LpTagReducedMotionFrame ReducedMotionFrame )
{
    public bool Supports( LpTagSurface surface )
    {
        return ( SupportedSurfaces & LpTagEffectCatalog.ToMask( surface ) ) != 0;
    }

    public double ResolvePeriodMilliseconds( int graphemeCount )
    {
        if ( TimingKind == LpTagTimingKind.Static || graphemeCount <= 0 )
            return 0d;

        return TimingKind == LpTagTimingKind.GraphemeScaledStep
            ? FixedPeriodMilliseconds * graphemeCount
            : FixedPeriodMilliseconds;
    }
}
