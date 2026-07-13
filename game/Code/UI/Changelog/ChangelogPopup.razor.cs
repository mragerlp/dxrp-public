namespace Dxura.RP.Game.UI;

public partial class ChangelogPopup
{
	private string Url { get; set; } = "";

	public static void Open()
	{
		Sandbox.Game.Overlay.CloseAll();
		GameManager.ShowUi<ChangelogPopup>();
	}

	private void Close()
	{
		Destroy();
	}

	protected override void OnStart()
	{
		var lastSeen = ChangelogPreferences.LoadLastSeen();
		var baseUrl = $"{Constants.BaseWebsiteUrl}/embed/changelog";
		Url = lastSeen.HasValue
			? $"{baseUrl}?since={Uri.EscapeDataString( lastSeen.Value.ToString( "o" ) )}"
			: baseUrl;

		ChangelogPreferences.SaveLastSeen( DateTimeOffset.UtcNow );
	}

	protected override int BuildHash()
	{
		return HashCode.Combine( Url );
	}
}
