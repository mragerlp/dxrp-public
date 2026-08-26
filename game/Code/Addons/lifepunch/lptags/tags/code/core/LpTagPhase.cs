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

public static class LpTagPhase
{
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    public static ulong Hash( ulong steamId )
    {
        unchecked
        {
            var hash = FnvOffsetBasis;
            for ( var shift = 0; shift <= 56; shift += 8 )
            {
                hash ^= (byte)( steamId >> shift );
                hash *= FnvPrime;
            }

            return hash;
        }
    }

    public static double OffsetFor( ulong steamId, double periodMilliseconds )
    {
        if ( periodMilliseconds <= 0d
            || double.IsNaN( periodMilliseconds )
            || double.IsInfinity( periodMilliseconds ) )
        {
            return 0d;
        }

        var offset = (double)Hash( steamId ) % periodMilliseconds;
        return offset < 0d ? offset + periodMilliseconds : offset;
    }
}

internal static partial class LpTagSchedulingRules
{
    public const double MinimumDelayMilliseconds = 1d;

    public static double ResolveDueMilliseconds(
        double nowMilliseconds,
        ulong steamId,
        LpTagEffectId effectId,
        int graphemeCount,
        int slideAmplitudePixels )
    {
        if ( !LpTagEffectCatalog.TryGet( effectId, out var definition ) )
            return double.PositiveInfinity;

        var period = definition.ResolvePeriodMilliseconds( graphemeCount );
        if ( period <= 0d || double.IsNaN( period ) || double.IsInfinity( period ) )
            return double.PositiveInfinity;

        var offset = LpTagPhase.OffsetFor( steamId, period );
        var phase = PositiveModulo( nowMilliseconds + offset, period );
        var delay = LpTagEffectCatalog.ResolveNextChangeDelayMilliseconds(
            effectId,
            graphemeCount,
            slideAmplitudePixels,
            phase );
        if ( double.IsNaN( delay ) || double.IsInfinity( delay ) )
            return double.PositiveInfinity;

        return nowMilliseconds + Math.Max( MinimumDelayMilliseconds, delay );
    }

    public static double PositiveModulo( double value, double modulus )
    {
        var remainder = value % modulus;
        return remainder < 0d ? remainder + modulus : remainder;
    }
}
