// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "lifepunchulx" (s&box ident: lifepunch.lifepunchulx · addon ident: lifepunchulx) is the sole-owned
// intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
// sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Author account: mrragerlp · Public alias (in-game · Steam · Discord): Bloodwave
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;

namespace LifePunch.DXRP.Addons.StaffMenu;

/// <summary>
/// Describes the source and availability of a parity field.
/// Unsynced host fields such as ServerName and MaxPlayers retain defaults on clients;
/// <see cref="HostOnly"/> prevents those defaults from being displayed as server data.
/// </summary>
public enum LpParityAvailability
{
	/// <summary>
	/// Read from a host-authoritative or host-synced source available to this viewer.
	/// </summary>
	Live,

	/// <summary>The source exists but only the host can read it, and this viewer is not the host.</summary>
	HostOnly,

	/// <summary>The server carries no portal authorization key, so no portal-fed value exists to show.</summary>
	Unlinked,

	/// <summary>Linked, but the portal has not answered yet (config not marked ready).</summary>
	Pending,

	/// <summary>Catalogued as unreachable from the game process at all — portal browser only.</summary>
	PortalOnly
}

/// <summary>
/// A display-ready parity field with its source availability, free of Dxura types for Razor.
/// When <see cref="Availability"/> is not <see cref="LpParityAvailability.Live"/>,
/// <see cref="Value"/> explains the unavailable state instead of showing a placeholder value.
/// </summary>
public readonly record struct LpParityField(
	string Label,
	string Value,
	LpParityAvailability Availability );

/// <summary>A titled group of <see cref="LpParityField"/> rows — one card in a parity view.</summary>
public readonly record struct LpParitySection(
	string Title,
	IReadOnlyList<LpParityField> Fields );

/// <summary>Where a portal capability can live.</summary>
public enum LpParityClass
{
	/// <summary>The data is already inside the game process; a read surface needs no new transport.</summary>
	InGameRead,

	/// <summary>A mutating path already exists in-tree that in-game UI could drive.</summary>
	InGameAction,

	/// <summary>No seam exists in this tree; reaching it means new backend endpoints.</summary>
	PortalOnly
}

/// <summary>
/// A portal capability, its game interface classification and the source used to classify it.
/// <see cref="Sensor"/> identifies the file or symbol to check when updating the catalogue.
/// </summary>
public readonly record struct LpParityCatalogRow(
	string Scope,
	string Capability,
	LpParityClass Class,
	bool Shipped,
	bool PrincipalDomain,
	string Sensor );

/// <summary>
/// A display rank observed on an online player, projected without Dxura types.
/// This represents the highest/display rank returned by GetPlayerRank, not the full tenant roster.
/// </summary>
public readonly record struct LpParityRank(
	string Name,
	int Order,
	string ColorHex,
	bool IsDefault,
	int PermissionCount,
	int HoldersOnline,
	bool HasWildcard );
