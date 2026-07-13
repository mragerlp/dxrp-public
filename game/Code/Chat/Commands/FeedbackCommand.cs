using Dxura.RP.Game.UI;

namespace Dxura.RP.Game.Commands;

public class FeedbackCommand : ICommand
{
	public string Command => "feedback";
	public string Help => "Opens the feedback form.";
	public string[] Aliases => ["fb"];
	public bool IsUsableWhileDead => true;
	public bool IsUsableWhileRestricted => true;

	public bool ExecuteLocal( string[] args, string raw )
	{
		FeedbackPopup.Open();
		return true;
	}

	public bool ExecuteHost( Player caller, string[] args, string raw )
	{
		// Handled entirely client-side via ExecuteLocal.
		return true;
	}
}
