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
using System.Collections.Generic;

namespace LifePunch.DXRP.Addons.Tags;

public static class LpTagEffectCatalog
{
    public const int MaximumKeyLength = 32;
    public const double CurveRadiansPerMillisecond = 0.004d;

    private static readonly LpTagEffectDefinition[] Definitions =
    {
        new( LpTagEffectId.None, "none", "Off", LpTagMotionClass.Static, LpTagTimingKind.Static, 0d, 0d, LpTagSurfaceMask.All, false, false, false, LpTagReducedMotionFrame.PlainIdentity ),
        new( LpTagEffectId.SolidRed, "solid-red-v1", "Solid Red", LpTagMotionClass.Static, LpTagTimingKind.Static, 0d, 0d, LpTagSurfaceMask.All, false, false, false, LpTagReducedMotionFrame.StaticEffect ),
        new( LpTagEffectId.SolidGreen, "solid-green-v1", "Solid Green", LpTagMotionClass.Static, LpTagTimingKind.Static, 0d, 0d, LpTagSurfaceMask.All, false, false, false, LpTagReducedMotionFrame.StaticEffect ),
        new( LpTagEffectId.SolidYellow, "solid-yellow-v1", "Solid Yellow", LpTagMotionClass.Static, LpTagTimingKind.Static, 0d, 0d, LpTagSurfaceMask.All, false, false, false, LpTagReducedMotionFrame.StaticEffect ),
        new( LpTagEffectId.SolidBlue, "solid-blue-v1", "Solid Blue", LpTagMotionClass.Static, LpTagTimingKind.Static, 0d, 0d, LpTagSurfaceMask.All, false, false, false, LpTagReducedMotionFrame.StaticEffect ),
        new( LpTagEffectId.SolidCyan, "solid-cyan-v1", "Solid Cyan", LpTagMotionClass.Static, LpTagTimingKind.Static, 0d, 0d, LpTagSurfaceMask.All, false, false, false, LpTagReducedMotionFrame.StaticEffect ),
        new( LpTagEffectId.ColorWipe, "color-wipe-v1", "Color Wipe", LpTagMotionClass.SteppedColor, LpTagTimingKind.GraphemeScaledStep, 500d, 100d, LpTagSurfaceMask.All, true, false, false, LpTagReducedMotionFrame.ColorWipe ),
        new( LpTagEffectId.RainbowWave, "rainbow-wave-v1", "Rainbow Wave", LpTagMotionClass.SteppedColor, LpTagTimingKind.FixedStep, 500d, 100d, LpTagSurfaceMask.All, true, false, false, LpTagReducedMotionFrame.RainbowWave ),
        new( LpTagEffectId.RedScanner, "red-scanner-v1", "Red Scanner", LpTagMotionClass.Scanner, LpTagTimingKind.DiscreteCurve, 2d * Math.PI / CurveRadiansPerMillisecond, 0d, LpTagSurfaceMask.All, true, false, false, LpTagReducedMotionFrame.RedScanner ),
        new( LpTagEffectId.Slide, "slide-v1", "Slide", LpTagMotionClass.Translation, LpTagTimingKind.DiscreteCurve, Math.PI / CurveRadiansPerMillisecond, 0d, LpTagSurfaceMask.All, false, false, true, LpTagReducedMotionFrame.Slide ),
        new( LpTagEffectId.BouncingDot, "bouncing-dot-v1", "Bouncing Dot", LpTagMotionClass.Decoration, LpTagTimingKind.FixedStep, 600d, 75d, LpTagSurfaceMask.All, false, true, false, LpTagReducedMotionFrame.BouncingDot ),
        new( LpTagEffectId.BouncingPlus, "bouncing-plus-v1", "Bouncing Plus", LpTagMotionClass.Decoration, LpTagTimingKind.FixedStep, 600d, 75d, LpTagSurfaceMask.All, false, true, false, LpTagReducedMotionFrame.BouncingPlus )
    };

    private static readonly Dictionary<LpTagEffectId, LpTagEffectDefinition> DefinitionsById;
    private static readonly Dictionary<string, LpTagEffectDefinition> DefinitionsByKey;

    public static IReadOnlyList<LpTagEffectDefinition> All => Definitions;

    static LpTagEffectCatalog()
    {
        ValidateDefinitions( Definitions );

        DefinitionsById = new Dictionary<LpTagEffectId, LpTagEffectDefinition>( Definitions.Length );
        DefinitionsByKey = new Dictionary<string, LpTagEffectDefinition>( Definitions.Length, StringComparer.Ordinal );
        foreach ( var definition in Definitions )
        {
            DefinitionsById.Add( definition.Id, definition );
            DefinitionsByKey.Add( definition.Key, definition );
        }
    }

    public static bool TryGet( LpTagEffectId effectId, out LpTagEffectDefinition definition )
    {
        return DefinitionsById.TryGetValue( effectId, out definition );
    }

    public static bool TryGet( string key, out LpTagEffectDefinition definition )
    {
        if ( string.IsNullOrEmpty( key ) || key.Length > MaximumKeyLength || !IsAscii( key ) )
        {
            definition = default;
            return false;
        }

        return DefinitionsByKey.TryGetValue( key, out definition );
    }

    public static LpTagEffectDefinition GetRequired( LpTagEffectId effectId )
    {
        if ( TryGet( effectId, out var definition ) )
            return definition;

        throw new ArgumentOutOfRangeException( nameof( effectId ), effectId, "Unknown LifePunch tag effect ID." );
    }

    public static LpTagSurfacePolicy GetPolicy( LpTagSurface surface )
    {
        return surface switch
        {
            LpTagSurface.Chat => new LpTagSurfacePolicy( LpTagSurface.Chat, 4, true, true ),
            LpTagSurface.Scoreboard => new LpTagSurfacePolicy( LpTagSurface.Scoreboard, 8, true, true ),
            LpTagSurface.Nameplate => new LpTagSurfacePolicy( LpTagSurface.Nameplate, 5, true, true ),
            _ => throw new ArgumentOutOfRangeException( nameof( surface ), surface, "Unknown LifePunch tag surface." )
        };
    }

    public static string PaletteHex( LpTagPaletteRole role )
    {
        return role switch
        {
            LpTagPaletteRole.Neutral => "#ffffff",
            LpTagPaletteRole.Red => "#ff3333",
            LpTagPaletteRole.Green => "#00ff00",
            LpTagPaletteRole.Yellow => "#ffff00",
            LpTagPaletteRole.Blue => "#0000ff",
            LpTagPaletteRole.Cyan => "#00ffff",
            _ => throw new ArgumentOutOfRangeException( nameof( role ), role, "Unknown LifePunch tag palette role." )
        };
    }

    public static double ResolveNextChangeDelayMilliseconds(
        LpTagEffectId effectId,
        int graphemeCount,
        int slideAmplitudePixels,
        double normalizedPhaseMilliseconds )
    {
        var definition = GetRequired( effectId );
        var period = definition.ResolvePeriodMilliseconds( graphemeCount );
        if ( period <= 0d || double.IsNaN( period ) || double.IsInfinity( period ) )
            return double.PositiveInfinity;

        var phase = PositiveModulo( normalizedPhaseMilliseconds, period );
        var delay = definition.TimingKind switch
        {
            LpTagTimingKind.FixedStep or LpTagTimingKind.GraphemeScaledStep => ResolveFixedStepDelayMilliseconds( definition.FixedStepMilliseconds, phase ),
            LpTagTimingKind.DiscreteCurve => ResolveDiscreteCurveDelayMilliseconds( effectId, graphemeCount, slideAmplitudePixels, phase, period ),
            _ => double.PositiveInfinity
        };

        return ClampPositiveDelayMilliseconds( delay, period );
    }

    public static LpTagSurfaceMask ToMask( LpTagSurface surface )
    {
        return surface switch
        {
            LpTagSurface.Chat => LpTagSurfaceMask.Chat,
            LpTagSurface.Scoreboard => LpTagSurfaceMask.Scoreboard,
            LpTagSurface.Nameplate => LpTagSurfaceMask.Nameplate,
            _ => throw new ArgumentOutOfRangeException( nameof( surface ), surface, "Unknown LifePunch tag surface." )
        };
    }

    private static void ValidateDefinitions( IReadOnlyList<LpTagEffectDefinition> definitions )
    {
        if ( definitions.Count != 12 )
            throw new InvalidOperationException( "LifePunch tag catalog must contain wire IDs 0 through 11." );

        var ids = new HashSet<byte>();
        var keys = new HashSet<string>( StringComparer.Ordinal );
        foreach ( var definition in definitions )
        {
            if ( !ids.Add( (byte)definition.Id ) )
                throw new InvalidOperationException( "LifePunch tag catalog contains a duplicate wire ID." );
            if ( string.IsNullOrEmpty( definition.Key ) || definition.Key.Length > MaximumKeyLength || !IsAscii( definition.Key ) )
                throw new InvalidOperationException( "LifePunch tag catalog contains an invalid stable key." );
            if ( !keys.Add( definition.Key ) )
                throw new InvalidOperationException( "LifePunch tag catalog contains a duplicate stable key." );
            if ( definition.SupportedSurfaces != LpTagSurfaceMask.All )
                throw new InvalidOperationException( "LifePunch tag catalog contains an unsupported surface combination." );
        }

        for ( byte wireId = 0; wireId <= 11; wireId++ )
        {
            if ( !ids.Contains( wireId ) )
                throw new InvalidOperationException( "LifePunch tag catalog wire IDs must be contiguous from 0 through 11." );
        }

        if ( !ids.Contains( (byte)LpTagEffectId.None ) )
            throw new InvalidOperationException( "LifePunch tag catalog must include wire ID 0." );
    }

    private static bool IsAscii( string value )
    {
        for ( var index = 0; index < value.Length; index++ )
        {
            if ( value[index] > 0x7f )
                return false;
        }

        return true;
    }

    private static double ResolveFixedStepDelayMilliseconds( double stepMilliseconds, double phaseMilliseconds )
    {
        return stepMilliseconds - PositiveModulo( phaseMilliseconds, stepMilliseconds );
    }

    private static double ResolveDiscreteCurveDelayMilliseconds(
        LpTagEffectId effectId,
        int graphemeCount,
        int slideAmplitudePixels,
        double phaseMilliseconds,
        double periodMilliseconds )
    {
        return effectId switch
        {
            LpTagEffectId.RedScanner => ResolveScannerBoundaryDelayMilliseconds( graphemeCount, phaseMilliseconds, periodMilliseconds ),
            LpTagEffectId.Slide => ResolveSlideBoundaryDelayMilliseconds( slideAmplitudePixels, phaseMilliseconds, periodMilliseconds ),
            _ => periodMilliseconds
        };
    }

    private static double ResolveScannerBoundaryDelayMilliseconds( int graphemeCount, double phaseMilliseconds, double periodMilliseconds )
    {
        if ( graphemeCount <= 0 )
            return double.PositiveInfinity;

        var radians = PositiveModulo( phaseMilliseconds * CurveRadiansPerMillisecond, 2d * Math.PI );
        var position = ScannerFrameKey( graphemeCount, radians );
        double boundaryRadians;
        if ( radians < Math.PI && position == 0 )
        {
            boundaryRadians = 2d * Math.PI - Math.Acos( ( 2d / graphemeCount ) - 1d );
        }
        else if ( radians <= Math.PI )
        {
            boundaryRadians = Math.Acos( ( 2d * position / graphemeCount ) - 1d );
        }
        else if ( position >= graphemeCount )
        {
            return 1d;
        }
        else
        {
            boundaryRadians = 2d * Math.PI - Math.Acos( ( 2d * ( position + 1 ) / graphemeCount ) - 1d );
        }

        return ResolveValidatedBoundaryDelayMilliseconds( LpTagEffectId.RedScanner, graphemeCount, 0, boundaryRadians / CurveRadiansPerMillisecond, phaseMilliseconds, periodMilliseconds );
    }

    private static double ResolveSlideBoundaryDelayMilliseconds( int amplitudePixels, double phaseMilliseconds, double periodMilliseconds )
    {
        if ( amplitudePixels <= 0 )
            return periodMilliseconds;

        var radians = PositiveModulo( phaseMilliseconds * CurveRadiansPerMillisecond, Math.PI );
        var translation = SlideFrameKey( amplitudePixels, radians );
        double threshold;
        double boundaryRadians;
        if ( radians < Math.PI / 2d && translation <= -amplitudePixels )
        {
            threshold = translation + 0.5d;
            boundaryRadians = Math.PI - Math.Asin( ( 1d - ( threshold / amplitudePixels ) ) / 2d );
        }
        else if ( radians <= Math.PI / 2d )
        {
            threshold = translation - 0.5d;
            if ( threshold < -amplitudePixels )
                return 1d;

            boundaryRadians = Math.Asin( ( 1d - ( threshold / amplitudePixels ) ) / 2d );
        }
        else if ( translation >= amplitudePixels )
        {
            threshold = translation - 0.5d;
            boundaryRadians = Math.Asin( ( 1d - ( threshold / amplitudePixels ) ) / 2d );
        }
        else
        {
            threshold = translation + 0.5d;
            if ( threshold > amplitudePixels )
                return 1d;

            boundaryRadians = Math.PI - Math.Asin( ( 1d - ( threshold / amplitudePixels ) ) / 2d );
        }

        return ResolveValidatedBoundaryDelayMilliseconds( LpTagEffectId.Slide, 0, amplitudePixels, boundaryRadians / CurveRadiansPerMillisecond, phaseMilliseconds, periodMilliseconds );
    }

    private static double ResolveValidatedBoundaryDelayMilliseconds(
        LpTagEffectId effectId,
        int graphemeCount,
        int slideAmplitudePixels,
        double boundaryPhaseMilliseconds,
        double phaseMilliseconds,
        double periodMilliseconds )
    {
        if ( Math.Abs( boundaryPhaseMilliseconds - phaseMilliseconds ) < 0.000000001d )
            return 1d;

        var beforeKey = DiscreteFrameKey( effectId, graphemeCount, slideAmplitudePixels, Math.BitDecrement( boundaryPhaseMilliseconds ) );
        while ( DiscreteFrameKey( effectId, graphemeCount, slideAmplitudePixels, boundaryPhaseMilliseconds ) == beforeKey )
            boundaryPhaseMilliseconds = Math.BitIncrement( boundaryPhaseMilliseconds );

        var delay = boundaryPhaseMilliseconds - phaseMilliseconds;
        if ( delay > 0d )
            return delay;

        return boundaryPhaseMilliseconds + periodMilliseconds - phaseMilliseconds;
    }

    private static int DiscreteFrameKey( LpTagEffectId effectId, int graphemeCount, int slideAmplitudePixels, double phaseMilliseconds )
    {
        return effectId switch
        {
            LpTagEffectId.RedScanner => ScannerFrameKey( graphemeCount, PositiveModulo( phaseMilliseconds * CurveRadiansPerMillisecond, 2d * Math.PI ) ),
            LpTagEffectId.Slide => SlideFrameKey( slideAmplitudePixels, PositiveModulo( phaseMilliseconds * CurveRadiansPerMillisecond, Math.PI ) ),
            _ => 0
        };
    }

    private static int ScannerFrameKey( int graphemeCount, double radians )
    {
        return (int)Math.Floor( ( Math.Cos( radians ) + 1d ) * 0.5d * graphemeCount );
    }

    private static int SlideFrameKey( int amplitudePixels, double radians )
    {
        var value = ( 2d * ( 1d - Math.Abs( Math.Sin( radians ) ) ) - 1d ) * amplitudePixels;
        return (int)Math.Floor( value + 0.5d );
    }

    private static double ClampPositiveDelayMilliseconds( double delayMilliseconds, double periodMilliseconds )
    {
        if ( double.IsPositiveInfinity( delayMilliseconds ) )
            return delayMilliseconds;
        if ( double.IsNaN( delayMilliseconds ) || delayMilliseconds <= 0d )
            return 1d;

        return Math.Min( Math.Max( 1d, delayMilliseconds ), periodMilliseconds );
    }

    private static double PositiveModulo( double value, double modulus )
    {
        var remainder = value % modulus;
        return remainder < 0d ? remainder + modulus : remainder;
    }
}
