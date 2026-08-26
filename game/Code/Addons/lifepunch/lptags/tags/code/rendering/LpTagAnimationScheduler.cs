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

/// <summary>
/// The single Tags-owned scene clock. Presenters register logical animation
/// deadlines here instead of creating per-name timers or update components.
/// </summary>
public sealed class LpTagAnimationScheduler : Component
{
    public const string ClientRootName = "LifePunch.Tags.Client";
    public const double MinimumDelayMilliseconds = LpTagSchedulingRules.MinimumDelayMilliseconds;

    private readonly PriorityQueue<ScheduledPresenter, double> _queue = new();
    private readonly HashSet<LpTagNamePresenter> _registeredPresenters = new();
    private readonly HashSet<LpTagNamePresenter> _ownedPresenters = new();
    private double _nowMilliseconds;

    internal int RegisteredPresenterCount => _registeredPresenters.Count;
    internal double CurrentTimeMilliseconds => _nowMilliseconds;

    public LpTagAnimationScheduler()
    {
    }

    public static LpTagAnimationScheduler GetOrCreate( Scene scene )
    {
        if ( scene is null )
            throw new ArgumentNullException( nameof( scene ) );

        var existing = new List<LpTagAnimationScheduler>();
        LpTagAnimationScheduler? keeper = null;
        foreach ( var scheduler in EnumerateSceneSchedulers( scene ) )
        {
            if ( !scheduler.IsValid() )
                continue;

            existing.Add( scheduler );
            if ( IsCanonicalScheduler( scheduler )
                && (keeper is null || PreferAsKeeper( scheduler, keeper )) )
            {
                keeper = scheduler;
            }
        }

        if ( keeper is null )
        {
            var root = scene.CreateObject();
            root.Name = ClientRootName;
            keeper = root.AddComponent<LpTagAnimationScheduler>();
        }

        keeper.GameObject.Enabled = true;
        keeper.Enabled = true;

        foreach ( var scheduler in existing )
            keeper._nowMilliseconds = Math.Max( keeper._nowMilliseconds, scheduler._nowMilliseconds );

        foreach ( var duplicate in existing )
        {
            if ( duplicate == keeper || !duplicate.IsValid() )
                continue;

            duplicate.MoveOwnedPresentersTo( keeper );
        }

        foreach ( var duplicate in existing )
        {
            if ( duplicate == keeper || !duplicate.IsValid() )
                continue;

            var duplicateRoot = duplicate.GameObject;
            if ( duplicateRoot.IsValid()
                && duplicateRoot != keeper.GameObject
                && duplicateRoot.IsRoot
                && duplicateRoot.Name == ClientRootName )
            {
                duplicateRoot.Destroy();
            }
            else
            {
                duplicate.Destroy();
            }
        }

        return keeper;
    }

    internal void AttachPresenter( LpTagNamePresenter presenter )
    {
        if ( presenter is null )
            throw new ArgumentNullException( nameof( presenter ) );
        if ( presenter.Scheduler != this )
            throw new InvalidOperationException( "A tag presenter can attach only to its owning scene scheduler." );

        _ownedPresenters.Add( presenter );
    }

    internal void DetachPresenter( LpTagNamePresenter presenter )
    {
        if ( presenter is null )
            return;

        Unregister( presenter );
        _ownedPresenters.Remove( presenter );
    }

    protected override void OnUpdate()
    {
        Advance( Sandbox.RealTime.GlobalNow * 1000d );
    }

    internal static IEnumerable<LpTagAnimationScheduler> EnumerateSceneSchedulers( Scene scene )
    {
        return scene.GetComponentsInChildren<LpTagAnimationScheduler>( true, true );
    }

    internal void Advance( double nowMilliseconds )
    {
        if ( double.IsNaN( nowMilliseconds ) || double.IsInfinity( nowMilliseconds ) )
            throw new ArgumentOutOfRangeException( nameof( nowMilliseconds ) );

        _nowMilliseconds = nowMilliseconds;
        while ( _queue.TryPeek( out _, out var dueMilliseconds )
            && dueMilliseconds <= nowMilliseconds )
        {
            var scheduled = _queue.Dequeue();
            var presenter = scheduled.Presenter;
            if ( !_registeredPresenters.Contains( presenter )
                || !presenter.IsScheduledWith( this, scheduled.Generation ) )
            {
                continue;
            }

            presenter.ApplySchedulerFrame( nowMilliseconds );
            if ( _registeredPresenters.Contains( presenter )
                && presenter.IsScheduledWith( this, scheduled.Generation ) )
            {
                ScheduleFreshEntry( presenter );
            }
        }
    }

    internal void Register( LpTagNamePresenter presenter )
    {
        if ( presenter is null )
            throw new ArgumentNullException( nameof( presenter ) );
        if ( presenter.Scheduler != this )
            throw new InvalidOperationException( "A tag presenter can register only with its owning scene scheduler." );
        if ( presenter.IsDisposed || !_registeredPresenters.Add( presenter ) )
            return;

        presenter.ApplySchedulerFrame( _nowMilliseconds );
        if ( presenter.IsDisposed || !_registeredPresenters.Contains( presenter ) )
            return;

        ScheduleFreshEntry( presenter );
    }

    internal void Unregister( LpTagNamePresenter presenter )
    {
        if ( presenter is null || !_registeredPresenters.Remove( presenter ) )
            return;

        presenter.ClearSchedule( this );
    }

    internal void Reschedule( LpTagNamePresenter presenter )
    {
        if ( presenter is null )
            throw new ArgumentNullException( nameof( presenter ) );
        if ( !_registeredPresenters.Contains( presenter )
            || !presenter.IsScheduledWith( this, presenter.ScheduleGeneration ) )
        {
            return;
        }

        ScheduleFreshEntry( presenter );
    }

    private void ScheduleFreshEntry( LpTagNamePresenter presenter )
    {
        var dueMilliseconds = LpTagSchedulingRules.ResolveDueMilliseconds(
            _nowMilliseconds,
            presenter.SteamId,
            presenter.EffectiveEffectId,
            presenter.GraphemeCount,
            presenter.SurfacePolicy.SlideAmplitudePixels );
        if ( double.IsNaN( dueMilliseconds ) || double.IsInfinity( dueMilliseconds ) )
        {
            Unregister( presenter );
            return;
        }

        var generation = presenter.ReplaceSchedule( this, dueMilliseconds );
        _queue.Enqueue( new ScheduledPresenter( presenter, generation ), dueMilliseconds );
    }

    private void MoveOwnedPresentersTo( LpTagAnimationScheduler keeper )
    {
        if ( keeper == this )
            return;

        var presenters = new List<LpTagNamePresenter>( _ownedPresenters );
        foreach ( var presenter in presenters )
            presenter.MoveToScheduler( this, keeper );
    }

    private static bool IsCanonicalScheduler( LpTagAnimationScheduler scheduler )
    {
        return scheduler.GameObject.IsValid()
            && scheduler.GameObject.IsRoot
            && scheduler.GameObject.Name == ClientRootName;
    }

    private static bool PreferAsKeeper(
        LpTagAnimationScheduler candidate,
        LpTagAnimationScheduler current )
    {
        var candidateHasRegistrations = candidate._registeredPresenters.Count > 0;
        var currentHasRegistrations = current._registeredPresenters.Count > 0;
        if ( candidateHasRegistrations != currentHasRegistrations )
            return candidateHasRegistrations;

        if ( candidate._ownedPresenters.Count != current._ownedPresenters.Count )
            return candidate._ownedPresenters.Count > current._ownedPresenters.Count;

        var candidateEnabled = candidate.Enabled && candidate.GameObject.Enabled;
        var currentEnabled = current.Enabled && current.GameObject.Enabled;
        return candidateEnabled && !currentEnabled;
    }

    private readonly record struct ScheduledPresenter(
        LpTagNamePresenter Presenter,
        ulong Generation );
}
