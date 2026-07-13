using Dxura.RP.Shared;
using System.Threading.Tasks;

namespace Dxura.RP.Game.UI;

public partial class FeedbackForm
{
	private const int TitleMaxLength = 100;
	private const int DescriptionMaxLength = 2000;

	private enum SubmitStatus
	{
		None,
		Success,
		Cooldown,
		Flagged,
		Error
	}

	public Action OnCancel { get; set; }
	public Func<Task<byte[]?>>? CaptureScreenshot { get; set; }

	private FeedbackType _type = FeedbackType.Bug;
	private string _title = "";
	private string _description = "";
	private bool _includeScreenshot;
	private bool _isSubmitting;
	private SubmitStatus _status = SubmitStatus.None;
	private string? _issueUrl;

	private bool CanSubmit => !_isSubmitting && !string.IsNullOrWhiteSpace( _title ) && !string.IsNullOrWhiteSpace( _description );

	protected override int BuildHash()
	{
		return HashCode.Combine( _type, _title, _description, _includeScreenshot, _isSubmitting, _status, _issueUrl );
	}

	private void Cancel()
	{
		Sound.Play( "pop" );
		OnCancel?.Invoke();
	}

	private void SelectType( FeedbackType type )
	{
		_type = type;
		StateHasChanged();
	}

	private void OnTitleChanged( string value )
	{
		_title = value;
		_status = SubmitStatus.None;
	}

	private void OnDescriptionChanged( string value )
	{
		_description = value;
		_status = SubmitStatus.None;
	}

	private string GetStatusClass() => _status switch
	{
		SubmitStatus.Success => "success",
		SubmitStatus.Cooldown => "warn",
		SubmitStatus.Flagged => "error",
		SubmitStatus.Error => "error",
		_ => ""
	};

	private string GetStatusIcon() => _status switch
	{
		SubmitStatus.Success => "check_circle",
		SubmitStatus.Cooldown => "schedule",
		SubmitStatus.Flagged => "block",
		SubmitStatus.Error => "error",
		_ => ""
	};

	private string GetStatusMessage() => _status switch
	{
		SubmitStatus.Success => Language.GetPhrase( "notify.feedback.success" ),
		SubmitStatus.Cooldown => Language.GetPhrase( "notify.feedback.cooldown" ),
		SubmitStatus.Flagged => Language.GetPhrase( "notify.feedback.flagged" ),
		SubmitStatus.Error => Language.GetPhrase( "notify.feedback.error" ),
		_ => ""
	};

	private void ToggleIncludeScreenshot()
	{
		Sound.Play( "pop" );
		_includeScreenshot = !_includeScreenshot;
		StateHasChanged();
	}

	private async void Submit()
	{
		if ( !CanSubmit )
		{
			return;
		}

		Sound.Play( "pop" );

		_isSubmitting = true;
		_status = SubmitStatus.None;
		StateHasChanged();

		FeedbackSubmitResult result;
		string? issueUrl;

		try
		{
			byte[]? screenshot = null;
			if ( _includeScreenshot && CaptureScreenshot != null )
			{
				screenshot = await CaptureScreenshot();
			}

			( result, issueUrl ) = await PlayerApiClient.SubmitFeedback( _type, _title.Trim(), _description.Trim(), screenshot );
		}
		catch ( Exception ex )
		{
			Log.Error( $"Failed to submit feedback: {ex.Message}" );
			result = FeedbackSubmitResult.Failed;
			issueUrl = null;
		}

		// Both screenshot encoding and the API request run off the UI thread.
		await GameTask.MainThread();

		_isSubmitting = false;
		_issueUrl = issueUrl;

		switch ( result )
		{
			case FeedbackSubmitResult.Success:
				_status = SubmitStatus.Success;
				_title = "";
				_description = "";
				_type = FeedbackType.Bug;
				_includeScreenshot = false;
				break;
			case FeedbackSubmitResult.Cooldown:
				_status = SubmitStatus.Cooldown;
				break;
			case FeedbackSubmitResult.Flagged:
				_status = SubmitStatus.Flagged;
				break;
			default:
				_status = SubmitStatus.Error;
				break;
		}

		StateHasChanged();
	}

	private void CopyIssueUrl()
	{
		if ( string.IsNullOrEmpty( _issueUrl ) )
		{
			return;
		}

		Sound.Play( "pop" );
		Clipboard.SetText( _issueUrl );
		Notify.Success( "#generic.copied" );
	}
}
