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

public static class LpTagEffectEvaluator
{
    private static readonly string[] DotFrames =
    {
        "o...", ".o..", "..o.", "...o"
    };

    private static readonly string[] PlusFrames =
    {
        "+---", "-+--", "--+-", "---+"
    };

    public static void Evaluate(
        LpTagTextAnalysis text,
        LpTagEffectId effectId,
        LpTagSurfacePolicy policy,
        double phaseMilliseconds,
        LpViewerTagPreferencesV1 viewerPreferences,
        LpTagEffectFrame frame )
    {
        if ( text is null )
            throw new ArgumentNullException( nameof( text ) );
        if ( frame is null )
            throw new ArgumentNullException( nameof( frame ) );

        var definition = LpTagEffectCatalog.GetRequired( effectId );
        frame.Reset( text.GraphemeCount );
        frame.RequiresWholeNameFallback = text.RequiresWholeNameFallback
            || ( definition.RequiresGraphemeRuns && !policy.AllowPerGrapheme );

        if ( text.GraphemeCount == 0 || !definition.Supports( policy.Surface ) )
            return;

        var isAnimated = definition.TimingKind != LpTagTimingKind.Static;
        if ( viewerPreferences.DisableAnimations && isAnimated )
            return;

        if ( definition.RequiresGraphemeRuns && frame.RequiresWholeNameFallback )
            return;

        if ( definition.RequiresDecorations
            && ( viewerPreferences.HideDotPlusDecorations || !policy.AllowDecorations ) )
        {
            return;
        }

        if ( viewerPreferences.ReduceMotion && isAnimated )
        {
            EvaluateReducedMotion( definition.ReducedMotionFrame, frame );
            return;
        }

        switch ( effectId )
        {
            case LpTagEffectId.None:
                return;
            case LpTagEffectId.SolidRed:
                EvaluateSolid( LpTagPaletteRole.Red, effectId, frame );
                return;
            case LpTagEffectId.SolidGreen:
                EvaluateSolid( LpTagPaletteRole.Green, effectId, frame );
                return;
            case LpTagEffectId.SolidYellow:
                EvaluateSolid( LpTagPaletteRole.Yellow, effectId, frame );
                return;
            case LpTagEffectId.SolidBlue:
                EvaluateSolid( LpTagPaletteRole.Blue, effectId, frame );
                return;
            case LpTagEffectId.SolidCyan:
                EvaluateSolid( LpTagPaletteRole.Cyan, effectId, frame );
                return;
            case LpTagEffectId.ColorWipe:
                EvaluateColorWipe( definition, phaseMilliseconds, frame );
                return;
            case LpTagEffectId.RainbowWave:
                EvaluateRainbowWave( definition, phaseMilliseconds, frame );
                return;
            case LpTagEffectId.RedScanner:
                EvaluateRedScanner( definition, phaseMilliseconds, frame );
                return;
            case LpTagEffectId.Slide:
                EvaluateSlide( definition, policy.SlideAmplitudePixels, phaseMilliseconds, frame );
                return;
            case LpTagEffectId.BouncingDot:
                EvaluateBouncingDot( definition, phaseMilliseconds, frame );
                return;
            case LpTagEffectId.BouncingPlus:
                EvaluateBouncingPlus( definition, phaseMilliseconds, frame );
                return;
            default:
                throw new ArgumentOutOfRangeException( nameof( effectId ), effectId, "Unknown LifePunch tag effect ID." );
        }
    }

    private static void EvaluateSolid(
        LpTagPaletteRole role,
        LpTagEffectId effectId,
        LpTagEffectFrame frame )
    {
        frame.SetAllRoles( role );
        frame.FrameKey = (int)effectId;
    }

    private static void EvaluateColorWipe(
        LpTagEffectDefinition definition,
        double phaseMilliseconds,
        LpTagEffectFrame frame )
    {
        var phase = NormalizePhase( phaseMilliseconds, definition.ResolvePeriodMilliseconds( frame.GraphemeCount ) );
        var step = (int)Math.Floor( phase / definition.FixedStepMilliseconds );
        var baseColor = step / frame.GraphemeCount % 5;
        var position = step % frame.GraphemeCount;
        for ( var index = 0; index < frame.GraphemeCount; index++ )
        {
            var paletteIndex = index < position ? baseColor : ( baseColor + 4 ) % 5;
            frame.SetRole( index, PaletteRoleAt( paletteIndex ) );
        }

        frame.FrameKey = step;
        frame.NextChangeDelayMilliseconds = LpTagEffectCatalog.ResolveNextChangeDelayMilliseconds(
            definition.Id,
            frame.GraphemeCount,
            0,
            phase );
    }

    private static void EvaluateRainbowWave(
        LpTagEffectDefinition definition,
        double phaseMilliseconds,
        LpTagEffectFrame frame )
    {
        var phase = NormalizePhase( phaseMilliseconds, definition.ResolvePeriodMilliseconds( frame.GraphemeCount ) );
        var step = (int)Math.Floor( phase / definition.FixedStepMilliseconds );
        var baseColor = step % 5;
        for ( var index = 0; index < frame.GraphemeCount; index++ )
            frame.SetRole( index, PaletteRoleAt( ( baseColor + index ) % 5 ) );

        frame.FrameKey = step;
        frame.NextChangeDelayMilliseconds = LpTagEffectCatalog.ResolveNextChangeDelayMilliseconds(
            definition.Id,
            frame.GraphemeCount,
            0,
            phase );
    }

    private static void EvaluateRedScanner(
        LpTagEffectDefinition definition,
        double phaseMilliseconds,
        LpTagEffectFrame frame )
    {
        var period = definition.ResolvePeriodMilliseconds( frame.GraphemeCount );
        var phase = NormalizePhase( phaseMilliseconds, period );
        var position = (int)Math.Floor(
            ( Math.Cos( phase * LpTagEffectCatalog.CurveRadiansPerMillisecond ) + 1d )
            * 0.5d
            * frame.GraphemeCount );

        if ( position >= 0 && position < frame.GraphemeCount )
            frame.SetRole( position, LpTagPaletteRole.Red );

        frame.FrameKey = position;
        frame.NextChangeDelayMilliseconds = LpTagEffectCatalog.ResolveNextChangeDelayMilliseconds(
            definition.Id,
            frame.GraphemeCount,
            0,
            phase );
    }

    private static void EvaluateSlide(
        LpTagEffectDefinition definition,
        int amplitudePixels,
        double phaseMilliseconds,
        LpTagEffectFrame frame )
    {
        var period = definition.ResolvePeriodMilliseconds( frame.GraphemeCount );
        var phase = NormalizePhase( phaseMilliseconds, period );
        var transform = (
            2d * ( 1d - Math.Abs( Math.Sin( phase * LpTagEffectCatalog.CurveRadiansPerMillisecond ) ) )
            - 1d ) * amplitudePixels;

        frame.TranslateXPixels = JavaScriptRound( transform );
        frame.FrameKey = frame.TranslateXPixels;
        frame.NextChangeDelayMilliseconds = LpTagEffectCatalog.ResolveNextChangeDelayMilliseconds(
            definition.Id,
            frame.GraphemeCount,
            amplitudePixels,
            phase );
    }

    private static void EvaluateBouncingDot(
        LpTagEffectDefinition definition,
        double phaseMilliseconds,
        LpTagEffectFrame frame )
    {
        EvaluateDecoration( definition, phaseMilliseconds, DotFrames, frame );
    }

    private static void EvaluateBouncingPlus(
        LpTagEffectDefinition definition,
        double phaseMilliseconds,
        LpTagEffectFrame frame )
    {
        EvaluateDecoration( definition, phaseMilliseconds, PlusFrames, frame );
    }

    private static void EvaluateDecoration(
        LpTagEffectDefinition definition,
        double phaseMilliseconds,
        string[] positions,
        LpTagEffectFrame frame )
    {
        var phase = NormalizePhase( phaseMilliseconds, definition.ResolvePeriodMilliseconds( frame.GraphemeCount ) );
        var step = (int)Math.Floor( phase / definition.FixedStepMilliseconds ) % 8;
        var position = step < 4 ? step : 7 - step;
        frame.Decoration = positions[position];
        frame.FrameKey = position;
        frame.NextChangeDelayMilliseconds = LpTagEffectCatalog.ResolveNextChangeDelayMilliseconds(
            definition.Id,
            frame.GraphemeCount,
            0,
            phase );
    }

    private static void EvaluateReducedMotion(
        LpTagReducedMotionFrame reducedMotionFrame,
        LpTagEffectFrame frame )
    {
        switch ( reducedMotionFrame )
        {
            case LpTagReducedMotionFrame.ColorWipe:
                var boundary = ( frame.GraphemeCount + 1 ) / 2;
                for ( var index = 0; index < frame.GraphemeCount; index++ )
                {
                    frame.SetRole(
                        index,
                        index < boundary ? LpTagPaletteRole.Red : LpTagPaletteRole.Cyan );
                }
                break;
            case LpTagReducedMotionFrame.RainbowWave:
                for ( var index = 0; index < frame.GraphemeCount; index++ )
                    frame.SetRole( index, PaletteRoleAt( index % 5 ) );
                break;
            case LpTagReducedMotionFrame.RedScanner:
                frame.SetRole( frame.GraphemeCount / 2, LpTagPaletteRole.Red );
                break;
            case LpTagReducedMotionFrame.Slide:
                frame.TranslateXPixels = 0;
                break;
            case LpTagReducedMotionFrame.BouncingDot:
                frame.Decoration = DotFrames[0];
                break;
            case LpTagReducedMotionFrame.BouncingPlus:
                frame.Decoration = PlusFrames[0];
                break;
            case LpTagReducedMotionFrame.PlainIdentity:
            case LpTagReducedMotionFrame.StaticEffect:
                break;
            default:
                throw new ArgumentOutOfRangeException( nameof( reducedMotionFrame ), reducedMotionFrame, "Unknown reduced-motion frame." );
        }

        frame.FrameKey = (int)reducedMotionFrame;
    }

    private static int JavaScriptRound( double value )
    {
        return (int)Math.Floor( value + 0.5d );
    }

    private static double NormalizePhase( double phaseMilliseconds, double periodMilliseconds )
    {
        if ( periodMilliseconds <= 0d
            || double.IsNaN( periodMilliseconds )
            || double.IsInfinity( periodMilliseconds )
            || double.IsNaN( phaseMilliseconds )
            || double.IsInfinity( phaseMilliseconds ) )
        {
            return 0d;
        }

        var phase = phaseMilliseconds % periodMilliseconds;
        return phase < 0d ? phase + periodMilliseconds : phase;
    }

    private static LpTagPaletteRole PaletteRoleAt( int paletteIndex )
    {
        return paletteIndex switch
        {
            0 => LpTagPaletteRole.Red,
            1 => LpTagPaletteRole.Green,
            2 => LpTagPaletteRole.Yellow,
            3 => LpTagPaletteRole.Blue,
            4 => LpTagPaletteRole.Cyan,
            _ => throw new ArgumentOutOfRangeException( nameof( paletteIndex ) )
        };
    }
}
