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

#if LIFEPUNCH_LOCAL
public sealed class LpTagLocalProfileClient : ILpTagProfileClient
{
    private static readonly object ProfileGate = new();
    private static LpStoredTagPreferencesV1 _processProfile = LpTagProfileRules.CreateNew();

    private readonly Queue<InjectedRead> _injectedReads = new();
    private readonly Queue<InjectedMutation> _injectedMutations = new();
    private bool _disposed;

    public LpTagClientMode Mode => LpTagClientMode.LocalPreview;

    public event Action<LpTagProfileReadResult>? ReadCompleted;
    public event Action<LpTagMutationResult>? MutationCompleted;

    public void ReadOwnProfile( LpTagProfileReadRequest request, ulong menuGeneration )
    {
        if ( _disposed )
            return;

        LpTagProfileReadResult result;
        lock ( ProfileGate )
        {
            if ( _injectedReads.Count == 0 )
            {
                result = new LpTagProfileReadResult(
                    request.RequestIdentifier,
                    menuGeneration,
                    LpTagProfileReadStatus.Success,
                    _processProfile,
                    string.Empty );
            }
            else
            {
                var injected = _injectedReads.Dequeue();
                var canonical = injected.HasCanonicalRecord ? injected.CanonicalRecord : _processProfile;
                result = new LpTagProfileReadResult(
                    request.RequestIdentifier,
                    menuGeneration,
                    injected.Status,
                    canonical,
                    injected.FilteredError );
            }
        }

        ReadCompleted?.Invoke( result );
    }

    public void MutateOwnProfile( LpTagMutationRequest request, ulong menuGeneration )
    {
        if ( _disposed )
            return;

        LpTagMutationResult result;
        lock ( ProfileGate )
        {
            var prior = _processProfile;
            if ( !LpTagProfileRules.TryApplyMutation( prior, request, out var candidate, out var validationError ) )
            {
                var status = validationError == LpTagProfileError.StaleRevision
                    ? LpTagMutationResultStatus.Conflict
                    : LpTagMutationResultStatus.Rejected;
                result = new LpTagMutationResult(
                    request.RequestIdentifier,
                    menuGeneration,
                    status,
                    prior,
                    validationError,
                    FilteredCopy( validationError ) );
            }
            else if ( prior.Revision == ulong.MaxValue )
            {
                result = new LpTagMutationResult(
                    request.RequestIdentifier,
                    menuGeneration,
                    LpTagMutationResultStatus.GuaranteedFailure,
                    prior,
                    LpTagProfileError.CorruptRecord,
                    "Local preview revision is unavailable" );
            }
            else
            {
                var applied = candidate with { Revision = prior.Revision + 1UL };
                var injected = _injectedMutations.Count > 0
                    ? _injectedMutations.Dequeue()
                    : new InjectedMutation( LpTagMutationResultStatus.Success, false, false, default, LpTagProfileError.None, string.Empty );
                result = ResolveMutation( request, menuGeneration, prior, applied, injected );
            }
        }

        MutationCompleted?.Invoke( result );
    }

    public void InjectNextReadResult(
        LpTagProfileReadStatus status,
        LpStoredTagPreferencesV1 canonicalRecord = default,
        string filteredError = "" )
    {
        if ( _disposed )
            return;

        var hasCanonicalRecord = canonicalRecord != default;
        if ( hasCanonicalRecord && status == LpTagProfileReadStatus.Success )
            RequireCanonical( canonicalRecord );

        lock ( ProfileGate )
        {
            _injectedReads.Enqueue( new InjectedRead( status, hasCanonicalRecord, canonicalRecord, filteredError ?? string.Empty ) );
        }
    }

    public void InjectNextMutationResult(
        LpTagMutationResultStatus status,
        bool commitCandidate = false,
        LpStoredTagPreferencesV1 canonicalRecord = default,
        LpTagProfileError error = LpTagProfileError.None,
        string filteredError = "" )
    {
        if ( _disposed )
            return;
        if ( status is not LpTagMutationResultStatus.Success and
             not LpTagMutationResultStatus.Conflict and
             not LpTagMutationResultStatus.Rejected and
             not LpTagMutationResultStatus.GuaranteedFailure and
             not LpTagMutationResultStatus.Uncertain )
            throw new ArgumentOutOfRangeException( nameof( status ) );

        var hasCanonicalRecord = canonicalRecord != default;
        if ( hasCanonicalRecord )
            RequireCanonical( canonicalRecord );

        lock ( ProfileGate )
        {
            _injectedMutations.Enqueue( new InjectedMutation(
                status,
                commitCandidate,
                hasCanonicalRecord,
                canonicalRecord,
                error,
                filteredError ?? string.Empty ) );
        }
    }

    public void Dispose()
    {
        if ( _disposed )
            return;

        _disposed = true;
        _injectedReads.Clear();
        _injectedMutations.Clear();
        ReadCompleted = null;
        MutationCompleted = null;
    }

    private static LpTagMutationResult ResolveMutation(
        LpTagMutationRequest request,
        ulong menuGeneration,
        LpStoredTagPreferencesV1 prior,
        LpStoredTagPreferencesV1 applied,
        InjectedMutation injected )
    {
        switch ( injected.Status )
        {
            case LpTagMutationResultStatus.Success:
                _processProfile = applied;
                return Result( request, menuGeneration, injected.Status, applied, LpTagProfileError.None, injected.FilteredError );
            case LpTagMutationResultStatus.Conflict:
            {
                var conflict = injected.HasCanonicalRecord && injected.CanonicalRecord.Revision > prior.Revision
                    ? injected.CanonicalRecord
                    : prior with { Revision = prior.Revision + 1UL };
                _processProfile = conflict;
                var error = injected.Error == LpTagProfileError.None ? LpTagProfileError.StaleRevision : injected.Error;
                return Result( request, menuGeneration, injected.Status, conflict, error, injected.FilteredError );
            }
            case LpTagMutationResultStatus.Rejected:
            {
                var error = injected.Error == LpTagProfileError.None ? LpTagProfileError.InvalidEffectKey : injected.Error;
                return Result( request, menuGeneration, injected.Status, prior, error, injected.FilteredError );
            }
            case LpTagMutationResultStatus.GuaranteedFailure:
            {
                var error = injected.Error == LpTagProfileError.None ? LpTagProfileError.StoreUnavailable : injected.Error;
                return Result( request, menuGeneration, injected.Status, prior, error, injected.FilteredError );
            }
            case LpTagMutationResultStatus.Uncertain:
                if ( injected.CommitCandidate )
                    _processProfile = applied;
                else if ( injected.HasCanonicalRecord )
                    _processProfile = injected.CanonicalRecord;

                return Result(
                    request,
                    menuGeneration,
                    injected.Status,
                    _processProfile,
                    injected.Error == LpTagProfileError.None ? LpTagProfileError.StoreUnavailable : injected.Error,
                    injected.FilteredError );
            default:
                throw new InvalidOperationException( "Unsupported local preview mutation result." );
        }
    }

    private static LpTagMutationResult Result(
        LpTagMutationRequest request,
        ulong menuGeneration,
        LpTagMutationResultStatus status,
        LpStoredTagPreferencesV1 canonical,
        LpTagProfileError error,
        string filteredError )
    {
        return new LpTagMutationResult(
            request.RequestIdentifier,
            menuGeneration,
            status,
            canonical,
            error,
            filteredError );
    }

    private static void RequireCanonical( LpStoredTagPreferencesV1 record )
    {
        if ( LpTagProfileRules.InspectStored( record, out var canonical, out _ ) != LpTagProfileInspectionStatus.Canonical || canonical != record )
            throw new ArgumentException( "Injected local preview records must be canonical.", nameof( record ) );
    }

    private static string FilteredCopy( LpTagProfileError error )
    {
        return error switch
        {
            LpTagProfileError.SelectionRequired => "Choose a style first",
            LpTagProfileError.StaleRevision => "Profile changed elsewhere",
            LpTagProfileError.StoreUnavailable => "Local preview operation failed",
            _ => "Change rejected"
        };
    }

    private readonly record struct InjectedRead(
        LpTagProfileReadStatus Status,
        bool HasCanonicalRecord,
        LpStoredTagPreferencesV1 CanonicalRecord,
        string FilteredError );

    private readonly record struct InjectedMutation(
        LpTagMutationResultStatus Status,
        bool CommitCandidate,
        bool HasCanonicalRecord,
        LpStoredTagPreferencesV1 CanonicalRecord,
        LpTagProfileError Error,
        string FilteredError );

    internal static void ResetForTests()
    {
        lock ( ProfileGate )
        {
            _processProfile = LpTagProfileRules.CreateNew();
        }
    }
}
#endif

public sealed class LpTagUnavailableProfileClient : ILpTagProfileClient
{
    private bool _disposed;

    public LpTagClientMode Mode => LpTagClientMode.Unavailable;

    public event Action<LpTagProfileReadResult>? ReadCompleted;
    public event Action<LpTagMutationResult>? MutationCompleted;

    public void ReadOwnProfile( LpTagProfileReadRequest request, ulong menuGeneration )
    {
        if ( _disposed )
            return;

        ReadCompleted?.Invoke( new LpTagProfileReadResult(
            request.RequestIdentifier,
            menuGeneration,
            LpTagProfileReadStatus.Unavailable,
            default,
            "Integration unavailable" ) );
    }

    public void MutateOwnProfile( LpTagMutationRequest request, ulong menuGeneration )
    {
        if ( _disposed )
            return;

        MutationCompleted?.Invoke( new LpTagMutationResult(
            request.RequestIdentifier,
            menuGeneration,
            LpTagMutationResultStatus.GuaranteedFailure,
            default,
            LpTagProfileError.StoreUnavailable,
            "Integration unavailable" ) );
    }

    public void Dispose()
    {
        if ( _disposed )
            return;

        _disposed = true;
        ReadCompleted = null;
        MutationCompleted = null;
    }
}
