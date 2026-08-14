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

internal static class LpTagPureChecks
{
    public static IReadOnlyList<string> RunAll()
    {
        var failures = new List<string>();
        CheckCatalog( failures );
        CheckEffects( failures );
        CheckPhase( failures );
        CheckScheduling( failures );
        CheckProfiles( failures );
        CheckReplay( failures );
        CheckMenuState( failures );
        CheckUnicode( failures );

        return failures;
    }

    private static void CheckCatalog( List<string> failures )
    {
        Check( LpTagEffectCatalog.All.Count == 12, failures, "catalog has wire IDs 0 through 11" );
        var keys = new HashSet<string>( StringComparer.Ordinal );
        for ( var index = 0; index < LpTagEffectCatalog.All.Count; index++ )
        {
            var definition = LpTagEffectCatalog.All[index];
            Check( (byte)definition.Id == index, failures, "catalog wire IDs are contiguous" );
            Check( keys.Add( definition.Key ), failures, "catalog keys are ordinal-unique" );
            Check( definition.Supports( LpTagSurface.Chat ) && definition.Supports( LpTagSurface.Scoreboard ) && definition.Supports( LpTagSurface.Nameplate ), failures, "catalog definition supports all three surfaces" );
        }
        Check( LpTagEffectCatalog.TryGet( LpTagEffectId.None, out var none ) && none.Key == "none", failures, "none key is stable" );
        Check( LpTagEffectCatalog.TryGet( "solid-red-v1", out var red ) && red.Id == LpTagEffectId.SolidRed, failures, "solid red key is stable" );
        Check( LpTagEffectCatalog.TryGet( "solid-green-v1", out var green ) && green.Id == LpTagEffectId.SolidGreen, failures, "solid green key is stable" );
        Check( LpTagEffectCatalog.TryGet( "solid-yellow-v1", out var yellow ) && yellow.Id == LpTagEffectId.SolidYellow, failures, "solid yellow key is stable" );
        Check( LpTagEffectCatalog.TryGet( "solid-blue-v1", out var blue ) && blue.Id == LpTagEffectId.SolidBlue, failures, "solid blue key is stable" );
        Check( LpTagEffectCatalog.TryGet( "solid-cyan-v1", out var cyan ) && cyan.Id == LpTagEffectId.SolidCyan, failures, "solid cyan key is stable" );
        Check( LpTagEffectCatalog.TryGet( "color-wipe-v1", out var wipe ) && wipe.Id == LpTagEffectId.ColorWipe, failures, "color wipe key is stable" );
        Check( LpTagEffectCatalog.TryGet( "rainbow-wave-v1", out var rainbow ) && rainbow.Id == LpTagEffectId.RainbowWave, failures, "rainbow key is stable" );
        Check( LpTagEffectCatalog.TryGet( "red-scanner-v1", out var scanner ) && scanner.Id == LpTagEffectId.RedScanner, failures, "scanner key is stable" );
        Check( LpTagEffectCatalog.TryGet( "slide-v1", out var slide ) && slide.Id == LpTagEffectId.Slide, failures, "slide key is stable" );
        Check( LpTagEffectCatalog.TryGet( "bouncing-dot-v1", out var dot ) && dot.Id == LpTagEffectId.BouncingDot, failures, "dot key is stable" );
        Check( LpTagEffectCatalog.TryGet( "bouncing-plus-v1", out var plus ) && plus.Id == LpTagEffectId.BouncingPlus, failures, "plus key is stable" );
        Check( LpTagEffectCatalog.PaletteHex( LpTagPaletteRole.Neutral ) == "#ffffff", failures, "neutral palette is exact" );
        Check( LpTagEffectCatalog.PaletteHex( LpTagPaletteRole.Red ) == "#ff3333", failures, "red palette is exact" );
        Check( LpTagEffectCatalog.PaletteHex( LpTagPaletteRole.Green ) == "#00ff00", failures, "green palette is exact" );
        Check( LpTagEffectCatalog.PaletteHex( LpTagPaletteRole.Yellow ) == "#ffff00", failures, "yellow palette is exact" );
        Check( LpTagEffectCatalog.PaletteHex( LpTagPaletteRole.Blue ) == "#0000ff", failures, "blue palette is exact" );
        Check( LpTagEffectCatalog.PaletteHex( LpTagPaletteRole.Cyan ) == "#00ffff", failures, "cyan palette is exact" );
        Check( wipe.ResolvePeriodMilliseconds( 4 ) == 2000d && wipe.FixedStepMilliseconds == 100d, failures, "wipe timing is exact" );
        Check( rainbow.ResolvePeriodMilliseconds( 6 ) == 500d && rainbow.FixedStepMilliseconds == 100d, failures, "rainbow timing is exact" );
        Check( scanner.ResolvePeriodMilliseconds( 4 ) == 2d * Math.PI / 0.004d && scanner.TimingKind == LpTagTimingKind.DiscreteCurve, failures, "scanner timing is exact" );
        Check( slide.ResolvePeriodMilliseconds( 4 ) == Math.PI / 0.004d && slide.TimingKind == LpTagTimingKind.DiscreteCurve, failures, "slide timing is exact" );
        Check( dot.ResolvePeriodMilliseconds( 4 ) == 600d && dot.FixedStepMilliseconds == 75d, failures, "dot timing is exact" );
        Check( plus.ResolvePeriodMilliseconds( 4 ) == 600d && plus.FixedStepMilliseconds == 75d, failures, "plus timing is exact" );
        Check( none.ResolvePeriodMilliseconds( 4 ) == 0d && red.ResolvePeriodMilliseconds( 4 ) == 0d, failures, "static effects have no period" );
        Check( wipe.SupportedSurfaces == LpTagSurfaceMask.All && rainbow.SupportedSurfaces == LpTagSurfaceMask.All && scanner.SupportedSurfaces == LpTagSurfaceMask.All && slide.SupportedSurfaces == LpTagSurfaceMask.All && dot.SupportedSurfaces == LpTagSurfaceMask.All && plus.SupportedSurfaces == LpTagSurfaceMask.All, failures, "animated masks are exact" );
        var chatPolicy = LpTagEffectCatalog.GetPolicy( LpTagSurface.Chat );
        var scoreboardPolicy = LpTagEffectCatalog.GetPolicy( LpTagSurface.Scoreboard );
        var nameplatePolicy = LpTagEffectCatalog.GetPolicy( LpTagSurface.Nameplate );
        Check( chatPolicy.Surface == LpTagSurface.Chat && chatPolicy.SlideAmplitudePixels == 4 && chatPolicy.AllowDecorations && chatPolicy.AllowPerGrapheme, failures, "chat policy is exact" );
        Check( scoreboardPolicy.Surface == LpTagSurface.Scoreboard && scoreboardPolicy.SlideAmplitudePixels == 8 && scoreboardPolicy.AllowDecorations && scoreboardPolicy.AllowPerGrapheme, failures, "scoreboard policy is exact" );
        Check( nameplatePolicy.Surface == LpTagSurface.Nameplate && nameplatePolicy.SlideAmplitudePixels == 5 && nameplatePolicy.AllowDecorations && nameplatePolicy.AllowPerGrapheme, failures, "nameplate policy is exact" );
        Check( wipe.Supports( LpTagSurface.Chat ) && wipe.Supports( LpTagSurface.Scoreboard ) && wipe.Supports( LpTagSurface.Nameplate ), failures, "color wipe supports all surfaces" );
        Check( rainbow.Supports( LpTagSurface.Chat ) && scanner.Supports( LpTagSurface.Scoreboard ) && slide.Supports( LpTagSurface.Nameplate ), failures, "animated effects support all surfaces" );
        Check( !LpTagEffectCatalog.TryGet( "NONE", out _ ) && !LpTagEffectCatalog.TryGet( "unknown-v1", out _ ), failures, "catalog rejects noncanonical keys" );
        Check( !LpTagEffectCatalog.TryGet( null, out _ ) && !LpTagEffectCatalog.TryGet( "solid-red-v1\u00e9", out _ ) && !LpTagEffectCatalog.TryGet( new string( 'a', 33 ), out _ ), failures, "catalog rejects null non-ASCII and over-length keys" );
    }

    private static void CheckEffects( List<string> failures )
    {
        var policy = LpTagEffectCatalog.GetPolicy( LpTagSurface.Chat );
        var text = LpTagTextSegmentation.Analyze( "ABCD" );
        var frame = new LpTagEffectFrame();

        LpTagEffectEvaluator.Evaluate( LpTagTextSegmentation.Analyze( null ), LpTagEffectId.ColorWipe, policy, 0d, default, frame );
        Check( frame.GraphemeCount == 0 && frame.Roles.Length == 0, failures, "empty input returns a plain empty frame" );
        Check( frame.Decoration == string.Empty && frame.TranslateXPixels == 0 && frame.FrameKey == 0, failures, "empty input has no decoration transform or frame state" );
        Check( double.IsPositiveInfinity( frame.NextChangeDelayMilliseconds ), failures, "empty input performs no timing modulo" );

        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.SolidRed, policy, 999d, default, frame );
        CheckRoles( frame, failures, "solid red", LpTagPaletteRole.Red, LpTagPaletteRole.Red, LpTagPaletteRole.Red, LpTagPaletteRole.Red );
        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.SolidGreen, policy, 999d, default, frame );
        CheckRoles( frame, failures, "solid green", LpTagPaletteRole.Green, LpTagPaletteRole.Green, LpTagPaletteRole.Green, LpTagPaletteRole.Green );
        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.SolidYellow, policy, 999d, default, frame );
        CheckRoles( frame, failures, "solid yellow", LpTagPaletteRole.Yellow, LpTagPaletteRole.Yellow, LpTagPaletteRole.Yellow, LpTagPaletteRole.Yellow );
        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.SolidBlue, policy, 999d, default, frame );
        CheckRoles( frame, failures, "solid blue", LpTagPaletteRole.Blue, LpTagPaletteRole.Blue, LpTagPaletteRole.Blue, LpTagPaletteRole.Blue );
        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.SolidCyan, policy, 999d, default, frame );
        CheckRoles( frame, failures, "solid cyan", LpTagPaletteRole.Cyan, LpTagPaletteRole.Cyan, LpTagPaletteRole.Cyan, LpTagPaletteRole.Cyan );
        Check( double.IsPositiveInfinity( frame.NextChangeDelayMilliseconds ), failures, "static color has no next change" );

        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.ColorWipe, policy, 0d, default, frame );
        CheckRoles( frame, failures, "wipe L=4 p=0", LpTagPaletteRole.Cyan, LpTagPaletteRole.Cyan, LpTagPaletteRole.Cyan, LpTagPaletteRole.Cyan );
        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.ColorWipe, policy, 100d, default, frame );
        CheckRoles( frame, failures, "wipe L=4 p=100", LpTagPaletteRole.Red, LpTagPaletteRole.Cyan, LpTagPaletteRole.Cyan, LpTagPaletteRole.Cyan );
        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.ColorWipe, policy, 400d, default, frame );
        CheckRoles( frame, failures, "wipe L=4 p=400", LpTagPaletteRole.Red, LpTagPaletteRole.Red, LpTagPaletteRole.Red, LpTagPaletteRole.Red );
        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.ColorWipe, policy, 500d, default, frame );
        CheckRoles( frame, failures, "wipe L=4 p=500", LpTagPaletteRole.Green, LpTagPaletteRole.Red, LpTagPaletteRole.Red, LpTagPaletteRole.Red );
        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.ColorWipe, policy, 2000d, default, frame );
        CheckRoles( frame, failures, "wipe L=4 period wrap p=2000", LpTagPaletteRole.Cyan, LpTagPaletteRole.Cyan, LpTagPaletteRole.Cyan, LpTagPaletteRole.Cyan );
        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.ColorWipe, policy, 100d, default, frame );
        var wipeStepKey = frame.FrameKey;
        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.ColorWipe, policy, 199.999d, default, frame );
        Check( frame.FrameKey == wipeStepKey, failures, "wipe FrameKey is stable inside a step" );
        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.ColorWipe, policy, 200d, default, frame );
        Check( frame.FrameKey != wipeStepKey, failures, "wipe FrameKey changes at a step boundary" );

        var rainbowText = LpTagTextSegmentation.Analyze( "ABCDEF" );
        LpTagEffectEvaluator.Evaluate( rainbowText, LpTagEffectId.RainbowWave, policy, 0d, default, frame );
        CheckRoles( frame, failures, "rainbow L=6 p=0", LpTagPaletteRole.Red, LpTagPaletteRole.Green, LpTagPaletteRole.Yellow, LpTagPaletteRole.Blue, LpTagPaletteRole.Cyan, LpTagPaletteRole.Red );
        LpTagEffectEvaluator.Evaluate( rainbowText, LpTagEffectId.RainbowWave, policy, 100d, default, frame );
        CheckRoles( frame, failures, "rainbow L=6 p=100", LpTagPaletteRole.Green, LpTagPaletteRole.Yellow, LpTagPaletteRole.Blue, LpTagPaletteRole.Cyan, LpTagPaletteRole.Red, LpTagPaletteRole.Green );
        LpTagEffectEvaluator.Evaluate( rainbowText, LpTagEffectId.RainbowWave, policy, 500d, default, frame );
        CheckRoles( frame, failures, "rainbow L=6 period wrap p=500", LpTagPaletteRole.Red, LpTagPaletteRole.Green, LpTagPaletteRole.Yellow, LpTagPaletteRole.Blue, LpTagPaletteRole.Cyan, LpTagPaletteRole.Red );
        LpTagEffectEvaluator.Evaluate( rainbowText, LpTagEffectId.RainbowWave, policy, 0d, default, frame );
        var rainbowStepKey = frame.FrameKey;
        LpTagEffectEvaluator.Evaluate( rainbowText, LpTagEffectId.RainbowWave, policy, 99.999d, default, frame );
        Check( frame.FrameKey == rainbowStepKey, failures, "rainbow FrameKey is stable inside a step" );
        LpTagEffectEvaluator.Evaluate( rainbowText, LpTagEffectId.RainbowWave, policy, 100d, default, frame );
        Check( frame.FrameKey != rainbowStepKey, failures, "rainbow FrameKey changes at a step boundary" );

        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.RedScanner, policy, 0d, default, frame );
        Check( frame.RedRoleCount == 0, failures, "scanner p=0 intentionally has no red grapheme" );
        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.RedScanner, policy, 785d, default, frame );
        Check( frame.RedRoleCount == 1 && frame.GetRole( 0 ) == LpTagPaletteRole.Red, failures, "scanner p=785 highlights the first grapheme" );
        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.Slide, policy, 0d, default, frame );
        Check( frame.TranslateXPixels == 4, failures, "slide p=0 is +4 px for chat" );
        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.Slide, policy, 393d, default, frame );
        Check( frame.TranslateXPixels == -4, failures, "slide p=393 is -4 px for chat" );
        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.Slide, policy, 785d, default, frame );
        Check( frame.TranslateXPixels == 4, failures, "slide p=785 is +4 px for chat" );

        var scannerHalfCycleMilliseconds = Math.PI / LpTagEffectCatalog.CurveRadiansPerMillisecond;
        var slideQuarterCycleMilliseconds = ( Math.PI / 2d ) / LpTagEffectCatalog.CurveRadiansPerMillisecond;
        CheckClose( scannerHalfCycleMilliseconds, 785.3981633974482d, 0.0000000001d, failures, "scanner radians convert to milliseconds" );
        CheckClose( slideQuarterCycleMilliseconds, 392.6990816987241d, 0.0000000001d, failures, "slide radians convert to milliseconds" );
        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.RedScanner, policy, scannerHalfCycleMilliseconds, default, frame );
        Check( frame.FrameKey == 0 && frame.GetRole( 0 ) == LpTagPaletteRole.Red, failures, "scanner half-cycle endpoint is position zero" );
        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.RedScanner, policy, 2d * Math.PI / LpTagEffectCatalog.CurveRadiansPerMillisecond, default, frame );
        Check( frame.FrameKey == 4 && frame.RedRoleCount == 0, failures, "scanner period wrap returns to the no-highlight frame" );
        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.Slide, policy, slideQuarterCycleMilliseconds, default, frame );
        Check( frame.TranslateXPixels == -4, failures, "slide quarter-cycle endpoint is negative amplitude" );

        var scoreboardPolicy = LpTagEffectCatalog.GetPolicy( LpTagSurface.Scoreboard );
        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.Slide, scoreboardPolicy, 0d, default, frame );
        Check( frame.TranslateXPixels == 8, failures, "scoreboard slide upper bound is +8 px" );
        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.Slide, scoreboardPolicy, 393d, default, frame );
        Check( frame.TranslateXPixels == -8, failures, "scoreboard slide lower bound is -8 px" );
        var nameplatePolicy = LpTagEffectCatalog.GetPolicy( LpTagSurface.Nameplate );
        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.Slide, nameplatePolicy, 0d, default, frame );
        Check( frame.TranslateXPixels == 5, failures, "nameplate slide upper bound is +5 px" );
        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.Slide, nameplatePolicy, 393d, default, frame );
        Check( frame.TranslateXPixels == -5, failures, "nameplate slide lower bound is -5 px" );

        CheckCurveBoundary( LpTagEffectId.RedScanner, text, policy, 100d, 161.79938779914943d, 3, 2, failures, "scanner descending boundary" );
        CheckCurveBoundary( LpTagEffectId.RedScanner, text, policy, 900d, 147.1975511965977d, 0, 1, failures, "scanner ascending boundary" );
        CheckCurveBoundary( LpTagEffectId.Slide, text, policy, 100d, 13.20414868623139d, 1, 0, failures, "slide descending boundary" );
        CheckCurveBoundary( LpTagEffectId.Slide, text, policy, 600d, 36.0465592361107d, -1, 0, failures, "slide ascending boundary" );
        CheckCurveBoundary( LpTagEffectId.Slide, text, policy, 780d, 21.03335384657105d, 4, 3, failures, "slide period-wrap boundary" );
        Check( LpTagEffectCatalog.ResolveNextChangeDelayMilliseconds( LpTagEffectId.RedScanner, 4, 4, 0d ) == 1d, failures, "scanner equal-boundary delay is the positive minimum" );
        Check( LpTagEffectCatalog.ResolveNextChangeDelayMilliseconds( LpTagEffectId.RedScanner, 4, 4, scannerHalfCycleMilliseconds ) == 1d, failures, "scanner endpoint delay is the positive minimum" );
        Check( LpTagEffectCatalog.ResolveNextChangeDelayMilliseconds( LpTagEffectId.Slide, 4, 4, slideQuarterCycleMilliseconds ) == 1d, failures, "slide endpoint delay is the positive minimum" );

        CheckDecorationSequence( LpTagEffectId.BouncingDot, policy, frame, failures, "dot", "o...", ".o..", "..o.", "...o", "...o", "..o.", ".o..", "o..." );
        CheckDecorationSequence( LpTagEffectId.BouncingPlus, policy, frame, failures, "plus", "+---", "-+--", "--+-", "---+", "---+", "--+-", "-+--", "+---" );
        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.BouncingDot, policy, 225d, default, frame );
        var dotEndpointKey = frame.FrameKey;
        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.BouncingDot, policy, 300d, default, frame );
        Check( frame.FrameKey == dotEndpointKey && frame.Decoration == "...o", failures, "dot duplicate endpoint keeps a stable visual FrameKey" );
        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.BouncingPlus, policy, 600d, default, frame );
        Check( frame.Decoration == "+---", failures, "plus period wrap returns to the first frame" );
        Check( LpTagEffectCatalog.GetRequired( LpTagEffectId.ColorWipe ).ResolvePeriodMilliseconds( 4 ) == 2000d, failures, "wipe period scales with grapheme count" );
        Check( LpTagEffectCatalog.GetRequired( LpTagEffectId.RainbowWave ).ResolvePeriodMilliseconds( 6 ) == 500d, failures, "rainbow period is exact" );
        Check( LpTagEffectCatalog.ResolveNextChangeDelayMilliseconds( LpTagEffectId.ColorWipe, 4, 4, 0d ) == 100d, failures, "wipe exact boundary waits one full step" );
        Check( LpTagEffectCatalog.ResolveNextChangeDelayMilliseconds( LpTagEffectId.ColorWipe, 4, 4, 100d ) == 100d, failures, "wipe nonzero exact boundary waits one full step" );
        Check( LpTagEffectCatalog.ResolveNextChangeDelayMilliseconds( LpTagEffectId.RainbowWave, 6, 4, 500d ) == 100d, failures, "rainbow period boundary waits one full step" );
        Check( LpTagEffectCatalog.ResolveNextChangeDelayMilliseconds( LpTagEffectId.BouncingDot, 4, 4, 75d ) == 75d, failures, "dot exact boundary waits one full step" );
        Check( LpTagEffectCatalog.ResolveNextChangeDelayMilliseconds( LpTagEffectId.BouncingPlus, 4, 4, 599d ) == 1d, failures, "plus last millisecond waits one millisecond" );
        Check( double.IsPositiveInfinity( LpTagEffectCatalog.ResolveNextChangeDelayMilliseconds( LpTagEffectId.SolidRed, 4, 4, 0d ) ), failures, "static delay is positive infinity" );
        Check( double.IsPositiveInfinity( LpTagEffectCatalog.ResolveNextChangeDelayMilliseconds( LpTagEffectId.ColorWipe, 0, 4, 0d ) ), failures, "empty dynamic delay is positive infinity" );

        var reusableCapacity = frame.RoleCapacity;
        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.None, policy, 0d, default, frame );
        Check( reusableCapacity == 6 && frame.RoleCapacity == reusableCapacity, failures, "caller-owned frame reuses grown palette storage" );
        CheckRoles( frame, failures, "frame reset clears active roles", LpTagPaletteRole.Neutral, LpTagPaletteRole.Neutral, LpTagPaletteRole.Neutral, LpTagPaletteRole.Neutral );

        var reducedMotion = new LpViewerTagPreferencesV1( true, false, false );
        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.ColorWipe, policy, 0d, reducedMotion, frame );
        CheckRoles( frame, failures, "reduced wipe", LpTagPaletteRole.Red, LpTagPaletteRole.Red, LpTagPaletteRole.Cyan, LpTagPaletteRole.Cyan );
        LpTagEffectEvaluator.Evaluate( rainbowText, LpTagEffectId.RainbowWave, policy, 0d, reducedMotion, frame );
        CheckRoles( frame, failures, "reduced rainbow", LpTagPaletteRole.Red, LpTagPaletteRole.Green, LpTagPaletteRole.Yellow, LpTagPaletteRole.Blue, LpTagPaletteRole.Cyan, LpTagPaletteRole.Red );
        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.RedScanner, policy, 0d, reducedMotion, frame );
        Check( frame.GetRole( 2 ) == LpTagPaletteRole.Red, failures, "reduced scanner highlights the middle grapheme" );
        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.Slide, policy, 0d, reducedMotion, frame );
        Check( frame.TranslateXPixels == 0, failures, "reduced slide is centered" );
        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.BouncingDot, policy, 0d, reducedMotion, frame );
        Check( frame.Decoration == "o...", failures, "reduced dot is exact" );
        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.BouncingPlus, policy, 0d, reducedMotion, frame );
        Check( frame.Decoration == "+---", failures, "reduced plus is exact" );
        Check( double.IsPositiveInfinity( frame.NextChangeDelayMilliseconds ), failures, "reduced motion frames are static" );

        var animationsDisabled = new LpViewerTagPreferencesV1( false, true, false );
        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.ColorWipe, policy, 400d, animationsDisabled, frame );
        CheckRoles( frame, failures, "disabled animation is plain identity", LpTagPaletteRole.Neutral, LpTagPaletteRole.Neutral, LpTagPaletteRole.Neutral, LpTagPaletteRole.Neutral );
        Check( double.IsPositiveInfinity( frame.NextChangeDelayMilliseconds ), failures, "disabled animation has no next change" );
        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.SolidRed, policy, 400d, animationsDisabled, frame );
        CheckRoles( frame, failures, "disabled animations retain static colors", LpTagPaletteRole.Red, LpTagPaletteRole.Red, LpTagPaletteRole.Red, LpTagPaletteRole.Red );
        var reducedAndDisabled = new LpViewerTagPreferencesV1( true, true, false );
        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.RedScanner, policy, 0d, reducedAndDisabled, frame );
        Check( frame.RedRoleCount == 0, failures, "DisableAnimations takes precedence over ReduceMotion" );
        var hiddenDecorations = new LpViewerTagPreferencesV1( false, false, true );
        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.BouncingDot, policy, 0d, hiddenDecorations, frame );
        Check( frame.Decoration == string.Empty && frame.GraphemeCount == 4, failures, "hidden dot decoration retains the plain name" );
        LpTagEffectEvaluator.Evaluate( text, LpTagEffectId.BouncingPlus, policy, 0d, hiddenDecorations, frame );
        Check( frame.Decoration == string.Empty && frame.GraphemeCount == 4, failures, "hidden plus decoration retains the plain name" );

        var arabic = LpTagTextSegmentation.Analyze( "مرحبا" );
        LpTagEffectEvaluator.Evaluate( arabic, LpTagEffectId.ColorWipe, policy, 100d, default, frame );
        Check( frame.RequiresWholeNameFallback && frame.RedRoleCount == 0, failures, "contextual text uses a plain whole-name animated fallback" );
        Check( double.IsPositiveInfinity( frame.NextChangeDelayMilliseconds ), failures, "whole-name animated fallback does not schedule" );
        LpTagEffectEvaluator.Evaluate( arabic, LpTagEffectId.SolidRed, policy, 100d, default, frame );
        Check( frame.RequiresWholeNameFallback && frame.GetRole( 0 ) == LpTagPaletteRole.Red, failures, "whole-name fallback retains static color" );
    }

    private static void CheckPhase( List<string> failures )
    {
        Check( LpTagPhase.Hash( 0UL ) == 12161962213042174405UL, failures, "FNV vector for zero" );
        Check( LpTagPhase.Hash( 1UL ) == 9929646806074584996UL, failures, "FNV vector for one" );
        Check( LpTagPhase.Hash( 76561198000000000UL ) == 8653794760055181559UL, failures, "FNV vector for first Steam ID" );
        Check( LpTagPhase.Hash( 76561198012345678UL ) == 14164129076051541741UL, failures, "FNV vector for second Steam ID" );
        Check( LpTagPhase.OffsetFor( 1UL, 0d ) == 0d, failures, "zero-period phase offset is zero" );
        Check( LpTagPhase.OffsetFor( 1UL, -500d ) == 0d, failures, "negative-period phase offset is zero" );
        Check( LpTagPhase.OffsetFor( 1UL, double.NaN ) == 0d, failures, "NaN-period phase offset is zero" );
        Check( LpTagPhase.OffsetFor( 1UL, double.PositiveInfinity ) == 0d, failures, "positive-infinity-period phase offset is zero" );
        Check( LpTagPhase.OffsetFor( 1UL, double.NegativeInfinity ) == 0d, failures, "negative-infinity-period phase offset is zero" );
        var fixedPeriod = LpTagEffectCatalog.GetRequired( LpTagEffectId.RainbowWave ).ResolvePeriodMilliseconds( 6 );
        var dynamicPeriod = LpTagEffectCatalog.GetRequired( LpTagEffectId.ColorWipe ).ResolvePeriodMilliseconds( 4 );
        Check( fixedPeriod == 500d && dynamicPeriod == 2000d, failures, "phase vectors use resolved fixed and dynamic periods" );
        Check( LpTagPhase.OffsetFor( 0UL, fixedPeriod ) == 452d, failures, "zero Steam ID fixed-period modulo is exact" );
        Check( LpTagPhase.OffsetFor( 0UL, dynamicPeriod ) == 1952d, failures, "zero Steam ID dynamic-period modulo is exact" );
        Check( LpTagPhase.OffsetFor( 1UL, fixedPeriod ) == 88d, failures, "one Steam ID fixed-period modulo is exact" );
        Check( LpTagPhase.OffsetFor( 1UL, dynamicPeriod ) == 1088d, failures, "one Steam ID dynamic-period modulo is exact" );
        Check( LpTagPhase.OffsetFor( 76561198000000000UL, fixedPeriod ) == 312d, failures, "first Steam ID fixed-period modulo is exact" );
        Check( LpTagPhase.OffsetFor( 76561198000000000UL, dynamicPeriod ) == 1312d, failures, "first Steam ID dynamic-period modulo is exact" );
        Check( LpTagPhase.OffsetFor( 76561198012345678UL, fixedPeriod ) == 16d, failures, "second Steam ID fixed-period modulo is exact" );
        Check( LpTagPhase.OffsetFor( 76561198012345678UL, dynamicPeriod ) == 16d, failures, "second Steam ID dynamic-period modulo is exact" );
        Check( LpTagPhase.OffsetFor( 1UL, 500d ) >= 0d && LpTagPhase.OffsetFor( 1UL, 500d ) < 500d, failures, "phase offset is positive modulo" );
    }

    private static void CheckScheduling( List<string> failures )
    {
        var chatPolicy = LpTagEffectCatalog.GetPolicy( LpTagSurface.Chat );
        var scoreboardPolicy = LpTagEffectCatalog.GetPolicy( LpTagSurface.Scoreboard );

        Check(
            LpTagSchedulingRules.ResolveTimingMode(
                LpTagEffectId.RainbowWave,
                6,
                false,
                chatPolicy,
                default ) == LpTagEffectiveTimingMode.Animated,
            failures,
            "default animated effect remains scheduler-eligible" );
        Check(
            LpTagSchedulingRules.ResolveTimingMode(
                LpTagEffectId.RainbowWave,
                6,
                false,
                chatPolicy,
                new LpViewerTagPreferencesV1( false, true, false ) ) == LpTagEffectiveTimingMode.Plain,
            failures,
            "Disable Animations resolves an animated effect to plain timing" );
        Check(
            LpTagSchedulingRules.ResolveTimingMode(
                LpTagEffectId.RainbowWave,
                6,
                false,
                chatPolicy,
                new LpViewerTagPreferencesV1( true, false, false ) ) == LpTagEffectiveTimingMode.StaticRepresentative,
            failures,
            "Reduce Motion resolves an animated effect to a static representative" );
        Check(
            LpTagSchedulingRules.ResolveTimingMode(
                LpTagEffectId.SolidBlue,
                6,
                false,
                chatPolicy,
                default ) == LpTagEffectiveTimingMode.StaticEffect,
            failures,
            "catalog static effects resolve to static timing" );
        Check(
            LpTagSchedulingRules.ResolveTimingMode(
                LpTagEffectId.BouncingDot,
                6,
                false,
                chatPolicy,
                new LpViewerTagPreferencesV1( false, false, true ) ) == LpTagEffectiveTimingMode.Plain,
            failures,
            "hidden Dot/Plus decorations resolve to plain timing" );
        Check(
            LpTagSchedulingRules.ResolveTimingMode(
                LpTagEffectId.ColorWipe,
                5,
                true,
                chatPolicy,
                default ) == LpTagEffectiveTimingMode.Plain,
            failures,
            "unsafe shaping resolves a per-grapheme animation to plain timing" );
        Check(
            LpTagSchedulingRules.ResolveTimingMode(
                (LpTagEffectId)255,
                4,
                false,
                chatPolicy,
                default ) == LpTagEffectiveTimingMode.Plain,
            failures,
            "unknown effect IDs resolve to plain timing" );

        Check(
            LpTagSchedulingRules.ShouldSchedule( true, false, LpTagEffectiveTimingMode.Animated ),
            failures,
            "visible animated pure state schedules" );
        Check(
            !LpTagSchedulingRules.ShouldSchedule( false, false, LpTagEffectiveTimingMode.Animated ),
            failures,
            "invisible animated pure state does not schedule" );
        Check(
            !LpTagSchedulingRules.ShouldSchedule( true, false, LpTagEffectiveTimingMode.StaticEffect )
            && !LpTagSchedulingRules.ShouldSchedule( true, false, LpTagEffectiveTimingMode.StaticRepresentative )
            && !LpTagSchedulingRules.ShouldSchedule( true, true, LpTagEffectiveTimingMode.Animated ),
            failures,
            "static and disposed pure states do not schedule" );

        CheckClose(
            LpTagSchedulingRules.ResolveDueMilliseconds(
                1000d,
                0UL,
                LpTagEffectId.RainbowWave,
                6,
                chatPolicy.SlideAmplitudePixels ),
            1048d,
            0.000001d,
            failures,
            "fixed-period offset maps now plus phase to the next boundary" );
        CheckClose(
            LpTagSchedulingRules.ResolveDueMilliseconds(
                47d,
                0UL,
                LpTagEffectId.RainbowWave,
                6,
                chatPolicy.SlideAmplitudePixels ),
            48d,
            0.000001d,
            failures,
            "period wrap schedules the first next-cycle boundary with positive delay" );
        CheckClose(
            LpTagSchedulingRules.ResolveDueMilliseconds(
                1000d,
                0UL,
                LpTagEffectId.ColorWipe,
                4,
                scoreboardPolicy.SlideAmplitudePixels ),
            1048d,
            0.000001d,
            failures,
            "grapheme-scaled period uses the Steam offset before resolving delay" );
    }

    private static void CheckProfiles( List<string> failures )
    {
        var created = LpTagProfileRules.CreateNew();
        Check( created.SchemaVersion == 1 && created.Revision == 0UL, failures, "new profile starts at schema one revision zero" );
        Check( !created.Chat.Enabled && created.Chat.SelectedEffectKey == "none", failures, "new chat preference is disabled none" );
        Check( !created.Scoreboard.Enabled && created.Scoreboard.SelectedEffectKey == "none", failures, "new scoreboard preference is disabled none" );
        Check( !created.Nameplate.Enabled && created.Nameplate.SelectedEffectKey == "none", failures, "new nameplate preference is disabled none" );

        var selectChat = new LpTagMutationRequest( "00000000000000000000000000000001", 0UL, LpTagSurface.Chat, LpTagMutationKind.SetEffect, false, "solid-red-v1" );
        Check( LpTagProfileRules.TryApplyMutation( created, selectChat, out var selected, out var selectedError ) && selectedError == LpTagProfileError.None && !selected.Chat.Enabled, failures, "selection does not enable chat" );
        Check( selected.Scoreboard == created.Scoreboard && selected.Nameplate == created.Nameplate && selected.Revision == created.Revision, failures, "effect selection changes only one surface without assigning a revision" );

        var enableChat = new LpTagMutationRequest( "00000000000000000000000000000002", 0UL, LpTagSurface.Chat, LpTagMutationKind.SetEnabled, true, string.Empty );
        Check( LpTagProfileRules.TryApplyMutation( selected, enableChat, out var enabled, out var enabledError ) && enabledError == LpTagProfileError.None && enabled.Chat.Enabled, failures, "selected chat surface can be enabled" );
        var disableChat = new LpTagMutationRequest( "00000000000000000000000000000003", 0UL, LpTagSurface.Chat, LpTagMutationKind.SetEnabled, false, string.Empty );
        Check( LpTagProfileRules.TryApplyMutation( enabled, disableChat, out var disabled, out _ ) && !disabled.Chat.Enabled && disabled.Chat.SelectedEffectKey == "solid-red-v1", failures, "enabled-to-disabled transition preserves selection" );

        var enableWithoutSelection = new LpTagMutationRequest( "00000000000000000000000000000004", 0UL, LpTagSurface.Chat, LpTagMutationKind.SetEnabled, true, string.Empty );
        Check( !LpTagProfileRules.TryApplyMutation( created, enableWithoutSelection, out _, out var enableError ) && enableError == LpTagProfileError.SelectionRequired, failures, "enable without selection rejects" );
        var selectScoreboard = new LpTagMutationRequest( "00000000000000000000000000000005", 0UL, LpTagSurface.Scoreboard, LpTagMutationKind.SetEffect, false, "rainbow-wave-v1" );
        Check( LpTagProfileRules.TryApplyMutation( selected, selectScoreboard, out var independent, out _ ) && independent.Chat == selected.Chat && independent.Nameplate == selected.Nameplate && independent.Scoreboard.SelectedEffectKey == "rainbow-wave-v1", failures, "surfaces remain independent" );

        CheckProfileMutationError( created, new LpTagMutationRequest( string.Empty, 0UL, LpTagSurface.Chat, LpTagMutationKind.SetEnabled, false, string.Empty ), LpTagProfileError.InvalidRequestId, failures, "empty request ID rejects" );
        CheckProfileMutationError( created, new LpTagMutationRequest( new string( 'a', 33 ), 0UL, LpTagSurface.Chat, LpTagMutationKind.SetEnabled, false, string.Empty ), LpTagProfileError.InvalidRequestId, failures, "overlong request ID rejects" );
        CheckProfileMutationError( created, new LpTagMutationRequest( new string( '0', 31 ) + "é", 0UL, LpTagSurface.Chat, LpTagMutationKind.SetEnabled, false, string.Empty ), LpTagProfileError.InvalidRequestId, failures, "exact-length non-ASCII request ID rejects" );
        CheckProfileMutationError( created, new LpTagMutationRequest( new string( '0', 31 ) + "A", 0UL, LpTagSurface.Chat, LpTagMutationKind.SetEnabled, false, string.Empty ), LpTagProfileError.InvalidRequestId, failures, "exact-length uppercase hexadecimal request ID rejects" );
        CheckProfileMutationError( created, new LpTagMutationRequest( "00000000000000000000000000000006", 1UL, LpTagSurface.Chat, LpTagMutationKind.SetEnabled, false, string.Empty ), LpTagProfileError.StaleRevision, failures, "stale revision rejects" );
        CheckProfileMutationError( created, new LpTagMutationRequest( "00000000000000000000000000000007", 0UL, (LpTagSurface)0, LpTagMutationKind.SetEnabled, false, string.Empty ), LpTagProfileError.InvalidSurface, failures, "invalid surface rejects" );
        CheckProfileMutationError( created, new LpTagMutationRequest( "00000000000000000000000000000008", 0UL, LpTagSurface.Chat, (LpTagMutationKind)0, false, string.Empty ), LpTagProfileError.InvalidMutationKind, failures, "invalid mutation kind rejects" );
        CheckProfileMutationError( selected, new LpTagMutationRequest( "00000000000000000000000000000009", 0UL, LpTagSurface.Chat, LpTagMutationKind.SetEnabled, true, "solid-red-v1" ), LpTagProfileError.InvalidEffectKey, failures, "SetEnabled rejects a nonempty inactive effect key" );
        CheckProfileMutationError( selected, new LpTagMutationRequest( "0000000000000000000000000000000a", 0UL, LpTagSurface.Chat, LpTagMutationKind.SetEffect, true, "solid-red-v1" ), LpTagProfileError.InvalidEffectKey, failures, "SetEffect rejects a true inactive enabled field" );
        CheckProfileMutationError( selected, new LpTagMutationRequest( "0000000000000000000000000000000b", 0UL, LpTagSurface.Chat, LpTagMutationKind.SetEffect, false, string.Empty ), LpTagProfileError.InvalidEffectKey, failures, "empty effect key rejects" );
        CheckProfileMutationError( selected, new LpTagMutationRequest( "0000000000000000000000000000000c", 0UL, LpTagSurface.Chat, LpTagMutationKind.SetEffect, false, "none" ), LpTagProfileError.InvalidEffectKey, failures, "incoming none mutation rejects" );
        CheckProfileMutationError( selected, new LpTagMutationRequest( "0000000000000000000000000000000d", 0UL, LpTagSurface.Chat, LpTagMutationKind.SetEffect, false, new string( 'a', 33 ) ), LpTagProfileError.InvalidEffectKey, failures, "overlong effect key rejects" );
        CheckProfileMutationError( selected, new LpTagMutationRequest( "0000000000000000000000000000000e", 0UL, LpTagSurface.Chat, LpTagMutationKind.SetEffect, false, "solid-réd-v1" ), LpTagProfileError.InvalidEffectKey, failures, "non-ASCII effect key rejects" );
        CheckProfileMutationError( selected, new LpTagMutationRequest( "0000000000000000000000000000000f", 0UL, LpTagSurface.Chat, LpTagMutationKind.SetEffect, false, "removed-effect-v1" ), LpTagProfileError.InvalidEffectKey, failures, "unknown effect key rejects" );
        var chatOnlyDefinition = LpTagEffectCatalog.GetRequired( LpTagEffectId.SolidRed ) with { SupportedSurfaces = LpTagSurfaceMask.Chat };
        Check( !LpTagProfileRules.TryValidateEffectCompatibility( chatOnlyDefinition, LpTagSurface.Scoreboard, out var incompatibleError ) && incompatibleError == LpTagProfileError.IncompatibleEffect, failures, "incompatible catalog effect maps to the stable error without weakening the v1 catalog" );

        Check( LpTagProfileRules.InspectStored( created, out var inspectedCreated, out var inspectError ) == LpTagProfileInspectionStatus.Canonical && inspectedCreated == created && inspectError == LpTagProfileError.None, failures, "supported canonical record inspects as canonical" );
        var unknownStored = new LpStoredTagPreferencesV1( 1, 17UL, new LpTagSurfacePreference( true, "removed-effect-v1" ), new LpTagSurfacePreference( true, "solid-green-v1" ), new LpTagSurfacePreference( false, "solid-blue-v1" ) );
        Check( LpTagProfileRules.InspectStored( unknownStored, out var repaired, out var repairError ) == LpTagProfileInspectionStatus.RepairRequired && repairError == LpTagProfileError.CorruptRecord, failures, "supported unknown key requires repair" );
        Check( !repaired.Chat.Enabled && repaired.Chat.SelectedEffectKey == "none" && repaired.Scoreboard == unknownStored.Scoreboard && repaired.Nameplate == unknownStored.Nameplate && repaired.Revision == 17UL, failures, "supported repair safely resets only the corrupt surface and preserves revision" );
        var enabledNone = created with { Revision = 18UL, Nameplate = new LpTagSurfacePreference( true, "none" ) };
        Check( LpTagProfileRules.InspectStored( enabledNone, out var repairedNone, out _ ) == LpTagProfileInspectionStatus.RepairRequired && !repairedNone.Nameplate.Enabled && repairedNone.Nameplate.SelectedEffectKey == "none", failures, "enabled none is safely repaired to disabled none" );
        var future = created with { SchemaVersion = 2, Revision = 19UL };
        Check( LpTagProfileRules.InspectStored( future, out var futureCandidate, out var futureError ) == LpTagProfileInspectionStatus.UnsupportedReadOnly && futureCandidate == default && futureError == LpTagProfileError.UnsupportedSchema, failures, "future schema stays read-only without a write candidate" );
        var structurallyInvalid = created with { Revision = 20UL, Chat = new LpTagSurfacePreference( false, null! ) };
        Check( LpTagProfileRules.InspectStored( structurallyInvalid, out var corruptCandidate, out var corruptError ) == LpTagProfileInspectionStatus.UnsupportedReadOnly && corruptCandidate == default && corruptError == LpTagProfileError.CorruptRecord, failures, "structurally invalid typed record stays read-only without a write candidate" );
        CheckProfileMutationError( future, new LpTagMutationRequest( "00000000000000000000000000000010", 19UL, LpTagSurface.Chat, LpTagMutationKind.SetEnabled, false, string.Empty ), LpTagProfileError.UnsupportedSchema, failures, "future schema cannot be mutated" );
        CheckProfileMutationError( structurallyInvalid, new LpTagMutationRequest( "00000000000000000000000000000011", 20UL, LpTagSurface.Chat, LpTagMutationKind.SetEnabled, false, string.Empty ), LpTagProfileError.CorruptRecord, failures, "corrupt typed record cannot be mutated" );

        var payload = LpTagProfileRules.ToWritePayload( independent );
        Check( payload.SchemaVersion == independent.SchemaVersion && payload.Chat == independent.Chat && payload.Scoreboard == independent.Scoreboard && payload.Nameplate == independent.Nameplate, failures, "write payload copies schema and all three normalized surfaces" );
        Check( new LpTagProfileWritePayloadV1( payload.SchemaVersion, payload.Chat, payload.Scoreboard, payload.Nameplate ) == payload, failures, "write payload cannot carry a revision" );
        Check( new LpTagProfileWritePayloadV1( independent.SchemaVersion, independent.Chat, independent.Scoreboard, independent.Nameplate ) == payload, failures, "write payload contains only schema and three surfaces" );

        var privacyRecord = new LpStoredTagPreferencesV1( 1, 21UL, new LpTagSurfacePreference( false, "solid-red-v1" ), new LpTagSurfacePreference( true, "solid-green-v1" ), new LpTagSurfacePreference( true, "removed-effect-v1" ) );
        var publicStyle = LpTagProfileRules.ProjectPublic( privacyRecord );
        Check( publicStyle.ChatEffectId == LpTagEffectId.None && publicStyle.ScoreboardEffectId == LpTagEffectId.SolidGreen && publicStyle.NameplateEffectId == LpTagEffectId.None && publicStyle.Revision == 21UL, failures, "public projection exposes only effective IDs and committed revision" );
        Check( new LpEffectiveTagStyleV1( publicStyle.ChatEffectId, publicStyle.ScoreboardEffectId, publicStyle.NameplateEffectId, publicStyle.Revision ) == publicStyle, failures, "public projection has no stored effect keys" );

        var foundRecord = enabled with { Revision = 12UL };
        var foundDecision = LpTagProfileRules.ResolveLoad( new LpTagProfileLoadResult( LpTagProfileLoadStatus.Found, foundRecord, string.Empty ), out var foundCandidate, out var foundError );
        Check( foundDecision == LpTagProfileLoadDecision.InspectFound && foundCandidate == foundRecord && foundError == LpTagProfileError.None, failures, "found load preserves the stored record for typed inspection" );
        var notFoundDecision = LpTagProfileRules.ResolveLoad( new LpTagProfileLoadResult( LpTagProfileLoadStatus.NotFound, default, string.Empty ), out var newCandidate, out var notFoundError );
        Check( notFoundDecision == LpTagProfileLoadDecision.CreateNew && newCandidate == created && notFoundError == LpTagProfileError.None, failures, "only not-found resolves to a new profile candidate" );
        var unreadableDecision = LpTagProfileRules.ResolveLoad( new LpTagProfileLoadResult( LpTagProfileLoadStatus.Unreadable, enabled, "unreadable" ), out var unreadableCandidate, out var unreadableError );
        Check( unreadableDecision == LpTagProfileLoadDecision.UnreadableReadOnly && unreadableCandidate == default && unreadableError == LpTagProfileError.CorruptRecord, failures, "unreadable load ignores even a supplied typed value and remains non-writing" );
        var unavailableDecision = LpTagProfileRules.ResolveLoad( new LpTagProfileLoadResult( LpTagProfileLoadStatus.Unavailable, enabled, "unavailable" ), out var unavailableCandidate, out var unavailableError );
        Check( unavailableDecision == LpTagProfileLoadDecision.UnavailableReadOnly && unavailableCandidate == default && unavailableError == LpTagProfileError.StoreUnavailable, failures, "unavailable load maps to StoreUnavailable without a write candidate" );

        var expectedStoreRevision = 12UL;
        var storedCanonical = enabled with { Revision = 13UL };
        var acceptedStoreResult = new LpTagProfileWriteResult( LpTagProfileWriteStatus.Success, storedCanonical, string.Empty );
        Check( LpTagProfileRules.TryAcceptWriteResult( expectedStoreRevision, acceptedStoreResult, out var acceptedCanonical, out var acceptedError ) && acceptedCanonical == storedCanonical && acceptedError == LpTagProfileError.None, failures, "write success accepts exactly the store-returned expected revision plus one" );
        var inventedRevisionResult = new LpTagProfileWriteResult( LpTagProfileWriteStatus.Success, enabled with { Revision = expectedStoreRevision }, string.Empty );
        Check( !LpTagProfileRules.TryAcceptWriteResult( expectedStoreRevision, inventedRevisionResult, out var inventedCandidate, out var inventedError ) && inventedCandidate == default && inventedError == LpTagProfileError.CorruptRecord, failures, "write success rejects a locally unchanged or invented canonical revision" );
        var unavailableWriteResult = new LpTagProfileWriteResult( LpTagProfileWriteStatus.Unavailable, default, "unavailable" );
        Check( !LpTagProfileRules.TryAcceptWriteResult( expectedStoreRevision, unavailableWriteResult, out var unavailableWriteCandidate, out var unavailableWriteError ) && unavailableWriteCandidate == default && unavailableWriteError == LpTagProfileError.StoreUnavailable, failures, "unavailable write maps to StoreUnavailable without a canonical candidate" );
        Check( LpTagMessageLimits.RequestIdentifierLength == 32 && LpTagMessageLimits.MaximumRequestIdentifierLength == 32 && LpTagMessageLimits.MaximumEffectKeyLength == 32, failures, "message identifier and effect key bounds are exact" );
        Check( LpTagMessageLimits.MutationBurstCapacity == 8 && LpTagMessageLimits.MutationRefillPerSecond == 4d && LpTagMessageLimits.ReadBurstCapacity == 2 && LpTagMessageLimits.ReadRefillPerSecond == 1d, failures, "message rate limits are exact" );
        var mutationShapeProbe = new LpTagMutationRequest( "00000000000000000000000000000012", 0UL, LpTagSurface.Chat, LpTagMutationKind.SetEnabled, false, string.Empty );
        var readShapeProbe = new LpTagProfileReadRequest( "00000000000000000000000000000013" );
        Check( new LpTagMutationRequest( mutationShapeProbe.RequestIdentifier, mutationShapeProbe.ExpectedRevision, mutationShapeProbe.Surface, mutationShapeProbe.Kind, mutationShapeProbe.Enabled, mutationShapeProbe.EffectKey ) == mutationShapeProbe && new LpTagProfileReadRequest( readShapeProbe.RequestIdentifier ) == readShapeProbe, failures, "private client requests contain no target Steam ID" );
        Check( default(LpViewerTagPreferencesV1) == new LpViewerTagPreferencesV1( false, false, false ), failures, "viewer preferences default independently to false" );
    }

    private static void CheckReplay( List<string> failures )
    {
        var cache = new LpTagRequestReplayCache();
        var canonical = LpTagProfileRules.CreateNew();
        for ( var index = 0; index < 32; index++ )
        {
            var requestIdentifier = index.ToString( "x32" );
            cache.Remember( 7UL, requestIdentifier, new LpTagMutationResult( requestIdentifier, (ulong)index, LpTagMutationResultStatus.Success, canonical, LpTagProfileError.None, string.Empty ) );
        }

        Check( cache.CountFor( 7UL ) == 32, failures, "replay cache bounds each player to latest 32 responses" );
        var duplicateIdentifier = 0.ToString( "x32" );
        var replacement = new LpTagMutationResult( duplicateIdentifier, 99UL, LpTagMutationResultStatus.Conflict, canonical, LpTagProfileError.StaleRevision, string.Empty );
        cache.Remember( 7UL, duplicateIdentifier, replacement );
        Check( cache.CountFor( 7UL ) == 32 && cache.TryGet( 7UL, duplicateIdentifier, out var replaced ) && replaced == replacement, failures, "replay replacement updates the response without growing the cache" );
        var newestIdentifier = 32.ToString( "x32" );
        cache.Remember( 7UL, newestIdentifier, new LpTagMutationResult( newestIdentifier, 32UL, LpTagMutationResultStatus.Rejected, canonical, LpTagProfileError.InvalidEffectKey, string.Empty ) );
        Check( !cache.TryGet( 7UL, duplicateIdentifier, out _ ) && cache.TryGet( 7UL, 1.ToString( "x32" ), out _ ) && cache.TryGet( 7UL, newestIdentifier, out _ ), failures, "replay cache evicts the oldest insertion and retains the latest 32" );
        Check( cache.CountFor( 7UL ) == 32, failures, "replay replacement does not grow the cache" );
        Check( cache.CountFor( 8UL ) == 0 && !cache.TryGet( 8UL, newestIdentifier, out _ ), failures, "replay responses are isolated by SteamID64" );

        var mismatchKey = 33.ToString( "x32" );
        var mismatchResponseId = 34.ToString( "x32" );
        CheckReplayAdmissionRejected( cache, 7UL, mismatchKey, new LpTagMutationResult( mismatchResponseId, 33UL, LpTagMutationResultStatus.Success, canonical, LpTagProfileError.None, string.Empty ), failures, "request and response ID mismatch is rejected atomically" );
        Check( !cache.TryGet( 7UL, mismatchResponseId, out _ ), failures, "mismatched response ID is not inserted under its own key" );
        var guaranteedFailureId = 35.ToString( "x32" );
        CheckReplayAdmissionRejected( cache, 7UL, guaranteedFailureId, new LpTagMutationResult( guaranteedFailureId, 35UL, LpTagMutationResultStatus.GuaranteedFailure, canonical, LpTagProfileError.StoreUnavailable, string.Empty ), failures, "guaranteed failure is rejected atomically" );
        var uncertainId = 36.ToString( "x32" );
        CheckReplayAdmissionRejected( cache, 7UL, uncertainId, new LpTagMutationResult( uncertainId, 36UL, LpTagMutationResultStatus.Uncertain, canonical, LpTagProfileError.StoreUnavailable, string.Empty ), failures, "uncertain result is rejected atomically" );
        var noncanonicalId = 37.ToString( "x32" );
        CheckReplayAdmissionRejected( cache, 7UL, noncanonicalId, new LpTagMutationResult( noncanonicalId, 37UL, LpTagMutationResultStatus.Success, canonical with { SchemaVersion = 2 }, LpTagProfileError.None, string.Empty ), failures, "noncanonical response record is rejected atomically" );

        cache.RemovePlayer( 7UL );
        Check( cache.CountFor( 7UL ) == 0 && !cache.TryGet( 7UL, newestIdentifier, out _ ), failures, "disconnect removes all replay responses" );
    }

    private static void CheckReplayAdmissionRejected(
        LpTagRequestReplayCache cache,
        ulong steamId,
        string requestIdentifier,
        LpTagMutationResult response,
        List<string> failures,
        string label )
    {
        var countBefore = cache.CountFor( steamId );
        var hadPrior = cache.TryGet( steamId, requestIdentifier, out var prior );
        var rejected = false;
        try
        {
            cache.Remember( steamId, requestIdentifier, response );
        }
        catch ( ArgumentException )
        {
            rejected = true;
        }

        var hasAfter = cache.TryGet( steamId, requestIdentifier, out var after );
        Check( rejected && cache.CountFor( steamId ) == countBefore && hasAfter == hadPrior && ( !hasAfter || after == prior ), failures, label );
    }

    private static void CheckProfileMutationError(
        LpStoredTagPreferencesV1 current,
        LpTagMutationRequest request,
        LpTagProfileError expected,
        List<string> failures,
        string label )
    {
        Check( !LpTagProfileRules.TryApplyMutation( current, request, out var unchanged, out var actual ) && unchanged == current && actual == expected, failures, label );
    }

    private static void CheckMenuState( List<string> failures )
    {
        CheckMenuLoadGenerationAndDisposal( failures );
        CheckMenuChangedDisposalReentrancy( failures );
        CheckMenuIndependentEditsAndCoalescing( failures );
        CheckMenuTerminalMutationResults( failures );
        CheckMenuUncertainVerification( failures );
        CheckMenuReadOnlyAndViewerSeparation( failures );
    }

    private static void CheckMenuLoadGenerationAndDisposal( List<string> failures )
    {
        var client = new LpTagDevProfileClient( LpTagClientMode.Production );
        var viewer = new LpTagDevViewerPreferences();
        var state = new LpTagsMenuState( client, viewer );
        Check( client.Reads.Count == 1 && client.ReadSubscriberCount == 1 && client.MutationSubscriberCount == 1, failures, "menu starts one private read and subscribes once" );

        var staleRead = client.Reads[0];
        client.EmitRead( staleRead, LpTagProfileReadStatus.Success, LpTagProfileRules.CreateNew() );
        state.Refresh();
        var currentRead = client.Reads[1];
        Check( currentRead.Generation != staleRead.Generation, failures, "refresh creates a new load generation" );
        client.EmitRead( staleRead, LpTagProfileReadStatus.Success, LpTagProfileRules.CreateNew() with { Revision = 99UL } );
        Check( state.Status == LpTagClientStatus.Loading && !state.HasCanonicalProfile, failures, "stale load-generation callback is ignored" );
        client.EmitRead( currentRead, LpTagProfileReadStatus.Success, LpTagProfileRules.CreateNew() );
        Check( state.HasCanonicalProfile && state.CanEditOutgoing, failures, "current generation adopts the canonical profile" );

        state.Dispose();
        state.Dispose();
        Check( client.DisposeCount == 1 && client.ReadSubscriberCount == 0 && client.MutationSubscriberCount == 0 && viewer.SubscriberCount == 0, failures, "menu disposal unsubscribes and disposes its owned client exactly once" );
        var disposedGeneration = state.LoadGeneration;
        client.EmitRead( currentRead, LpTagProfileReadStatus.Success, LpTagProfileRules.CreateNew() with { Revision = 99UL } );
        Check( state.LoadGeneration == disposedGeneration && state.Canonical.Revision == 0UL, failures, "callbacks cannot mutate a disposed menu" );
    }

    private static void CheckMenuChangedDisposalReentrancy( List<string> failures )
    {
        var refreshClient = new LpTagDevProfileClient( LpTagClientMode.Production );
        var refreshState = new LpTagsMenuState( refreshClient, new LpTagDevViewerPreferences() );
        refreshClient.EmitRead( refreshClient.Reads[0], LpTagProfileReadStatus.Success, LpTagProfileRules.CreateNew() );
        refreshState.Changed += refreshState.Dispose;
        refreshState.Refresh();
        Check( refreshClient.Reads.Count == 1 && refreshClient.DisposeCount == 1 && refreshClient.ReadSubscriberCount == 0, failures, "Refresh revalidates after Changed disposal and never calls the disposed client" );

        using ( var mutationState = CreateLoadedMenu( LpTagClientMode.Production, LpTagProfileRules.CreateNew(), out var mutationClient, out _ ) )
        {
            mutationState.Changed += mutationState.Dispose;
            mutationState.SelectEffect( LpTagSurface.Chat, "solid-red-v1" );
            Check( mutationClient.Mutations.Count == 0 && mutationClient.DisposeCount == 1 && mutationClient.MutationSubscriberCount == 0, failures, "mutation dispatch revalidates its active operation after Changed disposal" );
        }

        using ( var verificationState = CreateLoadedMenu( LpTagClientMode.Production, LpTagProfileRules.CreateNew(), out var verificationClient, out _ ) )
        {
            verificationState.SelectEffect( LpTagSurface.Chat, "solid-red-v1" );
            verificationState.Changed += verificationState.Dispose;
            verificationClient.EmitMutation( verificationClient.Mutations[0], LpTagMutationResultStatus.Uncertain, default, LpTagProfileError.StoreUnavailable, string.Empty );
            Check( verificationClient.Reads.Count == 1 && verificationClient.DisposeCount == 1 && verificationClient.ReadSubscriberCount == 0, failures, "uncertain verification revalidates its exact read after Changed disposal" );
        }
    }

    private static void CheckMenuIndependentEditsAndCoalescing( List<string> failures )
    {
        using ( var state = CreateLoadedMenu( LpTagClientMode.Production, LpTagProfileRules.CreateNew(), out var client, out _ ) )
        {
            state.SelectSurface( LpTagSurface.Scoreboard );
            state.SelectEffect( LpTagSurface.Chat, "solid-red-v1" );
            state.SelectEffect( LpTagSurface.Scoreboard, "solid-green-v1" );
            state.SelectEffect( LpTagSurface.Nameplate, "solid-yellow-v1" );
            Check( state.SelectedSurface == LpTagSurface.Scoreboard, failures, "surface selection changes only the editing context" );
            Check( state.Draft.Chat.SelectedEffectKey == "solid-red-v1" && !state.Draft.Chat.Enabled && state.Draft.Scoreboard.SelectedEffectKey == "solid-green-v1" && !state.Draft.Scoreboard.Enabled && state.Draft.Nameplate.SelectedEffectKey == "solid-yellow-v1" && !state.Draft.Nameplate.Enabled, failures, "all three surface selections remain independent and do not enable any surface" );
            state.SetEnabled( LpTagSurface.Chat, true );
            state.SetEnabled( LpTagSurface.Scoreboard, true );
            state.SetEnabled( LpTagSurface.Nameplate, true );
            Check( state.Draft.Chat.Enabled && state.Draft.Scoreboard.Enabled && state.Draft.Nameplate.Enabled && state.Draft.Chat.SelectedEffectKey == "solid-red-v1" && state.Draft.Scoreboard.SelectedEffectKey == "solid-green-v1" && state.Draft.Nameplate.SelectedEffectKey == "solid-yellow-v1", failures, "all three enabled fields edit independently without changing selections" );
            Check( client.Mutations.Count == 1 && state.QueuedDifferenceCount == 5, failures, "only one mutation is active while five other surface-and-field differences queue" );
        }

        var enabledChat = LpTagProfileRules.CreateNew() with
        {
            Revision = 5UL,
            Chat = new LpTagSurfacePreference( true, "solid-red-v1" )
        };
        using ( var state = CreateLoadedMenu( LpTagClientMode.Production, enabledChat, out var client, out _ ) )
        {
            state.SetEnabled( LpTagSurface.Chat, false );
            Check( !state.Draft.Chat.Enabled && state.Draft.Chat.SelectedEffectKey == "solid-red-v1", failures, "turning a surface off preserves its selected effect" );
            Check( client.Mutations.Count == 1 && client.Mutations[0].Request.Kind == LpTagMutationKind.SetEnabled && !client.Mutations[0].Request.Enabled, failures, "off dispatch changes only the enabled field" );
        }

        using ( var state = CreateLoadedMenu( LpTagClientMode.Production, LpTagProfileRules.CreateNew(), out var client, out _ ) )
        {
            state.SetEnabled( LpTagSurface.Chat, true );
            Check( client.Mutations.Count == 0 && state.StatusCopy == "Choose a style first", failures, "enabling none is rejected locally with usable copy" );
        }

        using ( var state = CreateLoadedMenu( LpTagClientMode.Production, LpTagProfileRules.CreateNew(), out var client, out _ ) )
        {
            state.SelectEffect( LpTagSurface.Chat, "solid-red-v1" );
            state.SelectEffect( LpTagSurface.Chat, "rainbow-wave-v1" );
            state.SelectEffect( LpTagSurface.Chat, "solid-yellow-v1" );
            Check( client.Mutations.Count == 1 && state.QueuedDifferenceCount == 1 && state.Draft.Chat.SelectedEffectKey == "solid-yellow-v1", failures, "queued surface-field edits coalesce to the latest desired value" );
            var first = client.Mutations[0];
            var firstCanonical = LpTagProfileRules.CreateNew() with { Revision = 1UL, Chat = new LpTagSurfacePreference( false, "solid-red-v1" ) };
            client.EmitMutation( first with { Generation = first.Generation + 1UL }, LpTagMutationResultStatus.Success, firstCanonical );
            Check( state.HasActiveMutation && client.Mutations.Count == 1 && state.Canonical.Revision == 0UL, failures, "stale mutation generation cannot complete the active request" );
            var wrongRequest = first.Request with { RequestIdentifier = 999.ToString( "x32" ) };
            client.EmitMutation( first with { Request = wrongRequest }, LpTagMutationResultStatus.Success, firstCanonical );
            Check( state.HasActiveMutation && client.Mutations.Count == 1 && state.Canonical.Revision == 0UL, failures, "mismatched mutation request ID cannot complete the active request" );
            client.EmitMutation( first, LpTagMutationResultStatus.Success, firstCanonical );
            Check( client.Mutations.Count == 2 && client.Mutations[1].Request.ExpectedRevision == 1UL && client.Mutations[1].Request.EffectKey == "solid-yellow-v1", failures, "coalesced follow-up derives from the returned canonical revision" );
            var secondCanonical = firstCanonical with { Revision = 2UL, Chat = new LpTagSurfacePreference( false, "solid-yellow-v1" ) };
            client.EmitMutation( client.Mutations[1], LpTagMutationResultStatus.Success, secondCanonical );
            Check( state.Status == LpTagClientStatus.Saved && state.StatusCopy == "Saved globally" && state.QueuedDifferenceCount == 0 && state.Draft == state.Canonical, failures, "production reports global success only after the queue drains with draft metadata rebased to canonical" );
        }

        using ( var state = CreateLoadedMenu( LpTagClientMode.LocalPreview, LpTagProfileRules.CreateNew(), out var client, out _ ) )
        {
            state.SelectEffect( LpTagSurface.Chat, "solid-red-v1" );
            var applied = LpTagProfileRules.CreateNew() with { Revision = 1UL, Chat = new LpTagSurfacePreference( false, "solid-red-v1" ) };
            client.EmitMutation( client.Mutations[0], LpTagMutationResultStatus.Success, applied );
            Check( state.Status == LpTagClientStatus.LocalPreview && state.StatusCopy == "LOCAL PREVIEW · Applied for this session" && state.Draft == state.Canonical && !state.IsOnline, failures, "local success drains with equal draft/canonical metadata and never reports ONLINE or Saved globally" );
        }
    }

    private static void CheckMenuTerminalMutationResults( List<string> failures )
    {
        CheckCompleteLatestDraftRetry( LpTagMutationResultStatus.Rejected, 0UL, failures, "rejected" );
        CheckCompleteLatestDraftRetry( LpTagMutationResultStatus.Conflict, 7UL, failures, "conflict" );
        CheckCompleteLatestDraftRetry( LpTagMutationResultStatus.GuaranteedFailure, 0UL, failures, "guaranteed failure" );

        var revisionFive = LpTagProfileRules.CreateNew() with { Revision = 5UL };
        using ( var state = CreateLoadedMenu( LpTagClientMode.Production, revisionFive, out var client, out _ ) )
        {
            state.SelectEffect( LpTagSurface.Chat, "solid-red-v1" );
            client.EmitMutation( client.Mutations[0], LpTagMutationResultStatus.Conflict, revisionFive, LpTagProfileError.StaleRevision, string.Empty );
            Check( state.Canonical == revisionFive && state.Status == LpTagClientStatus.SaveFailed && state.StatusCopy == "Save failed · Retry" && state.CanRetry && client.Mutations.Count == 1, failures, "non-newer conflict record cannot roll canonical state backward or trigger automatic resend" );
        }
    }

    private static void CheckCompleteLatestDraftRetry(
        LpTagMutationResultStatus terminalStatus,
        ulong retryBaseRevision,
        List<string> failures,
        string label )
    {
        var prior = LpTagProfileRules.CreateNew();
        using var state = CreateLoadedMenu( LpTagClientMode.Production, prior, out var client, out _ );
        state.SelectEffect( LpTagSurface.Chat, "solid-red-v1" );
        state.SelectEffect( LpTagSurface.Chat, "rainbow-wave-v1" );
        state.SelectEffect( LpTagSurface.Scoreboard, "solid-green-v1" );
        state.SetEnabled( LpTagSurface.Scoreboard, true );
        state.SelectEffect( LpTagSurface.Nameplate, "solid-yellow-v1" );
        Check( client.Mutations.Count == 1 && state.QueuedDifferenceCount == 4, failures, label + " retains four latest surface-and-field differences behind one active request" );
        if ( client.Mutations.Count < 1 )
            return;

        var initial = client.Mutations[0];
        var latestDraft = state.Draft;
        var returned = prior with { Revision = retryBaseRevision };
        if ( terminalStatus == LpTagMutationResultStatus.Rejected )
        {
            client.EmitMutation( initial, terminalStatus, prior, LpTagProfileError.InvalidEffectKey, "Style is not available" );
        }
        else if ( terminalStatus == LpTagMutationResultStatus.Conflict )
        {
            client.EmitMutation( initial, terminalStatus, returned, LpTagProfileError.StaleRevision, string.Empty );
        }
        else
        {
            client.EmitMutation( initial, terminalStatus, default, LpTagProfileError.StoreUnavailable, string.Empty );
        }

        var expectedCopy = terminalStatus switch
        {
            LpTagMutationResultStatus.Rejected => "Style is not available",
            LpTagMutationResultStatus.Conflict => "Profile changed elsewhere",
            _ => "Save failed · Retry"
        };
        Check( state.CanRetry && !state.HasActiveMutation && state.QueuedDifferenceCount == 0 && client.Mutations.Count == 1 && state.Draft == state.Canonical && state.StatusCopy == expectedCopy, failures, label + " restores canonical controls and never resends the complete latest draft automatically" );
        var identifiers = new HashSet<string>( StringComparer.Ordinal ) { initial.Request.RequestIdentifier };
        state.Retry();
        Check( state.Draft.Chat.SelectedEffectKey == latestDraft.Chat.SelectedEffectKey && state.Draft.Scoreboard == latestDraft.Scoreboard && state.Draft.Nameplate.SelectedEffectKey == latestDraft.Nameplate.SelectedEffectKey, failures, label + " Retry restores the newest values from every retained surface-and-field key" );
        Check( client.Mutations.Count == 2, failures, label + " Retry dispatches exactly one first request" );
        if ( client.Mutations.Count < 2 )
            return;

        var chatRequest = client.Mutations[1].Request;
        Check( chatRequest.ExpectedRevision == retryBaseRevision && chatRequest.Surface == LpTagSurface.Chat && chatRequest.Kind == LpTagMutationKind.SetEffect && chatRequest.EffectKey == "rainbow-wave-v1" && IsLowerHexIdentifier( chatRequest.RequestIdentifier ) && identifiers.Add( chatRequest.RequestIdentifier ), failures, label + " Retry starts one fresh Chat-effect request from the current canonical revision" );
        var chatCanonical = returned with { Revision = retryBaseRevision + 1UL, Chat = new LpTagSurfacePreference( false, "rainbow-wave-v1" ) };
        client.EmitMutation( client.Mutations[1], LpTagMutationResultStatus.Success, chatCanonical );
        Check( client.Mutations.Count == 3, failures, label + " Retry dispatches exactly one request after Chat success" );
        if ( client.Mutations.Count < 3 )
            return;

        var scoreboardEffectRequest = client.Mutations[2].Request;
        Check( scoreboardEffectRequest.ExpectedRevision == retryBaseRevision + 1UL && scoreboardEffectRequest.Surface == LpTagSurface.Scoreboard && scoreboardEffectRequest.Kind == LpTagMutationKind.SetEffect && scoreboardEffectRequest.EffectKey == "solid-green-v1" && IsLowerHexIdentifier( scoreboardEffectRequest.RequestIdentifier ) && identifiers.Add( scoreboardEffectRequest.RequestIdentifier ), failures, label + " Retry advances one fresh Scoreboard-effect request from the accepted revision" );
        var scoreboardEffectCanonical = chatCanonical with { Revision = retryBaseRevision + 2UL, Scoreboard = new LpTagSurfacePreference( false, "solid-green-v1" ) };
        client.EmitMutation( client.Mutations[2], LpTagMutationResultStatus.Success, scoreboardEffectCanonical );
        Check( client.Mutations.Count == 4, failures, label + " Retry dispatches exactly one request after Scoreboard-effect success" );
        if ( client.Mutations.Count < 4 )
            return;

        var scoreboardEnabledRequest = client.Mutations[3].Request;
        Check( scoreboardEnabledRequest.ExpectedRevision == retryBaseRevision + 2UL && scoreboardEnabledRequest.Surface == LpTagSurface.Scoreboard && scoreboardEnabledRequest.Kind == LpTagMutationKind.SetEnabled && scoreboardEnabledRequest.Enabled && IsLowerHexIdentifier( scoreboardEnabledRequest.RequestIdentifier ) && identifiers.Add( scoreboardEnabledRequest.RequestIdentifier ), failures, label + " Retry advances one fresh Scoreboard-enabled request from the accepted revision" );
        var scoreboardEnabledCanonical = scoreboardEffectCanonical with { Revision = retryBaseRevision + 3UL, Scoreboard = new LpTagSurfacePreference( true, "solid-green-v1" ) };
        client.EmitMutation( client.Mutations[3], LpTagMutationResultStatus.Success, scoreboardEnabledCanonical );
        Check( client.Mutations.Count == 5, failures, label + " Retry dispatches exactly one request after Scoreboard-enabled success" );
        if ( client.Mutations.Count < 5 )
            return;

        var nameplateRequest = client.Mutations[4].Request;
        Check( nameplateRequest.ExpectedRevision == retryBaseRevision + 3UL && nameplateRequest.Surface == LpTagSurface.Nameplate && nameplateRequest.Kind == LpTagMutationKind.SetEffect && nameplateRequest.EffectKey == "solid-yellow-v1" && IsLowerHexIdentifier( nameplateRequest.RequestIdentifier ) && identifiers.Add( nameplateRequest.RequestIdentifier ), failures, label + " Retry advances one fresh Nameplate-effect request from the accepted revision" );
        var finalCanonical = scoreboardEnabledCanonical with { Revision = retryBaseRevision + 4UL, Nameplate = new LpTagSurfacePreference( false, "solid-yellow-v1" ) };
        client.EmitMutation( client.Mutations[4], LpTagMutationResultStatus.Success, finalCanonical );
        Check( client.Mutations.Count == 5 && identifiers.Count == 5 && state.Canonical == finalCanonical && state.Draft == state.Canonical && state.QueuedDifferenceCount == 0 && state.StatusCopy == "Saved globally", failures, label + " Retry drains the complete latest draft one request at a time with fresh IDs and equal final state" );
    }

    private static void CheckMenuUncertainVerification( List<string> failures )
    {
        var prior = LpTagProfileRules.CreateNew();
        var candidate = prior with { Revision = 1UL, Chat = new LpTagSurfacePreference( false, "solid-red-v1" ) };

        using ( var state = CreateLoadedMenu( LpTagClientMode.Production, prior, out var client, out _ ) )
        {
            state.SelectEffect( LpTagSurface.Chat, "solid-red-v1" );
            state.SetEnabled( LpTagSurface.Chat, true );
            client.EmitMutation( client.Mutations[0], LpTagMutationResultStatus.Uncertain, default, LpTagProfileError.StoreUnavailable, string.Empty );
            Check( state.Status == LpTagClientStatus.Verifying && !state.CanEditOutgoing && client.Reads.Count == 2, failures, "uncertain mutation freezes outgoing controls and starts a fresh verification read" );
            client.EmitRead( client.Reads[1], LpTagProfileReadStatus.Success, candidate );
            Check( state.Canonical == candidate && state.Draft.Revision == candidate.Revision && state.Draft.SchemaVersion == candidate.SchemaVersion && state.Draft.Chat.Enabled && client.Mutations.Count == 2 && client.Mutations[1].Request.ExpectedRevision == candidate.Revision && client.Mutations[1].Request.Kind == LpTagMutationKind.SetEnabled, failures, "candidate verification rebases draft metadata while preserving and resuming later intent" );
            var enabledCandidate = candidate with { Revision = 2UL, Chat = new LpTagSurfacePreference( true, "solid-red-v1" ) };
            client.EmitMutation( client.Mutations[1], LpTagMutationResultStatus.Success, enabledCandidate );
            Check( state.Canonical == enabledCandidate && state.Draft == state.Canonical && state.QueuedDifferenceCount == 0 && state.StatusCopy == "Saved globally", failures, "candidate verification queue drains with draft equal to accepted canonical" );
        }

        using ( var state = CreateLoadedMenu( LpTagClientMode.Production, prior, out var client, out _ ) )
        {
            state.SelectEffect( LpTagSurface.Chat, "solid-red-v1" );
            client.EmitMutation( client.Mutations[0], LpTagMutationResultStatus.Uncertain, default, LpTagProfileError.StoreUnavailable, string.Empty );
            client.EmitRead( client.Reads[1], LpTagProfileReadStatus.Success, prior );
            Check( state.Canonical == prior && state.Draft == prior && state.CanRetry && client.Mutations.Count == 1 && state.StatusCopy == "Save failed · Retry", failures, "unchanged verification retains Retry state without automatic resend" );
        }

        var different = prior with { Revision = 4UL, Nameplate = new LpTagSurfacePreference( false, "solid-blue-v1" ) };
        using ( var state = CreateLoadedMenu( LpTagClientMode.Production, prior, out var client, out _ ) )
        {
            state.SelectEffect( LpTagSurface.Chat, "solid-red-v1" );
            client.EmitMutation( client.Mutations[0], LpTagMutationResultStatus.Uncertain, default, LpTagProfileError.StoreUnavailable, string.Empty );
            client.EmitRead( client.Reads[1], LpTagProfileReadStatus.Success, different );
            Check( state.Canonical == different && state.Draft == different && state.CanRetry && client.Mutations.Count == 1 && state.StatusCopy == "Profile changed elsewhere", failures, "different verification canonical is adopted without automatic resend" );
        }

        using ( var state = CreateLoadedMenu( LpTagClientMode.Production, prior, out var client, out _ ) )
        {
            state.SelectEffect( LpTagSurface.Chat, "solid-red-v1" );
            var oldRequestIdentifier = client.Mutations[0].Request.RequestIdentifier;
            client.EmitMutation( client.Mutations[0], LpTagMutationResultStatus.Uncertain, default, LpTagProfileError.StoreUnavailable, string.Empty );
            client.EmitRead( client.Reads[1], LpTagProfileReadStatus.Unavailable, default );
            Check( state.Canonical == prior && state.Draft == prior && !state.CanRetry && state.CanRefresh && client.Mutations.Count == 1 && state.StatusCopy == "Save outcome unresolved · Refresh required", failures, "unavailable verification never claims success and requires Refresh before Retry" );
            state.Refresh();
            var refreshed = prior with { Revision = 3UL };
            client.EmitRead( client.Reads[2], LpTagProfileReadStatus.Success, refreshed );
            Check( state.CanRetry && client.Mutations.Count == 1, failures, "successful Refresh unlocks the retained mutation Retry without resending" );
            state.Retry();
            Check( client.Mutations.Count == 2 && client.Mutations[1].Request.RequestIdentifier != oldRequestIdentifier && client.Mutations[1].Request.ExpectedRevision == refreshed.Revision, failures, "post-verification Retry uses a new ID and refreshed revision" );
        }
    }

    private static void CheckMenuReadOnlyAndViewerSeparation( List<string> failures )
    {
        var future = LpTagProfileRules.CreateNew() with
        {
            SchemaVersion = LpTagProfileRules.SchemaVersion + 1,
            Revision = 12UL,
            Chat = new LpTagSurfacePreference( true, "solid-red-v1" )
        };
        var unsupportedClient = new LpTagDevProfileClient( LpTagClientMode.Production );
        var unsupportedViewer = new LpTagDevViewerPreferences();
        using ( var state = new LpTagsMenuState( unsupportedClient, unsupportedViewer ) )
        {
            unsupportedClient.EmitRead( unsupportedClient.Reads[0], LpTagProfileReadStatus.UnsupportedReadOnly, future );
            Check( state.IsReadOnly && !state.CanRetry && !state.CanEditOutgoing && IsPlainProfile( state.Draft ) && state.StatusCopy == "Profile schema is unsupported · Read-only", failures, "unsupported schema exposes only an all-off plain read-only view" );
            state.SetReduceMotion( true );
            Check( state.ViewerPreferences.ReduceMotion && unsupportedClient.Mutations.Count == 0, failures, "Viewing remains usable in unsupported read-only state" );
        }

        var unreadableClient = new LpTagDevProfileClient( LpTagClientMode.Production );
        using ( var state = new LpTagsMenuState( unreadableClient, new LpTagDevViewerPreferences() ) )
        {
            unreadableClient.EmitRead( unreadableClient.Reads[0], LpTagProfileReadStatus.UnreadableReadOnly, default );
            Check( state.IsReadOnly && !state.CanRetry && IsPlainProfile( state.Canonical ) && state.StatusCopy == "Stored profile is unreadable · Read-only", failures, "unreadable stored data is distinct and never becomes a default mutation" );
        }

        using ( var state = CreateLoadedMenu( LpTagClientMode.Production, LpTagProfileRules.CreateNew(), out var client, out _ ) )
        {
            state.SelectEffect( LpTagSurface.Chat, "solid-red-v1" );
            client.EmitMutation( client.Mutations[0], LpTagMutationResultStatus.Uncertain, default, LpTagProfileError.StoreUnavailable, string.Empty );
            client.EmitRead( client.Reads[1], LpTagProfileReadStatus.UnreadableReadOnly, default );
            Check( state.IsReadOnly && !state.CanRetry && !state.HasActiveMutation && state.QueuedDifferenceCount == 0 && IsPlainProfile( state.Draft ), failures, "verification read-only result clears all automatic and Retry mutation intent" );
        }

        using ( var state = CreateLoadedMenu( LpTagClientMode.Production, LpTagProfileRules.CreateNew(), out var client, out var viewer ) )
        {
            state.SetReduceMotion( true );
            state.SetDisableAnimations( true );
            state.SetHideDotPlusDecorations( true );
            Check( state.ViewerPreferences == new LpViewerTagPreferencesV1( true, true, true ) && viewer.WriteCount == 3 && client.Mutations.Count == 0 && state.Canonical == LpTagProfileRules.CreateNew() && state.Draft == state.Canonical, failures, "viewer settings stay local and never enter owner/public mutation state" );
            state.SetHideDotPlusDecorations( true );
            Check( viewer.WriteCount == 3, failures, "setting the same typed viewer value is a no-op" );
        }

        using ( var unavailable = new LpTagUnavailableProfileClient() )
        {
            LpTagProfileReadResult read = default;
            LpTagMutationResult mutation = default;
            unavailable.ReadCompleted += result => read = result;
            unavailable.MutationCompleted += result => mutation = result;
            var readIdentifier = 97.ToString( "x32" );
            var mutationIdentifier = 98.ToString( "x32" );
            unavailable.ReadOwnProfile( new LpTagProfileReadRequest( readIdentifier ), 88UL );
            unavailable.MutateOwnProfile( new LpTagMutationRequest( mutationIdentifier, 0UL, LpTagSurface.Chat, LpTagMutationKind.SetEnabled, false, string.Empty ), 89UL );
            Check( read.RequestIdentifier == readIdentifier && read.MenuGeneration == 88UL && read.Status == LpTagProfileReadStatus.Unavailable && mutation.RequestIdentifier == mutationIdentifier && mutation.MenuGeneration == 89UL && mutation.Status == LpTagMutationResultStatus.GuaranteedFailure, failures, "unavailable client preserves response identity and never accepts a mutation" );
        }

        var unavailableViewer = new LpTagDevViewerPreferences();
        using var unavailableState = new LpTagsMenuState( new LpTagUnavailableProfileClient(), unavailableViewer );
        unavailableState.SetReduceMotion( true );
        Check( unavailableState.Status == LpTagClientStatus.Unavailable && unavailableState.StatusCopy == "Integration unavailable" && unavailableState.CanRefresh && unavailableState.ViewerPreferences.ReduceMotion && !unavailableState.IsOnline && unavailableState.StatusCopy != "Saved globally", failures, "unavailable menu keeps Viewing and Refresh usable without reporting ONLINE or global success" );
    }

    private static LpTagsMenuState CreateLoadedMenu(
        LpTagClientMode mode,
        LpStoredTagPreferencesV1 canonical,
        out LpTagDevProfileClient client,
        out LpTagDevViewerPreferences viewer )
    {
        client = new LpTagDevProfileClient( mode );
        viewer = new LpTagDevViewerPreferences();
        var state = new LpTagsMenuState( client, viewer );
        client.EmitRead( client.Reads[0], LpTagProfileReadStatus.Success, canonical );
        return state;
    }

    private static bool IsPlainProfile( LpStoredTagPreferencesV1 profile )
    {
        return !profile.Chat.Enabled && profile.Chat.SelectedEffectKey == LpTagProfileRules.NoneKey &&
            !profile.Scoreboard.Enabled && profile.Scoreboard.SelectedEffectKey == LpTagProfileRules.NoneKey &&
            !profile.Nameplate.Enabled && profile.Nameplate.SelectedEffectKey == LpTagProfileRules.NoneKey;
    }

    private static bool IsLowerHexIdentifier( string value )
    {
        if ( value is null || value.Length != LpTagMessageLimits.RequestIdentifierLength )
            return false;

        for ( var index = 0; index < value.Length; index++ )
        {
            var character = value[index];
            var isDecimal = character >= '0' && character <= '9';
            var isLowerHexLetter = character >= 'a' && character <= 'f';
            if ( !isDecimal && !isLowerHexLetter )
                return false;
        }

        return true;
    }

    private sealed class LpTagDevProfileClient : ILpTagProfileClient
    {
        private Action<LpTagProfileReadResult>? _readCompleted;
        private Action<LpTagMutationResult>? _mutationCompleted;

        public LpTagDevProfileClient( LpTagClientMode mode )
        {
            Mode = mode;
        }

        public LpTagClientMode Mode { get; }
        public List<DevRead> Reads { get; } = new();
        public List<DevMutation> Mutations { get; } = new();
        public int DisposeCount { get; private set; }
        public int ReadSubscriberCount => _readCompleted?.GetInvocationList().Length ?? 0;
        public int MutationSubscriberCount => _mutationCompleted?.GetInvocationList().Length ?? 0;

        public event Action<LpTagProfileReadResult>? ReadCompleted
        {
            add => _readCompleted += value;
            remove => _readCompleted -= value;
        }

        public event Action<LpTagMutationResult>? MutationCompleted
        {
            add => _mutationCompleted += value;
            remove => _mutationCompleted -= value;
        }

        public void ReadOwnProfile( LpTagProfileReadRequest request, ulong menuGeneration )
        {
            Reads.Add( new DevRead( request, menuGeneration ) );
        }

        public void MutateOwnProfile( LpTagMutationRequest request, ulong menuGeneration )
        {
            Mutations.Add( new DevMutation( request, menuGeneration ) );
        }

        public void EmitRead( DevRead read, LpTagProfileReadStatus status, LpStoredTagPreferencesV1 canonical, string filteredError = "" )
        {
            _readCompleted?.Invoke( new LpTagProfileReadResult( read.Request.RequestIdentifier, read.Generation, status, canonical, filteredError ) );
        }

        public void EmitMutation(
            DevMutation mutation,
            LpTagMutationResultStatus status,
            LpStoredTagPreferencesV1 canonical,
            LpTagProfileError error = LpTagProfileError.None,
            string filteredError = "" )
        {
            _mutationCompleted?.Invoke( new LpTagMutationResult( mutation.Request.RequestIdentifier, mutation.Generation, status, canonical, error, filteredError ) );
        }

        public void Dispose()
        {
            DisposeCount++;
        }

        internal readonly record struct DevRead( LpTagProfileReadRequest Request, ulong Generation );
        internal readonly record struct DevMutation( LpTagMutationRequest Request, ulong Generation );
    }

    private sealed class LpTagDevViewerPreferences : ILpTagViewerPreferenceEditor
    {
        private Action<LpViewerTagPreferencesV1>? _changed;

        public LpViewerTagPreferencesV1 Current { get; private set; }
        public int WriteCount { get; private set; }
        public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;

        public event Action<LpViewerTagPreferencesV1>? Changed
        {
            add => _changed += value;
            remove => _changed -= value;
        }

        public void SetReduceMotion( bool value )
        {
            Set( Current with { ReduceMotion = value } );
        }

        public void SetDisableAnimations( bool value )
        {
            Set( Current with { DisableAnimations = value } );
        }

        public void SetHideDotPlusDecorations( bool value )
        {
            Set( Current with { HideDotPlusDecorations = value } );
        }

        private void Set( LpViewerTagPreferencesV1 value )
        {
            if ( value == Current )
                return;

            Current = value;
            WriteCount++;
            _changed?.Invoke( Current );
        }
    }

    private static void CheckUnicode( List<string> failures )
    {
        var nullAnalysis = LpTagTextSegmentation.Analyze( null );
        Check( nullAnalysis.OriginalText == string.Empty && nullAnalysis.GraphemeCount == 0 && !nullAnalysis.RequiresWholeNameFallback, failures, "null input returns the empty analysis" );
        CheckAnalysis( string.Empty, 0, false, failures, "empty input" );
        CheckAnalysis( "e\u0301", 1, false, failures, "combining accent" );
        CheckAnalysis( "\U0001f600", 1, false, failures, "surrogate pair" );
        CheckAnalysis( "\U0001f468\u200d\U0001f469\u200d\U0001f467\u200d\U0001f466", 1, false, failures, "ZWJ family" );
        CheckAnalysis( "\U0001f44d\U0001f3fd", 1, false, failures, "skin tone" );
        CheckAnalysis( "\U0001f1fa\U0001f1f8", 1, false, failures, "flag" );
        CheckAnalysis( "مرحبا", 5, true, failures, "Arabic fallback" );
        CheckAnalysis( "שלום", 4, true, failures, "Hebrew fallback" );
        CheckAnalysis( "A\u202eB", 3, true, failures, "bidi-control fallback" );
        CheckAnalysis( "नमस्ते", 3, true, failures, "Indic fallback" );
        CheckAnalysis( "한글", 2, false, failures, "Hangul" );
        CheckAnalysis( "A\u061cB", 3, true, failures, "Arabic letter mark fallback" );
        CheckAnalysis( "A\u200eB", 3, true, failures, "left-to-right mark fallback" );
        CheckAnalysis( "A\u200fB", 3, true, failures, "right-to-left mark fallback" );
        CheckAnalysis( "A\u202aB", 3, true, failures, "bidi embedding fallback" );
        CheckAnalysis( "A\u2066B", 3, true, failures, "bidi isolate fallback" );
        CheckAnalysis( "\u0590", 1, true, failures, "contextual range U+0590 fallback" );
        CheckAnalysis( "\u08ff", 1, true, failures, "contextual range U+08FF fallback" );
        CheckAnalysis( "\u0900", 1, true, failures, "contextual range U+0900 fallback" );
        CheckAnalysis( "\u0dff", 1, true, failures, "contextual range U+0DFF fallback" );
        CheckAnalysis( "\ufb1d", 1, true, failures, "contextual range U+FB1D fallback" );
        CheckAnalysis( "\ufdff", 1, true, failures, "contextual range U+FDFF fallback" );
        CheckAnalysis( "\ufe70", 1, true, failures, "contextual range U+FE70 fallback" );
        CheckAnalysis( "\ufeff", 1, true, failures, "contextual range U+FEFF fallback" );
        var unpairedHigh = LpTagTextSegmentation.Analyze( "\ud800" );
        Check( unpairedHigh.OriginalText == "\ud800" && unpairedHigh.RequiresWholeNameFallback, failures, "unpaired high surrogate falls back without rewriting" );
        var unpairedLow = LpTagTextSegmentation.Analyze( "\udc00" );
        Check( unpairedLow.OriginalText == "\udc00" && unpairedLow.RequiresWholeNameFallback, failures, "unpaired low surrogate falls back without rewriting" );
        CheckAnalysis( new string( 'a', 64 ), 64, false, failures, "64 grapheme boundary" );
        var overGraphemeLimit = LpTagTextSegmentation.Analyze( new string( 'a', 65 ) );
        Check( overGraphemeLimit.RequiresWholeNameFallback, failures, "more than 64 graphemes falls back" );
        Check( overGraphemeLimit.GraphemeCount == 65 && overGraphemeLimit.Elements.Length == 64, failures, "over-limit analysis keeps an exact count with bounded element storage" );
        CheckAnalysis( Repeat( "\U0001f44d\U0001f3fd", 32 ), 32, false, failures, "256 UTF-8 byte boundary" );
        var overByteLimitText = Repeat( "\U0001f44d\U0001f3fd", 33 );
        CheckAnalysis( overByteLimitText, 33, true, failures, "more than 256 UTF-8 bytes with at most 64 graphemes" );

        CheckElements( "e\u0301", failures, "combining-accent boundaries", new LpTagTextElement( 0, 2 ) );
        CheckElements( "\U0001f600", failures, "surrogate-pair boundaries", new LpTagTextElement( 0, 2 ) );
        CheckElements( "\U0001f468\u200d\U0001f469\u200d\U0001f467\u200d\U0001f466", failures, "ZWJ-family boundaries", new LpTagTextElement( 0, 11 ) );
        CheckElements( "\U0001f44d\U0001f3fd", failures, "skin-tone boundaries", new LpTagTextElement( 0, 4 ) );
        CheckElements( "\U0001f1fa\U0001f1f8", failures, "flag boundaries", new LpTagTextElement( 0, 4 ) );
        CheckElements( "한글", failures, "Hangul boundaries", new LpTagTextElement( 0, 1 ), new LpTagTextElement( 1, 1 ) );

        var originalIdentity = string.Concat( "Life", "Punch", "\U0001f600" );
        var unchangedAnalysis = LpTagTextSegmentation.Analyze( originalIdentity );
        Check( ReferenceEquals( originalIdentity, unchangedAnalysis.OriginalText ), failures, "analysis retains the original string instance" );
        Check( unchangedAnalysis.OriginalText == "LifePunch\U0001f600", failures, "analysis does not rewrite the original identity" );
        var overByteAnalysis = LpTagTextSegmentation.Analyze( overByteLimitText );
        Check( ReferenceEquals( overByteLimitText, overByteAnalysis.OriginalText ), failures, "fallback retains the original string instance" );
    }

    private static string Repeat( string value, int count )
    {
        var result = string.Empty;
        for ( var index = 0; index < count; index++ )
            result += value;

        return result;
    }

    private static void CheckAnalysis( string text, int count, bool fallback, List<string> failures, string label )
    {
        var analysis = LpTagTextSegmentation.Analyze( text );
        Check( analysis.OriginalText == text, failures, label + " preserves the original identity" );
        Check( analysis.GraphemeCount == count, failures, label + " has the expected grapheme count" );
        Check( analysis.RequiresWholeNameFallback == fallback, failures, label + " has the expected fallback result" );
    }

    private static void CheckElements(
        string text,
        List<string> failures,
        string label,
        params LpTagTextElement[] expected )
    {
        var elements = LpTagTextSegmentation.Analyze( text ).Elements;
        Check( elements.Length == expected.Length, failures, label + " has the expected element count" );
        var comparableLength = Math.Min( elements.Length, expected.Length );
        for ( var index = 0; index < comparableLength; index++ )
            Check( elements[index] == expected[index], failures, label + " element " + index );
    }

    private static void CheckCurveBoundary(
        LpTagEffectId effectId,
        LpTagTextAnalysis text,
        LpTagSurfacePolicy policy,
        double startPhaseMilliseconds,
        double expectedDelayMilliseconds,
        int expectedBeforeKey,
        int expectedAtKey,
        List<string> failures,
        string label )
    {
        var delay = LpTagEffectCatalog.ResolveNextChangeDelayMilliseconds(
            effectId,
            text.GraphemeCount,
            policy.SlideAmplitudePixels,
            startPhaseMilliseconds );
        CheckClose( delay, expectedDelayMilliseconds, 0.000001d, failures, label + " delay is in milliseconds" );

        var boundaryPhaseMilliseconds = startPhaseMilliseconds + delay;
        var frame = new LpTagEffectFrame();
        LpTagEffectEvaluator.Evaluate( text, effectId, policy, Math.BitDecrement( boundaryPhaseMilliseconds ), default, frame );
        var beforeKey = frame.FrameKey;
        LpTagEffectEvaluator.Evaluate( text, effectId, policy, boundaryPhaseMilliseconds, default, frame );
        Check( beforeKey == expectedBeforeKey && frame.FrameKey == expectedAtKey, failures, label + " changes immediately before versus at the boundary" );
    }

    private static void CheckClose(
        double actual,
        double expected,
        double tolerance,
        List<string> failures,
        string label )
    {
        Check( Math.Abs( actual - expected ) <= tolerance, failures, label );
    }

    private static void CheckDecorationSequence( LpTagEffectId effectId, LpTagSurfacePolicy policy, LpTagEffectFrame frame, List<string> failures, string label, params string[] expected )
    {
        for ( var index = 0; index < expected.Length; index++ )
        {
            LpTagEffectEvaluator.Evaluate( LpTagTextSegmentation.Analyze( "ABCD" ), effectId, policy, index * 75d, default, frame );
            Check( frame.Decoration == expected[index], failures, label + " position " + index );
        }
    }

    private static void CheckRoles( LpTagEffectFrame frame, List<string> failures, string label, params LpTagPaletteRole[] expected )
    {
        Check( frame.GraphemeCount == expected.Length, failures, label + " has the expected run count" );
        for ( var index = 0; index < expected.Length; index++ )
            Check( frame.GetRole( index ) == expected[index], failures, label + " palette role " + index );
    }

    private static void Check( bool condition, List<string> failures, string description )
    {
        if ( !condition )
            failures.Add( description );
    }
}

#if LIFEPUNCH_LOCAL
internal static class LpTagEditorChecks
{
    public static void AppendSchedulerAndPresenterFailures( List<string> failures )
    {
        AppendLocalTransportAndViewerFailures( failures );

        var scene = Sandbox.Game.ActiveScene;
        if ( scene is null )
        {
            failures.Add( "editor scheduler checks require an active scene" );
            return;
        }

        AppendIncludeDisabledMenuDiscoveryFailures( scene, failures );

        var scheduler = LpTagAnimationScheduler.GetOrCreate( scene );
        scheduler.Advance( 1000d );
        var baselineRegistrations = scheduler.RegisteredPresenterCount;

        var presenter = LpTagNamePresenter.Create( scene );
        presenter.Configure( 1UL, "ABCD", LpTagSurface.Chat, LpTagEffectId.RainbowWave, default );
        Check( !presenter.IsScheduled && scheduler.RegisteredPresenterCount == baselineRegistrations, failures, "invisible presenter is not registered" );
        presenter.SetVisible( true );
        Check( presenter.IsScheduled && scheduler.RegisteredPresenterCount == baselineRegistrations + 1, failures, "visible animated presenter registers once" );
        var firstGeneration = presenter.ScheduleGeneration;
        presenter.SetVisible( true );
        Check( presenter.ScheduleGeneration == firstGeneration && scheduler.RegisteredPresenterCount == baselineRegistrations + 1, failures, "visibility reconciliation is idempotent" );
        presenter.SetVisible( false );
        Check( !presenter.IsScheduled && scheduler.RegisteredPresenterCount == baselineRegistrations, failures, "hiding unregisters an animated presenter" );

        presenter.SetVisible( true );
        var stableFrame = presenter.Frame;
        var stableRun = presenter.GetRun( 0 );
        var stableGeneration = presenter.ScheduleGeneration;
        var stableDue = presenter.ScheduledDueMilliseconds;
        presenter.Configure( 1UL, "ABCD", LpTagSurface.Chat, LpTagEffectId.RainbowWave, default );
        Check( ReferenceEquals( stableFrame, presenter.Frame ) && ReferenceEquals( stableRun, presenter.GetRun( 0 ) ), failures, "identical Configure reuses the frame and run objects" );
        Check( presenter.ScheduleGeneration == stableGeneration && presenter.ScheduledDueMilliseconds == stableDue, failures, "identical timing inputs preserve the registered due time" );

        presenter.Configure( 1UL, "A\U0001f600", LpTagSurface.Chat, LpTagEffectId.RainbowWave, default );
        Check( presenter.GraphemeCount == 2 && !ReferenceEquals( stableRun, presenter.GetRun( 0 ) ), failures, "identity change re-segments and replaces run objects" );

        presenter.Configure( 1UL, "ABCD", LpTagSurface.Chat, LpTagEffectId.SolidRed, default );
        Check( !presenter.IsScheduled && presenter.WholeNameClass == "lp-tag-name__role-red", failures, "visible static effect unregisters and keeps its trusted class" );
        presenter.Configure( 1UL, "ABCD", LpTagSurface.Chat, LpTagEffectId.RainbowWave, new LpViewerTagPreferencesV1( true, false, false ) );
        Check( !presenter.IsScheduled && presenter.TimingMode == LpTagEffectiveTimingMode.StaticRepresentative, failures, "Reduce Motion produces a static unregistered frame" );
        presenter.Configure( 1UL, "ABCD", LpTagSurface.Chat, LpTagEffectId.RainbowWave, new LpViewerTagPreferencesV1( false, true, false ) );
        Check( !presenter.IsScheduled && presenter.TimingMode == LpTagEffectiveTimingMode.Plain, failures, "Disable Animations produces plain unregistered output" );
        presenter.Configure( 1UL, "ABCD", LpTagSurface.Chat, LpTagEffectId.RainbowWave, default );
        Check( presenter.IsScheduled, failures, "restoring animated viewing re-registers while visible" );
        presenter.Configure( 1UL, "ABCD", LpTagSurface.Chat, LpTagEffectId.BouncingDot, new LpViewerTagPreferencesV1( false, false, true ) );
        Check( !presenter.IsScheduled && !presenter.HasDecoration, failures, "Hide Dot/Plus suppresses decoration and unregisters while visible" );
        presenter.Configure( 1UL, "ABCD", LpTagSurface.Chat, LpTagEffectId.BouncingDot, default );
        Check( presenter.IsScheduled && presenter.HasDecoration, failures, "restoring Dot/Plus viewing re-registers while visible" );

        var effectGeneration = presenter.ScheduleGeneration;
        presenter.Configure( 1UL, "ABCD", LpTagSurface.Chat, LpTagEffectId.ColorWipe, default );
        Check( presenter.IsScheduled && presenter.ScheduleGeneration > effectGeneration, failures, "animated effect change replaces the due entry" );
        var steamGeneration = presenter.ScheduleGeneration;
        presenter.Configure( 2UL, "ABCD", LpTagSurface.Chat, LpTagEffectId.ColorWipe, default );
        Check( presenter.ScheduleGeneration > steamGeneration, failures, "Steam-ID phase-offset change replaces the due entry" );
        var graphemeGeneration = presenter.ScheduleGeneration;
        presenter.Configure( 2UL, "ABCDE", LpTagSurface.Chat, LpTagEffectId.ColorWipe, default );
        Check( presenter.ScheduleGeneration > graphemeGeneration, failures, "grapheme-count timing change replaces the due entry" );
        presenter.Configure( 2UL, "ABCDE", LpTagSurface.Chat, LpTagEffectId.Slide, default );
        var amplitudeGeneration = presenter.ScheduleGeneration;
        presenter.Configure( 2UL, "ABCDE", LpTagSurface.Scoreboard, LpTagEffectId.Slide, default );
        Check( presenter.ScheduleGeneration > amplitudeGeneration, failures, "surface Slide-amplitude change replaces the due entry" );

        presenter.Configure( 2UL, "مرحبا", LpTagSurface.Chat, LpTagEffectId.ColorWipe, default );
        Check( presenter.RequiresWholeNameFallback && !presenter.UsesGraphemeRuns && !presenter.IsScheduled, failures, "unsafe shaping uses one plain whole-name fallback" );
        presenter.Configure( 2UL, "ABCD", LpTagSurface.Chat, (LpTagEffectId)255, default );
        Check( presenter.EffectiveEffectId == LpTagEffectId.None && presenter.WholeNameClass == "lp-tag-name__role-neutral" && !presenter.IsScheduled, failures, "unknown effect ID falls back to plain identity" );

        presenter.Configure( 2UL, "ABCD", LpTagSurface.Chat, LpTagEffectId.RainbowWave, default );
        var staleGeneration = presenter.ScheduleGeneration;
        scheduler.Reschedule( presenter );
        var replacementGeneration = presenter.ScheduleGeneration;
        var replacementDue = presenter.ScheduledDueMilliseconds;
        var wakeCount = presenter.SchedulerWakeCount;
        scheduler.Advance( replacementDue );
        Check( replacementGeneration > staleGeneration && presenter.SchedulerWakeCount == wakeCount + 1, failures, "stale generation is ignored and one current queue node wakes" );

        presenter.Dispose();
        presenter.Dispose();
        Check( presenter.IsDisposed && !presenter.IsScheduled && scheduler.RegisteredPresenterCount == baselineRegistrations, failures, "disposal unregisters exactly once" );

        var smokePresenters = new List<LpTagNamePresenter>( 64 );
        for ( var index = 0; index < 64; index++ )
        {
            var smokePresenter = LpTagNamePresenter.Create( scene );
            smokePresenter.Configure( (ulong)(index + 100), "Smoke", LpTagSurface.Nameplate, LpTagEffectId.RainbowWave, default );
            smokePresenter.SetVisible( true );
            smokePresenters.Add( smokePresenter );
        }

        Check( scheduler.RegisteredPresenterCount == baselineRegistrations + 64, failures, "bounded 64-visible-name local smoke registers 64 logical names" );
        Check( CountSchedulers( scene ) == 1, failures, "bounded smoke uses one scheduler driver rather than per-name timers" );
        foreach ( var smokePresenter in smokePresenters )
            smokePresenter.Dispose();
        Check( scheduler.RegisteredPresenterCount == baselineRegistrations, failures, "bounded smoke cleanup releases every logical registration" );

        var continuityOwner = LpTagAnimationScheduler.GetOrCreate( scene );
        var continuityBaseline = continuityOwner.RegisteredPresenterCount;
        var activeContinuityPresenter = LpTagNamePresenter.Create( scene );
        activeContinuityPresenter.Configure( 700UL, "Active continuity", LpTagSurface.Chat, LpTagEffectId.RainbowWave, default );
        activeContinuityPresenter.SetVisible( true );
        var dormantContinuityPresenter = LpTagNamePresenter.Create( scene );
        dormantContinuityPresenter.Configure( 701UL, "Dormant continuity", LpTagSurface.Chat, LpTagEffectId.RainbowWave, default );
        continuityOwner.GameObject.Name = "LifePunch.Tags.Client.Replaced.EditorCheck";
        var replacementRoot = scene.CreateObject();
        replacementRoot.Name = LpTagAnimationScheduler.ClientRootName;
        var replacementScheduler = replacementRoot.AddComponent<LpTagAnimationScheduler>();
        replacementRoot.Enabled = false;
        Check( CountSchedulers( scene ) == 2, failures, "include-disabled scheduler discovery sees the disabled replacement GameObject before continuity deduplication" );
        var continuityKeeper = LpTagAnimationScheduler.GetOrCreate( scene );
        Check( continuityKeeper == replacementScheduler
            && replacementRoot.Enabled
            && replacementScheduler.Enabled
            && !continuityOwner.IsValid()
            && activeContinuityPresenter.Scheduler == continuityKeeper
            && activeContinuityPresenter.IsScheduled
            && continuityKeeper.RegisteredPresenterCount == continuityBaseline + 1,
            failures,
            "dedup migrates every active registration before destroying its scheduler owner" );
        dormantContinuityPresenter.SetVisible( true );
        Check( dormantContinuityPresenter.Scheduler == continuityKeeper
            && dormantContinuityPresenter.IsScheduled
            && continuityKeeper.RegisteredPresenterCount == continuityBaseline + 2,
            failures,
            "dedup preserves dormant presenter ownership for later registration" );
        var continuityWakeCount = activeContinuityPresenter.SchedulerWakeCount;
        continuityKeeper.Advance( activeContinuityPresenter.ScheduledDueMilliseconds );
        Check( activeContinuityPresenter.SchedulerWakeCount == continuityWakeCount + 1,
            failures,
            "migrated active presenter remains driven by the surviving scheduler" );
        activeContinuityPresenter.Dispose();
        dormantContinuityPresenter.Dispose();
        Check( continuityKeeper.RegisteredPresenterCount == continuityBaseline,
            failures,
            "ownership-continuity cleanup releases migrated registrations" );

        var sameRootKeeper = LpTagAnimationScheduler.GetOrCreate( scene );
        var sameRootBaseline = sameRootKeeper.RegisteredPresenterCount;
        var keeperAnchorOne = LpTagNamePresenter.Create( scene );
        keeperAnchorOne.Configure( 710UL, "Keeper anchor one", LpTagSurface.Chat, LpTagEffectId.RainbowWave, default );
        keeperAnchorOne.SetVisible( true );
        var keeperAnchorTwo = LpTagNamePresenter.Create( scene );
        keeperAnchorTwo.Configure( 711UL, "Keeper anchor two", LpTagSurface.Chat, LpTagEffectId.RainbowWave, default );
        keeperAnchorTwo.SetVisible( true );
        var firstDuplicateActive = LpTagNamePresenter.Create( scene );
        firstDuplicateActive.Configure( 712UL, "First active", LpTagSurface.Chat, LpTagEffectId.RainbowWave, default );
        firstDuplicateActive.SetVisible( true );
        var firstDuplicateDormant = LpTagNamePresenter.Create( scene );
        firstDuplicateDormant.Configure( 713UL, "First dormant", LpTagSurface.Chat, LpTagEffectId.RainbowWave, default );
        var laterDuplicateActive = LpTagNamePresenter.Create( scene );
        laterDuplicateActive.Configure( 714UL, "Later active", LpTagSurface.Chat, LpTagEffectId.RainbowWave, default );
        laterDuplicateActive.SetVisible( true );
        var laterDuplicateDormant = LpTagNamePresenter.Create( scene );
        laterDuplicateDormant.Configure( 715UL, "Later dormant", LpTagSurface.Chat, LpTagEffectId.RainbowWave, default );

        var multiSchedulerRoot = scene.CreateObject();
        multiSchedulerRoot.Name = LpTagAnimationScheduler.ClientRootName;
        var firstDuplicateScheduler = multiSchedulerRoot.AddComponent<LpTagAnimationScheduler>();
        var laterDuplicateScheduler = multiSchedulerRoot.AddComponent<LpTagAnimationScheduler>();
        firstDuplicateActive.MoveToScheduler( sameRootKeeper, firstDuplicateScheduler );
        firstDuplicateDormant.MoveToScheduler( sameRootKeeper, firstDuplicateScheduler );
        laterDuplicateActive.MoveToScheduler( sameRootKeeper, laterDuplicateScheduler );
        laterDuplicateDormant.MoveToScheduler( sameRootKeeper, laterDuplicateScheduler );
        var laterGenerationOnDuplicate = laterDuplicateActive.ScheduleGeneration;
        var sameRootDeduplicated = LpTagAnimationScheduler.GetOrCreate( scene );
        Check( sameRootDeduplicated == sameRootKeeper
            && !multiSchedulerRoot.IsValid()
            && CountSchedulers( scene ) == 1
            && firstDuplicateActive.Scheduler == sameRootKeeper
            && firstDuplicateActive.IsScheduled
            && firstDuplicateDormant.Scheduler == sameRootKeeper
            && !firstDuplicateDormant.IsScheduled
            && laterDuplicateActive.Scheduler == sameRootKeeper
            && laterDuplicateActive.IsScheduled
            && laterDuplicateDormant.Scheduler == sameRootKeeper
            && !laterDuplicateDormant.IsScheduled
            && sameRootKeeper.RegisteredPresenterCount == sameRootBaseline + 4,
            failures,
            "two-phase dedup migrates active and dormant owners from every same-root scheduler before root destruction" );
        var laterWakeCount = laterDuplicateActive.SchedulerWakeCount;
        sameRootKeeper.Advance( laterDuplicateActive.ScheduledDueMilliseconds );
        Check( laterDuplicateActive.ScheduleGeneration > laterGenerationOnDuplicate
            && laterDuplicateActive.SchedulerWakeCount == laterWakeCount + 1,
            failures,
            "later same-root active owner receives a fresh generation and remains driven" );
        laterDuplicateDormant.SetVisible( true );
        Check( laterDuplicateDormant.Scheduler == sameRootKeeper
            && laterDuplicateDormant.IsScheduled
            && sameRootKeeper.RegisteredPresenterCount == sameRootBaseline + 5,
            failures,
            "later same-root dormant owner registers with the surviving scheduler" );
        keeperAnchorOne.Dispose();
        keeperAnchorTwo.Dispose();
        firstDuplicateActive.Dispose();
        firstDuplicateDormant.Dispose();
        laterDuplicateActive.Dispose();
        laterDuplicateDormant.Dispose();
        Check( sameRootKeeper.RegisteredPresenterCount == sameRootBaseline,
            failures,
            "same-root ownership cleanup releases every migrated registration" );

        var duplicateRoot = scene.CreateObject();
        duplicateRoot.Name = LpTagAnimationScheduler.ClientRootName;
        var disabledDuplicate = duplicateRoot.AddComponent<LpTagAnimationScheduler>();
        disabledDuplicate.Enabled = false;
        Check( CountSchedulers( scene ) == 2, failures, "include-disabled scheduler discovery sees the disabled duplicate before deduplication" );
        var deduplicated = LpTagAnimationScheduler.GetOrCreate( scene );
        Check( CountSchedulers( scene ) == 1 && deduplicated.Enabled && deduplicated.GameObject.IsRoot && deduplicated.GameObject.Name == LpTagAnimationScheduler.ClientRootName, failures, "GetOrCreate includes disabled duplicates and keeps one enabled Tags scene-root scheduler" );

        var menuObject = scene.CreateObject();
        menuObject.Name = "LifePunch.Tags.Menu.EditorCheck";
        var deceptiveChild = scene.CreateObject();
        deceptiveChild.Name = LpTagAnimationScheduler.ClientRootName;
        deceptiveChild.SetParent( menuObject );
        deceptiveChild.AddComponent<LpTagAnimationScheduler>();
        var rootBeforeMenuClose = LpTagAnimationScheduler.GetOrCreate( scene );
        Check( deceptiveChild.IsValid() && CountSchedulers( scene ) == 1 && rootBeforeMenuClose.GameObject.IsRoot, failures, "nested name-matching object loses only its duplicate scheduler component" );
        menuObject.Destroy();
        Check( CountSchedulers( scene ) == 1 && LpTagAnimationScheduler.GetOrCreate( scene ) == rootBeforeMenuClose, failures, "destroying a menu parent leaves the true scene-root scheduler alive" );
    }

    private static void AppendIncludeDisabledMenuDiscoveryFailures(
        Sandbox.Scene scene,
        List<string> failures )
    {
        var fixtureObject = scene.CreateObject();
        fixtureObject.Name = "LifePunch.Tags.Menu.DisabledDiscovery.EditorCheck";
        try
        {
            var fixtureMenu = fixtureObject.AddComponent<LpTagsMenu>();
            fixtureMenu.Enabled = false;
            Check(
                ContainsMenuReference( LpTagsMenuHost.EnumerateSceneMenus( scene ), fixtureMenu ),
                failures,
                "include-disabled menu discovery retains a disabled component" );

            fixtureMenu.Enabled = true;
            fixtureObject.Enabled = false;
            Check(
                ContainsMenuReference( LpTagsMenuHost.EnumerateSceneMenus( scene ), fixtureMenu ),
                failures,
                "include-disabled menu discovery retains a component on a disabled object" );
        }
        finally
        {
            if ( fixtureObject.IsValid() )
                fixtureObject.Destroy();
        }
    }

    private static bool ContainsMenuReference(
        IEnumerable<LpTagsMenu> menus,
        LpTagsMenu expected )
    {
        foreach ( var menu in menus )
        {
            if ( ReferenceEquals( menu, expected ) )
                return true;
        }

        return false;
    }

    private static void AppendLocalTransportAndViewerFailures( List<string> failures )
    {
        LpTagLocalProfileClient.ResetForTests();
        using ( var client = new LpTagLocalProfileClient() )
        {
            LpTagProfileReadResult readResult = default;
            LpTagMutationResult mutationResult = default;
            client.ReadCompleted += result => readResult = result;
            client.MutationCompleted += result => mutationResult = result;

            var readIdentifier = 101.ToString( "x32" );
            client.ReadOwnProfile( new LpTagProfileReadRequest( readIdentifier ), 41UL );
            Check( client.Mode == LpTagClientMode.LocalPreview && readResult.RequestIdentifier == readIdentifier && readResult.MenuGeneration == 41UL && readResult.Status == LpTagProfileReadStatus.Success && readResult.CanonicalRecord == LpTagProfileRules.CreateNew(), failures, "local preview read preserves identity and starts from deterministic process memory" );

            var invalidIdentifier = new string( 'A', 32 );
            client.MutateOwnProfile( new LpTagMutationRequest( invalidIdentifier, 0UL, LpTagSurface.Chat, LpTagMutationKind.SetEffect, false, "solid-red-v1" ), 42UL );
            Check( mutationResult.RequestIdentifier == invalidIdentifier && mutationResult.MenuGeneration == 42UL && mutationResult.Status == LpTagMutationResultStatus.Rejected && mutationResult.Error == LpTagProfileError.InvalidRequestId, failures, "local preview applies the shared validation rules before injection" );

            var successIdentifier = 102.ToString( "x32" );
            client.MutateOwnProfile( new LpTagMutationRequest( successIdentifier, 0UL, LpTagSurface.Chat, LpTagMutationKind.SetEffect, false, "solid-red-v1" ), 43UL );
            Check( mutationResult.RequestIdentifier == successIdentifier && mutationResult.MenuGeneration == 43UL && mutationResult.Status == LpTagMutationResultStatus.Success && mutationResult.CanonicalRecord.Revision == 1UL && mutationResult.CanonicalRecord.Chat.SelectedEffectKey == "solid-red-v1", failures, "local preview success commits exactly one canonical revision" );

            var naturalConflictIdentifier = 107.ToString( "x32" );
            client.MutateOwnProfile( new LpTagMutationRequest( naturalConflictIdentifier, 0UL, LpTagSurface.Chat, LpTagMutationKind.SetEnabled, true, string.Empty ), 48UL );
            Check( mutationResult.RequestIdentifier == naturalConflictIdentifier && mutationResult.MenuGeneration == 48UL && mutationResult.Status == LpTagMutationResultStatus.Conflict && mutationResult.Error == LpTagProfileError.StaleRevision && mutationResult.CanonicalRecord.Revision == 1UL, failures, "natural local stale revision returns the current canonical conflict rather than validation rejection" );

            client.InjectNextMutationResult( LpTagMutationResultStatus.Conflict );
            var conflictIdentifier = 103.ToString( "x32" );
            client.MutateOwnProfile( new LpTagMutationRequest( conflictIdentifier, 1UL, LpTagSurface.Chat, LpTagMutationKind.SetEnabled, true, string.Empty ), 44UL );
            Check( mutationResult.Status == LpTagMutationResultStatus.Conflict && mutationResult.CanonicalRecord.Revision == 2UL && mutationResult.RequestIdentifier == conflictIdentifier && mutationResult.MenuGeneration == 44UL, failures, "local preview can inject a deterministic newer conflict" );

            client.InjectNextMutationResult( LpTagMutationResultStatus.GuaranteedFailure );
            var failureIdentifier = 104.ToString( "x32" );
            client.MutateOwnProfile( new LpTagMutationRequest( failureIdentifier, 2UL, LpTagSurface.Chat, LpTagMutationKind.SetEnabled, true, string.Empty ), 45UL );
            Check( mutationResult.Status == LpTagMutationResultStatus.GuaranteedFailure && mutationResult.CanonicalRecord.Revision == 2UL && mutationResult.RequestIdentifier == failureIdentifier && mutationResult.MenuGeneration == 45UL, failures, "local preview can inject guaranteed non-commit failure" );

            client.InjectNextMutationResult( LpTagMutationResultStatus.Uncertain, commitCandidate: true );
            var uncertainIdentifier = 105.ToString( "x32" );
            client.MutateOwnProfile( new LpTagMutationRequest( uncertainIdentifier, 2UL, LpTagSurface.Chat, LpTagMutationKind.SetEnabled, true, string.Empty ), 46UL );
            Check( mutationResult.Status == LpTagMutationResultStatus.Uncertain && mutationResult.RequestIdentifier == uncertainIdentifier && mutationResult.MenuGeneration == 46UL, failures, "local preview can inject an uncertain result without losing response identity" );
            var verifyIdentifier = 106.ToString( "x32" );
            client.ReadOwnProfile( new LpTagProfileReadRequest( verifyIdentifier ), 47UL );
            Check( readResult.CanonicalRecord.Revision == 3UL && readResult.CanonicalRecord.Chat.Enabled && readResult.RequestIdentifier == verifyIdentifier && readResult.MenuGeneration == 47UL, failures, "local preview uncertain injection can expose the committed candidate to verification" );
        }
        LpTagLocalProfileClient.ResetForTests();

        LpTagViewerPreferenceStore.ResetSharedForTests();
        var viewer = LpTagViewerPreferenceStore.Shared;
        var sameViewer = LpTagViewerPreferenceStore.Shared;
        var firstSubscriberValues = new List<LpViewerTagPreferencesV1>();
        var secondSubscriberValues = new List<LpViewerTagPreferencesV1>();
        var originalReduceMotion = viewer.Current.ReduceMotion;
        var originalDisableAnimations = viewer.Current.DisableAnimations;
        var originalHideDotPlus = viewer.Current.HideDotPlusDecorations;
        var nestedWriteStarted = false;
        void FirstViewerSubscriber( LpViewerTagPreferencesV1 value )
        {
            firstSubscriberValues.Add( value );
            if ( nestedWriteStarted )
                return;

            nestedWriteStarted = true;
            viewer.SetDisableAnimations( !originalDisableAnimations );
        }
        void SecondViewerSubscriber( LpViewerTagPreferencesV1 value ) => secondSubscriberValues.Add( value );
        viewer.Changed += FirstViewerSubscriber;
        viewer.Changed += SecondViewerSubscriber;
        viewer.SetReduceMotion( !originalReduceMotion );
        viewer.SetReduceMotion( !originalReduceMotion );
        Check( ReferenceEquals( viewer, sameViewer ) && firstSubscriberValues.Count == 2 && secondSubscriberValues.Count == 2 && firstSubscriberValues[1] == viewer.Current && secondSubscriberValues[1] == viewer.Current && viewer.Current == new LpViewerTagPreferencesV1( !originalReduceMotion, !originalDisableAnimations, originalHideDotPlus ), failures, "shared viewer store serializes nested writes so every subscriber's last typed notification is Current" );
        viewer.Changed -= FirstViewerSubscriber;
        viewer.Changed -= SecondViewerSubscriber;
        viewer.SetReduceMotion( originalReduceMotion );
        viewer.SetDisableAnimations( originalDisableAnimations );
        Check( viewer.Current.ReduceMotion == originalReduceMotion && viewer.Current.DisableAnimations == originalDisableAnimations && firstSubscriberValues.Count == 2 && secondSubscriberValues.Count == 2, failures, "same-value viewer writes are no-ops and the nested cookie check restores original settings" );
        LpTagViewerPreferenceStore.ResetSharedForTests();
    }

    private static int CountSchedulers( Sandbox.Scene scene )
    {
        var count = 0;
        foreach ( var scheduler in LpTagAnimationScheduler.EnumerateSceneSchedulers( scene ) )
        {
            if ( scheduler.IsValid() )
                count++;
        }

        return count;
    }

    private static void Check( bool condition, List<string> failures, string description )
    {
        if ( !condition )
            failures.Add( description );
    }
}

internal static class LpTagDevChecks
{
    [Sandbox.ConCmd( "lp_tags_test" )]
    public static void Run()
    {
        var failures = new List<string>( LpTagPureChecks.RunAll() );
        LpTagEditorChecks.AppendSchedulerAndPresenterFailures( failures );
        if ( failures.Count > 0 )
            throw new InvalidOperationException(
                "lp_tags_test failed: " + string.Join( " | ", failures ) );

        Sandbox.Log.Info( "[lifepunchtags] pure and editor checks passed" );
    }
}
#endif
