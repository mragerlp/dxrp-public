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

public static class LpTagMessageLimits
{
    public const int RequestIdentifierLength = 32;
    public const int MaximumRequestIdentifierLength = 32;
    public const int MaximumEffectKeyLength = 32;
    public const int MutationBurstCapacity = 8;
    public const double MutationRefillPerSecond = 4d;
    public const int ReadBurstCapacity = 2;
    public const double ReadRefillPerSecond = 1d;
}

public enum LpTagMutationKind : byte
{
    SetEnabled = 1,
    SetEffect = 2
}

public enum LpTagClientStatus : byte
{
    Idle,
    Loading,
    LocalPreview,
    Saving,
    Verifying,
    Saved,
    Conflict,
    Unavailable,
    SaveFailed,
    ReadOnly
}

public enum LpTagClientMode : byte
{
    Unavailable = 0,
    LocalPreview = 1,
    Production = 2
}

public enum LpTagProfileReadStatus : byte
{
    Success = 1,
    Unavailable = 2,
    UnsupportedReadOnly = 3,
    UnreadableReadOnly = 4
}

public enum LpTagMutationResultStatus : byte
{
    Success = 1,
    Conflict = 2,
    Rejected = 3,
    GuaranteedFailure = 4,
    Uncertain = 5
}

public readonly record struct LpTagProfileReadRequest(
    string RequestIdentifier );

public readonly record struct LpTagMutationRequest(
    string RequestIdentifier,
    ulong ExpectedRevision,
    LpTagSurface Surface,
    LpTagMutationKind Kind,
    bool Enabled,
    string EffectKey );

public readonly record struct LpTagProfileReadResult(
    string RequestIdentifier,
    ulong MenuGeneration,
    LpTagProfileReadStatus Status,
    LpStoredTagPreferencesV1 CanonicalRecord,
    string FilteredError );

public readonly record struct LpTagMutationResult(
    string RequestIdentifier,
    ulong MenuGeneration,
    LpTagMutationResultStatus Status,
    LpStoredTagPreferencesV1 CanonicalRecord,
    LpTagProfileError Error,
    string FilteredError );
