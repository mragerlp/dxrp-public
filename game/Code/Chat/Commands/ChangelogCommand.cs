using Dxura.RP.Game.UI;

namespace Dxura.RP.Game.Commands;

public class ChangelogCommand : ICommand
{
	public string Command => "changelog";
	public string Help => "Opens the changelog.";
	public string[] Aliases => ["cl"];
	public bool IsUsableWhileDead => true;
	public bool IsUsableWhileRestricted => true;

	public bool ExecuteLocal( string[] args, string raw )
	{
		ChangelogPopup.Open();
		return true;
	}

	public bool ExecuteHost( Player caller, string[] args, string raw )
	{
		// Handled entirely client-side via ExecuteLocal.
		return true;
	}
}
