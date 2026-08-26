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

public sealed class LpTagViewerPreferenceStore : ILpTagViewerPreferenceEditor
{
    public const string ReduceMotionCookieKey = "lifepunchtags.viewer.reduce_motion";
    public const string DisableAnimationsCookieKey = "lifepunchtags.viewer.disable_animations";
    public const string HideDotPlusCookieKey = "lifepunchtags.viewer.hide_dot_plus";

    private static readonly object SharedGate = new();
    private static LpTagViewerPreferenceStore? _shared;

    private readonly Queue<LpViewerTagPreferencesV1> _pendingNotifications = new();
    private bool _isPublishingChanges;

    private LpTagViewerPreferenceStore()
    {
        Current = new LpViewerTagPreferencesV1(
            Game.Cookies.Get<bool>( ReduceMotionCookieKey, false ),
            Game.Cookies.Get<bool>( DisableAnimationsCookieKey, false ),
            Game.Cookies.Get<bool>( HideDotPlusCookieKey, false ) );
    }

    public static LpTagViewerPreferenceStore Shared
    {
        get
        {
            lock ( SharedGate )
            {
                return _shared ??= new LpTagViewerPreferenceStore();
            }
        }
    }

    public LpViewerTagPreferencesV1 Current { get; private set; }

    public event Action<LpViewerTagPreferencesV1>? Changed;

    public void SetReduceMotion( bool value )
    {
        if ( value == Current.ReduceMotion )
            return;

        Game.Cookies.Set<bool>( ReduceMotionCookieKey, value );
        Publish( Current with { ReduceMotion = value } );
    }

    public void SetDisableAnimations( bool value )
    {
        if ( value == Current.DisableAnimations )
            return;

        Game.Cookies.Set<bool>( DisableAnimationsCookieKey, value );
        Publish( Current with { DisableAnimations = value } );
    }

    public void SetHideDotPlusDecorations( bool value )
    {
        if ( value == Current.HideDotPlusDecorations )
            return;

        Game.Cookies.Set<bool>( HideDotPlusCookieKey, value );
        Publish( Current with { HideDotPlusDecorations = value } );
    }

    private void Publish( LpViewerTagPreferencesV1 value )
    {
        Current = value;
        _pendingNotifications.Enqueue( value );
        if ( _isPublishingChanges )
            return;

        _isPublishingChanges = true;
        try
        {
            while ( _pendingNotifications.Count > 0 )
            {
                var notification = _pendingNotifications.Dequeue();
                Changed?.Invoke( notification );
            }
        }
        finally
        {
            _isPublishingChanges = false;
        }
    }

#if LIFEPUNCH_LOCAL
    internal static void ResetSharedForTests()
    {
        lock ( SharedGate )
        {
            if ( _shared is not null )
                _shared.Changed = null;

            _shared = null;
        }
    }
#endif
}
