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
using Sandbox;

namespace LifePunch.DXRP.Addons.Tags;

public sealed class LpTagNameRun
{
    public string Text { get; }
    public string RoleClass { get; internal set; } = "lp-tag-name__role-neutral";

    internal LpTagNameRun( string text )
    {
        Text = text;
    }
}

/// <summary>
/// Cached, renderer-ready state for one player identity on one tag surface.
/// </summary>
public sealed class LpTagNamePresenter : IDisposable
{
    public const int DecorationCellCount = 4;

    private LpTagAnimationScheduler _scheduler;
    private readonly LpTagEffectFrame _frame = new();
    private LpTagTextAnalysis _text = LpTagTextSegmentation.Analyze( string.Empty );
    private LpTagNameRun[] _runs = Array.Empty<LpTagNameRun>();
    private LpTagSurfacePolicy _surfacePolicy = LpTagEffectCatalog.GetPolicy( LpTagSurface.Chat );
    private LpViewerTagPreferencesV1 _viewerPreferences;
    private LpTagEffectId _requestedEffectId;
    private LpTagEffectId _effectiveEffectId;
    private LpTagEffectiveTimingMode _timingMode;
    private LpTagAnimationScheduler? _scheduledWith;
    private ulong _steamId;
    private ulong _scheduleGeneration;
    private double _scheduledDueMilliseconds = double.PositiveInfinity;
    private bool _visible;
    private bool _disposed;
    private int _renderRevision;
    private int _schedulerWakeCount;

    public event Action? Changed;

    public string PlainIdentity => _text.OriginalText;
    public LpTagSurface Surface => _surfacePolicy.Surface;
    public LpTagEffectId EffectiveEffectId => _effectiveEffectId;
    public IReadOnlyList<LpTagNameRun> Runs => _runs;
    public LpTagEffectFrame Frame => _frame;
    public int GraphemeCount => _text.GraphemeCount;
    public int RunCount => _runs.Length;
    public int RenderRevision => _renderRevision;
    public bool RequiresWholeNameFallback => _frame.RequiresWholeNameFallback;
    public bool UsesGraphemeRuns => ResolvesToGraphemeRuns();
    public bool HasDecoration => _frame.Decoration.Length == DecorationCellCount;
    public bool IsVisible => _visible;
    public bool IsDisposed => _disposed;

    public string SurfaceClass => _surfacePolicy.Surface switch
    {
        LpTagSurface.Chat => "lp-tag-name--chat",
        LpTagSurface.Scoreboard => "lp-tag-name--scoreboard",
        LpTagSurface.Nameplate => "lp-tag-name--nameplate",
        _ => "lp-tag-name--chat"
    };

    public string WholeNameClass => RoleClassFor(
        _frame.GraphemeCount > 0
            ? _frame.GetRole( 0 )
            : LpTagPaletteRole.Neutral );

    public string TranslationClass => _frame.TranslateXPixels switch
    {
        -8 => "lp-tag-name__translate-n8",
        -7 => "lp-tag-name__translate-n7",
        -6 => "lp-tag-name__translate-n6",
        -5 => "lp-tag-name__translate-n5",
        -4 => "lp-tag-name__translate-n4",
        -3 => "lp-tag-name__translate-n3",
        -2 => "lp-tag-name__translate-n2",
        -1 => "lp-tag-name__translate-n1",
        1 => "lp-tag-name__translate-p1",
        2 => "lp-tag-name__translate-p2",
        3 => "lp-tag-name__translate-p3",
        4 => "lp-tag-name__translate-p4",
        5 => "lp-tag-name__translate-p5",
        6 => "lp-tag-name__translate-p6",
        7 => "lp-tag-name__translate-p7",
        8 => "lp-tag-name__translate-p8",
        _ => "lp-tag-name__translate-0"
    };

    internal LpTagAnimationScheduler Scheduler => _scheduler;
    internal ulong SteamId => _steamId;
    internal LpTagSurfacePolicy SurfacePolicy => _surfacePolicy;
    internal LpTagEffectiveTimingMode TimingMode => _timingMode;
    internal bool IsScheduled => _scheduledWith == _scheduler;
    internal ulong ScheduleGeneration => _scheduleGeneration;
    internal double ScheduledDueMilliseconds => _scheduledDueMilliseconds;
    internal int SchedulerWakeCount => _schedulerWakeCount;

    private LpTagNamePresenter( LpTagAnimationScheduler scheduler )
    {
        _scheduler = scheduler ?? throw new ArgumentNullException( nameof( scheduler ) );
        _scheduler.AttachPresenter( this );
    }

    public static LpTagNamePresenter Create( Scene scene )
    {
        return new LpTagNamePresenter( LpTagAnimationScheduler.GetOrCreate( scene ) );
    }

    public void Configure(
        ulong steamId,
        string displayName,
        LpTagSurface surface,
        LpTagEffectId effectId,
        LpViewerTagPreferencesV1 viewerPreferences )
    {
        ThrowIfDisposed();

        displayName ??= string.Empty;
        var identityChanged = !string.Equals( _text.OriginalText, displayName, StringComparison.Ordinal );
        var resultingText = identityChanged ? LpTagTextSegmentation.Analyze( displayName ) : _text;
        var resultingPolicy = LpTagEffectCatalog.GetPolicy( surface );
        var resultingEffectId = LpTagEffectCatalog.TryGet( effectId, out _ )
            ? effectId
            : LpTagEffectId.None;
        var resultingTimingMode = LpTagSchedulingRules.ResolveTimingMode(
            resultingEffectId,
            resultingText.GraphemeCount,
            resultingText.RequiresWholeNameFallback,
            resultingPolicy,
            viewerPreferences );
        var timingChanged = _steamId != steamId
            || _text.GraphemeCount != resultingText.GraphemeCount
            || _surfacePolicy.SlideAmplitudePixels != resultingPolicy.SlideAmplitudePixels
            || _requestedEffectId != effectId
            || _timingMode != resultingTimingMode;
        var presentationChanged = timingChanged
            || identityChanged
            || _surfacePolicy.Surface != resultingPolicy.Surface
            || _viewerPreferences != viewerPreferences;

        if ( identityChanged )
        {
            _text = resultingText;
            RebuildRuns();
        }

        _steamId = steamId;
        _surfacePolicy = resultingPolicy;
        _requestedEffectId = effectId;
        _effectiveEffectId = resultingEffectId;
        _viewerPreferences = viewerPreferences;
        _timingMode = resultingTimingMode;

        if ( presentationChanged )
            EvaluateFrame( _scheduler.CurrentTimeMilliseconds, true );

        ReconcileSchedulerEligibility( timingChanged );
    }

    public void SetVisible( bool visible )
    {
        ThrowIfDisposed();
        if ( _visible == visible )
        {
            ReconcileSchedulerEligibility( false );
            return;
        }

        _visible = visible;
        ReconcileSchedulerEligibility( false );
    }

    public char GetDecorationCell( int index )
    {
        if ( index < 0 || index >= DecorationCellCount || !HasDecoration )
            throw new ArgumentOutOfRangeException( nameof( index ) );

        return _frame.Decoration[index];
    }

    internal LpTagNameRun GetRun( int index )
    {
        return _runs[index];
    }

    internal void ApplySchedulerFrame( double nowMilliseconds )
    {
        if ( _disposed )
            return;

        _schedulerWakeCount++;
        EvaluateFrame( nowMilliseconds, false );
    }

    internal bool IsScheduledWith( LpTagAnimationScheduler scheduler, ulong generation )
    {
        return _scheduledWith == scheduler && _scheduleGeneration == generation;
    }

    internal ulong ReplaceSchedule(
        LpTagAnimationScheduler scheduler,
        double dueMilliseconds )
    {
        if ( scheduler != _scheduler )
            throw new InvalidOperationException( "A tag presenter cannot move between scene schedulers." );

        _scheduledWith = scheduler;
        _scheduledDueMilliseconds = dueMilliseconds;
        _scheduleGeneration++;
        return _scheduleGeneration;
    }

    internal void ClearSchedule( LpTagAnimationScheduler scheduler )
    {
        if ( _scheduledWith != scheduler )
            return;

        _scheduledWith = null;
        _scheduledDueMilliseconds = double.PositiveInfinity;
        _scheduleGeneration++;
    }

    internal void MoveToScheduler(
        LpTagAnimationScheduler current,
        LpTagAnimationScheduler replacement )
    {
        if ( current == replacement )
            return;
        if ( _scheduler != current )
            throw new InvalidOperationException( "A tag presenter can move only from its current scene scheduler." );

        current.DetachPresenter( this );
        _scheduler = replacement;
        replacement.AttachPresenter( this );
        ReconcileSchedulerEligibility( false );
    }

    public void Dispose()
    {
        if ( _disposed )
            return;

        _disposed = true;
        _scheduler.DetachPresenter( this );
        Changed = null;
    }

    private void ReconcileSchedulerEligibility( bool timingChanged )
    {
        var eligible = LpTagSchedulingRules.ShouldSchedule( _visible, _disposed, _timingMode );
        if ( !eligible )
        {
            _scheduler.Unregister( this );
            return;
        }

        if ( !IsScheduled )
        {
            _scheduler.Register( this );
            return;
        }

        if ( timingChanged )
            _scheduler.Reschedule( this );
    }

    private void RebuildRuns()
    {
        var elements = _text.Elements;
        if ( elements.Length == 0 )
        {
            _runs = Array.Empty<LpTagNameRun>();
            return;
        }

        var runs = new LpTagNameRun[elements.Length];
        for ( var index = 0; index < elements.Length; index++ )
        {
            var element = elements[index];
            runs[index] = new LpTagNameRun(
                _text.OriginalText.Substring( element.Start, element.Length ) );
        }

        _runs = runs;
    }

    private void EvaluateFrame( double nowMilliseconds, bool forceRenderRevision )
    {
        var previousFrameKey = _frame.FrameKey;
        var phase = 0d;
        if ( LpTagEffectCatalog.TryGet( _effectiveEffectId, out var definition ) )
        {
            var period = definition.ResolvePeriodMilliseconds( _text.GraphemeCount );
            if ( period > 0d && !double.IsNaN( period ) && !double.IsInfinity( period ) )
            {
                phase = LpTagSchedulingRules.PositiveModulo(
                    nowMilliseconds + LpTagPhase.OffsetFor( _steamId, period ),
                    period );
            }
        }

        LpTagEffectEvaluator.Evaluate(
            _text,
            _effectiveEffectId,
            _surfacePolicy,
            phase,
            _viewerPreferences,
            _frame );
        if ( !forceRenderRevision && _frame.FrameKey == previousFrameKey )
            return;

        ApplyRunRoles();
        _renderRevision++;
        Changed?.Invoke();
    }

    private void ApplyRunRoles()
    {
        var comparableLength = Math.Min( _runs.Length, _frame.GraphemeCount );
        for ( var index = 0; index < comparableLength; index++ )
            _runs[index].RoleClass = RoleClassFor( _frame.GetRole( index ) );
    }

    private bool ResolvesToGraphemeRuns()
    {
        if ( _timingMode is not LpTagEffectiveTimingMode.Animated
            and not LpTagEffectiveTimingMode.StaticRepresentative )
        {
            return false;
        }
        if ( !LpTagEffectCatalog.TryGet( _effectiveEffectId, out var definition ) )
            return false;

        return definition.RequiresGraphemeRuns && !_frame.RequiresWholeNameFallback;
    }

    private static string RoleClassFor( LpTagPaletteRole role )
    {
        return role switch
        {
            LpTagPaletteRole.Red => "lp-tag-name__role-red",
            LpTagPaletteRole.Green => "lp-tag-name__role-green",
            LpTagPaletteRole.Yellow => "lp-tag-name__role-yellow",
            LpTagPaletteRole.Blue => "lp-tag-name__role-blue",
            LpTagPaletteRole.Cyan => "lp-tag-name__role-cyan",
            _ => "lp-tag-name__role-neutral"
        };
    }

    private void ThrowIfDisposed()
    {
        if ( _disposed )
            throw new ObjectDisposedException( nameof( LpTagNamePresenter ) );
    }
}
