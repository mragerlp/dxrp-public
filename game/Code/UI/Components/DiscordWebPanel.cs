namespace Dxura.RP.Game.UI;

/// <summary>
/// Invisible WebPanel used purely to trigger the Steam overlay browser for a Discord invite link.
/// Copies the link to the clipboard and self-destructs after a short lifetime.
/// </summary>
internal class DiscordWebPanel : WebPanel
{
	private const float LifetimeSeconds = 5f;

	private RealTimeSince _timeSinceCreated;

	public static void Open( Panel parent, string url )
	{
		var panel = parent.AddChild<DiscordWebPanel>();
		panel.Surface.Url = url;
		panel._timeSinceCreated = 0;

		Clipboard.SetText( url );
		Notify.Success( "#generic.copied" );
	}

	public override void Tick()
	{
		base.Tick();

		if ( _timeSinceCreated > LifetimeSeconds )
		{
			Delete();
		}
	}
}
