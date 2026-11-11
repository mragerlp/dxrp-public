// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "AK-47" (s&box ident: lifepunch.ak47 · addon ident: ak47) is the sole-owned
// intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
// sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Author account: mrragerlp · Public alias (in-game · Steam · Discord): Bloodwave
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

using Dxura.RP.Game;
using Sandbox;

namespace LifePunch.DXRP.Addons.AK47;

/// <summary>
/// Cosmetic magazine drop for the AK world prefab: when a reload starts, a short-lived
/// physics prop falls from the weapon. PLACEHOLDER BODY — the AK mesh is one fused piece
/// with no separate magazine, so the prop is a scaled dev box until a magazine model exists
/// (swap <see cref="PlaceholderModelPath"/> and <see cref="Size"/> then).
/// Local only: the prop is never network-spawned, carries no state and touches no ammo.
/// Each machine drops its own copy when it sees the weapon's "reloading" tag rise.
/// </summary>
public sealed class Ak47MagazineDrop : Component
{
	/// <summary>Log mark; exists nowhere else, so its presence in a log identifies this build.</summary>
	public const string DropMark = "AK47_MAGDROP_PLACEHOLDER_1";

	/// <summary>Engine core dev box. Replace with the real magazine model when one exists.</summary>
	public const string PlaceholderModelPath = "models/dev/box.vmdl";

	/// <summary>Tag bound by ReloadWeaponComponent while a reload is running.</summary>
	private const string ReloadingTag = "reloading";

	/// <summary>
	/// Where the magazine leaves the weapon. Optional: place a child GameObject at the magazine
	/// well with the gizmo and assign it. Unset = the ejection port moved by <see cref="PortOffset"/>.
	/// </summary>
	[Property] public GameObject Anchor { get; set; }

	/// <summary>Fallback offset from the ejection port, in the port's local space. Untuned default.</summary>
	[Property] public Vector3 PortOffset { get; set; } = new Vector3( 2f, 0f, -4f );

	/// <summary>Placeholder magazine size in units (length, thickness, height).</summary>
	[Property] public Vector3 Size { get; set; } = new Vector3( 3f, 1f, 7f );

	/// <summary>Seconds after the reload starts before the magazine falls.</summary>
	[Property] public float DropDelay { get; set; } = 0.4f;

	/// <summary>Seconds the dropped magazine stays in the world.</summary>
	[Property] public float Lifetime { get; set; } = 6f;

	/// <summary>Initial downward speed, units per second.</summary>
	[Property] public float DropSpeed { get; set; } = 30f;

	private bool _wasReloading;
	private bool _pending;
	private TimeSince _sinceReloadStart;

	protected override void OnUpdate()
	{
		var equipment = Components.Get<Equipment>();
		if ( !equipment.IsValid() )
		{
			return;
		}

		var reloading = equipment.IsDeployed && equipment.GameObject.Tags.Has( ReloadingTag );

		if ( reloading && !_wasReloading )
		{
			_pending = true;
			_sinceReloadStart = 0;
		}
		else if ( !reloading )
		{
			// A cancelled or holstered reload never drops.
			_pending = false;
		}

		_wasReloading = reloading;

		if ( _pending && _sinceReloadStart >= DropDelay )
		{
			_pending = false;
			Drop( equipment );
		}
	}

	private void Drop( Equipment equipment )
	{
		var origin = ResolveOrigin( equipment );
		if ( !origin.IsValid() )
		{
			return;
		}

		var model = Model.Load( PlaceholderModelPath );
		if ( model is null || model.IsError )
		{
			Log.Warning( $"[ak47] {DropMark} placeholder model missing ({PlaceholderModelPath}); no magazine dropped." );
			return;
		}

		var position = Anchor.IsValid()
			? origin.WorldPosition
			: origin.WorldPosition + origin.WorldRotation * PortOffset;

		var go = new GameObject( true, "ak47_magazine_placeholder" );
		go.NetworkMode = NetworkMode.Never;
		go.Tags.Add( LifePunchPropPhysics.NoCollideTag );
		go.WorldPosition = position;
		go.WorldRotation = origin.WorldRotation;

		var native = model.Bounds.Size;
		if ( native.x > 0.01f && native.y > 0.01f && native.z > 0.01f )
		{
			go.WorldScale = new Vector3( Size.x / native.x, Size.y / native.y, Size.z / native.z );
		}

		go.Components.Create<ModelRenderer>().Model = model;

		var collider = go.Components.Create<BoxCollider>();
		collider.Center = model.Bounds.Center;
		collider.Scale = model.Bounds.Size;

		var body = go.Components.Create<Rigidbody>();
		body.Velocity = Vector3.Down * DropSpeed;

		go.DestroyAsync( Lifetime );

		Log.Info( $"[ak47] {DropMark} dropped at={position} origin={origin.Name} anchor={Anchor.IsValid()}" );
	}

	private GameObject ResolveOrigin( Equipment equipment )
	{
		if ( Anchor.IsValid() )
		{
			return Anchor;
		}

		// Same choice the shell casing makes: the viewmodel's port in first person, the world port otherwise.
		var viewModel = equipment.ViewModel;
		if ( viewModel.IsValid() && viewModel.EjectionPort.IsValid() )
		{
			return viewModel.EjectionPort;
		}

		return equipment.EjectionPort;
	}
}
