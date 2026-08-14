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
namespace LifePunch.DXRP.Addons.Tags;

public enum LpTagProfileError : byte
{
    None,
    InvalidRequestId,
    StaleRevision,
    InvalidSurface,
    InvalidMutationKind,
    InvalidEffectKey,
    IncompatibleEffect,
    SelectionRequired,
    UnsupportedSchema,
    CorruptRecord,
    StoreUnavailable
}

public enum LpTagProfileInspectionStatus : byte
{
    Canonical,
    RepairRequired,
    UnsupportedReadOnly
}

internal enum LpTagProfileLoadDecision : byte
{
    InspectFound,
    CreateNew,
    UnreadableReadOnly,
    UnavailableReadOnly
}

public static class LpTagProfileRules
{
    public const int SchemaVersion = 1;
    public const string NoneKey = "none";

    public static LpStoredTagPreferencesV1 CreateNew()
    {
        return new LpStoredTagPreferencesV1(
            SchemaVersion,
            0UL,
            new LpTagSurfacePreference( false, NoneKey ),
            new LpTagSurfacePreference( false, NoneKey ),
            new LpTagSurfacePreference( false, NoneKey ) );
    }

    public static bool TryApplyMutation(
        LpStoredTagPreferencesV1 current,
        LpTagMutationRequest request,
        out LpStoredTagPreferencesV1 normalized,
        out LpTagProfileError error )
    {
        normalized = current;
        error = LpTagProfileError.None;

        if ( !IsRequestIdentifier( request.RequestIdentifier ) )
            return Fail( LpTagProfileError.InvalidRequestId, out error );
        if ( request.ExpectedRevision != current.Revision )
            return Fail( LpTagProfileError.StaleRevision, out error );
        if ( !IsSurface( request.Surface ) )
            return Fail( LpTagProfileError.InvalidSurface, out error );
        if ( request.Kind is not LpTagMutationKind.SetEnabled and not LpTagMutationKind.SetEffect )
            return Fail( LpTagProfileError.InvalidMutationKind, out error );

        var inspection = InspectStored( current, out _, out var inspectionError );
        if ( inspection != LpTagProfileInspectionStatus.Canonical )
            return Fail( inspectionError, out error );

        var currentSurface = GetSurface( current, request.Surface );
        LpTagSurfacePreference nextSurface;
        if ( request.Kind == LpTagMutationKind.SetEnabled )
        {
            if ( request.EffectKey != string.Empty )
                return Fail( LpTagProfileError.InvalidEffectKey, out error );
            if ( request.Enabled && currentSurface.SelectedEffectKey == NoneKey )
                return Fail( LpTagProfileError.SelectionRequired, out error );

            nextSurface = new LpTagSurfacePreference( request.Enabled, currentSurface.SelectedEffectKey );
        }
        else
        {
            if ( request.Enabled || !IsBoundedAscii( request.EffectKey, LpTagMessageLimits.MaximumEffectKeyLength ) || request.EffectKey == NoneKey )
                return Fail( LpTagProfileError.InvalidEffectKey, out error );
            if ( !LpTagEffectCatalog.TryGet( request.EffectKey, out var definition ) )
                return Fail( LpTagProfileError.InvalidEffectKey, out error );
            if ( !TryValidateEffectCompatibility( definition, request.Surface, out error ) )
                return false;

            nextSurface = new LpTagSurfacePreference( currentSurface.Enabled, definition.Key );
        }

        normalized = SetSurface( current, request.Surface, nextSurface );
        return true;
    }

    public static LpTagProfileInspectionStatus InspectStored(
        LpStoredTagPreferencesV1 record,
        out LpStoredTagPreferencesV1 normalized,
        out LpTagProfileError error )
    {
        normalized = default;
        if ( record.SchemaVersion != SchemaVersion )
        {
            error = LpTagProfileError.UnsupportedSchema;
            return LpTagProfileInspectionStatus.UnsupportedReadOnly;
        }

        if ( record.Chat.SelectedEffectKey is null ||
             record.Scoreboard.SelectedEffectKey is null ||
             record.Nameplate.SelectedEffectKey is null )
        {
            error = LpTagProfileError.CorruptRecord;
            return LpTagProfileInspectionStatus.UnsupportedReadOnly;
        }

        var chatRepair = NormalizeStoredSurface( record.Chat, LpTagSurface.Chat, out var chat );
        var scoreboardRepair = NormalizeStoredSurface( record.Scoreboard, LpTagSurface.Scoreboard, out var scoreboard );
        var nameplateRepair = NormalizeStoredSurface( record.Nameplate, LpTagSurface.Nameplate, out var nameplate );
        normalized = new LpStoredTagPreferencesV1(
            record.SchemaVersion,
            record.Revision,
            chat,
            scoreboard,
            nameplate );

        if ( chatRepair || scoreboardRepair || nameplateRepair )
        {
            error = LpTagProfileError.CorruptRecord;
            return LpTagProfileInspectionStatus.RepairRequired;
        }

        error = LpTagProfileError.None;
        return LpTagProfileInspectionStatus.Canonical;
    }

    internal static bool TryValidateEffectCompatibility(
        LpTagEffectDefinition definition,
        LpTagSurface surface,
        out LpTagProfileError error )
    {
        if ( !definition.Supports( surface ) )
            return Fail( LpTagProfileError.IncompatibleEffect, out error );

        error = LpTagProfileError.None;
        return true;
    }

    internal static LpTagProfileLoadDecision ResolveLoad(
        LpTagProfileLoadResult result,
        out LpStoredTagPreferencesV1 candidate,
        out LpTagProfileError error )
    {
        candidate = default;
        switch ( result.Status )
        {
            case LpTagProfileLoadStatus.Found:
                candidate = result.Record;
                error = LpTagProfileError.None;
                return LpTagProfileLoadDecision.InspectFound;
            case LpTagProfileLoadStatus.NotFound:
                candidate = CreateNew();
                error = LpTagProfileError.None;
                return LpTagProfileLoadDecision.CreateNew;
            case LpTagProfileLoadStatus.Unreadable:
                error = LpTagProfileError.CorruptRecord;
                return LpTagProfileLoadDecision.UnreadableReadOnly;
            case LpTagProfileLoadStatus.Unavailable:
                error = LpTagProfileError.StoreUnavailable;
                return LpTagProfileLoadDecision.UnavailableReadOnly;
            default:
                error = LpTagProfileError.CorruptRecord;
                return LpTagProfileLoadDecision.UnreadableReadOnly;
        }
    }

    internal static bool TryAcceptWriteResult(
        ulong expectedRevision,
        LpTagProfileWriteResult result,
        out LpStoredTagPreferencesV1 canonical,
        out LpTagProfileError error )
    {
        canonical = default;
        if ( result.Status == LpTagProfileWriteStatus.Unavailable )
            return Fail( LpTagProfileError.StoreUnavailable, out error );
        if ( result.Status == LpTagProfileWriteStatus.Conflict )
            return Fail( LpTagProfileError.StaleRevision, out error );
        if ( result.Status != LpTagProfileWriteStatus.Success ||
             expectedRevision == ulong.MaxValue ||
             result.CanonicalRecord.Revision != expectedRevision + 1UL )
            return Fail( LpTagProfileError.CorruptRecord, out error );

        var inspection = InspectStored( result.CanonicalRecord, out var inspected, out var inspectionError );
        if ( inspection != LpTagProfileInspectionStatus.Canonical )
            return Fail( inspectionError, out error );

        canonical = inspected;
        error = LpTagProfileError.None;
        return true;
    }

    public static LpTagProfileWritePayloadV1 ToWritePayload( LpStoredTagPreferencesV1 normalized )
    {
        return new LpTagProfileWritePayloadV1(
            normalized.SchemaVersion,
            normalized.Chat,
            normalized.Scoreboard,
            normalized.Nameplate );
    }

    public static LpEffectiveTagStyleV1 ProjectPublic( LpStoredTagPreferencesV1 canonical )
    {
        return new LpEffectiveTagStyleV1(
            ProjectSurface( canonical.Chat, LpTagSurface.Chat ),
            ProjectSurface( canonical.Scoreboard, LpTagSurface.Scoreboard ),
            ProjectSurface( canonical.Nameplate, LpTagSurface.Nameplate ),
            canonical.Revision );
    }

    private static bool NormalizeStoredSurface(
        LpTagSurfacePreference preference,
        LpTagSurface surface,
        out LpTagSurfacePreference normalized )
    {
        if ( !LpTagEffectCatalog.TryGet( preference.SelectedEffectKey, out var definition ) ||
             !definition.Supports( surface ) ||
             definition.Id == LpTagEffectId.None && preference.Enabled )
        {
            normalized = new LpTagSurfacePreference( false, NoneKey );
            return true;
        }

        normalized = preference;
        return false;
    }

    private static LpTagEffectId ProjectSurface( LpTagSurfacePreference preference, LpTagSurface surface )
    {
        if ( !preference.Enabled )
            return LpTagEffectId.None;
        if ( !LpTagEffectCatalog.TryGet( preference.SelectedEffectKey, out var definition ) )
            return LpTagEffectId.None;

        return definition.Supports( surface ) ? definition.Id : LpTagEffectId.None;
    }

    private static LpTagSurfacePreference GetSurface( LpStoredTagPreferencesV1 record, LpTagSurface surface )
    {
        return surface switch
        {
            LpTagSurface.Chat => record.Chat,
            LpTagSurface.Scoreboard => record.Scoreboard,
            LpTagSurface.Nameplate => record.Nameplate,
            _ => default
        };
    }

    private static LpStoredTagPreferencesV1 SetSurface(
        LpStoredTagPreferencesV1 record,
        LpTagSurface surface,
        LpTagSurfacePreference preference )
    {
        return surface switch
        {
            LpTagSurface.Chat => record with { Chat = preference },
            LpTagSurface.Scoreboard => record with { Scoreboard = preference },
            LpTagSurface.Nameplate => record with { Nameplate = preference },
            _ => record
        };
    }

    private static bool IsSurface( LpTagSurface surface )
    {
        return surface is LpTagSurface.Chat or LpTagSurface.Scoreboard or LpTagSurface.Nameplate;
    }

    private static bool IsBoundedAscii( string value, int maximumLength )
    {
        if ( string.IsNullOrEmpty( value ) || value.Length > maximumLength )
            return false;

        for ( var index = 0; index < value.Length; index++ )
        {
            if ( value[index] > 0x7f )
                return false;
        }

        return true;
    }

    private static bool IsRequestIdentifier( string value )
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

    private static bool Fail( LpTagProfileError value, out LpTagProfileError error )
    {
        error = value;
        return false;
    }
}
