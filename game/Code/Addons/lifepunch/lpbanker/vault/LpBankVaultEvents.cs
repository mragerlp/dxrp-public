// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// BANK-VAULT-1 U3 (cc-opus-4): the scene event the vault raises when its door opens. Native s&box event-interface
// idiom, the same shape as ILpHubResult (lifepunchcore/LpHubResult.cs:22). Posted on the host only. No money.

using System;
using Sandbox;

namespace LifePunch.DXRP.Addons.Banker;

public interface ILpBankVaultEvent : ISceneEvent<ILpBankVaultEvent>
{
	/// <param name="vaultId">GameObject id of the vault whose door opened.</param>
	/// <param name="byKeypad">True when the linked keypad's code opened it; false for a Banker's command.</param>
	void OnBankVaultDoorOpened( Guid vaultId, bool byKeypad );
}
