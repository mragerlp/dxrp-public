using System.Threading;
using System.Threading.Tasks;
using Dxura.RP.Game.Tools;
using Dxura.RP.Game.UI;

namespace Dxura.RP.Game;

/// <summary>
/// Preloads materials when players join the game
/// </summary>
public class MaterialPreloader : Component, IGameEvents
{
	private CancellationTokenSource? _preloadCts;
	private int _preloadEpoch;

	protected override void OnStart()
	{
		StartPreload();
	}

	public void OnGameModeUpdated( GameModeDto? before, GameModeDto? after )
	{
		StartPreload();
		MaterialTool.RefreshFromGameMode( before, after );
		MaterialPicker.RefreshLive();
	}

	protected override void OnDestroy()
	{
		CancelPreload();
	}

	private void StartPreload()
	{
		CancelPreload();
		_preloadCts = new CancellationTokenSource();
		var epoch = ++_preloadEpoch;
		_ = PreloadMaterialsAsync( _preloadCts.Token, epoch );
	}

	private void CancelPreload()
	{
		_preloadCts?.Cancel();
		_preloadCts?.Dispose();
		_preloadCts = null;
	}

	private async Task PreloadMaterialsAsync( CancellationToken cancellationToken, int epoch )
	{
		try
		{
			foreach ( var materialPath in GameModeBuilding.Materials )
			{
				if ( cancellationToken.IsCancellationRequested || epoch != _preloadEpoch || !this.IsValid() )
				{
					return;
				}

				if ( string.IsNullOrEmpty( materialPath ) )
				{
					continue;
				}

				if ( GameModeJobDtoExtensions.IsCloudIdent( materialPath ) )
				{
					if ( GameModeBuilding.MaterialCloudOrgRejects( materialPath ) )
					{
						continue;
					}

					var package = await Package.FetchAsync( materialPath, true, true );

					if ( cancellationToken.IsCancellationRequested || epoch != _preloadEpoch )
					{
						return;
					}

					if ( package == null )
					{
						continue;
					}

					await package.MountAsync();

					if ( cancellationToken.IsCancellationRequested || epoch != _preloadEpoch )
					{
						return;
					}

					if ( !package.IsMounted() )
					{
						return;
					}

					var primaryAsset = package.GetMeta( "PrimaryAsset", "" );

					if ( string.IsNullOrEmpty( primaryAsset ) )
					{
						continue;
					}

					await Material.LoadAsync( primaryAsset );
				}
				else
				{
					await Material.LoadAsync( materialPath );
				}
			}
		}
		catch ( OperationCanceledException )
		{
		}
		catch ( Exception )
		{
			Log.Info( "Unable to preload materials" );
		}
	}
}
