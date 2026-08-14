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

internal sealed class LpTagRequestReplayCache
{
    public const int MaximumResponsesPerPlayer = 32;

    private sealed class PlayerResponses
    {
        public readonly LinkedList<LpTagMutationResult> InsertionOrder = new();
        public readonly Dictionary<string, LinkedListNode<LpTagMutationResult>> ByRequestIdentifier = new( StringComparer.Ordinal );
    }

    private readonly Dictionary<ulong, PlayerResponses> _players = new();

    public void Remember( ulong steamId, string requestIdentifier, LpTagMutationResult response )
    {
        if ( requestIdentifier != response.RequestIdentifier )
            throw new ArgumentException( "Replay key must match the canonical response request identifier.", nameof(requestIdentifier) );
        if ( response.Status is not LpTagMutationResultStatus.Success and
             not LpTagMutationResultStatus.Conflict and
             not LpTagMutationResultStatus.Rejected )
            throw new ArgumentException( "Only canonical host responses can enter the replay cache.", nameof(response) );
        if ( LpTagProfileRules.InspectStored( response.CanonicalRecord, out _, out _ ) != LpTagProfileInspectionStatus.Canonical )
            throw new ArgumentException( "Replay responses must carry a canonical profile record.", nameof(response) );

        if ( !_players.TryGetValue( steamId, out var responses ) )
        {
            responses = new PlayerResponses();
            _players.Add( steamId, responses );
        }

        if ( responses.ByRequestIdentifier.TryGetValue( requestIdentifier, out var existing ) )
        {
            existing.Value = response;
            return;
        }

        var node = responses.InsertionOrder.AddLast( response );
        responses.ByRequestIdentifier.Add( requestIdentifier, node );
        if ( responses.InsertionOrder.Count <= MaximumResponsesPerPlayer )
            return;

        var oldest = responses.InsertionOrder.First!;
        responses.InsertionOrder.RemoveFirst();
        responses.ByRequestIdentifier.Remove( oldest.Value.RequestIdentifier );
    }

    public bool TryGet( ulong steamId, string requestIdentifier, out LpTagMutationResult response )
    {
        if ( _players.TryGetValue( steamId, out var responses ) &&
             responses.ByRequestIdentifier.TryGetValue( requestIdentifier, out var node ) )
        {
            response = node.Value;
            return true;
        }

        response = default;
        return false;
    }

    public int CountFor( ulong steamId )
    {
        return _players.TryGetValue( steamId, out var responses )
            ? responses.InsertionOrder.Count
            : 0;
    }

    public void RemovePlayer( ulong steamId )
    {
        _players.Remove( steamId );
    }
}
