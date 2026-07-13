namespace Dxura.RP.Game.Entities;

/// <summary>
/// Marks a GameObject as an identifiable resource. Generic acceptors (like PalletEntity
/// and DrugDrop) key off this component instead of hardcoded tag strings.
/// </summary>
[Title( "Resource" )]
[Category( "Utilities" )]
public class ResourceComponent : Component, IDescription
{
	/// <summary>
	/// Identifies what kind of item this is (e.g. "weed_brick", "joint"). Used to keep a
	/// pallet's cargo a single homogeneous item type, and to resolve its translated
	/// "resource.&lt;ResourceId&gt;.name" display name.
	/// </summary>
	[Property]
	public string ResourceId { get; set; } = string.Empty;

	[Property]
	public bool PalletStackable { get; set; } = true;

	public string DisplayName => LabelResolver.Resolve( $"resource.{ResourceId}.name", null, ResourceId );
}
