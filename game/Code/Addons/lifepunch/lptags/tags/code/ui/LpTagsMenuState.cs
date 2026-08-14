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
using System.Threading;

namespace LifePunch.DXRP.Addons.Tags;

public sealed class LpTagsMenuState : IDisposable
{
    private static long _generationSeed;

    private readonly ILpTagProfileClient _client;
    private readonly ILpTagViewerPreferenceEditor _viewerPreferences;
    private readonly Dictionary<MenuField, QueuedValue> _queuedDifferences = new();

    private ActiveMutation? _activeMutation;
    private VerificationState? _verification;
    private LpStoredTagPreferencesV1? _retainedRetryDraft;
    private string? _activeReadIdentifier;
    private bool _hasCanonicalProfile;
    private bool _readOnly;
    private bool _requiresRefreshBeforeRetry;
    private bool _disposed;
    private ulong _loadGeneration;

    public LpTagsMenuState( ILpTagProfileClient client, ILpTagViewerPreferenceEditor viewerPreferences )
    {
        _client = client ?? throw new ArgumentNullException( nameof( client ) );
        _viewerPreferences = viewerPreferences ?? throw new ArgumentNullException( nameof( viewerPreferences ) );

        SelectedSurface = LpTagSurface.Chat;
        Canonical = PlainReadOnlyProfile( default );
        Draft = Canonical;
        ViewerPreferences = _viewerPreferences.Current;

        _client.ReadCompleted += AcceptReadResult;
        _client.MutationCompleted += AcceptMutationResult;
        _viewerPreferences.Changed += AcceptViewerPreferences;

        Refresh();
    }

    public event Action? Changed;

    public LpTagClientMode ClientMode => _client.Mode;
    public LpTagSurface SelectedSurface { get; private set; }
    public LpStoredTagPreferencesV1 Canonical { get; private set; }
    public LpStoredTagPreferencesV1 Draft { get; private set; }
    public LpViewerTagPreferencesV1 ViewerPreferences { get; private set; }
    public LpTagClientStatus Status { get; private set; }
    public string StatusCopy { get; private set; } = string.Empty;
    public ulong LoadGeneration => _loadGeneration;
    public bool HasCanonicalProfile => _hasCanonicalProfile;
    public bool IsReadOnly => _readOnly;
    public bool HasActiveMutation => _activeMutation.HasValue;
    public int QueuedDifferenceCount => _queuedDifferences.Count;

    public bool CanEditOutgoing =>
        !_disposed &&
        _client.Mode != LpTagClientMode.Unavailable &&
        _hasCanonicalProfile &&
        !_readOnly &&
        !_requiresRefreshBeforeRetry &&
        _activeReadIdentifier is null &&
        !_verification.HasValue;

    public bool CanRetry =>
        CanEditOutgoing &&
        !_activeMutation.HasValue &&
        _retainedRetryDraft.HasValue;

    public bool CanRefresh =>
        !_disposed &&
        !_activeMutation.HasValue &&
        !_verification.HasValue &&
        _activeReadIdentifier is null;

    public bool IsOnline =>
        !_disposed &&
        _client.Mode == LpTagClientMode.Production &&
        _hasCanonicalProfile &&
        Status is not LpTagClientStatus.Loading and not LpTagClientStatus.Unavailable;

    public void SelectSurface( LpTagSurface surface )
    {
        if ( _disposed || !IsSurface( surface ) || surface == SelectedSurface )
            return;

        SelectedSurface = surface;
        RaiseChanged();
    }

    public void SelectEffect( LpTagSurface surface, string effectKey )
    {
        if ( !CanEditOutgoing || !IsSurface( surface ) )
            return;
        if ( effectKey == LpTagProfileRules.NoneKey ||
             !LpTagEffectCatalog.TryGet( effectKey, out var definition ) ||
             !definition.Supports( surface ) )
        {
            SetStatus( LpTagClientStatus.SaveFailed, "Choose a valid style" );
            return;
        }

        var current = GetSurface( Draft, surface );
        if ( current.SelectedEffectKey == definition.Key )
            return;

        BeginNewDeliberateEdit();
        Draft = SetSurface( Draft, surface, current with { SelectedEffectKey = definition.Key } );
        QueueOrDispatch( new MenuField( surface, LpTagMutationKind.SetEffect ) );
    }

    public void SetEnabled( LpTagSurface surface, bool enabled )
    {
        if ( !CanEditOutgoing || !IsSurface( surface ) )
            return;

        var current = GetSurface( Draft, surface );
        if ( enabled && current.SelectedEffectKey == LpTagProfileRules.NoneKey )
        {
            SetStatus( LpTagClientStatus.SaveFailed, "Choose a style first" );
            return;
        }
        if ( current.Enabled == enabled )
            return;

        BeginNewDeliberateEdit();
        Draft = SetSurface( Draft, surface, current with { Enabled = enabled } );
        QueueOrDispatch( new MenuField( surface, LpTagMutationKind.SetEnabled ) );
    }

    public void SetReduceMotion( bool value )
    {
        if ( !_disposed )
            _viewerPreferences.SetReduceMotion( value );
    }

    public void SetDisableAnimations( bool value )
    {
        if ( !_disposed )
            _viewerPreferences.SetDisableAnimations( value );
    }

    public void SetHideDotPlusDecorations( bool value )
    {
        if ( !_disposed )
            _viewerPreferences.SetHideDotPlusDecorations( value );
    }

    public void Retry()
    {
        if ( !CanRetry )
            return;

        Draft = PrepareRetryDraft( Canonical, _retainedRetryDraft!.Value );
        _retainedRetryDraft = null;
        RebuildQueuedDifferences();
        if ( _queuedDifferences.Count == 0 )
        {
            SetSavedStatus();
            return;
        }

        DispatchNextDifference();
    }

    public void Refresh()
    {
        if ( _disposed || _activeMutation.HasValue || _verification.HasValue || _activeReadIdentifier is not null )
            return;

        var generation = NextGeneration();
        var requestIdentifier = NewRequestIdentifier();
        _loadGeneration = generation;
        _activeReadIdentifier = requestIdentifier;
        _hasCanonicalProfile = false;
        _readOnly = false;
        _queuedDifferences.Clear();
        SetStatusWithoutNotification( LpTagClientStatus.Loading, LoadingCopy() );
        var request = new LpTagProfileReadRequest( requestIdentifier );
        RaiseChanged();
        if ( CanInvokeRead( requestIdentifier, generation, null ) )
            _client.ReadOwnProfile( request, generation );
    }

    public void AcceptReadResult( LpTagProfileReadResult result )
    {
        if ( _disposed ||
             result.MenuGeneration != _loadGeneration ||
             _activeReadIdentifier is null ||
             !string.Equals( result.RequestIdentifier, _activeReadIdentifier, StringComparison.Ordinal ) )
            return;

        _activeReadIdentifier = null;
        if ( _verification.HasValue )
            AcceptVerificationRead( result );
        else
            AcceptProfileRead( result );
    }

    public void AcceptMutationResult( LpTagMutationResult result )
    {
        if ( _disposed || !_activeMutation.HasValue )
            return;

        var active = _activeMutation.Value;
        if ( result.MenuGeneration != _loadGeneration ||
             result.MenuGeneration != active.Generation ||
             !string.Equals( result.RequestIdentifier, active.RequestIdentifier, StringComparison.Ordinal ) )
            return;

        _activeMutation = null;
        switch ( result.Status )
        {
            case LpTagMutationResultStatus.Success:
                if ( !IsCanonical( result.CanonicalRecord ) ||
                     active.Prior.Revision == ulong.MaxValue ||
                     result.CanonicalRecord.Revision != active.Prior.Revision + 1UL )
                {
                    FailMutation( active.Prior, "Save failed · Retry" );
                    return;
                }

                Canonical = result.CanonicalRecord;
                Draft = RebaseDraftMetadata( Draft, Canonical );
                _hasCanonicalProfile = true;
                _retainedRetryDraft = null;
                RebuildQueuedDifferences();
                if ( _queuedDifferences.Count > 0 )
                    DispatchNextDifference();
                else
                    SetSavedStatus();
                return;
            case LpTagMutationResultStatus.Rejected:
                FailMutation(
                    active.Prior,
                    string.IsNullOrWhiteSpace( result.FilteredError ) ? "Change rejected · Retry" : result.FilteredError );
                return;
            case LpTagMutationResultStatus.Conflict:
                if ( IsCanonical( result.CanonicalRecord ) && result.CanonicalRecord.Revision > active.Prior.Revision )
                {
                    FailMutation(
                        result.CanonicalRecord,
                        "Profile changed elsewhere",
                        LpTagClientStatus.Conflict );
                }
                else
                {
                    FailMutation( active.Prior, "Save failed · Retry" );
                }
                return;
            case LpTagMutationResultStatus.GuaranteedFailure:
                FailMutation( active.Prior, "Save failed · Retry" );
                return;
            case LpTagMutationResultStatus.Uncertain:
                BeginVerification( active );
                return;
            default:
                FailMutation( active.Prior, "Save failed · Retry" );
                return;
        }
    }

    public void Dispose()
    {
        if ( _disposed )
            return;

        _disposed = true;
        unchecked
        {
            _loadGeneration++;
        }

        _activeReadIdentifier = null;
        _activeMutation = null;
        _verification = null;
        _retainedRetryDraft = null;
        _queuedDifferences.Clear();

        _viewerPreferences.Changed -= AcceptViewerPreferences;
        _client.ReadCompleted -= AcceptReadResult;
        _client.MutationCompleted -= AcceptMutationResult;
        _client.Dispose();
        Changed = null;
    }

    private void AcceptProfileRead( LpTagProfileReadResult result )
    {
        switch ( result.Status )
        {
            case LpTagProfileReadStatus.Success:
                if ( !IsCanonical( result.CanonicalRecord ) )
                {
                    EnterPermanentReadOnly( LpTagProfileReadStatus.UnreadableReadOnly, result.CanonicalRecord );
                    return;
                }

                Canonical = result.CanonicalRecord;
                Draft = Canonical;
                _hasCanonicalProfile = true;
                _readOnly = false;
                _requiresRefreshBeforeRetry = false;
                if ( _client.Mode == LpTagClientMode.Unavailable )
                    SetStatus( LpTagClientStatus.Unavailable, "Integration unavailable" );
                else if ( _client.Mode == LpTagClientMode.LocalPreview )
                    SetStatus( LpTagClientStatus.LocalPreview, "LOCAL PREVIEW · Ready for this session" );
                else
                    SetStatus( LpTagClientStatus.Idle, "Global profile loaded" );
                return;
            case LpTagProfileReadStatus.Unavailable:
                _readOnly = false;
                SetStatus( LpTagClientStatus.Unavailable, UnavailableCopy() );
                return;
            case LpTagProfileReadStatus.UnsupportedReadOnly:
            case LpTagProfileReadStatus.UnreadableReadOnly:
                EnterPermanentReadOnly( result.Status, result.CanonicalRecord );
                return;
            default:
                EnterPermanentReadOnly( LpTagProfileReadStatus.UnreadableReadOnly, result.CanonicalRecord );
                return;
        }
    }

    private void AcceptVerificationRead( LpTagProfileReadResult result )
    {
        var verification = _verification!.Value;
        _verification = null;

        if ( result.Status is LpTagProfileReadStatus.UnsupportedReadOnly or LpTagProfileReadStatus.UnreadableReadOnly )
        {
            EnterPermanentReadOnly( result.Status, result.CanonicalRecord );
            return;
        }

        if ( result.Status != LpTagProfileReadStatus.Success || !IsCanonical( result.CanonicalRecord ) )
        {
            Canonical = verification.Prior;
            Draft = Canonical;
            _hasCanonicalProfile = true;
            _retainedRetryDraft = verification.LatestDraft;
            _requiresRefreshBeforeRetry = true;
            _queuedDifferences.Clear();
            SetStatus( LpTagClientStatus.Unavailable, "Save outcome unresolved · Refresh required" );
            return;
        }

        _hasCanonicalProfile = true;
        _readOnly = false;
        _requiresRefreshBeforeRetry = false;
        if ( result.CanonicalRecord == verification.Candidate )
        {
            Canonical = result.CanonicalRecord;
            Draft = RebaseDraftMetadata( verification.LatestDraft, Canonical );
            _retainedRetryDraft = null;
            RebuildQueuedDifferences();
            SetSavedStatus();
            if ( _queuedDifferences.Count > 0 )
                DispatchNextDifference();
            return;
        }

        _retainedRetryDraft = verification.LatestDraft;
        _queuedDifferences.Clear();
        if ( result.CanonicalRecord == verification.Prior )
        {
            Canonical = verification.Prior;
            Draft = Canonical;
            SetStatus( LpTagClientStatus.SaveFailed, "Save failed · Retry" );
            return;
        }

        Canonical = result.CanonicalRecord;
        Draft = Canonical;
        SetStatus( LpTagClientStatus.Conflict, "Profile changed elsewhere" );
    }

    private void BeginVerification( ActiveMutation active )
    {
        var latestDraft = Draft;
        _retainedRetryDraft = latestDraft;
        _queuedDifferences.Clear();
        var verification = new VerificationState( active.Prior, active.Candidate, latestDraft );
        var generation = NextGeneration();
        var requestIdentifier = NewRequestIdentifier();
        _verification = verification;
        _loadGeneration = generation;
        _activeReadIdentifier = requestIdentifier;
        SetStatusWithoutNotification( LpTagClientStatus.Verifying, "Verifying…" );
        var request = new LpTagProfileReadRequest( requestIdentifier );
        RaiseChanged();
        if ( CanInvokeRead( requestIdentifier, generation, verification ) )
            _client.ReadOwnProfile( request, generation );
    }

    private void EnterPermanentReadOnly( LpTagProfileReadStatus status, LpStoredTagPreferencesV1 supplied )
    {
        Canonical = PlainReadOnlyProfile( supplied );
        Draft = Canonical;
        _hasCanonicalProfile = true;
        _readOnly = true;
        _requiresRefreshBeforeRetry = false;
        _activeMutation = null;
        _verification = null;
        _retainedRetryDraft = null;
        _queuedDifferences.Clear();

        var copy = status == LpTagProfileReadStatus.UnsupportedReadOnly
            ? "Profile schema is unsupported · Read-only"
            : "Stored profile is unreadable · Read-only";
        SetStatus( LpTagClientStatus.ReadOnly, copy );
    }

    private void BeginNewDeliberateEdit()
    {
        if ( !_activeMutation.HasValue )
            _retainedRetryDraft = null;
    }

    private void QueueOrDispatch( MenuField field )
    {
        var baseline = _activeMutation?.Candidate ?? Canonical;
        if ( FieldEquals( Draft, baseline, field ) )
            _queuedDifferences.Remove( field );
        else
            _queuedDifferences[field] = GetQueuedValue( Draft, field );

        if ( !_activeMutation.HasValue )
            DispatchNextDifference();
        else
            RaiseChanged();
    }

    private void DispatchNextDifference()
    {
        if ( _disposed || _activeMutation.HasValue || !CanEditOutgoing )
            return;

        if ( !TryGetNextDifference( out var field, out var value ) )
        {
            SetSavedStatus();
            return;
        }

        if ( Canonical.Revision == ulong.MaxValue )
        {
            _retainedRetryDraft = Draft;
            Draft = Canonical;
            _queuedDifferences.Clear();
            SetStatus( LpTagClientStatus.SaveFailed, "Save failed · Retry" );
            return;
        }

        _queuedDifferences.Remove( field );
        var requestIdentifier = NewRequestIdentifier();
        var request = field.Kind == LpTagMutationKind.SetEffect
            ? new LpTagMutationRequest(
                requestIdentifier,
                Canonical.Revision,
                field.Surface,
                LpTagMutationKind.SetEffect,
                false,
                value.EffectKey )
            : new LpTagMutationRequest(
                requestIdentifier,
                Canonical.Revision,
                field.Surface,
                LpTagMutationKind.SetEnabled,
                value.Enabled,
                string.Empty );
        var candidate = ApplyQueuedValue( Canonical, field, value ) with { Revision = Canonical.Revision + 1UL };
        var active = new ActiveMutation( requestIdentifier, _loadGeneration, Canonical, candidate );
        _activeMutation = active;
        SetStatusWithoutNotification(
            LpTagClientStatus.Saving,
            _client.Mode == LpTagClientMode.LocalPreview ? "LOCAL PREVIEW · Applying for this session…" : "Saving…" );
        RaiseChanged();
        if ( CanInvokeMutation( active ) )
            _client.MutateOwnProfile( request, active.Generation );
    }

    private bool TryGetNextDifference( out MenuField field, out QueuedValue value )
    {
        var surfaces = new[] { LpTagSurface.Chat, LpTagSurface.Scoreboard, LpTagSurface.Nameplate };
        foreach ( var surface in surfaces )
        {
            var effectField = new MenuField( surface, LpTagMutationKind.SetEffect );
            if ( _queuedDifferences.TryGetValue( effectField, out value ) )
            {
                field = effectField;
                return true;
            }

            var enabledField = new MenuField( surface, LpTagMutationKind.SetEnabled );
            if ( _queuedDifferences.TryGetValue( enabledField, out value ) )
            {
                field = enabledField;
                return true;
            }
        }

        field = default;
        value = default;
        return false;
    }

    private void RebuildQueuedDifferences()
    {
        _queuedDifferences.Clear();
        AddDifference( LpTagSurface.Chat, LpTagMutationKind.SetEffect );
        AddDifference( LpTagSurface.Chat, LpTagMutationKind.SetEnabled );
        AddDifference( LpTagSurface.Scoreboard, LpTagMutationKind.SetEffect );
        AddDifference( LpTagSurface.Scoreboard, LpTagMutationKind.SetEnabled );
        AddDifference( LpTagSurface.Nameplate, LpTagMutationKind.SetEffect );
        AddDifference( LpTagSurface.Nameplate, LpTagMutationKind.SetEnabled );
    }

    private void AddDifference( LpTagSurface surface, LpTagMutationKind kind )
    {
        var field = new MenuField( surface, kind );
        if ( !FieldEquals( Draft, Canonical, field ) )
            _queuedDifferences[field] = GetQueuedValue( Draft, field );
    }

    private void FailMutation(
        LpStoredTagPreferencesV1 canonical,
        string copy,
        LpTagClientStatus status = LpTagClientStatus.SaveFailed )
    {
        _retainedRetryDraft = Draft;
        Canonical = canonical;
        Draft = Canonical;
        _hasCanonicalProfile = true;
        _queuedDifferences.Clear();
        SetStatus( status, copy );
    }

    private void SetSavedStatus()
    {
        if ( _client.Mode == LpTagClientMode.Production )
            SetStatus( LpTagClientStatus.Saved, "Saved globally" );
        else if ( _client.Mode == LpTagClientMode.LocalPreview )
            SetStatus( LpTagClientStatus.LocalPreview, "LOCAL PREVIEW · Applied for this session" );
        else
            SetStatus( LpTagClientStatus.Unavailable, "Integration unavailable" );
    }

    private string LoadingCopy()
    {
        return _client.Mode switch
        {
            LpTagClientMode.LocalPreview => "LOCAL PREVIEW · Loading session profile…",
            LpTagClientMode.Unavailable => "Integration unavailable",
            _ => "Loading global profile…"
        };
    }

    private string UnavailableCopy()
    {
        return _client.Mode switch
        {
            LpTagClientMode.LocalPreview => "LOCAL PREVIEW · Session profile unavailable · Refresh",
            LpTagClientMode.Unavailable => "Integration unavailable",
            _ => "Global profile unavailable · Refresh"
        };
    }

    private void AcceptViewerPreferences( LpViewerTagPreferencesV1 preferences )
    {
        if ( _disposed || preferences == ViewerPreferences )
            return;

        ViewerPreferences = preferences;
        RaiseChanged();
    }

    private void SetStatus( LpTagClientStatus status, string copy )
    {
        SetStatusWithoutNotification( status, copy );
        RaiseChanged();
    }

    private void SetStatusWithoutNotification( LpTagClientStatus status, string copy )
    {
        Status = status;
        StatusCopy = copy;
    }

    private void RaiseChanged()
    {
        Changed?.Invoke();
    }

    private bool CanInvokeRead(
        string requestIdentifier,
        ulong generation,
        VerificationState? verification )
    {
        return !_disposed &&
            _loadGeneration == generation &&
            string.Equals( _activeReadIdentifier, requestIdentifier, StringComparison.Ordinal ) &&
            _verification == verification;
    }

    private bool CanInvokeMutation( ActiveMutation active )
    {
        return !_disposed &&
            _loadGeneration == active.Generation &&
            _activeMutation.HasValue &&
            _activeMutation.Value == active;
    }

    private static LpStoredTagPreferencesV1 PrepareRetryDraft(
        LpStoredTagPreferencesV1 canonical,
        LpStoredTagPreferencesV1 retained )
    {
        return retained with
        {
            SchemaVersion = canonical.SchemaVersion,
            Revision = canonical.Revision,
            Chat = PrepareRetrySurface( canonical.Chat, retained.Chat ),
            Scoreboard = PrepareRetrySurface( canonical.Scoreboard, retained.Scoreboard ),
            Nameplate = PrepareRetrySurface( canonical.Nameplate, retained.Nameplate )
        };
    }

    private static LpStoredTagPreferencesV1 RebaseDraftMetadata(
        LpStoredTagPreferencesV1 draft,
        LpStoredTagPreferencesV1 canonical )
    {
        return draft with
        {
            SchemaVersion = canonical.SchemaVersion,
            Revision = canonical.Revision
        };
    }

    private static LpTagSurfacePreference PrepareRetrySurface(
        LpTagSurfacePreference canonical,
        LpTagSurfacePreference retained )
    {
        return retained.SelectedEffectKey == LpTagProfileRules.NoneKey &&
               canonical.SelectedEffectKey != LpTagProfileRules.NoneKey
            ? retained with { SelectedEffectKey = canonical.SelectedEffectKey }
            : retained;
    }

    private static bool FieldEquals(
        LpStoredTagPreferencesV1 left,
        LpStoredTagPreferencesV1 right,
        MenuField field )
    {
        var leftSurface = GetSurface( left, field.Surface );
        var rightSurface = GetSurface( right, field.Surface );
        return field.Kind == LpTagMutationKind.SetEffect
            ? string.Equals( leftSurface.SelectedEffectKey, rightSurface.SelectedEffectKey, StringComparison.Ordinal )
            : leftSurface.Enabled == rightSurface.Enabled;
    }

    private static QueuedValue GetQueuedValue( LpStoredTagPreferencesV1 profile, MenuField field )
    {
        var surface = GetSurface( profile, field.Surface );
        return new QueuedValue( surface.Enabled, surface.SelectedEffectKey );
    }

    private static LpStoredTagPreferencesV1 ApplyQueuedValue(
        LpStoredTagPreferencesV1 profile,
        MenuField field,
        QueuedValue value )
    {
        var surface = GetSurface( profile, field.Surface );
        var changed = field.Kind == LpTagMutationKind.SetEffect
            ? surface with { SelectedEffectKey = value.EffectKey }
            : surface with { Enabled = value.Enabled };
        return SetSurface( profile, field.Surface, changed );
    }

    private static LpTagSurfacePreference GetSurface( LpStoredTagPreferencesV1 profile, LpTagSurface surface )
    {
        return surface switch
        {
            LpTagSurface.Chat => profile.Chat,
            LpTagSurface.Scoreboard => profile.Scoreboard,
            LpTagSurface.Nameplate => profile.Nameplate,
            _ => default
        };
    }

    private static LpStoredTagPreferencesV1 SetSurface(
        LpStoredTagPreferencesV1 profile,
        LpTagSurface surface,
        LpTagSurfacePreference preference )
    {
        return surface switch
        {
            LpTagSurface.Chat => profile with { Chat = preference },
            LpTagSurface.Scoreboard => profile with { Scoreboard = preference },
            LpTagSurface.Nameplate => profile with { Nameplate = preference },
            _ => profile
        };
    }

    private static LpStoredTagPreferencesV1 PlainReadOnlyProfile( LpStoredTagPreferencesV1 supplied )
    {
        var off = new LpTagSurfacePreference( false, LpTagProfileRules.NoneKey );
        return new LpStoredTagPreferencesV1(
            supplied.SchemaVersion == 0 ? LpTagProfileRules.SchemaVersion : supplied.SchemaVersion,
            supplied.Revision,
            off,
            off,
            off );
    }

    private static bool IsCanonical( LpStoredTagPreferencesV1 profile )
    {
        return LpTagProfileRules.InspectStored( profile, out var inspected, out _ ) == LpTagProfileInspectionStatus.Canonical &&
            inspected == profile;
    }

    private static bool IsSurface( LpTagSurface surface )
    {
        return surface is LpTagSurface.Chat or LpTagSurface.Scoreboard or LpTagSurface.Nameplate;
    }

    private static string NewRequestIdentifier()
    {
        return Guid.NewGuid().ToString( "N" );
    }

    private static ulong NextGeneration()
    {
        return unchecked( (ulong)Interlocked.Increment( ref _generationSeed ) );
    }

    private readonly record struct MenuField( LpTagSurface Surface, LpTagMutationKind Kind );
    private readonly record struct QueuedValue( bool Enabled, string EffectKey );
    private readonly record struct ActiveMutation(
        string RequestIdentifier,
        ulong Generation,
        LpStoredTagPreferencesV1 Prior,
        LpStoredTagPreferencesV1 Candidate );
    private readonly record struct VerificationState(
        LpStoredTagPreferencesV1 Prior,
        LpStoredTagPreferencesV1 Candidate,
        LpStoredTagPreferencesV1 LatestDraft );
}
