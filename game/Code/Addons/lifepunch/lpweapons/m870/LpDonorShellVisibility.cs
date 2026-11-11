// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using Sandbox;

namespace LifePunch.DXRP.Addons.Weapons.M870;

/// <summary>
/// First-person shells on reload. The donor viewmodel already animates real shells when upstream
/// ReloadWeaponComponent drives its reload clips (ReloadWeaponComponent.cs:164-167), but the prefab hides
/// the whole donor with one MaterialOverride, shells included. This swaps that blanket override for
/// per-slot overrides: every donor material slot is hidden except the ones whose material path contains
/// KeepMaterial, so the shells show and the donor gun stays hidden. Slots are matched by name at
/// runtime; if none matches, the blanket override stays and nothing changes. Local and visual only.
/// Shape followed: renderer.Materials.Count / GetOriginal( i ) (LpBitcoinDevSpawn.cs:648-650).
/// </summary>
[Title( "Pump Shotgun - Show Donor Shells" )]
[Icon( "inventory" )]
[Group( "Weapon Components" )]
public sealed class LpDonorShellVisibility : Component
{
	/// <summary>The donor viewmodel renderer.</summary>
	[Property] public SkinnedModelRenderer? Donor { get; set; }

	/// <summary>The material every hidden slot gets (the prefab's invisible material).</summary>
	[Property] public Material? Hidden { get; set; }

	/// <summary>Slots whose original material path contains this stay visible.</summary>
	[Property] public string KeepMaterial { get; set; } = "12g_shell";

	private Model? _applied;

	protected override void OnUpdate()
	{
		if ( !Donor.IsValid() || Hidden is null || Donor.Model is null || _applied == Donor.Model )
		{
			return;
		}

		_applied = Donor.Model;

		var materials = Donor.Materials;
		var kept = 0;
		var hidden = 0;
		for ( var i = 0; i < materials.Count; i++ )
		{
			var original = materials.GetOriginal( i );
			var path = original?.ResourcePath ?? "";
			if ( path.Contains( KeepMaterial, StringComparison.OrdinalIgnoreCase ) )
			{
				kept++;
				continue;
			}

			materials.SetOverride( i, Hidden );
			hidden++;
		}

		if ( kept == 0 )
		{
			Log.Warning( $"lp_m870_shells: no donor slot matches '{KeepMaterial}' (slots={materials.Count}); donor stays fully hidden." );
			return;
		}

		// Slot overrides are in place first, so clearing the blanket override never flashes the donor gun.
		Donor.MaterialOverride = null;
		Log.Info( $"lp_m870_shells: donor {Donor.Model.ResourcePath} slots={materials.Count} kept={kept} hidden={hidden}" );
	}
}
