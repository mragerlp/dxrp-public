using Dxura.RP.Game;

namespace LifePunch.DXRP.Addons.Weapons.Attachments;

[Title( "Weapon Attachment Render Controller" )]
[Group( "LifePunch Weapons" )]
public sealed class WeaponAttachmentRenderController : Component, IDroppedWeaponPresentationSource
{
	[Property]
	public WeaponAttachmentPerspective Perspective { get; set; }

	[Property]
	public ModelRenderer PistolSuppressorRenderer { get; set; }

	[Property]
	public ModelRenderer PbsSuppressorRenderer { get; set; }

	[Property]
	public ModelRenderer RedDotRenderer { get; set; }

	[Property]
	public ModelRenderer LaserBodyRenderer { get; set; }

	[Property]
	public ModelRenderer LaserBeamRenderer { get; set; }

	public bool TryCopyPresentationTo( DroppedEquipment dropped )
	{
		if ( Perspective != WeaponAttachmentPerspective.ThirdPerson
		     || !dropped.IsValid()
		     || !TryResolveDropSourceContext( out var equipment, out var attachmentState )
		     || !AreRendererBindingsDistinct()
		     || !HasRequiredRendererBindings( attachmentState.Current )
		     || !AreAttachmentRenderersUnder( equipment.GameObject ) )
		{
			return false;
		}

		var sourceBaseRenderer = equipment.ModelRenderer;
		var droppedBaseRenderer = dropped.Components.Get<ModelRenderer>();
		var droppedState = dropped.Components.Get<WeaponAttachmentState>( FindMode.EverythingInSelfAndDescendants );
		if ( !sourceBaseRenderer.IsValid()
		     || !droppedBaseRenderer.IsValid()
		     || !droppedState.IsValid()
		     || droppedState.Current != attachmentState.Current )
		{
			return false;
		}

		var cloneRoot = new GameObject( dropped.GameObject, true, "weapon_attachment_presentation" )
		{
			NetworkMode = NetworkMode.Snapshot
		};
		var droppedController = cloneRoot.Components.Create<WeaponAttachmentRenderController>( false );
		droppedController.Perspective = WeaponAttachmentPerspective.Dropped;
		droppedController.PistolSuppressorRenderer = CloneRendererForDrop(
			cloneRoot,
			sourceBaseRenderer,
			PistolSuppressorRenderer );
		droppedController.PbsSuppressorRenderer = CloneRendererForDrop(
			cloneRoot,
			sourceBaseRenderer,
			PbsSuppressorRenderer );
		droppedController.RedDotRenderer = CloneRendererForDrop(
			cloneRoot,
			sourceBaseRenderer,
			RedDotRenderer );
		droppedController.LaserBodyRenderer = CloneRendererForDrop(
			cloneRoot,
			sourceBaseRenderer,
			LaserBodyRenderer );
		droppedController.LaserBeamRenderer = CloneRendererForDrop(
			cloneRoot,
			sourceBaseRenderer,
			LaserBeamRenderer );

		if ( !droppedController.AreRendererBindingsDistinct()
		     || !droppedController.HasRequiredRendererBindings( attachmentState.Current ) )
		{
			cloneRoot.Destroy();
			return false;
		}

		droppedController.Enabled = true;
		return true;
	}

	protected override void OnEnabled()
	{
		ApplyRendererState();
	}

	protected override void OnUpdate()
	{
		ApplyRendererState();
	}

	protected override void OnDisabled()
	{
		DisableAllRenderers();
	}

	private void ApplyRendererState()
	{
		if ( !TryResolveContext( out var attachmentState, out var baseVisible ) || !baseVisible )
		{
			DisableAllRenderers();
			return;
		}

		var loadout = attachmentState.Current;
		if ( !HasRequiredRendererBindings( loadout ) )
		{
			DisableAllRenderers();
			return;
		}

		SetRendererEnabled(
			PistolSuppressorRenderer,
			WeaponAttachmentPresentation.IsAttachmentRendererVisible(
				loadout,
				WeaponAttachmentKind.PistolSuppressor ) );
		SetRendererEnabled(
			PbsSuppressorRenderer,
			WeaponAttachmentPresentation.IsAttachmentRendererVisible(
				loadout,
				WeaponAttachmentKind.PbsSuppressor ) );
		SetRendererEnabled(
			RedDotRenderer,
			WeaponAttachmentPresentation.IsAttachmentRendererVisible(
				loadout,
				WeaponAttachmentKind.RedDot ) );
		SetRendererEnabled(
			LaserBodyRenderer,
			WeaponAttachmentPresentation.IsAttachmentRendererVisible(
				loadout,
				WeaponAttachmentKind.Laser ) );
		SetRendererEnabled(
			LaserBeamRenderer,
			WeaponAttachmentPresentation.IsLaserVisible( loadout, Perspective ) );
	}

	private bool TryResolveContext( out WeaponAttachmentState attachmentState, out bool baseVisible )
	{
		attachmentState = null;
		baseVisible = false;

		if ( Perspective == WeaponAttachmentPerspective.Dropped )
		{
			var dropped = Components.Get<DroppedEquipment>( FindMode.EverythingInSelfAndAncestors );
			if ( !dropped.IsValid() )
			{
				return false;
			}

			var droppedBaseRenderer = dropped.Components.Get<ModelRenderer>();
			var droppedBaseModel = droppedBaseRenderer.IsValid() ? droppedBaseRenderer.Model : null;
			baseVisible = droppedBaseRenderer.IsValid()
				&& droppedBaseRenderer.Enabled
				&& droppedBaseModel is not null
				&& droppedBaseModel.IsValid()
				&& !droppedBaseModel.IsError
				&& AreAttachmentRenderersUnder( dropped.GameObject );
			if ( !AreRendererBindingsDistinct() )
			{
				return false;
			}

			attachmentState = dropped.Components.Get<WeaponAttachmentState>( FindMode.EverythingInSelfAndDescendants );
			return attachmentState.IsValid();
		}

		Equipment equipment;
		if ( Perspective == WeaponAttachmentPerspective.FirstPerson )
		{
			var viewModel = Components.Get<ViewModel>( FindMode.EverythingInSelfAndAncestors );
			if ( !viewModel.IsValid() || !viewModel.Equipment.IsValid() )
			{
				return false;
			}

			equipment = viewModel.Equipment;
			baseVisible = viewModel.ModelRenderer.IsValid()
				&& viewModel.ModelRenderer.Enabled
				&& viewModel.RenderingEnabled
				&& AreAttachmentRenderersUnder( viewModel.AdditionalRendererRoot );
		}
		else if ( Perspective == WeaponAttachmentPerspective.ThirdPerson )
		{
			equipment = Components.Get<Equipment>( FindMode.EverythingInSelfAndAncestors );
			if ( !equipment.IsValid() )
			{
				return false;
			}

			baseVisible = equipment.ModelRenderer.IsValid()
				&& equipment.ModelRenderer.Enabled
				&& AreAttachmentRenderersUnder( equipment.GameObject );
		}
		else
		{
			return false;
		}

		if ( !AreRendererBindingsDistinct() )
		{
			return false;
		}

		attachmentState = equipment.Components.Get<WeaponAttachmentState>( FindMode.EverythingInSelfAndDescendants );
		return attachmentState.IsValid();
	}

	private bool TryResolveDropSourceContext(
		out Equipment equipment,
		out WeaponAttachmentState attachmentState )
	{
		equipment = Components.Get<Equipment>( FindMode.EverythingInSelfAndAncestors );
		attachmentState = equipment.IsValid()
			? equipment.Components.Get<WeaponAttachmentState>( FindMode.EverythingInSelfAndDescendants )
			: null;
		return equipment.IsValid() && attachmentState.IsValid();
	}

	private static ModelRenderer CloneRendererForDrop(
		GameObject cloneRoot,
		ModelRenderer sourceBaseRenderer,
		ModelRenderer sourceRenderer )
	{
		if ( !sourceRenderer.IsValid() )
		{
			return null;
		}

		var cloneObject = new GameObject( cloneRoot, true, sourceRenderer.GameObject.Name )
		{
			LocalTransform = sourceBaseRenderer.WorldTransform.ToLocal( sourceRenderer.WorldTransform ),
			NetworkMode = NetworkMode.Snapshot
		};
		var cloneRenderer = cloneObject.Components.Create<ModelRenderer>( false );
		cloneRenderer.CopyFrom( sourceRenderer );
		cloneRenderer.Enabled = false;
		return cloneRenderer;
	}

	private bool HasRequiredRendererBindings( WeaponAttachmentLoadout loadout )
	{
		return HasRendererForVisibleAttachment(
			loadout,
			WeaponAttachmentKind.PistolSuppressor,
			PistolSuppressorRenderer )
			&& HasRendererForVisibleAttachment(
				loadout,
				WeaponAttachmentKind.PbsSuppressor,
				PbsSuppressorRenderer )
			&& HasRendererForVisibleAttachment(
				loadout,
				WeaponAttachmentKind.RedDot,
				RedDotRenderer )
			&& HasRendererForVisibleAttachment(
				loadout,
				WeaponAttachmentKind.Laser,
				LaserBodyRenderer )
			&& (!WeaponAttachmentPresentation.IsLaserVisible( loadout, Perspective )
				|| LaserBeamRenderer.IsValid());
	}

	private static bool HasRendererForVisibleAttachment(
		WeaponAttachmentLoadout loadout,
		WeaponAttachmentKind kind,
		ModelRenderer renderer )
	{
		return !WeaponAttachmentPresentation.IsAttachmentRendererVisible( loadout, kind )
			|| renderer.IsValid();
	}

	private bool AreRendererBindingsDistinct()
	{
		ModelRenderer[] renderers =
		[
			PistolSuppressorRenderer,
			PbsSuppressorRenderer,
			RedDotRenderer,
			LaserBodyRenderer,
			LaserBeamRenderer
		];

		for ( var i = 0; i < renderers.Length; i++ )
		{
			if ( !renderers[i].IsValid() )
			{
				continue;
			}

			for ( var j = i + 1; j < renderers.Length; j++ )
			{
				if ( renderers[j].IsValid() && ReferenceEquals( renderers[i], renderers[j] ) )
				{
					return false;
				}
			}
		}

		return true;
	}

	private bool AreAttachmentRenderersUnder( GameObject root )
	{
		if ( !root.IsValid() )
		{
			return false;
		}

		return IsRendererUnder( root, PistolSuppressorRenderer )
			&& IsRendererUnder( root, PbsSuppressorRenderer )
			&& IsRendererUnder( root, RedDotRenderer )
			&& IsRendererUnder( root, LaserBodyRenderer )
			&& IsRendererUnder( root, LaserBeamRenderer );
	}

	private static bool IsRendererUnder( GameObject root, ModelRenderer renderer )
	{
		if ( !renderer.IsValid() )
		{
			return true;
		}

		foreach ( var childRenderer in root.GetComponentsInChildren<ModelRenderer>( true ) )
		{
			if ( ReferenceEquals( childRenderer, renderer ) )
			{
				return true;
			}
		}

		return false;
	}

	private void DisableAllRenderers()
	{
		SetRendererEnabled( PistolSuppressorRenderer, false );
		SetRendererEnabled( PbsSuppressorRenderer, false );
		SetRendererEnabled( RedDotRenderer, false );
		SetRendererEnabled( LaserBodyRenderer, false );
		SetRendererEnabled( LaserBeamRenderer, false );
	}

	private static void SetRendererEnabled( ModelRenderer renderer, bool enabled )
	{
		if ( !renderer.IsValid() || renderer.Enabled == enabled )
		{
			return;
		}

		renderer.Enabled = enabled;
	}
}
