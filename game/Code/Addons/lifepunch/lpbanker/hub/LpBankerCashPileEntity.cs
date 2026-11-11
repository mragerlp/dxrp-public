// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// BANKER-HUB-1 skeleton (DRIVE-AWAY-1-D): the Banker-only cash pile. Press opens the Banker hub for the presser
// (pattern: LpBitcoinHubEntity CanPress / Press / [Rpc.Host] / [Rpc.Broadcast], HE:283-319). It holds NO money:
// no balance, no synced amount, no rail. What a raid takes and what persists are I3 and I6.
// BANKER-HUB-2 (DRIVE-AWAY-2-U1): the pile carries its own body (the money_stacked model + a box collider from its
// bounds) and the host gates on the calling player (LpBankerAccess).
// SPINE-3 (SPINE-1 R1 U2): the pile passes the shared hub press gate and the host reach check (LpHubAccess).
// Still no money: the pile holds none and this change moves none.

using System;
using Dxura.RP.Game;
using Sandbox;

namespace LifePunch.DXRP.Addons.Banker;

[Title( "Banker Cash Pile" ), Category( "LifePunch" )]
public sealed class LpBankerCashPileEntity : Component, Component.IPressable
{
	/// <summary>Pile model; empty = the PLACEHOLDER money stack (LpBankerIdent.PileModelPath, I4 owed).</summary>
	[Property] public Model PileModel { get; set; }

	protected override void OnStart()
	{
		EnsureBody();
	}

	/// <summary>Adds the model and a static box collider when the object does not carry them already.</summary>
	private void EnsureBody()
	{
		var renderer = GameObject.GetComponent<ModelRenderer>();
		if ( renderer is null )
			renderer = GameObject.AddComponent<ModelRenderer>();

		if ( renderer.Model is null || renderer.Model.IsError )
			renderer.Model = PileModel ?? Model.Load( LpBankerIdent.PileModelPath );

		var model = renderer.Model;
		if ( model is null || model.IsError || GameObject.GetComponent<Collider>() is not null )
			return;

		var collider = GameObject.AddComponent<BoxCollider>();
		collider.Center = model.Bounds.Center;
		collider.Scale = model.Bounds.Size;
		collider.Static = true;
	}

	// SPINE-3: the job gate AND the shared hub press gate (a held pile never answers; reach to the model's bounds;
	// hands priority) - the same press gate the Bitcoin hub passes (LpHubAccess.CanPress).
	public bool CanPress( IPressable.Event e ) => LpBankerAccess.CanUseLocal() && LpHubAccess.CanPress( GameObject );

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
		if ( !LpBankerAccess.CanUse( GameUtils.GetPlayerByConnectionId( Rpc.CallerId ) ) )
			return;

		// SPINE-3: the host also checks reach and a held pile, which the Bitcoin hub's OpenHubHost already did and this
		// pile did not (F-HUB-4). The in-range tier, not CanOpenHost: the whole gate's ownership permission
		// (GameUtils.HasPermission) refuses an object with no entity tag and no owner, and this pile is one.
		if ( !LpHubAccess.CanOpenHostInRange( Rpc.Caller, GameObject ) )
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
