// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// LifePunch shared ground-alignment for placeable props (mesh bottom → surface).
// ─────────────────────────────────────────────────────────────────────────────

using Sandbox;

namespace LifePunch.DXRP.Addons;

/// <summary>Aligns a prop's rendered mesh bottom to the ground trace under it.</summary>
public static class LifePunchGroundContact
{
	private const float TraceUp = 64f;
	private const float TraceDown = 4096f;
	private const float VerticalDropHeight = 8192f;
	private const float Epsilon = 0.05f;

	/// <summary>Lifts <paramref name="go"/> so compiled mesh bottom sits on the surface below.</summary>
	/// <returns>True when a ground hit was applied.</returns>
	public static bool AlignMeshBottom( GameObject go )
	{
		if ( !go.IsValid() )
			return false;

		var renderer = go.Components.Get<ModelRenderer>( FindMode.EverythingInSelf );
		if ( !renderer.IsValid() || renderer.Model is null )
			return false;

		var meshBottom = GetWorldMeshBottom( go, renderer );

		var scene = go.Scene ?? Game.ActiveScene;
		if ( scene is null )
			return false;

		var box = go.Components.Get<BoxCollider>( FindMode.EverythingInSelf );
		var boxWasEnabled = box.IsValid() && box.Enabled;
		if ( boxWasEnabled )
			box.Enabled = false;

		try
		{
			var start = meshBottom + Vector3.Up * TraceUp;
			var end = meshBottom - Vector3.Up * TraceDown;
			SceneTraceResult trace = scene.Trace.Ray( start, end )
				.IgnoreGameObjectHierarchy( go )
				.Run();
			if ( trace.Hit )
			{
				LiftBy( go, trace.HitPosition.z - meshBottom.z );
				return true;
			}

			// Long vertical drop under root XY — survives bad aim-ray spawn height on large maps.
			var dropStart = go.WorldPosition + Vector3.Up * VerticalDropHeight;
			var dropEnd = go.WorldPosition - Vector3.Up * VerticalDropHeight;
			SceneTraceResult drop = scene.Trace.Ray( dropStart, dropEnd )
				.IgnoreGameObjectHierarchy( go )
				.Run();
			if ( drop.Hit )
			{
				meshBottom = GetWorldMeshBottom( go, renderer );
				LiftBy( go, drop.HitPosition.z - meshBottom.z );
				return true;
			}
		}
		finally
		{
			if ( boxWasEnabled && box.IsValid() )
				box.Enabled = true;
		}

		return false;
	}

	static Vector3 GetWorldMeshBottom( GameObject go, ModelRenderer renderer )
	{
		// Model local bounds + import align_origin_z — authoritative for feet (render AABB can lag one frame).
		var localBounds = renderer.Model.Bounds;
		var modelFoot = go.WorldPosition + go.WorldRotation * localBounds.Mins;

		if ( renderer.IsValid() && renderer.Bounds.Size.Length > 1f )
		{
			var renderFoot = renderer.Bounds.Mins;
			// Use whichever foot read is lower — catches offset collider vs mesh mismatch.
			if ( renderFoot.z < modelFoot.z )
				return renderFoot;
		}

		return modelFoot;
	}

	static void LiftBy( GameObject go, float delta )
	{
		if ( System.Math.Abs( delta ) <= Epsilon )
			return;

		go.WorldPosition += Vector3.Up * delta;
	}
}
