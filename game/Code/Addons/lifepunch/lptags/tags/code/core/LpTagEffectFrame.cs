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

public sealed class LpTagEffectFrame
{
    private LpTagPaletteRole[] _roles = Array.Empty<LpTagPaletteRole>();

    public int GraphemeCount { get; private set; }
    public ReadOnlySpan<LpTagPaletteRole> Roles => _roles.AsSpan( 0, GraphemeCount );
    public string Decoration { get; internal set; } = string.Empty;
    public int TranslateXPixels { get; internal set; }
    public int FrameKey { get; internal set; }
    public double NextChangeDelayMilliseconds { get; internal set; } = double.PositiveInfinity;
    public bool RequiresWholeNameFallback { get; internal set; }
    internal int RoleCapacity => _roles.Length;

    public int RedRoleCount
    {
        get
        {
            var count = 0;
            for ( var index = 0; index < GraphemeCount; index++ )
            {
                if ( _roles[index] == LpTagPaletteRole.Red )
                    count++;
            }

            return count;
        }
    }

    public void Reset( int graphemeCount )
    {
        if ( graphemeCount < 0 )
            throw new ArgumentOutOfRangeException( nameof( graphemeCount ) );

        if ( _roles.Length < graphemeCount )
            _roles = new LpTagPaletteRole[graphemeCount];
        else if ( graphemeCount > 0 )
            Array.Clear( _roles, 0, graphemeCount );

        GraphemeCount = graphemeCount;
        Decoration = string.Empty;
        TranslateXPixels = 0;
        FrameKey = 0;
        NextChangeDelayMilliseconds = double.PositiveInfinity;
        RequiresWholeNameFallback = false;
    }

    public LpTagPaletteRole GetRole( int index )
    {
        if ( index < 0 || index >= GraphemeCount )
            throw new ArgumentOutOfRangeException( nameof( index ) );

        return _roles[index];
    }

    internal void SetRole( int index, LpTagPaletteRole role )
    {
        if ( index < 0 || index >= GraphemeCount )
            throw new ArgumentOutOfRangeException( nameof( index ) );

        _roles[index] = role;
    }

    internal void SetAllRoles( LpTagPaletteRole role )
    {
        for ( var index = 0; index < GraphemeCount; index++ )
            _roles[index] = role;
    }
}
