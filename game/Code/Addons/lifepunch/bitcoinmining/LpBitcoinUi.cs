// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH Bitcoin Miner for DXRP" (s&box ident: lifepunch.bitcoin · addon ident: bitcoinmining)
// ─────────────────────────────────────────────────────────────────────────────

using System.Linq;
using Sandbox;

namespace LifePunch.DXRP.Addons.Bitcoin;

/// <summary>Coordinates the two bitcoin UI surfaces — only one open at a time.</summary>
internal static class LpBitcoinUi
{
	public static void CloseAll()
	{
		LpHashdUiHost.CloseOpen();
		LpBitcoinTerminalUiHost.CloseOpen();
		CloseSuiPreviews();
	}

	/// <summary>
	/// UI Designer scratch preview (<c>lp_bitcoin_sui_hub_preview</c>) mounts a static layout that
	/// shows pin + hub body at once — destroy it before opening ship Razor panels.
	/// </summary>
	public static void CloseSuiPreviews()
	{
		var scene = Game.ActiveScene;
		if ( scene is null )
			return;

		foreach ( var go in scene.GetAllObjects( true ) )
		{
			if ( !go.IsValid() || go.Name != "LpSuiHubPreview" )
				continue;

			go.Destroy();
		}
	}

	public static LpBitcoinHubEntity GetOrCreatePreviewHub( bool withSampleRacks = false )
	{
		var scene = Game.ActiveScene;
		if ( scene is null )
			return null;

		var existing = scene.GetAllComponents<LpBitcoinHubEntity>()
			.FirstOrDefault( h => h.IsValid() && !h.GameObject.Name.StartsWith( "LpBitcoinPreview" ) );

		if ( existing.IsValid() )
			return existing;

		var go = scene.CreateObject();
		go.Name = "LpBitcoinPreviewHub";
		var hub = go.AddComponent<LpBitcoinHubEntity>();
		hub.BindOwnerFromLocalViewer();
		hub.ApplyPoweredState( true );

		if ( withSampleRacks )
			EnsureSampleRacks( scene, hub );

		return hub;
	}

	private static void EnsureSampleRacks( Scene scene, LpBitcoinHubEntity hub )
	{
		if ( hub.GetLinkedRacks().Count > 0 )
			return;

		// The canonical rig0 mix, built from the slot caps rather than a count: 2× standard +
		// 1× advanced. AdvancedRack is set BEFORE LinkToHub because the hub's reconcile sweep
		// reads it at link time to stamp AssignedSlotToken. Leaving it at its `= true` default
		// gave three advanced racks, which all claimed the single advanced token and left both
		// standard slots empty — GPU Rack 1 and 2 were unreachable in preview.
		for ( var i = 0; i < LpBitcoinIdent.PortalMaxStandardRacksPerHub; i++ )
			CreatePreviewRack( scene, hub, $"LpBitcoinPreviewRack{i + 1}", advanced: false );

		for ( var i = 0; i < LpBitcoinIdent.PortalMaxAdvancedRacksPerHub; i++ )
			CreatePreviewRack( scene, hub, $"LpBitcoinPreviewAdvancedRack{i + 1}", advanced: true );

		// Sensor: names the mix it actually built, so a gate can prove the running assembly
		// carries this harness rather than a stale one that minted three advanced racks.
		Log.Info(
			$"lp_bitcoin preview harness: rig0 mix linked — {LpBitcoinIdent.PortalMaxStandardRacksPerHub} standard + " +
			$"{LpBitcoinIdent.PortalMaxAdvancedRacksPerHub} advanced." );
	}

	private static void CreatePreviewRack( Scene scene, LpBitcoinHubEntity hub, string name, bool advanced )
	{
		var rackGo = scene.CreateObject();
		rackGo.Name = name;
		var rack = rackGo.AddComponent<LpBitcoinRackEntity>();
		rack.AdvancedRack = advanced;
		rack.LinkToHub( hub );
	}
}
