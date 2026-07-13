namespace Dxura.RP.Game.UI;

// Tracks when the player last opened the changelog, purely client-side (mirrors
// TabMenuSectionPreferences), so re-opening it only badges what's actually new.
public static class ChangelogPreferences
{
	private const string PreferencesPath = "changelog-seen.json";

	public static DateTimeOffset? LoadLastSeen()
	{
		if ( !FileSystem.OrganizationData.FileExists( PreferencesPath ) )
		{
			return null;
		}

		try
		{
			var json = FileSystem.OrganizationData.ReadAllText( PreferencesPath );
			var data = global::System.Text.Json.JsonSerializer.Deserialize<ChangelogSeenDto>( json );
			return data?.LastSeenAt;
		}
		catch ( Exception ex )
		{
			Log.Warning( $"Failed to load changelog preferences: {ex.Message}" );
			return null;
		}
	}

	public static void SaveLastSeen( DateTimeOffset lastSeenAt )
	{
		var json = global::System.Text.Json.JsonSerializer.Serialize( new ChangelogSeenDto { LastSeenAt = lastSeenAt } );
		FileSystem.OrganizationData.WriteAllText( PreferencesPath, json );
	}

	private sealed class ChangelogSeenDto
	{
		public DateTimeOffset LastSeenAt { get; set; }
	}
}
