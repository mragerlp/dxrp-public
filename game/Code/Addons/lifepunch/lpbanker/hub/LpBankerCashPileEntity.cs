// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// BANKER-HUB-1 skeleton (DRIVE-AWAY-1-D): the Banker-only cash pile. Press opens the Banker hub for the presser
// (pattern: LpBitcoinHubEntity CanPress / Press / [Rpc.Host] / [Rpc.Broadcast], HE:283-319). It holds NO money:
// no balance, no synced amount, no rail. What a raid takes and what persists are rulings 3 and 6.

using System;
using Sandbox;

namespace LifePunch.DXRP.Addons.Banker;

[Title( "Banker Cash Pile" ), Category( "LifePunch" )]
public sealed class LpBankerCashPileEntity : Component, Component.IPressable
{
	public bool CanPress( IPressable.Event e ) => LpBankerAccess.CanUse();

	public bool Press( IPressable.Event e )
	{
		OpenHost();
		return true;
	}

	public void Hover( IPressable.Event e ) { }
	public void Blur( IPressable.Event e ) { }

	[Rpc.Host]
	private void OpenHost()
	{
		if ( !LpBankerAccess.CanUse() )
			return;

		Open( Rpc.CallerId );
	}

	[Rpc.Broadcast]
	private void Open( Guid callerId )
	{
		if ( Connection.Local.Id != callerId )
			return;

		LpBankerHubUi.Open( 0 );
	}
}
