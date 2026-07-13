namespace Dxura.RP.Game.UI;

public sealed class TabMenuSectionHost : Panel
{
	public TabMenuSectionDefinition? Section { get; set; }
	public int Revision { get; set; }

	private string? _mountedSectionId;
	private int _mountedRevision = -1;

	protected override void OnParametersSet()
	{
		base.OnParametersSet();
		MountSection();
	}

	private void MountSection()
	{
		if ( Section == null )
		{
			DeleteChildren( true );
			_mountedSectionId = null;
			_mountedRevision = -1;
			return;
		}

		if ( Section.PanelType == null )
		{
			DeleteChildren( true );
			_mountedSectionId = null;
			_mountedRevision = -1;
			return;
		}

		if ( _mountedSectionId == Section.Id && _mountedRevision == Revision )
		{
			return;
		}

		DeleteChildren( true );

		var type = TypeLibrary.GetType( Section.PanelType );
		var panel = type?.Create<Panel>();
		if ( !panel.IsValid() )
		{
			Log.Warning( $"Failed to create tab menu section '{Section.Id}' ({Section.PanelType.Name})." );
			_mountedSectionId = null;
			_mountedRevision = -1;
			return;
		}

		panel.Parent = this;
		_mountedSectionId = Section.Id;
		_mountedRevision = Revision;
	}
}
