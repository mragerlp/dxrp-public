using System.Threading.Tasks;

namespace Dxura.RP.Game.UI;

public partial class FeedbackPopup
{
	private bool _capturing;

	public static void Open()
	{
		Sandbox.Game.Overlay.CloseAll();
		GameManager.ShowUi<FeedbackPopup>();
	}

	private void Close()
	{
		Destroy();
	}

	// Hides this popup and closes the pause menu (which is what opened it) so the resulting
	// screenshot shows the rest of the game/HUD underneath. Runs on the same GameObject as the
	// HUD, so everything else (chat, health, etc.) stays visible in the capture.
	private async Task<byte[]?> CaptureScreenshot()
	{
		// UI callbacks normally start on the main thread, but their continuations do not
		// necessarily stay there. All scene and render-target work must remain on it.
		await GameTask.MainThread();

		if ( !Scene.Camera.IsValid() )
		{
			return null;
		}

		_capturing = true;
		PauseMenu.Close();
		StateHasChanged();

		// Give the hidden state a couple of real frames to actually render before capturing.
		await GameTask.DelayRealtime( 50 );

		// DelayRealtime resumes on a worker thread. Return to the main thread before
		// interacting with the camera or GPU resources.
		await GameTask.MainThread();

		Texture? texture = null;
		byte[]? png = null;

		try
		{
			if ( Scene.Camera.IsValid() )
			{
				texture = Texture.CreateRenderTarget().WithSize( (int)Screen.Width, (int)Screen.Height ).Create();
				Scene.Camera.RenderToTexture( texture );

				await GameTask.WorkerThread();
				using var bitmap = texture.GetBitmap( 0 );
				png = bitmap.ToPng();
			}
		}
		catch ( Exception ex )
		{
			Log.Error( $"Failed to capture feedback screenshot: {ex.Message}" );
		}

		await GameTask.MainThread();
		texture?.Dispose();
		_capturing = false;
		StateHasChanged();

		return png;
	}
}
