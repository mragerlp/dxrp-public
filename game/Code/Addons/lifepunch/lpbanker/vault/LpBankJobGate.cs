// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// BANK-VAULT-1 U4a (cc-opus-4): the bank's job gate, keyed on a job TAG. It follows the Banker gate that the hub
// already has (LpBankerAccess, LpBankerIdent.cs) and the probe that reads it (lp_banker_gate_probe).
//
// NO JOB IS DEFINED IN CODE. A job reaches the server from the Portal (server config); this file only asks whether
// the caller's job carries a named tag (JobTags.HasNamedTag, JobTags.cs:92, which reads the job's own JobTags list).
// The Portal rows the principal enters are written out in the BANK-1 return.
//
// BANK-GATE-1: LpBankerAccess.JobGateEnabled is on. In a real game every staff check here is the job tag alone,
// and it fails closed: until the Portal rows exist nobody passes. The editor seam is kept: inside the editor every
// check passes without the job, so the dev commands and the proof still run.

using Dxura.RP.Game;
using Sandbox;

namespace LifePunch.DXRP.Addons.Banker;

public static class LpBankJobGate
{
	/// <summary>The Banker job's tag (the one the hub gate already names).</summary>
	public const string BankerTag = LpBankerAccess.BankerJobTag;

	/// <summary>The Bank Guard job's tag in its Portal row (ruled 2026-11-11: "bankguard").</summary>
	public const string GuardTag = "bankguard";

	/// <summary>Whether the player's job carries the named tag. No seam: this is the plain reading.</summary>
	public static bool HasTag( Player player, string tag )
		=> player.IsValid() && JobTags.HasNamedTag( player.Job, tag );

	/// <summary>The gate for one tag: the editor always passes (the seam); a real game passes the job tag.</summary>
	public static bool Passes( Player player, string tag )
		=> Application.IsEditor || ( LpBankerAccess.JobGateEnabled && HasTag( player, tag ) );

	public static bool IsBanker( Player player ) => Passes( player, BankerTag );

	public static bool IsGuard( Player player ) => Passes( player, GuardTag );

	/// <summary>Banker or Bank Guard.</summary>
	public static bool IsStaff( Player player ) => IsBanker( player ) || IsGuard( player );

	/// <summary>
	/// Who is told when the bank alarm trips: Bank Guards and police (the core Police role). PROVISIONAL: the alarm
	/// audience is a WAIT. The editor always passes (the seam).
	/// </summary>
	public static bool IsAlarmAudience( Player player )
	{
		if ( Application.IsEditor )
			return true;

		return LpBankerAccess.JobGateEnabled && player.IsValid() && ( HasTag( player, GuardTag ) || player.Job.IsPoliceRole() );
	}
}
