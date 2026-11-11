// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// BANK-VAULT-1 U4 (cc-opus-4): the scene event the keypad raises when a caller is locked out after wrong codes.
// Native s&box event-interface idiom, the same shape as ILpHubResult (lifepunchcore/LpHubResult.cs:22). Posted on
// the host only, once, at the onset of a lockout. It carries no code and no digest. No money (Law B).

using System;
using Sandbox;

namespace LifePunch.DXRP.Addons.Keypad;

public interface ILpKeypadEvent : ISceneEvent<ILpKeypadEvent>
{
	/// <param name="keypadId">GameObject id of the keypad that locked the caller out.</param>
	/// <param name="callerId">Connection id of the caller who is locked out.</param>
	void OnKeypadLockout( Guid keypadId, Guid callerId );
}
