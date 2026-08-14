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
using Sandbox.UI;
#if !LIFEPUNCH_LOCAL
using Dxura.RP.Game;
#endif

namespace LifePunch.DXRP.Addons.Tags;

/// <summary>
/// Owns the focused Tags menu mount, its per-open profile state, and safe teardown.
/// The animation scheduler is scene-owned separately and survives every menu close.
/// </summary>
public static class LpTagsMenuHost
{
    private const string MenuObjectName = "LifePunch.Tags.Menu";
    private const int SceneClosureMaxPasses = 8;

    private enum MenuLifecycle
    {
        Closed,
        Opening,
        Open,
        Closing
    }

    private enum TeardownOutcome
    {
        Success,
        Failed
    }

    private enum CameraRestoreOutcome
    {
        Success,
        Failed
    }

    private readonly record struct CameraLockOwnership(
        Component? Owner,
        bool IsOwned,
        bool CapturedValue );

    /// <summary>
    /// Keeps direct, retryable ownership of the concrete client after the menu
    /// state receives it. Disposal is complete only after the concrete client
    /// returns successfully.
    /// </summary>
    private sealed class ProfileClientLease : ILpTagProfileClient
    {
        private ILpTagProfileClient? _client;

        public ProfileClientLease( ILpTagProfileClient client )
        {
            _client = client;
        }

        private ILpTagProfileClient Client =>
            _client ?? throw new ObjectDisposedException( nameof(ProfileClientLease) );

        public LpTagClientMode Mode => Client.Mode;

        public event Action<LpTagProfileReadResult>? ReadCompleted
        {
            add => Client.ReadCompleted += value;
            remove => Client.ReadCompleted -= value;
        }

        public event Action<LpTagMutationResult>? MutationCompleted
        {
            add => Client.MutationCompleted += value;
            remove => Client.MutationCompleted -= value;
        }

        public void ReadOwnProfile( LpTagProfileReadRequest request, ulong menuGeneration ) =>
            Client.ReadOwnProfile( request, menuGeneration );

        public void MutateOwnProfile( LpTagMutationRequest request, ulong menuGeneration ) =>
            Client.MutateOwnProfile( request, menuGeneration );

        public void Dispose()
        {
            var client = _client;
            if ( client is null )
                return;

            client.Dispose();
            _client = null;
        }
    }

    private sealed class MenuTarget
    {
        public LpTagsMenu? Menu;
        public GameObject? OwnedObject;
        public bool MenuStateDisposeRequired;
        public bool MenuStateDisposed;
    }

    private sealed class MenuOwnership
    {
        public readonly List<MenuTarget> Targets = new();
        public Scene? MountScene;
        public LpTagsMenuState? DetachedState;
        public ProfileClientLease? ClientLease;
        public CameraLockOwnership CameraLock;

        public LpTagsMenu? FirstValidMenu()
        {
            foreach ( var target in Targets )
            {
                if ( target.Menu.IsValid() )
                    return target.Menu;
            }

            return null;
        }

        public bool ContainsMenu( LpTagsMenu menu )
        {
            foreach ( var target in Targets )
            {
                if ( target.Menu == menu )
                    return true;
            }

            return false;
        }
    }

    private static LpTagsMenu? _instance;
    private static MenuOwnership? _ownership;
    private static MenuLifecycle _lifecycle;
    private static bool _closeAttemptActive;

#if !LIFEPUNCH_LOCAL
    private static Func<ILpTagProfileClient>? _productionClientFactory;
#endif

    public static bool IsOpen => _lifecycle == MenuLifecycle.Open && _instance.IsValid();

    [ConCmd( "lifepunchtags" )]
    public static void LifePunchTagsConCmd() => Toggle();

    [ConCmd( "tags" )]
    public static void TagsConCmd() => Toggle();

    /// <summary>
    /// Configures the live transport seam. Each invocation must return a new,
    /// non-shared, disposable production client for the menu that will own it.
    /// </summary>
    public static void ConfigureProductionClientFactory( Func<ILpTagProfileClient>? factory )
    {
#if LIFEPUNCH_LOCAL
        _ = factory;
#else
        _productionClientFactory = factory;
#endif
    }

    /// <summary>Opens a fresh Tags menu, or closes/retries closing the owned target.</summary>
    public static void Toggle()
    {
        if ( _lifecycle == MenuLifecycle.Opening )
            return;

        if ( _lifecycle == MenuLifecycle.Closing )
        {
            RequestClose();
            return;
        }

        if ( _lifecycle == MenuLifecycle.Open && _instance.IsValid() )
        {
            RequestClose();
            return;
        }

        if ( _lifecycle == MenuLifecycle.Open )
        {
            RequestClose();
            if ( _lifecycle != MenuLifecycle.Closed )
                return;
        }

        OpenFreshMenu();
    }

    private static void OpenFreshMenu()
    {
        if ( _lifecycle != MenuLifecycle.Closed )
            return;

        _lifecycle = MenuLifecycle.Opening;
        var ownership = new MenuOwnership();
        _ownership = ownership;
        var published = false;

        try
        {
            if ( !Mount( ownership ) )
                return;

            var target = ownership.Targets.Count == 1 ? ownership.Targets[0] : null;
            var menu = target?.Menu;
            if ( target is null || !menu.IsValid() )
            {
                Log.Warning( "[lifepunchtags] Menu mount did not produce one valid owned target." );
                return;
            }

            ownership.ClientLease = new ProfileClientLease( CreateClient() );
            ownership.DetachedState = new LpTagsMenuState(
                ownership.ClientLease,
                LpTagViewerPreferenceStore.Shared );

            // Initialize may partially attach before throwing, so its hook becomes
            // mandatory before any later component/object destruction.
            target.MenuStateDisposeRequired = true;
            menu.Initialize( ownership.DetachedState );
            ownership.DetachedState = null; // The initialized menu owns its state.

            ownership.CameraLock = CaptureCameraLock();
            ApplyMenuCameraLock( ownership.CameraLock );

            _instance = menu;
            _lifecycle = MenuLifecycle.Open;
            published = true;
        }
        catch ( Exception )
        {
            Log.Warning( "[lifepunchtags] Menu open failed; provisional ownership is being released." );
        }
        finally
        {
            if ( !published )
                CompleteFailedOpen( ownership );
        }
    }

    private static void CompleteFailedOpen( MenuOwnership ownership )
    {
        if ( TryCloseOwnershipScene( ownership ) == TeardownOutcome.Failed )
        {
            _instance = ownership.FirstValidMenu();
            _ownership = ownership;
            _lifecycle = MenuLifecycle.Closing;
            Log.Warning( "[lifepunchtags] Provisional cleanup failed; opening is blocked until close retry succeeds." );
            return;
        }

        if ( TryRestoreCameraLock( ownership ) == CameraRestoreOutcome.Failed )
        {
            _instance = ownership.FirstValidMenu();
            _ownership = ownership;
            _lifecycle = MenuLifecycle.Closing;
            Log.Warning( "[lifepunchtags] Camera restoration failed; camera-only ownership retained for retry." );
            return;
        }

        _instance = null;
        _ownership = null;
        _lifecycle = MenuLifecycle.Closed;
    }

    /// <summary>
    /// Idempotently retries teardown of the retained ownership ledger. Camera
    /// ownership is released only after every valid Tags target is gone.
    /// </summary>
    public static void RequestClose()
    {
        if ( _lifecycle is MenuLifecycle.Closed or MenuLifecycle.Opening || _closeAttemptActive )
            return;

        var ownership = _ownership;
        if ( ownership is null )
        {
            _lifecycle = MenuLifecycle.Closing;
            Log.Warning( "[lifepunchtags] Close is blocked because its ownership ledger is unavailable." );
            return;
        }

        _lifecycle = MenuLifecycle.Closing;
        _closeAttemptActive = true;
        try
        {
            if ( TryCloseOwnershipScene( ownership ) == TeardownOutcome.Failed )
            {
                _instance = ownership.FirstValidMenu();
                Log.Warning( "[lifepunchtags] Close failed; retained Tags ownership is available for retry." );
                return;
            }

            if ( TryRestoreCameraLock( ownership ) == CameraRestoreOutcome.Failed )
            {
                _instance = ownership.FirstValidMenu();
                Log.Warning( "[lifepunchtags] Camera restoration failed; camera-only ownership retained for retry." );
                return;
            }

            _instance = null;
            _ownership = null;
            _lifecycle = MenuLifecycle.Closed;
        }
        finally
        {
            _closeAttemptActive = false;
        }
    }

    private static bool Mount( MenuOwnership ownership )
    {
        var scene = Game.ActiveScene;
        if ( scene is null )
        {
            Log.Warning( "[lifepunchtags] ActiveScene is null — cannot mount menu." );
            return false;
        }

        ownership.MountScene = scene;

        // Acquire the durable Tags scheduler before inspecting or creating menu UI.
        LpTagAnimationScheduler.GetOrCreate( scene );

        if ( TryCloseOwnershipScene( ownership ) == TeardownOutcome.Failed )
        {
            Log.Warning( "[lifepunchtags] Stale Tags cleanup failed; fresh mount aborted." );
            return false;
        }

#if LIFEPUNCH_LOCAL
        return MountOnScreenPanel( scene, ownership );
#else
        var baseline = CaptureMenuBaseline( scene );
        if ( baseline.Count > 0 )
        {
            RetainBaselineMenus( baseline, ownership );
            Log.Warning( "[lifepunchtags] Nonempty ShowUi baseline retained; fresh mount aborted." );
            return false;
        }

        LpTagsMenu? menu;
        try
        {
            menu = GameManager.ShowUi<LpTagsMenu>();
        }
        catch
        {
            RetainNewShowUiMenus( scene, baseline, ownership );
            Log.Warning( "[lifepunchtags] GameManager.ShowUi threw; only new Tags targets were retained." );
            return false;
        }

        RetainNewShowUiMenus( scene, baseline, ownership );
        if ( menu.IsValid() && baseline.Contains( menu ) )
        {
            RetainBaselineMenus( baseline, ownership );
            Log.Warning( "[lifepunchtags] ShowUi returned a baseline Tags target; fresh mount aborted." );
            return false;
        }

        if ( menu.IsValid() && !ownership.ContainsMenu( menu ) )
        {
            ownership.Targets.Add( new MenuTarget
            {
                Menu = menu,
                MenuStateDisposeRequired = true
            } );
        }

        if ( menu.IsValid() )
        {
            if ( ownership.Targets.Count == 1 )
                return true;

            if ( TryCloseOwnershipScene( ownership ) == TeardownOutcome.Failed )
            {
                Log.Warning( "[lifepunchtags] Ambiguous ShowUi targets could not be closed; fresh mount aborted." );
                return false;
            }

            Log.Warning( "[lifepunchtags] ShowUi produced ambiguous Tags targets; fresh mount aborted." );
            return false;
        }

        if ( TryCloseOwnershipScene( ownership ) == TeardownOutcome.Failed )
        {
            Log.Warning( "[lifepunchtags] Invalid ShowUi attempt could not be cleaned; fallback mount aborted." );
            return false;
        }

        Log.Warning( "[lifepunchtags] GameManager.ShowUi returned null — falling back to ScreenPanel." );
        return MountOnScreenPanel( scene, ownership );
#endif
    }

    /// <summary>
    /// Closes the Tags ownership boundary around the exact scene captured for
    /// this mount. Disposal/destruction callbacks may create another Tags menu,
    /// so every pass rescans after teardown and retains newly discovered targets.
    /// </summary>
    private static TeardownOutcome TryCloseOwnershipScene( MenuOwnership ownership )
    {
        for ( var pass = 0; pass < SceneClosureMaxPasses; pass++ )
        {
            if ( !TryRetainSceneMenus( ownership ) )
                return TeardownOutcome.Failed;

            var teardownOutcome = TryTeardownOwnership( ownership );

            // Rescan even when teardown failed: a hook may have created a new
            // direct scene target before reporting that failure.
            var rescanSucceeded = TryRetainSceneMenus( ownership );
            if ( teardownOutcome == TeardownOutcome.Failed || !rescanSucceeded )
                return TeardownOutcome.Failed;

            if ( ownership.Targets.Count == 0 )
                return TeardownOutcome.Success;
        }

        Log.Warning( "[lifepunchtags] Tags scene closure pass limit reached; ownership retained for retry." );
        return TeardownOutcome.Failed;
    }

    private static bool TryRetainSceneMenus( MenuOwnership ownership )
    {
        var scene = ownership.MountScene;
        if ( scene is null )
            return true;

        try
        {
            foreach ( var menu in EnumerateSceneMenus( scene ) )
            {
                if ( !menu.IsValid() || ownership.ContainsMenu( menu ) )
                    continue;

                ownership.Targets.Add( new MenuTarget
                {
                    Menu = menu,
                    MenuStateDisposeRequired = true
                } );
            }

            return true;
        }
        catch ( Exception )
        {
            Log.Warning( "[lifepunchtags] Tags scene enumeration failed; ownership retained for retry." );
            return false;
        }
    }

#if !LIFEPUNCH_LOCAL
    private static HashSet<LpTagsMenu> CaptureMenuBaseline( Scene scene )
    {
        var baseline = new HashSet<LpTagsMenu>();
        foreach ( var menu in EnumerateSceneMenus( scene ) )
        {
            if ( menu.IsValid() )
                baseline.Add( menu );
        }

        return baseline;
    }

    private static void RetainNewShowUiMenus(
        Scene scene,
        HashSet<LpTagsMenu> baseline,
        MenuOwnership ownership )
    {
        foreach ( var menu in EnumerateSceneMenus( scene ) )
        {
            if ( !menu.IsValid() || baseline.Contains( menu ) || ownership.ContainsMenu( menu ) )
                continue;

            ownership.Targets.Add( new MenuTarget
            {
                Menu = menu,
                MenuStateDisposeRequired = true
            } );
        }
    }

    private static void RetainBaselineMenus(
        HashSet<LpTagsMenu> baseline,
        MenuOwnership ownership )
    {
        foreach ( var menu in baseline )
        {
            if ( !menu.IsValid() || ownership.ContainsMenu( menu ) )
                continue;

            ownership.Targets.Add( new MenuTarget
            {
                Menu = menu,
                MenuStateDisposeRequired = true
            } );
        }
    }
#endif

    internal static IEnumerable<LpTagsMenu> EnumerateSceneMenus( Scene scene )
    {
        return scene.GetComponentsInChildren<LpTagsMenu>( true, true );
    }

    private static bool MountOnScreenPanel( Scene scene, MenuOwnership ownership )
    {
        try
        {
            var menuObject = scene.CreateObject();
            var target = new MenuTarget { OwnedObject = menuObject };
            ownership.Targets.Add( target );

            // The ledger already owns the exact object reference. Name is only
            // diagnostic chrome and is never consulted for cleanup authority.
            menuObject.Name = MenuObjectName;
            menuObject.AddComponent<ScreenPanel>();
            target.Menu = menuObject.AddComponent<LpTagsMenu>();
            return target.Menu.IsValid();
        }
        catch
        {
            Log.Warning( "[lifepunchtags] ScreenPanel mount failed; exact created object retained for cleanup." );
            return false;
        }
    }

    private static ILpTagProfileClient CreateClient()
    {
#if LIFEPUNCH_LOCAL
        return new LpTagLocalProfileClient();
#else
        var factory = _productionClientFactory;
        if ( factory is null )
            return new LpTagUnavailableProfileClient();

        ILpTagProfileClient? candidate;

        try
        {
            candidate = factory();
        }
        catch ( Exception )
        {
            Log.Warning( "[lifepunchtags] Production client factory threw; using unavailable integration." );
            return new LpTagUnavailableProfileClient();
        }

        if ( candidate is null )
        {
            Log.Warning( "[lifepunchtags] Production client factory returned null; using unavailable integration." );
            return new LpTagUnavailableProfileClient();
        }

        bool isProduction;
        try
        {
            isProduction = candidate.Mode == LpTagClientMode.Production;
        }
        catch ( Exception )
        {
            Log.Warning( "[lifepunchtags] Production client mode inspection failed; rejecting client." );
            DisposeRejectedClient( candidate );
            return new LpTagUnavailableProfileClient();
        }

        if ( isProduction )
            return candidate;

        Log.Warning( "[lifepunchtags] Production client factory returned a non-production client; rejecting client." );
        DisposeRejectedClient( candidate );
        return new LpTagUnavailableProfileClient();
#endif
    }

    private static void DisposeRejectedClient( ILpTagProfileClient client )
    {
        try
        {
            client.Dispose();
        }
        catch ( Exception )
        {
            Log.Warning( "[lifepunchtags] Rejected production client disposal failed." );
        }
    }

    private static TeardownOutcome TryTeardownOwnership( MenuOwnership ownership )
    {
        var stateCleanupSucceeded = TryDisposeDetachedState( ownership );
        foreach ( var target in ownership.Targets )
            stateCleanupSucceeded &= TryDisposeTargetState( target );

        if ( !stateCleanupSucceeded )
            return TeardownOutcome.Failed;

        // The state may have attempted and failed to dispose the same lease.
        // Retaining it in this ledger makes the next close retry call it directly.
        if ( !TryDisposeClientLease( ownership ) )
            return TeardownOutcome.Failed;

        var outcome = TeardownOutcome.Success;
        for ( var index = ownership.Targets.Count - 1; index >= 0; index-- )
        {
            var target = ownership.Targets[index];
            if ( TryDestroyTarget( target ) == TeardownOutcome.Success )
                ownership.Targets.RemoveAt( index );
            else
                outcome = TeardownOutcome.Failed;
        }

        return ownership.Targets.Count == 0 ? outcome : TeardownOutcome.Failed;
    }

    private static bool TryDisposeDetachedState( MenuOwnership ownership )
    {
        var state = ownership.DetachedState;
        if ( state is null )
            return true;

        try
        {
            state.Dispose();
            ownership.DetachedState = null;
            return true;
        }
        catch ( Exception )
        {
            Log.Warning( "[lifepunchtags] Detached menu state disposal failed; ownership retained." );
            return false;
        }
    }

    private static bool TryDisposeClientLease( MenuOwnership ownership )
    {
        var lease = ownership.ClientLease;
        if ( lease is null )
            return true;

        try
        {
            lease.Dispose();
            ownership.ClientLease = null;
            return true;
        }
        catch ( Exception )
        {
            Log.Warning( "[lifepunchtags] Profile client lease disposal failed; ownership retained." );
            return false;
        }
    }

    private static bool TryDisposeTargetState( MenuTarget target )
    {
        var menu = target.Menu;
        if ( menu.IsValid() && target.MenuStateDisposeRequired && !target.MenuStateDisposed )
        {
            try
            {
                menu.DisposeMenuState();
                target.MenuStateDisposed = true;
            }
            catch ( Exception )
            {
                Log.Warning( "[lifepunchtags] Menu state disposal failed; target retained without destruction." );
                return false;
            }
        }
        else if ( !menu.IsValid() )
        {
            target.Menu = null;
            target.MenuStateDisposed = true;
        }

        return true;
    }

    private static TeardownOutcome TryDestroyTarget( MenuTarget target )
    {
        if ( !target.MenuStateDisposed && target.MenuStateDisposeRequired )
        {
            Log.Warning( "[lifepunchtags] Target destruction blocked until menu state disposal succeeds." );
            return TeardownOutcome.Failed;
        }

        var ownedObject = target.OwnedObject;
        if ( ownedObject.IsValid() )
        {
            try
            {
                ownedObject.Destroy();
            }
            catch ( Exception )
            {
                Log.Warning( "[lifepunchtags] Exact Tags menu object destruction failed; target retained." );
                return TeardownOutcome.Failed;
            }

            if ( ownedObject.IsValid() )
            {
                Log.Warning( "[lifepunchtags] Exact Tags menu object remained valid after destruction; target retained." );
                return TeardownOutcome.Failed;
            }

            target.OwnedObject = null;
            if ( target.Menu.IsValid() )
            {
                Log.Warning( "[lifepunchtags] Tags menu component remained valid after owned-object destruction." );
                return TeardownOutcome.Failed;
            }

            target.Menu = null;
            return TeardownOutcome.Success;
        }

        target.OwnedObject = null;
        var menu = target.Menu;
        if ( !menu.IsValid() )
        {
            target.Menu = null;
            return TeardownOutcome.Success;
        }

        try
        {
            menu.Destroy();
        }
        catch ( Exception )
        {
            Log.Warning( "[lifepunchtags] Tags menu component destruction failed; target retained." );
            return TeardownOutcome.Failed;
        }

        if ( menu.IsValid() )
        {
            Log.Warning( "[lifepunchtags] Tags menu component remained valid after destruction; target retained." );
            return TeardownOutcome.Failed;
        }

        target.Menu = null;
        return TeardownOutcome.Success;
    }

    private static CameraLockOwnership CaptureCameraLock()
    {
#if !LIFEPUNCH_LOCAL
        var player = Player.Local;
        if ( player.IsValid() )
            return new CameraLockOwnership( player, true, player.LockCamera );
#endif

        return default;
    }

    private static void ApplyMenuCameraLock( CameraLockOwnership ownership )
    {
#if !LIFEPUNCH_LOCAL
        if ( ownership.IsOwned && ownership.Owner is Player player && player.IsValid() )
            player.LockCamera = true;
#endif
    }

    private static CameraRestoreOutcome TryRestoreCameraLock( MenuOwnership ownership )
    {
#if !LIFEPUNCH_LOCAL
        var cameraLock = ownership.CameraLock;
        if ( !cameraLock.IsOwned )
            return CameraRestoreOutcome.Success;

        if ( cameraLock.Owner is not Player player || !player.IsValid() )
        {
            // The exact captured owner is definitively gone, so restoration is
            // no longer applicable and the ownership token can be released.
            ownership.CameraLock = default;
            return CameraRestoreOutcome.Success;
        }

        try
        {
            player.LockCamera = cameraLock.CapturedValue;
        }
        catch ( Exception )
        {
            Log.Warning( "[lifepunchtags] Captured camera-lock restoration failed." );
            return CameraRestoreOutcome.Failed;
        }

        ownership.CameraLock = default;
#endif

        return CameraRestoreOutcome.Success;
    }
}
