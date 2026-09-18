// -----------------------------------------------------------------------------
// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH Server Hub for DXRP" (s&box ident: lifepunch.serverhub - addon ident: serverhub)
// Author account: mrragerlp - Public alias (in-game / Steam / Discord): Bloodwave
// -----------------------------------------------------------------------------

using System.Collections.Generic;

namespace LifePunch.DXRP.Addons.ServerHub;

/// <summary>
/// THE BANKER SOCKET. This is an interface note, not an implementation.
/// </summary>
/// <remarks>
/// <para>
/// No Banker logic exists in this addon and none is intended here. What exists is the SHAPE
/// the Banker lane will hand to the hub, declared up front so that lane inherits a socket
/// instead of a rewrite. The contract is deliberately one-directional and inert:
/// </para>
/// <para>
/// 1. The Banker lane constructs an <see cref="LpServerHubBankerVm"/> and supplies it to
///    <c>LpServerHubBanker</c> through its <c>Model</c> property. That is the whole seam.
/// 2. When <c>Model</c> is null - which is every code path today - the tab renders its
///    reserved state. Nothing else in the hub inspects this type, so an empty socket cannot
///    affect the Jobs or Info tabs.
/// 3. The shell, the nav, and the tab host require NO change when the Banker arrives. The
///    tab is already routed; only the model becomes non-null.
/// </para>
/// <para>
/// The fields below are a starting proposal for that conversation, not a settled schema.
/// They are all display strings for the same reason the job rows are: formatting belongs on
/// the data side of the seam, so the view cannot invent a precision or a currency sign. When
/// the Banker lane disagrees with a field, the field changes - this type is theirs to shape.
/// </para>
/// <para>
/// DELIBERATELY ABSENT: any balance mutation, transfer call, interest calculation, RPC, or
/// command. Those belong to the Banker lane and to the economy law that governs it. A socket
/// that carried them would be the feature, not a placeholder for it.
/// </para>
/// </remarks>
/// <param name="BalanceAmount">
/// Account balance, pre-formatted. The sign is applied by the view per currency law.
/// </param>
/// <param name="AccountLabel">
/// Whatever the Banker lane decides an account is identified by, as display text.
/// </param>
/// <param name="RecentActivity">
/// Most recent activity, newest first. Empty is a valid, renderable state - not an error.
/// </param>
public sealed record LpServerHubBankerVm(
	string BalanceAmount,
	string AccountLabel,
	IReadOnlyList<LpServerHubBankerLineVm> RecentActivity );

/// <summary>One line of banker activity. Display-only.</summary>
/// <param name="Label">Human-readable description of the movement.</param>
/// <param name="Amount">Pre-formatted magnitude, unsigned.</param>
/// <param name="IsCredit">
/// True when the line increases the balance. Drives the sign and colour, nothing else.
/// </param>
public sealed record LpServerHubBankerLineVm(
	string Label,
	string Amount,
	bool IsCredit );
