namespace Dxura.RP.Game;

/// <summary>
/// Additive, component-owned state carried inside an equipment snapshot.
/// Implementations must not mutate inventory or other economy state while capturing or restoring.
/// </summary>
public interface IEquipmentSnapshotState
{
	string SnapshotKey { get; }

	bool TryCaptureSnapshotState( out string payload );

	bool TryRestoreSnapshotState( string payload );
}
