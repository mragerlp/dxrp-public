// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH Bitcoin Miner for DXRP" (s&box ident: lifepunch.bitcoin · addon ident: bitcoinmining)
// is the sole-owned intellectual property of lifepunch.co. It is NOT licensed for resale,
// redistribution, sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Author account: mrragerlp · Public alias (in-game · Steam · Discord): Bloodwave
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

namespace LifePunch.DXRP.Addons.Bitcoin;

/// <summary>
/// T3 shipped-defaults for the lpbitcoin COMPUTE upgrade ladder — the Monnow
/// <c>GetConfig&lt;T&gt;</c> idiom (BLOCK-0 spec v1.1, APPROVED 2026-07-12). ONE config class,
/// flat <c>{ get; init; }</c> fields, defaults in the initializers = the shipped ladder (no
/// separate defaults JSON — like <c>MonnowPrinterConfig</c>). Read once via
/// <c>GetConfig( new LpBitcoinRackConfig() )</c> on a rack content entry; the portal Config
/// Override JSON on that content entry overrides these (edit → Save → Sync → restart, NOT live —
/// live cash dials live in the Store lane, <see cref="LpBitcoinPortalEconomySync"/>).
///
/// SCOPE (v1): the COMPUTE ladder only — per-tier effect multiplier + per-tier cost. This is
/// <see cref="LpBitcoinComputeTrack"/>'s surface, which was already run-time data (the inline
/// price ladder), so exposing it needs NO change to the <see cref="LpBitcoinEconomy"/> base-rig
/// constants. Base-rig scalar overrides (StartClockGhz, BaseSpeed, PayoutIntervalSeconds,
/// BufferCapTicks, yields) are a DEFERRED follow-up: they are <c>const</c> today, so making them
/// overridable is a const→static conversion of the economy core — a propose-and-STOP change held
/// for its own GO. Their shipped values stay canon in <see cref="LpBitcoinEconomy"/>.
///
/// LATCH: the ladder is ONE process-wide registry entry (<c>rack_compute</c>) shared by every rack
/// — a global one-shot latch (<see cref="LpBitcoinComputeTrack.EnsureRegistered"/>). The FIRST rack
/// to reconcile latches the ladder from ITS config; a later rack whose config diverges (e.g. a
/// separately-tuned <c>advancedgpurack</c> content type) is ignored by the latch and logged as a
/// mismatch. Tier LEVELS stay in-game <c>[Sync]</c> state (<c>ComputeTier</c>), never content entries
/// (v1.1 V2 finding). Pure data — no DXRP dependency, compiles under LIFEPUNCH_LOCAL; the
/// <c>GetConfig</c> READ (a BaseEntity method) is what carries the local guard.
///
/// Defaults mirror the pre-extraction ladder EXACTLY, so an absent/empty override = identical
/// behavior: effect ×2/4/8/16/32 (stock <c>1&lt;&lt;tier</c>) and price ladder
/// 0.25/0.75/2/6/16 BTC.
/// </summary>
public sealed class LpBitcoinRackConfig
{
	/// <summary>Highest purchasable COMPUTE tier (I–V shipped). The effect/cost arrays are 5-wide;
	/// a smaller value simply caps upgrades below the array length.</summary>
	public int MaxTier { get; init; } = 5;

	// ── Per-tier effect multiplier vs stock (T0 = ×1 implicit; feeds mining rate via ClockGhz) ──
	public int Tier1EffectMultiplier { get; init; } = 2;
	public int Tier2EffectMultiplier { get; init; } = 4;
	public int Tier3EffectMultiplier { get; init; } = 8;
	public int Tier4EffectMultiplier { get; init; } = 16;
	public int Tier5EffectMultiplier { get; init; } = 32;

	// ── Per-tier base cost in sats (before the rack's yield multiplier at quote time) ──
	public long Tier1CostSats { get; init; } = 25_000_000;    // I   — 0.25 BTC
	public long Tier2CostSats { get; init; } = 75_000_000;    // II  — 0.75 BTC
	public long Tier3CostSats { get; init; } = 200_000_000;   // III — 2 BTC
	public long Tier4CostSats { get; init; } = 600_000_000;   // IV  — 6 BTC
	public long Tier5CostSats { get; init; } = 1_600_000_000; // V   — 16 BTC
}
