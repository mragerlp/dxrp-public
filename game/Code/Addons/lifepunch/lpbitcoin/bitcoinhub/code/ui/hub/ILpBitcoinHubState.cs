// -----------------------------------------------------------------------------
// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH DXRP Addons" (s&box ident: lifepunch.* - addon ident: lifepunch)
// BTC-UI-2 pre-build (slice SHELL-1b): the ONE-STATE view-model contract for the Bitcoin Hub UI,
// from docs/superpowers/specs/2026-10-17-btc-ui-design.md section 5 (field table) and section 2.3
// (hub app readouts H1-H41). READ-ONLY: this contract carries no action, no delegate and no rail.
// The section 5 "Actions" row is left out on purpose (dispatch S4: NO rails, NO money paths); the
// build slice adds it after its own GO. Values are raw numbers; text comes only from
// ILpBitcoinHubFormat, so the open precision rulings (R-BTCUI-1..4) change one class, not the tabs.
// UNVERIFIED in the editor until the driving seat proves it.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace LifePunch.DXRP.Addons.Bitcoin.HubUi;

/// <summary>
/// One client-side reading of one Bitcoin Hub (spec section 5). Every hub UI tab, the Player Hub
/// $BTC tab and the terminal readouts are meant to read this one object, so one fact reads one way.
/// Source cites are the spec's, at b3d3b525; the build slice re-greps each before binding it.
/// </summary>
public interface ILpBitcoinHubState
{
	/// <summary>False when no hub is bound; every other member is then default.</summary>
	bool HasHub { get; }

	/// <summary>The bound hub's GameObject.Id (UH:35; Player Hub resolver BS:31-35 per R-BTCUI-13).</summary>
	Guid HubId { get; }

	/// <summary>Hubs the viewer owns; the Player Hub shows "n hubs owned" when above 1 (R-BTCUI-13 option c).</summary>
	int OwnedHubCount { get; }

	/// <summary>H1, H6, H13, H40, K19, K29. IsPowered [Sync] (HE:37). One stored field, one writer in the UI (R-BTCUI-5).</summary>
	bool IsPowered { get; }

	/// <summary>H4, K35. AccessPinIsSet (HE:38).</summary>
	bool PinSet { get; }

	/// <summary>H4. AccessPinNeedsReEnrollment (HE:49).</summary>
	bool PinNeedsReEnroll { get; }

	/// <summary>H35, K13. Owner Steam id (HE:50-52; the non-LOCAL replication path is UNVERIFIED).</summary>
	long OwnerSteamId { get; }

	/// <summary>H2, H11, H16, H29, H36, K14, K30. HubWalletBtc (HE:54). Precision R-BTCUI-1.</summary>
	float WalletBtc { get; }

	/// <summary>H11, H31. Wallet only, never wallet + pending under "Hub wallet" (R-BTCUI-3). EC:77.</summary>
	float WalletUsd { get; }

	/// <summary>H29, H38, K17, K31. Still on racks (HE:792): the single pending expression.</summary>
	float PendingBtc { get; }

	/// <summary>Pending at the display rate (EC:77), shown as its own row (R-BTCUI-3 option c).</summary>
	float PendingUsd { get; }

	/// <summary>H32, H37, K16. Display exchange rate, USD per BTC (EC:90-96). Refresh on PortalEconomyRevision (EC:75).</summary>
	float UsdPerBtc { get; }

	/// <summary>H32. True while a rate event is live; then <see cref="RateEventMultiplier"/> is shown as "(m x)".</summary>
	bool RateEventActive { get; }

	/// <summary>H32. Event multiplier; meaningful only while <see cref="RateEventActive"/>.</summary>
	float RateEventMultiplier { get; }

	/// <summary>H7, H14, H18, H30, K21. HasLinkedTerminal (HE:490).</summary>
	bool TerminalLinked { get; }

	/// <summary>H7. A terminal within link range (HE:510-511, TE:30 = 512).</summary>
	bool TerminalInRange { get; }

	/// <summary>H8, H10, H13, K2, K35. Linked rack count (HE:356-365).</summary>
	int RackCount { get; }

	/// <summary>H10, H13, H15. Slot cap (ID:52-58), 3 at b3d3b525.</summary>
	int RackMax { get; }

	/// <summary>Linked racks, each carrying its own slot token. Tabs map slots BY TOKEN, never by position (R-BTCUI-4).</summary>
	IReadOnlyList<ILpBitcoinRackState> Racks { get; }

	/// <summary>H39, K18. The synced alert feed (HE:57, decoded HE:794-795), newest first.</summary>
	IReadOnlyList<LpBitcoinHubAlertRow> Alerts { get; }

	/// <summary>H12, H40, K22. UnreadAlertCount (HE:58).</summary>
	int UnreadAlerts { get; }

	/// <summary>H33, K11. The viewer's on-hand cash, NOT the bank a cash-out pays (WA:53-60). Hashed (new).</summary>
	int LocalCashUsd { get; }

	/// <summary>H3, H28, H4 error line. The last host result envelope (HE:1254-1263, 1268-1291, 1314-1329).</summary>
	LpBitcoinHubLastResult LastResult { get; }

	/// <summary>
	/// One hash over every input shown above, including the four the Player Hub hash misses today
	/// (PortalEconomyRevision, ClockGhz, AssignedSlotToken, HasLinkedTerminal; BS:42-61).
	/// </summary>
	int StateHash { get; }
}

/// <summary>One linked GPU rack (spec section 5, Rack.* rows; readouts H15, H19-H28, K3-K8, K34).</summary>
public interface ILpBitcoinRackState
{
	/// <summary>The rack's GameObject.Id (BS:93): the future action target, never the label.</summary>
	Guid Id { get; }

	/// <summary>AssignedSlotToken (RE:45): gpurack-1, gpurack-2 or advancedgpurack (ID:143-155).</summary>
	string Token { get; }

	/// <summary>Display name, token-first (ID:124-136). Never derived from list position (HP:1983-2022 retires).</summary>
	string Name { get; }

	/// <summary>Advanced rack (RE:32, [Property]; replication UNVERIFIED).</summary>
	bool Advanced { get; }

	/// <summary>H15, H21, K3, K34. RE:46.</summary>
	bool IsMining { get; }

	/// <summary>H15, H24, K4, K34. Rack buffer (RE:47). Precision R-BTCUI-1.</summary>
	float Btc { get; }

	/// <summary>H24. Buffer cap (EC:120-123).</summary>
	float CapBtc { get; }

	/// <summary>H24. Fill, integer percent (HP:1476-1480).</summary>
	int FillPct { get; }

	/// <summary>H15, H21, K3. Btc at or above cap (RE:324-325).</summary>
	bool Full { get; }

	/// <summary>H21. BTC put into tier purchases on this rack (HP:2187-2195). Source cite carried from the spec.</summary>
	float InvestedBtc { get; }

	/// <summary>H22, H25, K5, K34. Current tier, 0 = stock (RE:55). Label per R-BTCUI-4.</summary>
	int Tier { get; }

	/// <summary>H25. Top tier of the ladder (I-V at b3d3b525, CT:129-144).</summary>
	int MaxTier { get; }

	/// <summary>H20, H22, K4, K34. Synced ClockGhz (RE:48), never a projection for current state. Units R-BTCUI-2.</summary>
	float ClockGhz { get; }

	/// <summary>H20, H23, K4, K34. MiningRatePerMinute (RE:61; EC:108-112). BTC per minute; no TH/s (R-BTCUI-2).</summary>
	float RateBtcPerMin { get; }

	/// <summary>H21, K4. Yield factor (RE:58-60).</summary>
	float Yield { get; }

	/// <summary>H26, K6. Next tier price in sats, host-resolved (CT:149-157). Zero at the top tier.</summary>
	long NextQuoteSats { get; }

	/// <summary>H26, K6. Next tier price at the display rate (EC:77).</summary>
	float NextQuoteUsd { get; }

	/// <summary>H27. Clock at the next tier, for the "+x GHz" delta (HP:1400-1421). Projection is allowed only here.</summary>
	float NextClockGhz { get; }

	/// <summary>H27, K6. Payback of the next tier in minutes (HP:1400-1421); 0 when not computable.</summary>
	float PaybackMinutes { get; }
}

/// <summary>
/// One alert feed row (H39, K18). Kind is the existing feed enum
/// (lpbitcoin/bitcoinhub/code/components/LpBitcoinHubAlert.cs:13-19), reused, not redeclared.
/// </summary>
public readonly record struct LpBitcoinHubAlertRow(
	LpBitcoinHubAlertKind Kind,
	string Message,
	float AgeSeconds );

/// <summary>Which host envelope the last result came from.</summary>
public enum LpBitcoinHubResultKind
{
	None,
	CashOut,
	Purchase,
	Pin,
}

/// <summary>
/// The last host result (H3 cash-out chip, H28 confirm chip, H4 PIN error). State comes from the host
/// envelope only (K8). <see cref="PaidUsd"/> is the figure the host paid (R-BTCUI-14), never a preview.
/// </summary>
public readonly record struct LpBitcoinHubLastResult(
	LpBitcoinHubResultKind Kind,
	bool Ok,
	string Message,
	float PaidUsd );

/// <summary>
/// The one text seam. Every figure on every tab is turned into text here and nowhere else.
/// Each method names the ruling that fixes its format; none is ruled at SHELL-1b.
/// </summary>
public interface ILpBitcoinHubFormat
{
	/// <summary>A BTC balance, pending, buffer or wallet figure. R-BTCUI-1.</summary>
	string Btc( float btc );

	/// <summary>A ladder price given in sats. R-BTCUI-1 (option b trims it).</summary>
	string BtcPrice( long sats );

	/// <summary>A USD figure. R-BTCUI-3 (recommendation N0: the bank pays whole dollars, HE:1255).</summary>
	string Usd( float usd );

	/// <summary>A rate in BTC per minute, without the sign. R-BTCUI-2.</summary>
	string RatePerMin( float btcPerMin );

	/// <summary>A clock in GHz, without a core suffix. R-BTCUI-2.</summary>
	string Clock( float ghz );

	/// <summary>A tier label: "STOCK" at 0, no "TIER " prefix (R-BTCUI-4).</summary>
	string Tier( int tier );

	/// <summary>The yield factor (spec 0.#).</summary>
	string Yield( float factor );

	/// <summary>A payback time in minutes (spec N0 min / 0.# hr / d buckets).</summary>
	string Payback( float minutes );

	/// <summary>A relative age for the Logs tab (spec "just now / Nm ago / Nh ago"; clock skew UNVERIFIED).</summary>
	string Age( float seconds );
}
