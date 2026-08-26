// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// LifePunch shared physics — world machines (terminal, rack) vs grabbable placeables (hub @ spawn).
// ─────────────────────────────────────────────────────────────────────────────

using Sandbox;
#if !LIFEPUNCH_LOCAL
using Dxura.RP.Game;
#endif

namespace LifePunch.DXRP.Addons;

/// <summary>Collider + ground contact for world props (racks, terminals, hubs).</summary>
public static class LifePunchPropPhysics
{
	public const string GrabbedTag = "grabbed";
	public const string PlayerClipTag = "playerclip";
	public const string NoCollideTag = "no_collide";

	/// <summary>Align feet, then enforce immovable world-machine physics (never grabbable printer mode).</summary>
	public static void SetupWorldMachine( GameObject go, bool alignGround = true )
	{
		if ( !go.IsValid() )
			return;

		DenyHandsGrabTags( go );

		var rb = go.Components.Get<Rigidbody>( FindMode.EverythingInSelf );
		if ( rb.IsValid() )
		{
			rb.Velocity = Vector3.Zero;
			rb.AngularVelocity = Vector3.Zero;
			rb.MotionEnabled = false;
		}

		if ( alignGround )
		{
			var aligned = LifePunchGroundContact.AlignMeshBottom( go );
#if !LIFEPUNCH_LOCAL
			if ( !aligned )
				Log.Warning( $"LIFEPUNCH_PROP_PHYSICS ground align missed for '{go.Name}' at {go.WorldPosition}" );
#endif
		}

		EnforceWorldMachine( go );
	}

	public static void SetupPhysicalProp( GameObject go, bool alignGround = true )
		=> SetupWorldMachine( go, alignGround );

	/// <summary>
	/// DXRP money printer spawn physics — dynamic RB + gravity, non-static box (see printer.prefab).
	/// Hub uses this briefly at spawn, then <see cref="EnforceWorldMachine"/>.
	/// </summary>
	public static void EnablePrinterStyleSpawnPhysics( GameObject go )
	{
		if ( !go.IsValid() )
			return;

		DenyHandsGrabTags( go );
		SyncBoxColliderFromModel( go );

		var box = go.Components.Get<BoxCollider>( FindMode.EverythingInSelf );
		if ( box.IsValid() )
		{
			box.IsTrigger = false;
			box.Static = false;
			box.OnPhysicsChanged();
		}

		var rb = go.Components.Get<Rigidbody>( FindMode.EverythingInSelf );
		if ( rb.IsValid() )
		{
			rb.Gravity = true;
			rb.MotionEnabled = true;
			rb.Velocity = Vector3.Zero;
			rb.AngularVelocity = Vector3.Zero;
		}
	}

	/// <summary>After printer-style drop: snap mesh feet, then freeze as world machine.</summary>
	public static void FinishSpawnAsWorldMachine( GameObject go )
	{
		if ( !go.IsValid() )
			return;

		LifePunchGroundContact.AlignMeshBottom( go );
		EnforceWorldMachine( go );
	}

	/// <summary>Host tick — strip grab tags, kill CollideGuard, static collider, frozen RB (forces hands release).</summary>
	public static bool EnforceWorldMachine( GameObject go )
	{
		if ( !go.IsValid() )
			return false;

		DenyHandsGrabTags( go );
		ClearHandsGrabCollisionTags( go );

		if ( !SyncBoxColliderFromModel( go ) )
			return false;

		var box = go.Components.Get<BoxCollider>( FindMode.EverythingInSelf );
		if ( box.IsValid() )
		{
			box.IsTrigger = false;
			box.Static = true;
			box.OnPhysicsChanged();
		}

		var rb = go.Components.Get<Rigidbody>( FindMode.EverythingInSelf );
		if ( rb.IsValid() )
		{
			rb.Velocity = Vector3.Zero;
			rb.AngularVelocity = Vector3.Zero;
			rb.MotionEnabled = false;
		}

		return true;
	}

	public static bool SettleAsWorldMachine( GameObject go )
		=> EnforceWorldMachine( go );

	/// <summary>
	/// DXRP printer spawn — dynamic RB + gravity, hands grab, no ground teleport.
	/// Prefer <paramref name="syncColliderFromModel"/> false on first frame (bounds may lag).
	/// </summary>
	public static void BeginGrabbablePrinterDrop( GameObject go, bool syncColliderFromModel = true )
	{
		if ( !go.IsValid() )
			return;

		AllowHandsGrabTags( go );
		go.Tags.Remove( "pocket_item" );

		if ( syncColliderFromModel )
			SyncBoxColliderFromModel( go );

		var box = go.Components.Get<BoxCollider>( FindMode.EverythingInSelf );
		if ( box.IsValid() )
		{
			box.IsTrigger = false;
			box.Static = false;
			box.OnPhysicsChanged();
		}

		var rb = go.Components.Get<Rigidbody>( FindMode.EverythingInSelf );
		if ( rb.IsValid() )
		{
			rb.Gravity = true;
			rb.MotionEnabled = true;
			rb.Velocity = Vector3.Zero;
			rb.AngularVelocity = Vector3.Zero;
		}

		go.Tags.Add( "entity" );
		go.Tags.Add( "solid" );
	}

	/// <summary>
	/// Bitcoin hub @ spawn — printer-like hands grab (LMB move/rotate) without pocket.
	/// Keeps dynamic RB + <c>hands_interact</c>; strips DXRP <c>grabbed</c> ghost tag on host tick.
	/// </summary>
	public static void SetupGrabbablePlaceableProp( GameObject go, bool alignGround = true )
	{
		if ( !go.IsValid() )
			return;

		BeginGrabbablePrinterDrop( go, syncColliderFromModel: true );

		if ( alignGround )
		{
			var aligned = LifePunchGroundContact.AlignMeshBottom( go );
#if !LIFEPUNCH_LOCAL
			if ( !aligned )
				Log.Warning( $"LIFEPUNCH_PROP_PHYSICS ground align missed for '{go.Name}' at {go.WorldPosition}" );
#endif
		}
	}

	/// <summary>Host — keep hub solid while hands move it; clear stuck post-release clip tags.</summary>
	public static void MaintainGrabbablePlaceableProp( GameObject go )
	{
		if ( !go.IsValid() )
			return;

#if !LIFEPUNCH_LOCAL
		if ( !Networking.IsHost )
			return;

		// DXRP hands add "grabbed" so the holder walks through — hub stays solid for placement.
		if ( go.Tags.Has( GrabbedTag ) )
		{
			if ( GameManager.Instance.IsValid() )
				GameManager.Instance.BroadcastTagHost( go, false, GrabbedTag );
			else
				go.Tags.Remove( GrabbedTag );
		}

		if ( go.Tags.Has( NoCollideTag ) )
		{
			if ( GameManager.Instance.IsValid() )
				GameManager.Instance.BroadcastTagHost( go, false, NoCollideTag );
			else
				go.Tags.Remove( NoCollideTag );
		}

		// CollideGuard removes itself after players clear; strip orphan playerclip.
		if ( go.Tags.Has( PlayerClipTag ) && !go.Components.Get<CollideGuard>( FindMode.EverythingInSelf ).IsValid() )
		{
			if ( GameManager.Instance.IsValid() )
				GameManager.Instance.BroadcastTagHost( go, false, PlayerClipTag );
			else
				go.Tags.Remove( PlayerClipTag );
		}

		AllowHandsGrabTags( go );
#endif

		go.Tags.Add( "entity" );
		go.Tags.Add( "solid" );
	}

	/// <summary>Market / dev placeables — <c>hands_interact</c> like DXRP printer (no pocket_item).</summary>
	public static void AllowHandsGrabTags( GameObject go )
	{
		if ( !go.IsValid() )
			return;

#if !LIFEPUNCH_LOCAL
		if ( Networking.IsHost && GameManager.Instance.IsValid() )
			GameManager.Instance.BroadcastTagHost( go, true, Constants.HandsInteractTag );
#endif

		go.Tags.Add( "hands_interact" );
	}

	/// <summary>Immovable machines — no hands grab (terminal, rack after settle).</summary>
	public static void DenyHandsGrabTags( GameObject go )
	{
		if ( !go.IsValid() )
			return;

#if !LIFEPUNCH_LOCAL
		if ( Networking.IsHost && GameManager.Instance.IsValid() )
		{
			GameManager.Instance.BroadcastTagHost( go, false, Constants.HandsInteractTag, Constants.PocketItemTag );
		}
#endif

		go.Tags.Remove( "hands_interact" );
		go.Tags.Remove( "pocket_item" );
	}

	/// <summary>Strip DXRP hands-grab ghost tags and restore player-blocking collision (host-broadcast).</summary>
	public static void ClearHandsGrabCollisionTags( GameObject go )
	{
		if ( !go.IsValid() )
			return;

#if !LIFEPUNCH_LOCAL
		if ( Networking.IsHost && GameManager.Instance.IsValid() )
		{
			GameManager.Instance.BroadcastTagHost( go, false, GrabbedTag, PlayerClipTag, NoCollideTag );
		}

		foreach ( var guard in go.Components.GetAll<CollideGuard>( FindMode.EverythingInSelf ) )
		{
			if ( guard.IsValid() )
				guard.Destroy();
		}
#else
		go.Tags.Remove( NoCollideTag );
		go.Tags.Remove( GrabbedTag );
		go.Tags.Remove( PlayerClipTag );
#endif

		go.Tags.Add( "entity" );
		go.Tags.Add( "solid" );
	}

	/// <summary>DXRP Prop pattern — <c>model.Bounds</c> drives BoxCollider center + scale in model space (matches ModelDoc white wireframe).</summary>
	public static bool SyncBoxColliderFromModel( GameObject go )
	{
		if ( !go.IsValid() )
			return false;

		var renderer = go.Components.Get<ModelRenderer>( FindMode.EverythingInSelf );
		var box = go.Components.Get<BoxCollider>( FindMode.EverythingInSelf );
		if ( !renderer.IsValid() || !box.IsValid() || renderer.Model is null )
			return false;

		var bounds = renderer.Model.Bounds;
		box.Enabled = true;
		box.Center = bounds.Center;
		box.Scale = bounds.Size;
		box.IsTrigger = false;
		box.OnPhysicsChanged();
		return true;
	}

	/// <summary>Logs <c>model.Bounds</c> center/size for prefab BoxCollider bake (copy into prefab JSON).</summary>
	public static bool TryGetModelColliderBake( GameObject go, out Vector3 center, out Vector3 size )
	{
		center = default;
		size = default;

		if ( !go.IsValid() )
			return false;

		var renderer = go.Components.Get<ModelRenderer>( FindMode.EverythingInSelf );
		if ( !renderer.IsValid() || renderer.Model is null )
			return false;

		var bounds = renderer.Model.Bounds;
		center = bounds.Center;
		size = bounds.Size;
		return true;
	}

	/// <summary>Dev / scale-audit logging for ModelDoc + prefab tuning.</summary>
	public static void LogModelPhysics( GameObject go, string tag )
	{
		if ( !go.IsValid() )
			return;

		var renderer = go.Components.Get<ModelRenderer>( FindMode.EverythingInSelf );
		var box = go.Components.Get<BoxCollider>( FindMode.EverythingInSelf );
		if ( renderer.IsValid() && renderer.Model is not null )
		{
			var bounds = renderer.Model.Bounds;
			Log.Info( $"LIFEPUNCH_PROP_PHYSICS {tag} modelBounds center={bounds.Center} size={bounds.Size} extents={bounds.Extents}" );
		}

		if ( renderer.IsValid() )
		{
			var mesh = renderer.Bounds;
			Log.Info( $"LIFEPUNCH_PROP_PHYSICS {tag} renderBounds size={mesh.Size} mins={mesh.Mins} maxs={mesh.Maxs}" );
		}

		if ( box.IsValid() )
			Log.Info( $"LIFEPUNCH_PROP_PHYSICS {tag} boxCollider center={box.Center} scale={box.Scale} static={box.Static}" );
	}
}
