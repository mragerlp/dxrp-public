using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dxura.RP.Game;

namespace LifePunch.DXRP.Addons.Weapons.Attachments;

internal static class Program
{
	private const string Glock = "glock";
	private const string Aks74uOriginal = "aks74u_original";
	private const string Aks74uMilitary = "aks74u_military";
	private const string Ak47 = "ak47";
	private const string GlockWorldPrefab =
		"addons/lifepunch/lpweapons/glock/equipment/w_glock/w_glock.prefab";
	private const string Aks74uOriginalWorldPrefab =
		"addons/lifepunch/lpweapons/aks74ucovert/equipment/w_aks74u_original/w_aks74u_original.prefab";
	private const string Ak47WorldPrefab =
		"addons/lifepunch/lpweapons/ak47/equipment/w_ak47/w_ak47.prefab";

	private static readonly Guid PistolSuppressorItem = Guid.Parse( "11111111-1111-1111-1111-111111111111" );
	private static readonly Guid PbsSuppressorItem = Guid.Parse( "22222222-2222-2222-2222-222222222222" );
	private static readonly Guid RedDotItem = Guid.Parse( "33333333-3333-3333-3333-333333333333" );
	private static readonly Guid LaserItem = Guid.Parse( "44444444-4444-4444-4444-444444444444" );

	private static int _failed;

	private static int Main()
	{
		Compatibility_AllowsOnlyRuledWeaponAttachmentPairs();
		Compatibility_BindsControllerIdentityToTheEquipmentPrefab();
		Catalog_BindsKindsToAccessoryGrantIdentifiers();
		Slots_RejectASecondAttachmentWithoutReplacingTheFirst();
		Transactions_AccountForInventoryAndCompensateFailures();
		InventoryApiAcknowledgements_OwnCustodyClassification();
		Drops_RefuseDuplicateMergeWhenAnItemIdentityWouldBeLost();
		Presentation_IsResolvedAtShotTimeWithoutBallisticCoupling();
		PresentationRules_CoverSuppressorDetachAndFallbackMatrix();
		LaserVisibility_IsInvariantBetweenFirstAndThirdPerson();
		AttachmentRendererVisibility_FollowsReplicatedLoadout();
		SnapshotCodec_RoundTripsExactAttachmentCustody();
		State_IsHostAuthoritativeAndDropPersistent();
		SnapshotIntegration_PreservesStateWithoutInventoryMutation();
		AttachmentTransactions_BlockDropsWithoutChangingSnapshotEligibility();
		Controller_IsHostOwnedAndCleanupWhitelistSafe();
		Controller_SerializesInventoryMutationsAcrossWeaponInstances();
		Controller_QuarantinesQueuedRetriesAfterAmbiguousCustody();
		Controller_FailsClosedWhenTakeAcknowledgementIsMissing();
		Controller_DoesNotRestoreDetachFromAggregateInventoryEquality();
		Controller_ReleasesGlobalGateIfEquipmentInvalidatesWhileWaiting();
		Controller_SettlesCustodyBeforeBestEffortInventoryRefresh();
		Controller_ReturnsToMainThreadBeforeExceptionalCleanup();
		Controller_PreservesReconciliationFlagsOnExceptionalOutcomes();
		InteractionCommand_DelegatesToValidatedHostEntryPoints();
		InteractionCommand_ReportsCompletedTransactionResults();
		RenderController_ConsumesReplicatedStateAndOnlyTogglesRenderers();
		CoreContracts_AreAddonNeutral();
		DroppedEquipment_ConsultsIdentityPolicyBeforeAmmoMerge();
		DroppedEquipment_CopiesAuthoredAttachmentPresentationBeforeSpawn();
		ShootWeapon_UsesHostPresentationForProviderBearingWeapons();
		ShootWeapon_ConsumesOnePresentationByteWithNormalFallback();
		AttachmentSources_DoNotOwnWeaponStats();

		if ( _failed != 0 )
		{
			Console.Error.WriteLine(
				$"{{\"contract\":\"lp-weapon-attachment-runtime\",\"status\":\"FAIL\",\"proofCeiling\":\"pure-contract-and-source-shape\",\"failedAssertions\":{_failed},\"exitCode\":1}}" );
			return 1;
		}

		Console.WriteLine(
			"{\"contract\":\"lp-weapon-attachment-runtime\",\"status\":\"PASS\",\"proofCeiling\":\"pure-contract-and-source-shape\",\"failedAssertions\":0,\"exitCode\":0}" );
		return 0;
	}

	private static void AttachmentTransactions_BlockDropsWithoutChangingSnapshotEligibility()
	{
		var controllerSource = ReadSource( "WeaponAttachmentController.source.cs" );
		var equipmentSource = ReadSource( "Equipment.source.cs" );
		var playerEquipmentSource = ReadSource( "PlayerEquipment.source.cs" );
		var playerSource = ReadSource( "Player.source.cs" );

		var baselineValid = AttachmentDropLockSourceContractValid(
			controllerSource,
			equipmentSource,
			playerEquipmentSource,
			playerSource );
		Expect(
			"attachment transactions block runtime drops without changing authored snapshot eligibility",
			baselineValid );

		var authoredEligibilityMutation = playerSource.Replace(
			"if ( !equipment.CanDrop )",
			"if ( !equipment.CanDropNow )",
			StringComparison.Ordinal );
		Expect(
			"snapshot-overlap mutation probe rejects transient drop state as persistence eligibility",
			!baselineValid
			|| (authoredEligibilityMutation != playerSource
			&& !AttachmentDropLockSourceContractValid(
				controllerSource,
				equipmentSource,
				playerEquipmentSource,
				authoredEligibilityMutation )) );

		var unlockedPredicateMutation = equipmentSource.Replace(
			"public bool CanDropNow => CanDrop && !IsDropLocked;",
			"public bool CanDropNow => CanDrop;",
			StringComparison.Ordinal );
		Expect(
			"drop-lock mutation probe rejects a predicate that ignores the transient lock",
			!baselineValid
			|| (unlockedPredicateMutation != equipmentSource
			&& !AttachmentDropLockSourceContractValid(
				controllerSource,
				unlockedPredicateMutation,
				playerEquipmentSource,
				playerSource )) );

		var missingCleanupMutation = ReplaceFirstOrdinal(
			controllerSource,
			"Equipment.IsDropLocked = false;",
			"Equipment.IsDropLocked = true;" );
		Expect(
			"drop-lock mutation probe rejects missing transaction cleanup",
			!baselineValid
			|| (missingCleanupMutation != controllerSource
			&& !AttachmentDropLockSourceContractValid(
				missingCleanupMutation,
				equipmentSource,
				playerEquipmentSource,
				playerSource )) );

		var forceRemoveMutation = playerEquipmentSource.Replace(
			"if ( weapon.CanDropNow || forceRemove )",
			"if ( weapon.CanDropNow )",
			StringComparison.Ordinal );
		Expect(
			"drop-lock mutation probe preserves the explicit force-remove bypass",
			!baselineValid
			|| (forceRemoveMutation != playerEquipmentSource
			&& !AttachmentDropLockSourceContractValid(
				controllerSource,
				equipmentSource,
				forceRemoveMutation,
				playerSource )) );
	}

	private static void Compatibility_AllowsOnlyRuledWeaponAttachmentPairs()
	{
		Expect( "Glock accepts pistol suppressor",
			WeaponAttachmentCompatibility.CanAttach( Glock, WeaponAttachmentKind.PistolSuppressor ) );
		Expect( "Glock accepts red dot",
			WeaponAttachmentCompatibility.CanAttach( Glock, WeaponAttachmentKind.RedDot ) );
		Expect( "Glock accepts laser",
			WeaponAttachmentCompatibility.CanAttach( Glock, WeaponAttachmentKind.Laser ) );
		Expect( "Glock rejects PBS suppressor",
			!WeaponAttachmentCompatibility.CanAttach( Glock, WeaponAttachmentKind.PbsSuppressor ) );

		Expect( "Original AKS-74U accepts PBS suppressor",
			WeaponAttachmentCompatibility.CanAttach( Aks74uOriginal, WeaponAttachmentKind.PbsSuppressor ) );
		Expect( "AK-47 accepts PBS suppressor",
			WeaponAttachmentCompatibility.CanAttach( Ak47, WeaponAttachmentKind.PbsSuppressor ) );
		Expect( "Military AKS-74U rejects PBS suppressor",
			!WeaponAttachmentCompatibility.CanAttach( Aks74uMilitary, WeaponAttachmentKind.PbsSuppressor ) );
	}

	private static void Compatibility_BindsControllerIdentityToTheEquipmentPrefab()
	{
		var method = typeof( WeaponAttachmentCompatibility ).GetMethod(
			"MatchesEquipmentPrefab",
			BindingFlags.Public | BindingFlags.Static );
		var controllerSource = ReadSource( "WeaponAttachmentController.source.cs" );
		var controllerBindsIdentity = ControllerEquipmentIdentityBindingSourceContractValid(
			controllerSource );

		var exactRuledMap = method is not null
			&& InvokeEquipmentPrefabMatch( method, Glock, GlockWorldPrefab )
			&& InvokeEquipmentPrefabMatch( method, Ak47, Ak47WorldPrefab )
			&& InvokeEquipmentPrefabMatch( method, Aks74uOriginal, Aks74uOriginalWorldPrefab )
			&& !InvokeEquipmentPrefabMatch( method, Glock, Ak47WorldPrefab )
			&& !InvokeEquipmentPrefabMatch( method, Ak47, Aks74uOriginalWorldPrefab )
			&& !InvokeEquipmentPrefabMatch(
				method,
				Aks74uMilitary,
				"addons/lifepunch/lpweapons/aks74u/equipment/w_aks74u/w_aks74u.prefab" );

		Expect( "controller weapon identity is bound to the exact ruled equipment prefab",
			exactRuledMap && controllerBindsIdentity );
		Expect( "controller identity mutation probe rejects a bypassed equipment-prefab binding",
			!ControllerEquipmentIdentityBindingSourceContractValid( controllerSource.Replace(
				"WeaponAttachmentCompatibility.MatchesEquipmentPrefab( WeaponId, Equipment.Resource.PrefabPath() )",
				"true",
				StringComparison.Ordinal ) ) );
	}

	private static void Slots_RejectASecondAttachmentWithoutReplacingTheFirst()
	{
		Expect( "pistol suppressor occupies muzzle slot",
			WeaponAttachmentSlots.GetSlot( WeaponAttachmentKind.PistolSuppressor ) == WeaponAttachmentSlot.Muzzle );
		Expect( "PBS suppressor occupies muzzle slot",
			WeaponAttachmentSlots.GetSlot( WeaponAttachmentKind.PbsSuppressor ) == WeaponAttachmentSlot.Muzzle );
		Expect( "red dot occupies optic slot",
			WeaponAttachmentSlots.GetSlot( WeaponAttachmentKind.RedDot ) == WeaponAttachmentSlot.Optic );
		Expect( "laser occupies rail slot",
			WeaponAttachmentSlots.GetSlot( WeaponAttachmentKind.Laser ) == WeaponAttachmentSlot.Rail );

		var empty = WeaponAttachmentLoadout.Empty;
		Expect( "first muzzle attachment is accepted",
			empty.TryAttach( WeaponAttachmentKind.PistolSuppressor, PistolSuppressorItem, out var suppressed ) );
		Expect( "second muzzle attachment is rejected",
			!suppressed.TryAttach( WeaponAttachmentKind.PbsSuppressor, PbsSuppressorItem, out var unchanged ) );
		Expect( "rejected second muzzle does not replace first", unchanged == suppressed );

		Expect( "optic remains independently attachable",
			suppressed.TryAttach( WeaponAttachmentKind.RedDot, RedDotItem, out var withOptic ) );
		Expect( "rail remains independently attachable",
			withOptic.TryAttach( WeaponAttachmentKind.Laser, LaserItem, out var withLaser ) );
		Expect( "laser cannot be enabled before its rail item exists",
			!empty.TrySetLaserEnabled( true, out _ ) );
		Expect( "laser can be enabled after attachment",
			withLaser.TrySetLaserEnabled( true, out var laserEnabled ) && laserEnabled.LaserEnabled );
	}

	private static void Catalog_BindsKindsToAccessoryGrantIdentifiers()
	{
		Expect( "pistol suppressor has one canonical grant",
			WeaponAttachmentCatalog.GetGrantIdentifier( WeaponAttachmentKind.PistolSuppressor )
			== "lifepunch.weapon_attachment.pistol_suppressor" );
		Expect( "PBS suppressor has one canonical grant",
			WeaponAttachmentCatalog.GetGrantIdentifier( WeaponAttachmentKind.PbsSuppressor )
			== "lifepunch.weapon_attachment.pbs_suppressor" );
		Expect( "red dot has one canonical grant",
			WeaponAttachmentCatalog.GetGrantIdentifier( WeaponAttachmentKind.RedDot )
			== "lifepunch.weapon_attachment.red_dot" );
		Expect( "laser has one canonical grant",
			WeaponAttachmentCatalog.GetGrantIdentifier( WeaponAttachmentKind.Laser )
			== "lifepunch.weapon_attachment.laser" );
		Expect( "canonical grant matching is case insensitive",
			WeaponAttachmentCatalog.MatchesGrantIdentifier(
				WeaponAttachmentKind.RedDot,
				"LIFEPUNCH.WEAPON_ATTACHMENT.RED_DOT" ) );
		Expect( "mismatched item grant cannot impersonate an attachment kind",
			!WeaponAttachmentCatalog.MatchesGrantIdentifier(
				WeaponAttachmentKind.PistolSuppressor,
				"lifepunch.weapon_attachment.laser" ) );
	}

	private static void Transactions_AccountForInventoryAndCompensateFailures()
	{
		Expect( "transaction result is an immutable value",
			IsImmutableValueType( typeof( WeaponAttachmentTransactionResult ) ) );

		var inventory = new FakeInventory( PistolSuppressorItem, 1 );
		var state = new FakeState( WeaponAttachmentLoadout.Empty );
		var attached = WeaponAttachmentTransactionController.Attach(
			Glock,
			WeaponAttachmentKind.PistolSuppressor,
			PistolSuppressorItem,
			inventory,
			state );

		Expect( "successful attach reports success without compensation", attached.Succeeded && !attached.CompensationRequired );
		Expect( "successful attach takes exactly one inventory item",
			inventory.TakeCalls == 1 && inventory.GiveCalls == 0 && inventory.Quantity == 0 );
		Expect( "successful attach commits the exact item identity",
			state.Current.Muzzle.ItemId == PistolSuppressorItem
			&& state.Current.Muzzle.Kind == WeaponAttachmentKind.PistolSuppressor );

		var incompatibleInventory = new FakeInventory( PbsSuppressorItem, 1 );
		var incompatibleState = new FakeState( WeaponAttachmentLoadout.Empty );
		var incompatible = WeaponAttachmentTransactionController.Attach(
			Aks74uMilitary,
			WeaponAttachmentKind.PbsSuppressor,
			PbsSuppressorItem,
			incompatibleInventory,
			incompatibleState );
		Expect( "incompatible attach fails before inventory mutation",
			!incompatible.Succeeded
			&& incompatible.Failure == WeaponAttachmentTransactionFailure.Incompatible
			&& incompatibleInventory.TakeCalls == 0
			&& incompatibleInventory.GiveCalls == 0 );

		var occupiedInventory = new FakeInventory( PbsSuppressorItem, 1 );
		var occupiedState = new FakeState( state.Current );
		var occupied = WeaponAttachmentTransactionController.Attach(
			Glock,
			WeaponAttachmentKind.PbsSuppressor,
			PbsSuppressorItem,
			occupiedInventory,
			occupiedState );
		Expect( "occupied slot fails before inventory mutation",
			!occupied.Succeeded
			&& occupied.Failure == WeaponAttachmentTransactionFailure.SlotOccupied
			&& occupiedInventory.TakeCalls == 0 );

		var missingInventory = new FakeInventory( RedDotItem, 0 );
		var missingInventoryState = new FakeState( WeaponAttachmentLoadout.Empty );
		var missing = WeaponAttachmentTransactionController.Attach(
			Glock,
			WeaponAttachmentKind.RedDot,
			RedDotItem,
			missingInventory,
			missingInventoryState );
		Expect( "missing inventory item fails without state mutation",
			!missing.Succeeded
			&& missing.Failure == WeaponAttachmentTransactionFailure.InventoryTakeFailed
			&& missingInventory.TakeCalls == 1
			&& missingInventory.GiveCalls == 0
			&& missingInventoryState.Current == WeaponAttachmentLoadout.Empty );

		var rejectedInventory = new FakeInventory( RedDotItem, 1 );
		var rejectedState = new FakeState( WeaponAttachmentLoadout.Empty, false );
		var rejected = WeaponAttachmentTransactionController.Attach(
			Glock,
			WeaponAttachmentKind.RedDot,
			RedDotItem,
			rejectedInventory,
			rejectedState );
		Expect( "failed state commit refunds the taken item",
			!rejected.Succeeded
			&& rejected.Failure == WeaponAttachmentTransactionFailure.StateCommitFailed
			&& !rejected.CompensationRequired
			&& rejectedInventory.TakeCalls == 1
			&& rejectedInventory.GiveCalls == 1
			&& rejectedInventory.Quantity == 1 );

		var lostRefundInventory = new FakeInventory( RedDotItem, 1 ) { AllowGive = false };
		var lostRefundState = new FakeState( WeaponAttachmentLoadout.Empty, false );
		var lostRefund = WeaponAttachmentTransactionController.Attach(
			Glock,
			WeaponAttachmentKind.RedDot,
			RedDotItem,
			lostRefundInventory,
			lostRefundState );
		Expect( "failed refund is surfaced as compensation required",
			!lostRefund.Succeeded
			&& lostRefund.CompensationRequired
			&& lostRefund.Failure == WeaponAttachmentTransactionFailure.CompensationFailed );

		var detachInventory = new FakeInventory( PistolSuppressorItem, 0 );
		var detachState = new FakeState( state.Current );
		var detached = WeaponAttachmentTransactionController.Detach(
			WeaponAttachmentSlot.Muzzle,
			detachInventory,
			detachState );
		Expect( "successful detach gives exactly one inventory item",
			detached.Succeeded
			&& detachInventory.TakeCalls == 0
			&& detachInventory.GiveCalls == 1
			&& detachInventory.Quantity == 1 );
		Expect( "successful detach clears only the selected slot",
			detachState.Current.Muzzle.ItemId == Guid.Empty );

		var failedGiveInventory = new FakeInventory( PistolSuppressorItem, 0 ) { AllowGive = false };
		var failedGiveState = new FakeState( state.Current, true, true );
		var failedGive = WeaponAttachmentTransactionController.Detach(
			WeaponAttachmentSlot.Muzzle,
			failedGiveInventory,
			failedGiveState );
		Expect( "failed detach give restores the attached state",
			!failedGive.Succeeded
			&& failedGive.Failure == WeaponAttachmentTransactionFailure.InventoryGiveFailed
			&& !failedGive.CompensationRequired
			&& failedGiveState.Current == state.Current );

		var failedRestoreInventory = new FakeInventory( PistolSuppressorItem, 0 ) { AllowGive = false };
		var failedRestoreState = new FakeState( state.Current, true, false );
		var failedRestore = WeaponAttachmentTransactionController.Detach(
			WeaponAttachmentSlot.Muzzle,
			failedRestoreInventory,
			failedRestoreState );
		Expect( "failed detach restore is surfaced as compensation required",
			!failedRestore.Succeeded
			&& failedRestore.CompensationRequired
			&& failedRestore.Failure == WeaponAttachmentTransactionFailure.CompensationFailed );
	}

	private static void InventoryApiAcknowledgements_OwnCustodyClassification()
	{
		var controllerSource = ReadSource( "WeaponAttachmentController.source.cs" );
		var contractSource = ReadSource( "WeaponAttachmentContract.source.cs" );
		var baselineValid = InventoryApiAcknowledgementContractValid(
			controllerSource,
			contractSource );
		Expect( "attachment custody follows operation acknowledgements, never aggregate count inference",
			baselineValid );

		if ( !baselineValid )
		{
			return;
		}

		var ordinaryTakeFailureMutation = controllerSource.Replace(
			"WeaponAttachmentTransactionFailure.CompensationFailed,\n\t\t\t\t\t\ttrue );",
			"WeaponAttachmentTransactionFailure.InventoryTakeFailed );",
			StringComparison.Ordinal );
		Expect( "acknowledgement mutation probe rejects retry-safe classification of an unknown take",
			ordinaryTakeFailureMutation != controllerSource
			&& !InventoryApiAcknowledgementContractValid(
				ordinaryTakeFailureMutation,
				contractSource ) );

		var aggregateDetachMutation = controllerSource.Replace(
			"if ( givenItem is not null )",
			"if ( giveVerification.Matches )",
			StringComparison.Ordinal );
		Expect( "acknowledgement mutation probe rejects aggregate detach verification",
			aggregateDetachMutation != controllerSource
			&& !InventoryApiAcknowledgementContractValid(
				aggregateDetachMutation,
				contractSource ) );
	}

	private static void Drops_RefuseDuplicateMergeWhenAnItemIdentityWouldBeLost()
	{
		Expect( "empty attachment state permits ordinary duplicate ammo merge",
			WeaponAttachmentDropPolicy.CanMergeDuplicate( WeaponAttachmentLoadout.Empty ) );

		var empty = WeaponAttachmentLoadout.Empty;
		Expect( "identity fixture attaches red dot",
			empty.TryAttach( WeaponAttachmentKind.RedDot, RedDotItem, out var identityBearing ) );
		Expect( "identity-bearing dropped weapon refuses duplicate merge",
			!WeaponAttachmentDropPolicy.CanMergeDuplicate( identityBearing ) );
	}

	private static void Presentation_IsResolvedAtShotTimeWithoutBallisticCoupling()
	{
		var ordinary = WeaponAttachmentPresentation.ResolveFirePresentation( WeaponAttachmentLoadout.Empty );
		Expect( "ordinary weapon preserves presentation fallback byte zero", ordinary == (byte)0 );

		var empty = WeaponAttachmentLoadout.Empty;
		Expect( "suppressed presentation fixture attaches",
			empty.TryAttach( WeaponAttachmentKind.PistolSuppressor, PistolSuppressorItem, out var suppressed ) );
		var suppressedPresentation = (WeaponFirePresentation)WeaponAttachmentPresentation.ResolveFirePresentation( suppressed );
		Expect( "suppressor requests suppressed sound at shot time",
			suppressedPresentation.HasFlag( WeaponFirePresentation.UseSuppressedSound ) );
		Expect( "suppressor suppresses muzzle flash at shot time",
			suppressedPresentation.HasFlag( WeaponFirePresentation.SuppressMuzzleFlash ) );

		Expect( "non-suppressor fixture attaches red dot",
			empty.TryAttach( WeaponAttachmentKind.RedDot, RedDotItem, out var opticOnly ) );
		Expect( "red dot does not alter fire presentation",
			WeaponAttachmentPresentation.ResolveFirePresentation( opticOnly ) == (byte)0 );

		var method = typeof( IWeaponFirePresentationProvider ).GetMethod(
			"GetFirePresentation",
			BindingFlags.Public | BindingFlags.Instance );
		Expect( "presentation provider exposes exactly one shot-time byte",
			method is not null
			&& method.ReturnType == typeof( byte )
			&& method.GetParameters().Length == 0 );
	}

	private static void PresentationRules_CoverSuppressorDetachAndFallbackMatrix()
	{
		var rulesType = typeof( WeaponFirePresentation ).Assembly.GetType(
			"Dxura.RP.Game.WeaponFirePresentationRules" );
		var suppressFlash = rulesType?.GetMethod(
			"ShouldSuppressMuzzleFlash",
			[typeof( byte )] );
		var useSuppressedSound = rulesType?.GetMethod(
			"ShouldUseSuppressedSound",
			[typeof( byte ), typeof( bool )] );

		if ( suppressFlash is null
		     || suppressFlash.ReturnType != typeof( bool )
		     || !suppressFlash.IsStatic
		     || useSuppressedSound is null
		     || useSuppressedSound.ReturnType != typeof( bool )
		     || !useSuppressedSound.IsStatic )
		{
			Expect( "core presentation rules expose the suppressor decision matrix", false );
			return;
		}

		bool SuppressesFlash( byte presentation ) =>
			(bool)suppressFlash.Invoke( null, [presentation] )!;
		bool UsesSuppressedSound( byte presentation, bool suppressedSoundAvailable ) =>
			(bool)useSuppressedSound.Invoke( null, [presentation, suppressedSoundAvailable] )!;

		var empty = WeaponAttachmentLoadout.Empty;
		var ordinary = WeaponAttachmentPresentation.ResolveFirePresentation( empty );
		Expect( "detached muzzle keeps normal flash",
			!SuppressesFlash( ordinary ) );
		Expect( "detached muzzle keeps normal sound",
			!UsesSuppressedSound( ordinary, true ) );

		Expect( "pistol suppressor matrix fixture attaches",
			empty.TryAttach( WeaponAttachmentKind.PistolSuppressor, PistolSuppressorItem, out var pistol ) );
		var pistolPresentation = WeaponAttachmentPresentation.ResolveFirePresentation( pistol );
		Expect( "pistol suppressor hides muzzle flash",
			SuppressesFlash( pistolPresentation ) );
		Expect( "pistol suppressor selects available suppressed sound",
			UsesSuppressedSound( pistolPresentation, true ) );
		Expect( "missing suppressed event falls back to normal sound",
			!UsesSuppressedSound( pistolPresentation, false ) );
		Expect( "pistol suppressor detaches from the muzzle slot",
			pistol.TryDetach( WeaponAttachmentSlot.Muzzle, out _, out var pistolDetached ) );
		var pistolDetachedPresentation = WeaponAttachmentPresentation.ResolveFirePresentation( pistolDetached );
		Expect( "pistol suppressor detach restores normal flash and sound",
			!SuppressesFlash( pistolDetachedPresentation )
			&& !UsesSuppressedSound( pistolDetachedPresentation, true ) );

		Expect( "PBS suppressor matrix fixture attaches",
			empty.TryAttach( WeaponAttachmentKind.PbsSuppressor, PbsSuppressorItem, out var pbs ) );
		var pbsPresentation = WeaponAttachmentPresentation.ResolveFirePresentation( pbs );
		Expect( "PBS suppressor hides muzzle flash and selects suppressed sound",
			SuppressesFlash( pbsPresentation )
			&& UsesSuppressedSound( pbsPresentation, true ) );
		Expect( "PBS suppressor detaches from the muzzle slot",
			pbs.TryDetach( WeaponAttachmentSlot.Muzzle, out _, out var pbsDetached ) );
		var pbsDetachedPresentation = WeaponAttachmentPresentation.ResolveFirePresentation( pbsDetached );
		Expect( "PBS suppressor detach restores normal flash and sound",
			!SuppressesFlash( pbsDetachedPresentation )
			&& !UsesSuppressedSound( pbsDetachedPresentation, true ) );

		Expect( "optic-only matrix fixture attaches",
			empty.TryAttach( WeaponAttachmentKind.RedDot, RedDotItem, out var opticOnly ) );
		var opticPresentation = WeaponAttachmentPresentation.ResolveFirePresentation( opticOnly );
		Expect( "optic-only loadout keeps normal flash and sound",
			!SuppressesFlash( opticPresentation )
			&& !UsesSuppressedSound( opticPresentation, true ) );
	}

	private static void LaserVisibility_IsInvariantBetweenFirstAndThirdPerson()
	{
		var empty = WeaponAttachmentLoadout.Empty;
		Expect( "laser fixture attaches",
			empty.TryAttach( WeaponAttachmentKind.Laser, LaserItem, out var attached ) );
		Expect( "laser fixture enables",
			attached.TrySetLaserEnabled( true, out var enabled ) );

		var firstPerson = WeaponAttachmentPresentation.IsLaserVisible(
			enabled,
			WeaponAttachmentPerspective.FirstPerson );
		var thirdPerson = WeaponAttachmentPresentation.IsLaserVisible(
			enabled,
			WeaponAttachmentPerspective.ThirdPerson );
		Expect( "enabled laser is visible in first person", firstPerson );
		Expect( "enabled laser is visible in third person", thirdPerson );
		Expect( "laser visibility is perspective invariant", firstPerson == thirdPerson );
		Expect( "laser remains visual-only at shot time",
			WeaponAttachmentPresentation.ResolveFirePresentation( enabled ) == (byte)0 );
	}

	private static void AttachmentRendererVisibility_FollowsReplicatedLoadout()
	{
		var empty = WeaponAttachmentLoadout.Empty;
		Expect( "empty loadout hides the PBS renderer",
			!WeaponAttachmentPresentation.IsAttachmentRendererVisible(
				empty,
				WeaponAttachmentKind.PbsSuppressor ) );

		Expect( "PBS renderer fixture attaches",
			empty.TryAttach( WeaponAttachmentKind.PbsSuppressor, PbsSuppressorItem, out var pbs ) );
		Expect( "attached PBS renderer is visible",
			WeaponAttachmentPresentation.IsAttachmentRendererVisible(
				pbs,
				WeaponAttachmentKind.PbsSuppressor ) );
		Expect( "PBS state does not reveal the pistol suppressor renderer",
			!WeaponAttachmentPresentation.IsAttachmentRendererVisible(
				pbs,
				WeaponAttachmentKind.PistolSuppressor ) );

		Expect( "red-dot renderer fixture attaches",
			pbs.TryAttach( WeaponAttachmentKind.RedDot, RedDotItem, out var withOptic ) );
		Expect( "attached red-dot renderer is visible",
			WeaponAttachmentPresentation.IsAttachmentRendererVisible(
				withOptic,
				WeaponAttachmentKind.RedDot ) );

		Expect( "laser renderer fixture attaches",
			withOptic.TryAttach( WeaponAttachmentKind.Laser, LaserItem, out var withLaser ) );
		Expect( "attached laser body is visible while its beam is disabled",
			WeaponAttachmentPresentation.IsAttachmentRendererVisible(
				withLaser,
				WeaponAttachmentKind.Laser )
			&& !WeaponAttachmentPresentation.IsLaserVisible(
				withLaser,
				WeaponAttachmentPerspective.FirstPerson ) );
	}

	private static void SnapshotCodec_RoundTripsExactAttachmentCustody()
	{
		var codecType = typeof( WeaponAttachmentLoadout ).Assembly.GetType(
			"LifePunch.DXRP.Addons.Weapons.Attachments.WeaponAttachmentSnapshotCodec" );
		var glockFixture = new WeaponAttachmentLoadout(
			new WeaponAttachmentIdentity( WeaponAttachmentKind.PistolSuppressor, PistolSuppressorItem ),
			new WeaponAttachmentIdentity( WeaponAttachmentKind.RedDot, RedDotItem ),
			new WeaponAttachmentIdentity( WeaponAttachmentKind.Laser, LaserItem ),
			true );
		var pbsFixture = new WeaponAttachmentLoadout(
			new WeaponAttachmentIdentity( WeaponAttachmentKind.PbsSuppressor, PbsSuppressorItem ),
			WeaponAttachmentIdentity.Empty,
			WeaponAttachmentIdentity.Empty,
			false );

		var glockRoundTrip = TrySnapshotRoundTrip( codecType, glockFixture, out var restoredGlock );
		var pbsRoundTrip = TrySnapshotRoundTrip( codecType, pbsFixture, out var restoredPbs );
		Expect( "snapshot codec preserves exact ruled Glock and PBS custody",
			glockRoundTrip
			&& restoredGlock == glockFixture
			&& pbsRoundTrip
			&& restoredPbs == pbsFixture );
		Expect( "snapshot codec rejects malformed or incoherent custody",
			codecType is not null
			&& !TrySnapshotDeserialize( codecType, "1|2|00000000000000000000000000000000|0|00000000000000000000000000000000|0|00000000000000000000000000000000|0", out _ )
			&& !TrySnapshotDeserialize( codecType, "not-a-snapshot", out _ ) );
		Expect( "snapshot restore compatibility rejects cross-weapon loadouts",
			WeaponAttachmentCompatibility.CanRestoreLoadout( Glock, glockFixture )
			&& WeaponAttachmentCompatibility.CanRestoreLoadout( Ak47, pbsFixture )
			&& WeaponAttachmentCompatibility.CanRestoreLoadout( Aks74uOriginal, pbsFixture )
			&& !WeaponAttachmentCompatibility.CanRestoreLoadout( Glock, pbsFixture )
			&& !WeaponAttachmentCompatibility.CanRestoreLoadout( Ak47, glockFixture ) );
	}

	private static void State_IsHostAuthoritativeAndDropPersistent()
	{
		var source = ReadSource( "WeaponAttachmentState.source.cs" );
		Expect( "WeaponAttachmentState uses one host-owned atomic synchronized loadout",
			StateSourceContractValid( source ) );
		var replacementValidationValid = StateReplacementValidationSourceContractValid( source );
		Expect( "state rejects an incoherent replacement before atomic publication",
			replacementValidationValid );
		if ( replacementValidationValid )
		{
			var validationBypassMutation = source.Replace(
				"WeaponAttachmentSnapshotCodec.IsValid( replacement )",
				"true",
				StringComparison.Ordinal );
			Expect( "state mutation probe rejects bypassing replacement validation",
				validationBypassMutation != source
				&& !StateReplacementValidationSourceContractValid( validationBypassMutation ) );
		}
		Expect( "state mutation probe rejects loss of FromHost authority",
			!StateSourceContractValid( source.Replace( "SyncFlags.FromHost", "SyncFlags.None", StringComparison.Ordinal ) ) );
		Expect( "state mutation probe rejects loss of dropped-state persistence",
			!StateSourceContractValid( source.Replace(
				"IDroppedWeaponState<WeaponAttachmentState>",
				"IDisposable",
				StringComparison.Ordinal ) ) );
		Expect( "state mutation probe rejects loss of the atomic loadout",
			!StateSourceContractValid( source.Replace(
				"ReplicatedLoadout",
				"RemovedAtomicLoadout",
				StringComparison.Ordinal ) ) );
		Expect( "replicated muzzle state drives the authoritative fire-presentation byte",
			StateFirePresentationSourceContractValid( source ) );
		Expect( "state mutation probe rejects bypassing the replicated muzzle presentation",
			!StateFirePresentationSourceContractValid( source.Replace(
				"return WeaponAttachmentPresentation.ResolveFirePresentation( Current );",
				"return (byte)WeaponFirePresentation.None;",
				StringComparison.Ordinal ) ) );
	}

	private static void SnapshotIntegration_PreservesStateWithoutInventoryMutation()
	{
		var playerSource = ReadSource( "Player.source.cs" );
		var snapshotDataSource = ReadSource( "PlayerSnapshotData.source.cs" );
		var stateSource = ReadSource( "WeaponAttachmentState.source.cs" );
		var interfaceSource = ReadSourceOrEmpty( "IEquipmentSnapshotState.source.cs" );

		var baselineValid = SnapshotIntegrationSourceContractValid(
			playerSource,
			snapshotDataSource,
			stateSource,
			interfaceSource );
		Expect( "equipment snapshots capture and restore addon state after prefab creation",
			baselineValid );
		var removedRestore = playerSource.Replace(
			"TryRestoreSnapshotState",
			"RemovedRestore",
			StringComparison.Ordinal );
		Expect( "snapshot mutation probe rejects removed restore application",
			baselineValid
			&& Count( playerSource, "TryRestoreSnapshotState" ) == 1
			&& !SnapshotIntegrationSourceContractValid(
				removedRestore,
				snapshotDataSource,
				stateSource,
				interfaceSource ) );
		var economyMutation = playerSource.Replace(
			"SpawnHost( false",
			"ServerApiClient.GivePlayerItem( 0, default );\n\t\tSpawnHost( false",
			StringComparison.Ordinal );
		Expect( "snapshot mutation probe rejects inventory economy side effects",
			baselineValid
			&& Count( playerSource, "SpawnHost( false" ) == 1
			&& economyMutation != playerSource
			&& !SnapshotIntegrationSourceContractValid(
				economyMutation,
				snapshotDataSource,
				stateSource,
				interfaceSource ) );
		var compatibilityMutation = stateSource.Replace(
			"WeaponAttachmentCompatibility.CanRestoreLoadout",
			"RemovedCompatibility.Check",
			StringComparison.Ordinal );
		Expect( "snapshot mutation probe rejects weapon-compatibility bypass",
			baselineValid
			&& Count( stateSource, "WeaponAttachmentCompatibility.CanRestoreLoadout" ) == 1
			&& !SnapshotIntegrationSourceContractValid(
				playerSource,
				snapshotDataSource,
				compatibilityMutation,
				interfaceSource ) );

		var identityBindingValid = SnapshotEquipmentIdentityBindingSourceContractValid( stateSource );
		var identityBindingMutation = stateSource.Replace(
			"WeaponAttachmentCompatibility.MatchesEquipmentPrefab( controller.WeaponId, equipment.Resource.PrefabPath() )",
			"true",
			StringComparison.Ordinal );
		Expect( "snapshot restore binds controller identity to the equipment prefab and rejects bypass mutation",
			identityBindingValid
			&& identityBindingMutation != stateSource
			&& !SnapshotEquipmentIdentityBindingSourceContractValid( identityBindingMutation ) );
	}

	private static void Controller_IsHostOwnedAndCleanupWhitelistSafe()
	{
		var source = ReadSource( "WeaponAttachmentController.source.cs" );
		Expect( "WeaponAttachmentController is a host transaction adapter",
			ControllerSourceContractValid( source ) );
		Expect( "controller cleanup avoids await-in-finally whitelist violations",
			ControllerCleanupWhitelistSafe( source ) );
		var outerFinallyAwaitMutation = source.Replace(
			"TransactionLock.Release();",
			"await GameTask.MainThread();\n\t\t\tTransactionLock.Release();",
			StringComparison.Ordinal );
		Expect( "controller cleanup mutation probe rejects await in the outer finally",
			outerFinallyAwaitMutation != source
			&& !ControllerCleanupWhitelistSafe( outerFinallyAwaitMutation ) );
		var authorityGateValid = ControllerAuthorityGateSourceContractValid( source );
		Expect( "controller mutation authority gate binds host, owner, deployed equipment, and synthetic exclusion",
			authorityGateValid );
		if ( authorityGateValid )
		{
			(string Name, string Token)[] authorityPredicates =
			[
				("host authority", "Networking.IsHost"),
				("inventory enablement", "InventoryTransactionsEnabled"),
				("weapon identity", "!string.IsNullOrWhiteSpace( WeaponId )"),
				("equipment validity", "Equipment.IsValid()"),
				("state validity", "AttachmentState.IsValid()"),
				("owner validity", "owner.IsValid()"),
				("alive owner", "!owner.IsDead"),
				("synthetic exclusion", "!SyntheticActorRegistry.IsSynthetic( owner.SteamId, owner.IsDebugPlayer )"),
				("equipment ownership", "Equipment.Owner == owner"),
				("deployed equipment", "Equipment.IsDeployed"),
				("current equipment identity", "owner.CurrentEquipment == Equipment")
			];

			foreach ( var (name, token) in authorityPredicates )
			{
				var mutation = source.Replace( token, "true", StringComparison.Ordinal );
				Expect( $"controller authority mutation probe rejects removed {name}",
					mutation != source
					&& !ControllerAuthorityGateSourceContractValid( mutation ) );
			}
		}

		Expect( "controller mutation probe rejects bypassing pure attach transaction",
			!ControllerSourceContractValid( source.Replace(
				"WeaponAttachmentTransactionController.Attach",
				"Bypass.Attach",
				StringComparison.Ordinal ) ) );
	}

	private static void Controller_SerializesInventoryMutationsAcrossWeaponInstances()
	{
		var source = ReadSource( "WeaponAttachmentController.source.cs" );
		var baselineValid = ControllerInventoryConcurrencyContractValid( source );
		Expect( "attachment inventory mutations share one gate and honor take outcomes",
			baselineValid );
		var instanceGateMutation = source.Replace(
			"private static readonly SemaphoreSlim TransactionLock",
			"private readonly SemaphoreSlim TransactionLock",
			StringComparison.Ordinal );
		Expect( "controller mutation probe rejects a per-weapon inventory gate",
			baselineValid
			&& Count( source, "private static readonly SemaphoreSlim TransactionLock" ) == 1
			&& !ControllerInventoryConcurrencyContractValid( instanceGateMutation ) );
		var takeBypassMutation = source.Replace(
			"if ( !takeSucceeded )",
			"if ( false )",
			StringComparison.Ordinal );
		Expect( "controller mutation probe rejects a bypassed take result",
			baselineValid
			&& Count( source, "if ( !takeSucceeded )" ) == 1
			&& !ControllerInventoryConcurrencyContractValid( takeBypassMutation ) );
	}

	private static void Controller_ReleasesGlobalGateIfEquipmentInvalidatesWhileWaiting()
	{
		var source = ReadSource( "WeaponAttachmentController.source.cs" );
		Expect( "every attachment action enters try before reading equipment under the global gate",
			GlobalGateReleaseContractValid( source ) );
	}

	private static void Controller_FailsClosedWhenTakeAcknowledgementIsMissing()
	{
		var source = ReadSource( "WeaponAttachmentController.source.cs" );
		Expect( "attach requires a positive take acknowledgement before committing custody",
			AttachTakeOutcomeCompletionContractValid( source ) );
	}

	private static void Controller_DoesNotRestoreDetachFromAggregateInventoryEquality()
	{
		var source = ReadSource( "WeaponAttachmentController.source.cs" );
		var baselineValid = DetachAmbiguityPreservesSingleCustodyContractValid( source );
		Expect( "detach keeps state detached when the give acknowledgement is unavailable",
			baselineValid );

		var unresolvedStart = source.IndexOf(
			"State remains detached to prevent duplicate ownership.",
			StringComparison.Ordinal );
		var retrySafeMutation = unresolvedStart < 0
			? source
			: string.Concat(
				source.AsSpan( 0, unresolvedStart ),
				ReplaceFirstOrdinal(
					source[unresolvedStart..],
					"\t\t\t\t\ttrue );",
					"\t\t\t\t\tfalse );" ) );
		Expect( "detach ambiguity mutation probe rejects a retry-safe unresolved outcome",
			baselineValid
			&& retrySafeMutation != source
			&& !DetachAmbiguityPreservesSingleCustodyContractValid( retrySafeMutation ) );
	}

	private static void Controller_SettlesCustodyBeforeBestEffortInventoryRefresh()
	{
		var source = ReadSource( "WeaponAttachmentController.source.cs" );
		var baselineValid = InventoryRefreshDoesNotOwnTransactionOutcomeContractValid( source );
		Expect( "inventory refresh is best-effort after attachment custody is settled",
			baselineValid );

		if ( !baselineValid )
		{
			return;
		}

		var directRefreshMutation = source.Replace(
			"BroadcastInventoryRefreshBestEffort( owner, itemId, expectedQuantityAfterTake );",
			"owner.BroadcastInventoryRefresh( itemId, expectedQuantityAfterTake );",
			StringComparison.Ordinal );
		Expect( "inventory refresh mutation probe rejects an uncontained success notification",
			directRefreshMutation != source
			&& !InventoryRefreshDoesNotOwnTransactionOutcomeContractValid( directRefreshMutation ) );

		var unsettledDetachMutation = source.Replace(
			"stateDetached = false;",
			"stateDetached = true;",
			StringComparison.Ordinal );
		Expect( "inventory refresh mutation probe rejects an unsettled detach success",
			unsettledDetachMutation != source
			&& !InventoryRefreshDoesNotOwnTransactionOutcomeContractValid( unsettledDetachMutation ) );

		var refreshOutsideTryMutation = source.Replace(
			"\t\ttry\n\t\t{\n\t\t\towner.BroadcastInventoryRefresh( itemId, quantity );",
			"\t\towner.BroadcastInventoryRefresh( itemId, quantity );\n\n\t\ttry\n\t\t{",
			StringComparison.Ordinal );
		Expect( "inventory refresh mutation probe rejects notification outside the helper try",
			refreshOutsideTryMutation != source
			&& !InventoryRefreshDoesNotOwnTransactionOutcomeContractValid( refreshOutsideTryMutation ) );
	}

	private static void Controller_ReturnsToMainThreadBeforeExceptionalCleanup()
	{
		var source = ReadSource( "WeaponAttachmentController.source.cs" );
		var baselineValid = ControllerExceptionalCleanupReturnsToMainThreadContractValid( source );
		Expect( "controller returns to the main thread before exceptional equipment cleanup",
			baselineValid );

		if ( !baselineValid )
		{
			return;
		}

		var cleanupThreadMutation = source.Replace(
			"catch ( Exception exception )\n\t\t\t{\n\t\t\t\tawait GameTask.MainThread();",
			"catch ( Exception exception )\n\t\t\t{",
			StringComparison.Ordinal );
		Expect( "exception cleanup mutation probe rejects a missing main-thread return",
			cleanupThreadMutation != source
			&& !ControllerExceptionalCleanupReturnsToMainThreadContractValid( cleanupThreadMutation ) );
	}

	private static void Controller_PreservesReconciliationFlagsOnExceptionalOutcomes()
	{
		var source = ReadSource( "WeaponAttachmentController.source.cs" );
		var baselineValid = ControllerExceptionalReconciliationFlagsContractValid( source );
		Expect( "controller exceptional outcomes preserve unresolved custody flags",
			baselineValid );

		if ( !baselineValid )
		{
			return;
		}

		var attachFlagMutation = source.Replace(
			"inventoryTaken );",
			"false );",
			StringComparison.Ordinal );
		Expect( "exception outcome mutation probe rejects a suppressed attach reconciliation flag",
			attachFlagMutation != source
			&& !ControllerExceptionalReconciliationFlagsContractValid( attachFlagMutation ) );

		var detachFlagMutation = source.Replace(
			"stateDetached );",
			"false );",
			StringComparison.Ordinal );
		Expect( "exception outcome mutation probe rejects a suppressed detach reconciliation flag",
			detachFlagMutation != source
			&& !ControllerExceptionalReconciliationFlagsContractValid( detachFlagMutation ) );
	}

	private static void Controller_QuarantinesQueuedRetriesAfterAmbiguousCustody()
	{
		var contractSource = ReadSource( "WeaponAttachmentContract.source.cs" );
		var controllerSource = ReadSource( "WeaponAttachmentController.source.cs" );
		var baselineValid = ReconciliationQuarantineBehaviorValid()
			&& ReconciliationQuarantineQueuedBehaviorValid()
			&& ControllerReconciliationQuarantineContractValid(
				contractSource,
				controllerSource );

		Expect(
			"ambiguous attachment custody quarantines queued and later player actions before mutation",
			baselineValid );

		if ( !baselineValid )
		{
			return;
		}

		var queuedGateMutation = ReplaceFirstOrdinal(
			controllerSource,
			"if ( IsReconciliationRequired( ownerId ) )",
			"if ( false )" );
		Expect(
			"reconciliation mutation probe rejects removal of the post-lock queued-action gate",
			queuedGateMutation != controllerSource
			&& !ControllerReconciliationQuarantineContractValid(
				contractSource,
				queuedGateMutation ) );

		var quarantineMutation = ReplaceFirstOrdinal(
			controllerSource,
			"RequireReconciliation( ownerId );",
			"_ = ownerId;" );
		Expect(
			"reconciliation mutation probe rejects an ambiguous outcome that does not quarantine",
			quarantineMutation != controllerSource
			&& !ControllerReconciliationQuarantineContractValid(
				contractSource,
				quarantineMutation ) );

		var ownerCaptureMutation = ReplaceFirstOrdinal(
			controllerSource,
			"var ownerId = owner.SteamId;",
			"const long ownerId = 76561198000000001;" );
		Expect(
			"reconciliation mutation probe rejects a quarantine key detached from the requester",
			ownerCaptureMutation != controllerSource
			&& !ControllerReconciliationQuarantineContractValid(
				contractSource,
				ownerCaptureMutation ) );
	}

	private static void InteractionCommand_DelegatesToValidatedHostEntryPoints()
	{
		var commandSource = ReadSourceOrEmpty( "WeaponAttachmentCommand.source.cs" );
		var controllerSource = ReadSource( "WeaponAttachmentController.source.cs" );
		var baselineValid = AttachmentInteractionSourceContractValid(
			commandSource,
			controllerSource );

		Expect( "attachment chat interaction source shape delegates every action to validated host entry points",
			baselineValid );

		if ( !baselineValid )
		{
			return;
		}

		var directInventoryMutation = commandSource + "\nServerApiClient.TakePlayerItem();";
		Expect( "interaction mutation probe rejects direct inventory ownership",
			!AttachmentInteractionSourceContractValid(
				directInventoryMutation,
				controllerSource ) );

		var directRpcMutation = commandSource.Replace(
			"TryAttachForHost",
			"AttachHost",
			StringComparison.Ordinal );
		Expect( "interaction mutation probe rejects bypassing the host adapter",
			directRpcMutation != commandSource
			&& !AttachmentInteractionSourceContractValid(
				directRpcMutation,
				controllerSource ) );

		var ownerGateMutation = controllerSource.Replace(
			"CanMutate( owner )",
			"BypassOwnerGate( owner )",
			StringComparison.Ordinal );
		Expect( "interaction mutation probe rejects owner revalidation removal",
			ownerGateMutation != controllerSource
			&& !AttachmentInteractionSourceContractValid(
				commandSource,
				ownerGateMutation ) );

		var rpcDelegationMutation = controllerSource.Replace(
			"TryDetachForHost( owner, slot );",
			"_ = DetachAsync( owner, slot );",
			StringComparison.Ordinal );
		Expect( "interaction mutation probe rejects RPC adapter bypass",
			rpcDelegationMutation != controllerSource
			&& !AttachmentInteractionSourceContractValid(
				commandSource,
				rpcDelegationMutation ) );
	}

	private static void InteractionCommand_ReportsCompletedTransactionResults()
	{
		var commandSource = ReadSourceOrEmpty( "WeaponAttachmentCommand.source.cs" );
		var controllerSource = ReadSource( "WeaponAttachmentController.source.cs" );
		var baselineValid = AttachmentCompletedResultSourceContractValid(
			commandSource,
			controllerSource );

		Expect( "attachment command reports the completed typed transaction result",
			baselineValid );

		if ( !baselineValid )
		{
			return;
		}

		var queueAdmissionMutation = controllerSource.Replace(
			"return AttachAsync( owner, kind, itemId );",
			"_ = AttachAsync( owner, kind, itemId ); return Task.FromResult( WeaponAttachmentTransactionResult.Success );",
			StringComparison.Ordinal );
		Expect( "completed-result mutation probe rejects queue admission as success",
			queueAdmissionMutation != controllerSource
			&& !AttachmentCompletedResultSourceContractValid(
				commandSource,
				queueAdmissionMutation ) );

		var discardedResultMutation = commandSource.Replace(
			"var result = await operation;",
			"await operation; var result = WeaponAttachmentTransactionResult.Success;",
			StringComparison.Ordinal );
		Expect( "completed-result mutation probe rejects a discarded operation result",
			discardedResultMutation != commandSource
			&& !AttachmentCompletedResultSourceContractValid(
				discardedResultMutation,
				controllerSource ) );

		var reconciliationMutation = commandSource.Replace(
			"result.CompensationRequired",
			"false",
			StringComparison.Ordinal );
		Expect( "completed-result mutation probe preserves reconciliation-required reporting",
			reconciliationMutation != commandSource
			&& !AttachmentCompletedResultSourceContractValid(
				reconciliationMutation,
				controllerSource ) );

		var retainedReturnMutation = controllerSource.Replace(
			"return AttachAsync( owner, kind, itemId );",
			"if ( owner.IsValid() ) { _ = AttachAsync( owner, kind, itemId ); " +
			"return Task.FromResult( WeaponAttachmentTransactionResult.Success ); } " +
			"return AttachAsync( owner, kind, itemId );",
			StringComparison.Ordinal );
		Expect( "completed-result mutation probe rejects queue admission even when a direct return remains",
			retainedReturnMutation != controllerSource
			&& !AttachmentCompletedResultSourceContractValid(
				commandSource,
				retainedReturnMutation ) );

		var resultOverwriteMutation = commandSource.Replace(
			"var result = await operation;",
			"var result = await operation; result = WeaponAttachmentTransactionResult.Success;",
			StringComparison.Ordinal );
		Expect( "completed-result mutation probe rejects an overwritten operation result",
			resultOverwriteMutation != commandSource
			&& !AttachmentCompletedResultSourceContractValid(
				resultOverwriteMutation,
				controllerSource ) );

		var optimisticNotificationMutation = commandSource.Replace(
			"var result = await operation;",
			"caller.Success( successMessage ); var result = await operation;",
			StringComparison.Ordinal );
		Expect( "completed-result mutation probe rejects notification before completion",
			optimisticNotificationMutation != commandSource
			&& !AttachmentCompletedResultSourceContractValid(
				optimisticNotificationMutation,
				controllerSource ) );

		var reconciliationPolarityMutation = commandSource.Replace(
			"if ( result.CompensationRequired )",
			"if ( !result.CompensationRequired )",
			StringComparison.Ordinal );
		Expect( "completed-result mutation probe rejects inverted reconciliation reporting",
			reconciliationPolarityMutation != commandSource
			&& !AttachmentCompletedResultSourceContractValid(
				reconciliationPolarityMutation,
				controllerSource ) );
	}

	private static void RenderController_ConsumesReplicatedStateAndOnlyTogglesRenderers()
	{
		var source = ReadSource( "WeaponAttachmentRenderController.source.cs" );
		Expect( "render controller resolves live FP and TP equipment state before mapping every renderer",
			RenderControllerSourceContractValid( source ) );
		Expect( "render controller mutation probe rejects a severed first-person equipment backlink",
			!RenderControllerSourceContractValid( source.Replace(
				"viewModel.Equipment",
				"default( Equipment )",
				StringComparison.Ordinal ) ) );
		Expect( "render controller mutation probe rejects a hard-coded laser perspective",
			!RenderControllerSourceContractValid( source.Replace(
				"IsLaserVisible( loadout, Perspective )",
				"IsLaserVisible( loadout, WeaponAttachmentPerspective.FirstPerson )",
				StringComparison.Ordinal ) ) );
		Expect( "render controller mutation probe rejects visibility-policy bypass",
			!RenderControllerSourceContractValid( source.Replace(
				"WeaponAttachmentPresentation.IsAttachmentRendererVisible",
				"Bypass.IsVisible",
				StringComparison.Ordinal ) ) );
		Expect( "render controller mutation probe rejects missing active-binding validation",
			!RenderControllerSourceContractValid( source.Replace(
				"HasRequiredRendererBindings( loadout )",
				"true",
				StringComparison.Ordinal ) ) );
		Expect( "render controller mutation probe rejects duplicate renderer bindings",
			!RenderControllerSourceContractValid( source.Replace(
				"AreRendererBindingsDistinct()",
				"true",
				StringComparison.Ordinal ) ) );
		Expect( "render controller mutation probe rejects missing third-person containment",
			!RenderControllerSourceContractValid( source.Replace(
				"AreAttachmentRenderersUnder( equipment.GameObject )",
				"true",
				StringComparison.Ordinal ) ) );
		Expect( "render controller rejects authored transform ownership",
			!RenderControllerSourceContractValid( source + "\nLocalPosition = Vector3.Zero;" ) );
	}

	private static void CoreContracts_AreAddonNeutral()
	{
		var source = ReadSource( "WeaponPresentationContracts.source.cs" );
		Expect( "shared presentation and drop contracts live in the DXRP core namespace",
			CoreContractSourceValid( source ) );
		Expect( "core contracts do not depend on the LifePunch addon namespace",
			!source.Contains( "LifePunch.", StringComparison.Ordinal ) );
	}

	private static void DroppedEquipment_ConsultsIdentityPolicyBeforeAmmoMerge()
	{
		var source = ReadSource( "DroppedEquipment.source.cs" );
		Expect( "duplicate pickup consults attachment identity policy before ammo merge",
			DroppedEquipmentSourceContractValid( source ) );
		Expect( "drop mutation probe rejects identity policy removal",
			!DroppedEquipmentSourceContractValid( source.Replace(
				"IDroppedWeaponMergePolicy",
				"RemovedMergePolicy",
				StringComparison.Ordinal ) ) );

		Expect( "drop lifecycle copies attachment state before dropped/picked-up observers and destruction",
			DroppedEquipmentLifecycleSourceContractValid( source ) );

		const string droppedEvent =
			"IEquipmentEvents.Post( x => x.OnEquipmentDropped( droppedWeapon, heldWeapon?.Owner ) );";
		const string copyToDropped = "state.CopyToDroppedWeapon( droppedWeapon );";
		var eventBeforeCopyMutation = source.Replace(
			droppedEvent,
			string.Empty,
			StringComparison.Ordinal );
		eventBeforeCopyMutation = eventBeforeCopyMutation.Replace(
			copyToDropped,
			$"{droppedEvent}\n\t\t\t\t{copyToDropped}",
			StringComparison.Ordinal );
		Expect( "drop lifecycle mutation probe rejects an observer before state copy",
			!DroppedEquipmentLifecycleSourceContractValid( eventBeforeCopyMutation ) );

		Expect( "drop lifecycle mutation probe rejects merge policy lookup on the existing weapon",
			!DroppedEquipmentLifecycleSourceContractValid( source.Replace(
				"var mergePolicies = Components.GetAll<IDroppedWeaponMergePolicy>();",
				"var mergePolicies = existingWeapon.Components.GetAll<IDroppedWeaponMergePolicy>();",
				StringComparison.Ordinal ) ) );
	}

	private static void DroppedEquipment_CopiesAuthoredAttachmentPresentationBeforeSpawn()
	{
		var coreSource = ReadSource( "WeaponPresentationContracts.source.cs" );
		var droppedSource = ReadSource( "DroppedEquipment.source.cs" );
		var renderSource = ReadSource( "WeaponAttachmentRenderController.source.cs" );
		const string providerLoopMarker =
			"foreach ( var presentationSource in heldWeapon.Components.GetAll<IDroppedWeaponPresentationSource>( FindMode.EverythingInSelfAndDescendants ) )";

		Expect( "drop creation copies authored attachment presentation after state and before observers/spawn",
			DroppedPresentationSourceContractValid( coreSource, droppedSource, renderSource ) );

		const string providerCall = "presentationSource.TryCopyPresentationTo( droppedWeapon )";
		const string stateCopy = "state.CopyToDroppedWeapon( droppedWeapon );";
		var providerLoop = SliceBraceBlock( droppedSource, providerLoopMarker );
		var providerBeforeStateMutation = droppedSource.Replace(
			providerLoop,
			string.Empty,
			StringComparison.Ordinal ).Replace(
			"foreach ( var state in heldWeapon.Components.GetAll<IDroppedWeaponState>() )",
			$"{providerLoop}\n\t\t\tforeach ( var state in heldWeapon.Components.GetAll<IDroppedWeaponState>() )",
			StringComparison.Ordinal );
		Expect( "dropped presentation mutation probe rejects presentation copy before state copy",
			!string.IsNullOrEmpty( providerLoop )
			&& providerBeforeStateMutation != droppedSource
			&& !DroppedPresentationSourceContractValid(
				coreSource,
				providerBeforeStateMutation,
				renderSource ) );

		var descendantsMutation = droppedSource.Replace(
			"heldWeapon.Components.GetAll<IDroppedWeaponPresentationSource>( FindMode.EverythingInSelfAndDescendants )",
			"heldWeapon.Components.GetAll<IDroppedWeaponPresentationSource>()",
			StringComparison.Ordinal );
		Expect( "dropped presentation mutation probe rejects root-only provider discovery",
			descendantsMutation != droppedSource
			&& !DroppedPresentationSourceContractValid(
				coreSource,
				descendantsMutation,
				renderSource ) );

		var duplicateProviderCallMutation = droppedSource.Replace(
			providerCall,
			$"{providerCall}\n\t\t\t\t{providerCall}",
			StringComparison.Ordinal );
		Expect( "dropped presentation mutation probe rejects duplicate provider invocation",
			duplicateProviderCallMutation != droppedSource
			&& !DroppedPresentationSourceContractValid(
				coreSource,
				duplicateProviderCallMutation,
				renderSource ) );

		var nestedProviderMutation = droppedSource.Replace(
			providerLoop,
			string.Empty,
			StringComparison.Ordinal ).Replace(
			stateCopy,
			$"{stateCopy}\n\t\t\t{providerLoop}",
			StringComparison.Ordinal );
		Expect( "dropped presentation mutation probe rejects provider invocation inside the state loop",
			!string.IsNullOrEmpty( providerLoop )
			&& nestedProviderMutation != droppedSource
			&& !DroppedPresentationSourceContractValid(
				coreSource,
				nestedProviderMutation,
				renderSource ) );

		var copyFromMutation = renderSource.Replace(
			"cloneRenderer.CopyFrom( sourceRenderer );",
			"cloneRenderer.Model = sourceRenderer.Model;",
			StringComparison.Ordinal );
		Expect( "dropped presentation mutation probe rejects partial renderer copying",
			copyFromMutation != renderSource
			&& !DroppedPresentationSourceContractValid(
				coreSource,
				droppedSource,
				copyFromMutation ) );

		var transformMutation = renderSource.Replace(
			"sourceBaseRenderer.WorldTransform.ToLocal( sourceRenderer.WorldTransform )",
			"sourceRenderer.LocalTransform",
			StringComparison.Ordinal );
		Expect( "dropped presentation mutation probe rejects non-base-relative transforms",
			transformMutation != renderSource
			&& !DroppedPresentationSourceContractValid(
				coreSource,
				droppedSource,
				transformMutation ) );

		var perspectiveMutation = renderSource.Replace(
			"Perspective != WeaponAttachmentPerspective.ThirdPerson",
			"Perspective != WeaponAttachmentPerspective.FirstPerson",
			StringComparison.Ordinal );
		Expect( "dropped presentation mutation probe rejects first-person emitters",
			perspectiveMutation != renderSource
			&& !DroppedPresentationSourceContractValid(
				coreSource,
				droppedSource,
				perspectiveMutation ) );

		var stateEqualityMutation = renderSource.Replace(
			"droppedState.Current != attachmentState.Current",
			"droppedState.Current == attachmentState.Current",
			StringComparison.Ordinal );
		Expect( "dropped presentation mutation probe rejects mismatched copied state",
			stateEqualityMutation != renderSource
			&& !DroppedPresentationSourceContractValid(
				coreSource,
				droppedSource,
				stateEqualityMutation ) );

		var rootSnapshotMutation = ReplaceFirstOrdinal(
			renderSource,
			"NetworkMode = NetworkMode.Snapshot",
			"NetworkMode = NetworkMode.Never" );
		Expect( "dropped presentation mutation probe rejects a non-snapshot clone root",
			rootSnapshotMutation != renderSource
			&& !DroppedPresentationSourceContractValid(
				coreSource,
				droppedSource,
				rootSnapshotMutation ) );

		var childSnapshotMutation = ReplaceLastOrdinal(
			renderSource,
			"NetworkMode = NetworkMode.Snapshot",
			"NetworkMode = NetworkMode.Never" );
		Expect( "dropped presentation mutation probe rejects non-snapshot renderer children",
			childSnapshotMutation != renderSource
			&& childSnapshotMutation != rootSnapshotMutation
			&& !DroppedPresentationSourceContractValid(
				coreSource,
				droppedSource,
				childSnapshotMutation ) );

		var enabledControllerMutation = renderSource.Replace(
			"Components.Create<WeaponAttachmentRenderController>( false )",
			"Components.Create<WeaponAttachmentRenderController>()",
			StringComparison.Ordinal );
		Expect( "dropped presentation mutation probe rejects an enabled controller before binding",
			enabledControllerMutation != renderSource
			&& !DroppedPresentationSourceContractValid(
				coreSource,
				droppedSource,
				enabledControllerMutation ) );

		var enabledRendererMutation = renderSource.Replace(
			"Components.Create<ModelRenderer>( false )",
			"Components.Create<ModelRenderer>()",
			StringComparison.Ordinal );
		Expect( "dropped presentation mutation probe rejects enabled renderers before copying",
			enabledRendererMutation != renderSource
			&& !DroppedPresentationSourceContractValid(
				coreSource,
				droppedSource,
				enabledRendererMutation ) );

		var sourceContainmentMutation = ReplaceFirstOrdinal(
			renderSource,
			"AreAttachmentRenderersUnder( equipment.GameObject )",
			"true" );
		Expect( "dropped presentation mutation probe rejects copy-time source containment bypass",
			sourceContainmentMutation != renderSource
			&& !DroppedPresentationSourceContractValid(
				coreSource,
				droppedSource,
				sourceContainmentMutation ) );

		var postCloneDistinctMutation = renderSource.Replace(
			"!droppedController.AreRendererBindingsDistinct()",
			"false",
			StringComparison.Ordinal );
		Expect( "dropped presentation mutation probe rejects removal of post-clone distinctness",
			postCloneDistinctMutation != renderSource
			&& !DroppedPresentationSourceContractValid(
				coreSource,
				droppedSource,
				postCloneDistinctMutation ) );

		var postCloneRequiredMutation = renderSource.Replace(
			"!droppedController.HasRequiredRendererBindings( attachmentState.Current )",
			"false",
			StringComparison.Ordinal );
		Expect( "dropped presentation mutation probe rejects removal of post-clone required bindings",
			postCloneRequiredMutation != renderSource
			&& !DroppedPresentationSourceContractValid(
				coreSource,
				droppedSource,
				postCloneRequiredMutation ) );

		var rootParentMutation = renderSource.Replace(
			"new GameObject( dropped.GameObject, true, \"weapon_attachment_presentation\" )",
			"new GameObject( true, \"weapon_attachment_presentation\" )",
			StringComparison.Ordinal );
		Expect( "dropped presentation mutation probe rejects a detached clone root",
			rootParentMutation != renderSource
			&& !DroppedPresentationSourceContractValid(
				coreSource,
				droppedSource,
				rootParentMutation ) );

		var childParentMutation = renderSource.Replace(
			"new GameObject( cloneRoot, true, sourceRenderer.GameObject.Name )",
			"new GameObject( dropped.GameObject, true, sourceRenderer.GameObject.Name )",
			StringComparison.Ordinal );
		Expect( "dropped presentation mutation probe rejects renderer children outside the clone root",
			childParentMutation != renderSource
			&& !DroppedPresentationSourceContractValid(
				coreSource,
				droppedSource,
				childParentMutation ) );

		var invalidBaseModelMutation = renderSource.Replace(
			"&& !droppedBaseModel.IsError",
			string.Empty,
			StringComparison.Ordinal );
		Expect( "dropped presentation mutation probe rejects floating attachments over an error model",
			invalidBaseModelMutation != renderSource
			&& !DroppedPresentationSourceContractValid(
				coreSource,
				droppedSource,
				invalidBaseModelMutation ) );

		Expect( "dropped presentation source remains model-path and weapon-name agnostic",
			!DroppedPresentationSourceContractValid(
				coreSource,
				droppedSource,
				renderSource + "\nModel.Load( \"weapon.vmdl\" );" ) );
	}

	private static void ShootWeapon_UsesHostPresentationForProviderBearingWeapons()
	{
		var source = ReadSource( "ShootWeaponComponent.source.cs" );
		var shootBlock = SliceBraceBlock( source, "private void Shoot()" );
		var localFallbackBlock = SliceBraceBlock(
			shootBlock,
			"if ( presentationProvider is null )" );
		var localContractValid = shootBlock.Contains(
			"IWeaponFirePresentationProvider",
			StringComparison.Ordinal )
			&& !shootBlock.Contains( "GetFirePresentation", StringComparison.Ordinal )
			&& Count( shootBlock, "DoShootEffects(" ) == 1
			&& localFallbackBlock.Contains(
				"DoShootEffects( (byte)WeaponFirePresentation.None )",
				StringComparison.Ordinal );
		Expect( "provider-bearing owner defers presentation effects until host acceptance",
			localContractValid );

		var hostBlock = SliceBraceBlock( source, "private void DoShootHost()" );
		var providerBroadcastBlock = SliceBraceBlock(
			hostBlock,
			"if ( presentationProvider is not null )" );
		var hostContractValid = hostBlock.Contains(
			"presentationProvider?.GetFirePresentation()",
			StringComparison.Ordinal )
			&& providerBroadcastBlock.Contains(
				"BroadcastShootEffects( presentation )",
				StringComparison.Ordinal )
			&& !providerBroadcastBlock.Contains( "FilterExclude", StringComparison.Ordinal )
			&& hostBlock.Contains( "using ( Rpc.FilterExclude( caller ) )", StringComparison.Ordinal );
		Expect( "host includes provider-bearing owner in authoritative presentation broadcast",
			hostContractValid );

		if ( localContractValid )
		{
			Expect( "local presentation mutation probe rejects unconditional prediction",
				!SliceBraceBlock(
					source.Replace(
						"if ( presentationProvider is null )",
						"if ( true )",
						StringComparison.Ordinal ),
					"private void Shoot()" ).Contains(
						"if ( presentationProvider is null )",
						StringComparison.Ordinal ) );
		}

		if ( hostContractValid )
		{
			var filteredProviderMutation = source.Replace(
				"if ( presentationProvider is not null )\n\t\t{\n\t\t\tBroadcastShootEffects( presentation );",
				"if ( presentationProvider is not null )\n\t\t{\n\t\t\tusing ( Rpc.FilterExclude( caller ) )\n\t\t\t{\n\t\t\t\tBroadcastShootEffects( presentation );\n\t\t\t}",
				StringComparison.Ordinal );
			var mutatedProviderBlock = SliceBraceBlock(
				SliceBraceBlock( filteredProviderMutation, "private void DoShootHost()" ),
				"if ( presentationProvider is not null )" );
			Expect( "host presentation mutation probe rejects excluding the owner",
				filteredProviderMutation != source
				&& mutatedProviderBlock.Contains( "FilterExclude", StringComparison.Ordinal ) );
		}
	}

	private static void ShootWeapon_ConsumesOnePresentationByteWithNormalFallback()
	{
		var source = ReadSource( "ShootWeaponComponent.source.cs" );
		Expect( "ShootWeaponComponent resolves and broadcasts shot-time presentation",
			ShootSourceContractValid( source ) );
		Expect( "shoot mutation probe rejects provider bypass",
			!ShootSourceContractValid( source.Replace(
				"IWeaponFirePresentationProvider",
				"IRemovedPresentationProvider",
				StringComparison.Ordinal ) ) );
		Expect( "shoot mutation probe rejects presentation byte removal",
			!ShootSourceContractValid( source.Replace(
				"BroadcastShootEffects( byte",
				"BroadcastShootEffects( int",
				StringComparison.Ordinal ) ) );

		var rulesContractValid = ShootPresentationRulesSourceContractValid( source );
		Expect( "ShootWeaponComponent delegates flash and sound selection to the core presentation rules",
			rulesContractValid );
		if ( rulesContractValid )
		{
			var flashNegationMutation = source.Replace(
				"if ( !WeaponFirePresentationRules.ShouldSuppressMuzzleFlash( presentation )",
				"if ( WeaponFirePresentationRules.ShouldSuppressMuzzleFlash( presentation )",
				StringComparison.Ordinal );
			Expect( "shoot rules mutation probe rejects inverted muzzle-flash policy",
				flashNegationMutation != source
				&& !ShootPresentationRulesSourceContractValid( flashNegationMutation ) );

			var availabilityMutation = source.Replace(
				"SuppressedShootSound is not null )",
				"ShootSound is not null )",
				StringComparison.Ordinal );
			Expect( "shoot rules mutation probe rejects the wrong sound-availability input",
				availabilityMutation != source
				&& !ShootPresentationRulesSourceContractValid( availabilityMutation ) );

			var branchMutation = source.Replace(
				"? SuppressedShootSound\n\t\t\t: ShootSound;",
				"? ShootSound\n\t\t\t: SuppressedShootSound;",
				StringComparison.Ordinal );
			Expect( "shoot rules mutation probe rejects swapped normal and suppressed branches",
				branchMutation != source
				&& !ShootPresentationRulesSourceContractValid( branchMutation ) );
		}
	}

	private static void AttachmentSources_DoNotOwnWeaponStats()
	{
		var contract = ReadSource( "WeaponAttachmentContract.source.cs" );
		var state = ReadSource( "WeaponAttachmentState.source.cs" );
		var controller = ReadSource( "WeaponAttachmentController.source.cs" );
		var renderController = ReadSource( "WeaponAttachmentRenderController.source.cs" );
		var combined = string.Concat( contract, "\n", state, "\n", controller, "\n", renderController );
		Expect( "attachment subsystem remains presentation-only and does not own ballistics",
			AttachmentSourcesAreStatFree( combined ) );
		Expect( "stat mutation probe rejects damage coupling",
			!AttachmentSourcesAreStatFree( combined + "\nBaseDamage = 999;" ) );
	}

	private static bool StateSourceContractValid( string source )
	{
		var atomicSync = Regex.IsMatch(
			source,
			@"\[Property\]\s*\[Sync\(\s*SyncFlags\.FromHost\s*\)\]\s*public\s+WeaponAttachmentLoadout\s+ReplicatedLoadout\s*\{\s*get;\s*set;\s*\}",
			RegexOptions.CultureInvariant );
		var applyBlock = SliceBraceBlock( source, "private void Apply" );

		return source.Contains( "class WeaponAttachmentState", StringComparison.Ordinal )
		       && source.Contains( "IDroppedWeaponState<WeaponAttachmentState>", StringComparison.Ordinal )
		       && source.Contains( "IDroppedWeaponMergePolicy", StringComparison.Ordinal )
		       && Count( source, "SyncFlags.FromHost" ) == 1
		       && atomicSync
		       && source.Contains(
			       "public WeaponAttachmentLoadout Current => ReplicatedLoadout;",
			       StringComparison.Ordinal )
		       && applyBlock.Contains( "ReplicatedLoadout = loadout;", StringComparison.Ordinal );
	}

	private static bool StateReplacementValidationSourceContractValid( string source )
	{
		var replaceBlock = SliceBraceBlock( source, "public bool TryReplace" );
		var validation = replaceBlock.IndexOf(
			"WeaponAttachmentSnapshotCodec.IsValid( replacement )",
			StringComparison.Ordinal );
		var publication = replaceBlock.IndexOf( "Apply( replacement )", StringComparison.Ordinal );

		return validation >= 0 && publication > validation;
	}

	private static bool StateFirePresentationSourceContractValid( string source )
	{
		var presentationMethod = SliceBraceBlock( source, "public byte GetFirePresentation" );
		var replicatedAtomicLoadout = Regex.IsMatch(
			source,
			@"\[Sync\(\s*SyncFlags\.FromHost\s*\)\]\s*public\s+WeaponAttachmentLoadout\s+ReplicatedLoadout\s*\{\s*get;\s*set;\s*\}",
			RegexOptions.CultureInvariant );
		var replicatedLegacyMuzzleKind = Regex.IsMatch(
			source,
			@"\[Property\]\s*\[Sync\(\s*SyncFlags\.FromHost\s*\)\]\s*public\s+WeaponAttachmentKind\s+MuzzleKind\s*\{\s*get;\s*set;\s*\}",
			RegexOptions.CultureInvariant );

		return (replicatedAtomicLoadout || replicatedLegacyMuzzleKind)
		       && presentationMethod.Contains(
			       "return WeaponAttachmentPresentation.ResolveFirePresentation( Current );",
			       StringComparison.Ordinal );
	}

	private static bool SnapshotIntegrationSourceContractValid(
		string playerSource,
		string snapshotDataSource,
		string stateSource,
		string interfaceSource )
	{
		var saveBlock = SliceBraceBlock( playerSource, "SnapshotData ISnapshot.Save()" );
		var loadBlock = SliceBraceBlock( playerSource, "void ISnapshot.Load( SnapshotData data )" );
		var canDropGate = saveBlock.IndexOf( "if ( !equipment.CanDrop )", StringComparison.Ordinal );
		var captureLoop = saveBlock.IndexOf(
			"foreach ( var snapshotState in equipment.Components.GetAll<IEquipmentSnapshotState>() )",
			StringComparison.Ordinal );
		var addEquipment = saveBlock.IndexOf( "data.Equipment.Add( equipmentData )", StringComparison.Ordinal );
		var prefabCreation = loadBlock.IndexOf( "var equipment = GiveHost( resource", StringComparison.Ordinal );
		var restoreLoop = loadBlock.IndexOf(
			"foreach ( var snapshotState in equipment.Components.GetAll<IEquipmentSnapshotState>() )",
			StringComparison.Ordinal );
		var restoreCall = loadBlock.IndexOf( "snapshotState.TryRestoreSnapshotState", StringComparison.Ordinal );
		var restoreAfterPrefabCreation = prefabCreation >= 0
		                                && restoreLoop > prefabCreation
		                                && restoreCall > restoreLoop;
		string[] forbiddenEconomyTokens =
		[
			"ServerApiClient",
			"TakePlayerItem",
			"GivePlayerItem",
			"AttachHost",
			"DetachHost"
		];

		return interfaceSource.Contains( "interface IEquipmentSnapshotState", StringComparison.Ordinal )
		       && interfaceSource.Contains( "SnapshotKey", StringComparison.Ordinal )
		       && interfaceSource.Contains( "TryCaptureSnapshotState", StringComparison.Ordinal )
		       && interfaceSource.Contains( "TryRestoreSnapshotState", StringComparison.Ordinal )
		       && snapshotDataSource.Contains(
			       "Dictionary<string, string> ComponentStates",
			       StringComparison.Ordinal )
		       && canDropGate >= 0
		       && captureLoop > canDropGate
		       && addEquipment > captureLoop
		       && saveBlock.Contains( "TryCaptureSnapshotState", StringComparison.Ordinal )
		       && saveBlock.Contains( "equipmentData.ComponentStates.Add( key, payload )", StringComparison.Ordinal )
		       && restoreLoop >= 0
		       && loadBlock.Contains( "restoredStateKeys.Add( key )", StringComparison.Ordinal )
		       && loadBlock.Contains(
			       "equipmentData.ComponentStates.TryGetValue( key, out var payload )",
			       StringComparison.Ordinal )
		       && restoreAfterPrefabCreation
		       && stateSource.Contains( "IEquipmentSnapshotState", StringComparison.Ordinal )
		       && stateSource.Contains( "WeaponAttachmentSnapshotCodec", StringComparison.Ordinal )
		       && stateSource.Contains( "TryCaptureSnapshotState", StringComparison.Ordinal )
		       && stateSource.Contains( "TryRestoreSnapshotState", StringComparison.Ordinal )
		       && stateSource.Contains(
			       "WeaponAttachmentCompatibility.CanRestoreLoadout",
			       StringComparison.Ordinal )
		       && stateSource.Contains( "TryReplace( Current, restored )", StringComparison.Ordinal )
		       && forbiddenEconomyTokens.All( token => !saveBlock.Contains( token, StringComparison.Ordinal ) )
		       && forbiddenEconomyTokens.All( token => !loadBlock.Contains( token, StringComparison.Ordinal ) );
	}

	private static bool SnapshotEquipmentIdentityBindingSourceContractValid( string stateSource )
	{
		var restoreBlock = SliceBraceBlock(
			stateSource,
			"bool IEquipmentSnapshotState.TryRestoreSnapshotState" );
		var equipmentLookup = restoreBlock.IndexOf(
			"Components.Get<Equipment>( FindMode.EverythingInSelfAndAncestors )",
			StringComparison.Ordinal );
		var identityBinding = restoreBlock.IndexOf(
			"WeaponAttachmentCompatibility.MatchesEquipmentPrefab( controller.WeaponId, equipment.Resource.PrefabPath() )",
			StringComparison.Ordinal );
		var compatibilityGate = restoreBlock.IndexOf(
			"WeaponAttachmentCompatibility.CanRestoreLoadout( controller.WeaponId, restored )",
			StringComparison.Ordinal );
		var replace = restoreBlock.IndexOf(
			"TryReplace( Current, restored )",
			StringComparison.Ordinal );

		return equipmentLookup >= 0
		       && identityBinding > equipmentLookup
		       && compatibilityGate > identityBinding
		       && replace > compatibilityGate;
	}

	private static bool AttachmentDropLockSourceContractValid(
		string controllerSource,
		string equipmentSource,
		string playerEquipmentSource,
		string playerSource )
	{
		var snapshotSave = SliceBraceBlock( playerSource, "SnapshotData ISnapshot.Save()" );
		var dropHost = SliceBraceBlock( playerEquipmentSource, "public void DropHost" );
		var deathDrop = SliceBraceBlock( playerEquipmentSource, "private void OnDeathEquipment" );
		var dropAll = SliceBraceBlock( playerEquipmentSource, "private void DropAllDroppableEquipment" );

		return equipmentSource.Contains(
			       "internal bool IsDropLocked { get; set; }",
			       StringComparison.Ordinal )
		       && equipmentSource.Contains(
			       "public bool CanDropNow => CanDrop && !IsDropLocked;",
			       StringComparison.Ordinal )
		       && ControllerDropLockMethodValid(
			       controllerSource,
			       "private async Task<WeaponAttachmentTransactionResult> AttachAsync" )
		       && ControllerDropLockMethodValid(
			       controllerSource,
			       "private async Task<WeaponAttachmentTransactionResult> DetachAsync" )
		       && ControllerDropLockMethodValid(
			       controllerSource,
			       "private async Task<WeaponAttachmentTransactionResult> SetLaserEnabledAsync" )
		       && !controllerSource.Contains( "Equipment.CanDrop =", StringComparison.Ordinal )
		       && !controllerSource.Contains( "previousCanDrop", StringComparison.Ordinal )
		       && Count( dropHost, "weapon.CanDropNow" ) == 2
		       && dropHost.Contains( "if ( weapon.CanDropNow )", StringComparison.Ordinal )
		       && dropHost.Contains( "if ( weapon.CanDropNow || forceRemove )", StringComparison.Ordinal )
		       && !dropHost.Contains( "weapon.CanDrop )", StringComparison.Ordinal )
		       && Count( deathDrop, "CurrentEquipment.CanDropNow" ) == 1
		       && !deathDrop.Contains( "CurrentEquipment.CanDrop )", StringComparison.Ordinal )
		       && Count( dropAll, "x.CanDropNow" ) == 1
		       && !dropAll.Contains( "x.CanDrop\r\n", StringComparison.Ordinal )
		       && !dropAll.Contains( "x.CanDrop\n", StringComparison.Ordinal )
		       && snapshotSave.Contains( "if ( !equipment.CanDrop )", StringComparison.Ordinal )
		       && !snapshotSave.Contains( "CanDropNow", StringComparison.Ordinal );
	}

	private static bool ControllerDropLockMethodValid( string source, string methodMarker )
	{
		var block = SliceBraceBlock( source, methodMarker );
		var lockOn = block.IndexOf( "Equipment.IsDropLocked = true;", StringComparison.Ordinal );
		var lockOff = block.IndexOf( "Equipment.IsDropLocked = false;", StringComparison.Ordinal );
		var innerFinally = lockOn >= 0
			? block.IndexOf( "finally", lockOn, StringComparison.Ordinal )
			: -1;
		var innerFinallyBlock = innerFinally >= 0
			? SliceBraceBlock( block[innerFinally..], "finally" )
			: string.Empty;
		var outerFinally = block.LastIndexOf( "finally", StringComparison.Ordinal );
		var release = block.LastIndexOf( "TransactionLock.Release()", StringComparison.Ordinal );

		return Count( block, "Equipment.IsDropLocked = true;" ) == 1
		       && Count( block, "Equipment.IsDropLocked = false;" ) == 1
		       && lockOn >= 0
		       && innerFinally > lockOn
		       && Count( innerFinallyBlock, "Equipment.IsDropLocked = false;" ) == 1
		       && outerFinally > lockOff
		       && release > outerFinally;
	}

	private static bool ControllerSourceContractValid( string source )
	{
		return source.Contains( "class WeaponAttachmentController", StringComparison.Ordinal )
		       && Count( source, "[Rpc.Host" ) >= 2
		       && source.Contains( "WeaponAttachmentTransactionController.Attach", StringComparison.Ordinal )
		       && source.Contains( "WeaponAttachmentTransactionController.Detach", StringComparison.Ordinal )
		       && source.Contains( "ItemType.Accessory", StringComparison.Ordinal )
		       && source.Contains( "WeaponAttachmentCatalog.MatchesGrantIdentifier", StringComparison.Ordinal )
		       && source.Contains(
			       "var takeSucceeded = await ServerApiClient.TakePlayerItem",
			       StringComparison.Ordinal )
		       && source.Contains(
			       "var givenItem = await ServerApiClient.GivePlayerItem",
			       StringComparison.Ordinal )
		       && !source.Contains( "VerifyInventoryQuantityAsync", StringComparison.Ordinal )
		       && source.Contains( "public bool InventoryTransactionsEnabled { get; set; }", StringComparison.Ordinal )
		       && source.Contains( "&& InventoryTransactionsEnabled", StringComparison.Ordinal )
		       && source.Contains(
			       "SyntheticActorRegistry.IsSynthetic( owner.SteamId, owner.IsDebugPlayer )",
			       StringComparison.Ordinal )
		       && source.Contains( "try", StringComparison.Ordinal );
	}

	private static bool ControllerAuthorityGateSourceContractValid( string source )
	{
		var gate = SliceBraceBlock( source, "private bool CanMutate" );
		return gate.Contains( "Networking.IsHost", StringComparison.Ordinal )
		       && gate.Contains( "InventoryTransactionsEnabled", StringComparison.Ordinal )
		       && gate.Contains( "!string.IsNullOrWhiteSpace( WeaponId )", StringComparison.Ordinal )
		       && gate.Contains( "Equipment.IsValid()", StringComparison.Ordinal )
		       && gate.Contains( "AttachmentState.IsValid()", StringComparison.Ordinal )
		       && gate.Contains( "owner.IsValid()", StringComparison.Ordinal )
		       && gate.Contains( "!owner.IsDead", StringComparison.Ordinal )
		       && gate.Contains(
			       "!SyntheticActorRegistry.IsSynthetic( owner.SteamId, owner.IsDebugPlayer )",
			       StringComparison.Ordinal )
		       && gate.Contains( "Equipment.Owner == owner", StringComparison.Ordinal )
		       && gate.Contains( "Equipment.IsDeployed", StringComparison.Ordinal )
		       && gate.Contains( "owner.CurrentEquipment == Equipment", StringComparison.Ordinal );
	}

	private static bool ControllerCleanupWhitelistSafe( string source )
	{
		string[] methodMarkers =
		[
			"private async Task<WeaponAttachmentTransactionResult> AttachAsync",
			"private async Task<WeaponAttachmentTransactionResult> DetachAsync",
			"private async Task<WeaponAttachmentTransactionResult> SetLaserEnabledAsync"
		];

		return methodMarkers.All( marker =>
		{
			var methodBlock = SliceBraceBlock( source, marker );
			var finallyMatches = Regex.Matches( methodBlock, @"\bfinally\b" );
			if ( finallyMatches.Count < 2 )
			{
				return false;
			}

			foreach ( Match match in finallyMatches )
			{
				var finallyBlock = SliceBraceBlock( methodBlock[match.Index..], "finally" );
				if ( Regex.IsMatch( finallyBlock, @"\bawait\b" ) )
				{
					return false;
				}
			}

			return true;
		} );
	}

	private static bool ControllerEquipmentIdentityBindingSourceContractValid( string source )
	{
		var gate = SliceBraceBlock( source, "private bool CanMutate" );
		return gate.Contains(
			"WeaponAttachmentCompatibility.MatchesEquipmentPrefab( WeaponId, Equipment.Resource.PrefabPath() )",
			StringComparison.Ordinal );
	}

	private static bool ControllerInventoryConcurrencyContractValid( string source )
	{
		var attachBlock = SliceBraceBlock(
			source,
			"private async Task<WeaponAttachmentTransactionResult> AttachAsync" );
		var takeCall = attachBlock.IndexOf(
			"var takeSucceeded = await ServerApiClient.TakePlayerItem",
			StringComparison.Ordinal );
		var takeRejection = attachBlock.IndexOf( "if ( !takeSucceeded )", StringComparison.Ordinal );
		var stateCommit = attachBlock.IndexOf( "AttachmentState.TryReplace", StringComparison.Ordinal );
		var unknownTake = SliceBraceBlock( attachBlock, "if ( !takeSucceeded )" );

		return source.Contains(
			       "private static readonly SemaphoreSlim TransactionLock = new( 1, 1 )",
			       StringComparison.Ordinal )
		       && !source.Contains( "private readonly SemaphoreSlim _transactionLock", StringComparison.Ordinal )
		       && Count( source, "await TransactionLock.WaitAsync()" ) >= 3
		       && Count( source, "TransactionLock.Release()" ) >= 3
		       && takeCall >= 0
		       && takeRejection > takeCall
		       && stateCommit > takeRejection
		       && unknownTake.Contains(
			       "WeaponAttachmentTransactionFailure.CompensationFailed",
			       StringComparison.Ordinal )
		       && unknownTake.Contains( "true );", StringComparison.Ordinal )
		       && attachBlock.Contains( "CompensateTakenItemAsync", StringComparison.Ordinal );
	}

	private static bool ReconciliationQuarantineBehaviorValid()
	{
		var quarantineType = typeof( WeaponAttachmentLoadout ).Assembly.GetType(
			"LifePunch.DXRP.Addons.Weapons.Attachments.WeaponAttachmentReconciliationQuarantine" );
		if ( quarantineType is null )
		{
			return false;
		}

		var isRequired = quarantineType.GetMethod(
			"IsRequired",
			BindingFlags.Public | BindingFlags.Instance,
			null,
			[typeof( long )],
			null );
		var require = quarantineType.GetMethod(
			"Require",
			BindingFlags.Public | BindingFlags.Instance,
			null,
			[typeof( long )],
			null );
		var instance = Activator.CreateInstance( quarantineType );
		if ( instance is null || isRequired is null || require is null )
		{
			return false;
		}

		const long firstOwner = 76561198000000001;
		const long secondOwner = 76561198000000002;
		var initiallyClear = isRequired.Invoke( instance, [firstOwner] ) is false
			&& isRequired.Invoke( instance, [secondOwner] ) is false;
		var firstRequire = require.Invoke( instance, [firstOwner] ) is true;
		var ownerIsolation = isRequired.Invoke( instance, [firstOwner] ) is true
			&& isRequired.Invoke( instance, [secondOwner] ) is false;
		var duplicateRequire = require.Invoke( instance, [firstOwner] ) is false;

		return initiallyClear && firstRequire && ownerIsolation && duplicateRequire;
	}

	private static bool ReconciliationQuarantineQueuedBehaviorValid()
	{
		try
		{
			const long quarantinedOwner = 76561198000000011;
			const long independentOwner = 76561198000000012;
			var quarantine = new WeaponAttachmentReconciliationQuarantine();
			using var transactionGate = new SemaphoreSlim( 1, 1 );
			var firstEntered = new TaskCompletionSource<bool>(
				TaskCreationOptions.RunContinuationsAsynchronously );
			var allowAmbiguousCompletion = new TaskCompletionSource<bool>(
				TaskCreationOptions.RunContinuationsAsynchronously );
			var blockedMutationCalls = 0;
			var independentMutationCalls = 0;

			var ambiguousTransaction = Task.Run( async () =>
			{
				await transactionGate.WaitAsync();
				try
				{
					firstEntered.TrySetResult( true );
					await allowAmbiguousCompletion.Task;
					quarantine.Require( quarantinedOwner );
				}
				finally
				{
					transactionGate.Release();
				}
			} );

			firstEntered.Task.GetAwaiter().GetResult();

			Task QueueMutationAttempt( long ownerId, Action mutation )
			{
				return Task.Run( async () =>
				{
					await transactionGate.WaitAsync();
					try
					{
						if ( !quarantine.IsRequired( ownerId ) )
						{
							mutation();
						}
					}
					finally
					{
						transactionGate.Release();
					}
				} );
			}

			Task[] queuedSameOwner =
			[
				QueueMutationAttempt(
					quarantinedOwner,
					() => Interlocked.Increment( ref blockedMutationCalls ) ),
				QueueMutationAttempt(
					quarantinedOwner,
					() => Interlocked.Increment( ref blockedMutationCalls ) ),
				QueueMutationAttempt(
					quarantinedOwner,
					() => Interlocked.Increment( ref blockedMutationCalls ) )
			];
			var queuedIndependentOwner = QueueMutationAttempt(
				independentOwner,
				() => Interlocked.Increment( ref independentMutationCalls ) );

			allowAmbiguousCompletion.TrySetResult( true );
			Task.WhenAll( queuedSameOwner.Append( queuedIndependentOwner ).Append( ambiguousTransaction ) )
				.GetAwaiter()
				.GetResult();

			return quarantine.IsRequired( quarantinedOwner )
				&& !quarantine.IsRequired( independentOwner )
				&& blockedMutationCalls == 0
				&& independentMutationCalls == 1;
		}
		catch
		{
			return false;
		}
	}

	private static bool ControllerReconciliationQuarantineContractValid(
		string contractSource,
		string controllerSource )
	{
		var failureEnum = SliceBraceBlock(
			contractSource,
			"public enum WeaponAttachmentTransactionFailure" );
		var quarantine = SliceBraceBlock(
			contractSource,
			"public sealed class WeaponAttachmentReconciliationQuarantine" );
		var resultHelper = SliceBraceBlock(
			controllerSource,
			"private static WeaponAttachmentTransactionResult ReconciliationRequiredResult" );
		var requireHelper = SliceBraceBlock(
			controllerSource,
			"private static void RequireReconciliation" );
		var authorityGate = SliceBraceBlock( controllerSource, "private bool CanMutate" );
		var attachEntryPoint = SliceBraceBlock(
			controllerSource,
			"Task<WeaponAttachmentTransactionResult> TryAttachForHost" );
		var detachEntryPoint = SliceBraceBlock(
			controllerSource,
			"Task<WeaponAttachmentTransactionResult> TryDetachForHost" );
		var laserEntryPoint = SliceBraceBlock(
			controllerSource,
			"Task<WeaponAttachmentTransactionResult> TrySetLaserEnabledForHost" );
		var attachTransaction = SliceBraceBlock(
			controllerSource,
			"private async Task<WeaponAttachmentTransactionResult> AttachAsync" );
		var detachTransaction = SliceBraceBlock(
			controllerSource,
			"private async Task<WeaponAttachmentTransactionResult> DetachAsync" );
		var laserTransaction = SliceBraceBlock(
			controllerSource,
			"private async Task<WeaponAttachmentTransactionResult> SetLaserEnabledAsync" );

		return failureEnum.Contains( "ReconciliationRequired", StringComparison.Ordinal )
		       && quarantine.Contains( "HashSet<long>", StringComparison.Ordinal )
		       && quarantine.Contains( "lock", StringComparison.Ordinal )
		       && quarantine.Contains( "public bool IsRequired( long ownerId )", StringComparison.Ordinal )
		       && quarantine.Contains( "public bool Require( long ownerId )", StringComparison.Ordinal )
		       && !Regex.IsMatch(
			       quarantine,
			       @"\b(?:Clear|Remove|Reset)\b",
			       RegexOptions.CultureInvariant )
		       && !quarantine.Contains( "GetPlayerInventory", StringComparison.Ordinal )
		       && controllerSource.Contains(
			       "private static readonly WeaponAttachmentReconciliationQuarantine ReconciliationQuarantine = new()",
			       StringComparison.Ordinal )
		       && resultHelper.Contains(
			       "WeaponAttachmentTransactionFailure.ReconciliationRequired",
			       StringComparison.Ordinal )
		       && resultHelper.Contains( "true );", StringComparison.Ordinal )
		       && requireHelper.Contains(
			       "ReconciliationQuarantine.Require( ownerId );",
			       StringComparison.Ordinal )
		       && authorityGate.Contains( "&& !IsReconciliationRequired( owner )", StringComparison.Ordinal )
		       && ReconciliationEntryPointContractValid( attachEntryPoint )
		       && ReconciliationEntryPointContractValid( detachEntryPoint )
		       && ReconciliationEntryPointContractValid( laserEntryPoint )
		       && ReconciliationQueuedMethodContractValid( attachTransaction )
		       && ReconciliationQueuedMethodContractValid( detachTransaction )
		       && ReconciliationQueuedMethodContractValid( laserTransaction )
		       && AmbiguousBranchQuarantinesBeforeLog(
			       attachTransaction,
			       "if ( !takeSucceeded )" )
		       && AmbiguousDetachQuarantinesBeforeLog( detachTransaction )
		       && ExceptionalAmbiguityQuarantinesBeforeFallibleWork(
			       attachTransaction,
			       "inventoryTaken" )
		       && ExceptionalAmbiguityQuarantinesBeforeFallibleWork(
			       detachTransaction,
			       "stateDetached" )
		       && DetachCommitUncertaintyContractValid( detachTransaction )
		       && Count( attachTransaction, "RequireReconciliation( ownerId );" ) >= 3
		       && Count( detachTransaction, "RequireReconciliation( ownerId );" ) >= 2
		       && Count( laserTransaction, "RequireReconciliation( ownerId );" ) == 0;
	}

	private static bool ReconciliationEntryPointContractValid( string block )
	{
		var quarantineGate = block.IndexOf(
			"if ( IsReconciliationRequired( owner ) )",
			StringComparison.Ordinal );
		var authorityGate = block.IndexOf( "CanMutate( owner )", StringComparison.Ordinal );
		var queuedTransaction = block.IndexOf( "Async( owner", StringComparison.Ordinal );

		return quarantineGate >= 0
		       && authorityGate > quarantineGate
		       && queuedTransaction > authorityGate;
	}

	private static bool ReconciliationQueuedMethodContractValid( string block )
	{
		var ownerCapture = block.IndexOf( "var ownerId = owner.SteamId;", StringComparison.Ordinal );
		var wait = block.IndexOf( "await TransactionLock.WaitAsync()", StringComparison.Ordinal );
		var mainThread = block.IndexOf( "await GameTask.MainThread()", StringComparison.Ordinal );
		var quarantineGate = block.IndexOf(
			"if ( IsReconciliationRequired( ownerId ) )",
			StringComparison.Ordinal );
		var dropLock = block.IndexOf( "Equipment.IsDropLocked = true;", StringComparison.Ordinal );
		var inventoryApi = block.IndexOf( "ServerApiClient", StringComparison.Ordinal );

		return ownerCapture >= 0
		       && wait > ownerCapture
		       && mainThread > wait
		       && quarantineGate > mainThread
		       && dropLock > quarantineGate
		       && (inventoryApi < 0 || inventoryApi > quarantineGate);
	}

	private static bool AmbiguousBranchQuarantinesBeforeLog( string transaction, string marker )
	{
		var branch = SliceBraceBlock( transaction, marker );
		var quarantine = branch.IndexOf( "RequireReconciliation( ownerId );", StringComparison.Ordinal );
		var log = branch.IndexOf( "Log.Error", StringComparison.Ordinal );

		return quarantine >= 0 && log > quarantine;
	}

	private static bool AmbiguousDetachQuarantinesBeforeLog( string transaction )
	{
		var message = transaction.IndexOf(
			"State remains detached to prevent duplicate ownership.",
			StringComparison.Ordinal );
		var log = message < 0
			? -1
			: transaction.LastIndexOf( "Log.Error", message, StringComparison.Ordinal );
		var quarantine = log < 0
			? -1
			: transaction.LastIndexOf(
				"RequireReconciliation( ownerId );",
				log,
				StringComparison.Ordinal );

		return log >= 0 && quarantine >= 0 && quarantine < log;
	}

	private static bool ExceptionalAmbiguityQuarantinesBeforeFallibleWork(
		string transaction,
		string uncertaintyFlag )
	{
		var catchBlock = SliceBraceBlock( transaction, "catch ( Exception exception )" );
		var guard = SliceBraceBlock( catchBlock, $"if ( {uncertaintyFlag} )" );
		var quarantine = guard.IndexOf( "RequireReconciliation( ownerId );", StringComparison.Ordinal );
		var mainThread = catchBlock.IndexOf( "await GameTask.MainThread();", StringComparison.Ordinal );
		var log = catchBlock.IndexOf( "Log.Error", StringComparison.Ordinal );

		return quarantine >= 0
		       && mainThread > quarantine
		       && log > mainThread;
	}

	private static bool DetachCommitUncertaintyContractValid( string transaction )
	{
		var potentiallyDetached = transaction.IndexOf( "stateDetached = true;", StringComparison.Ordinal );
		var commitCall = transaction.IndexOf(
			"if ( !AttachmentState.TryReplace( expected, stagedState.Current ) )",
			StringComparison.Ordinal );
		var knownFailure = SliceBraceBlock(
			transaction,
			"if ( !AttachmentState.TryReplace( expected, stagedState.Current ) )" );

		return potentiallyDetached >= 0
		       && commitCall > potentiallyDetached
		       && knownFailure.Contains( "stateDetached = false;", StringComparison.Ordinal );
	}

	private static bool InventoryApiAcknowledgementContractValid(
		string controllerSource,
		string contractSource )
	{
		var attachBlock = SliceBraceBlock(
			controllerSource,
			"private async Task<WeaponAttachmentTransactionResult> AttachAsync" );
		var unknownTake = SliceBraceBlock( attachBlock, "if ( !takeSucceeded )" );
		var takeCall = attachBlock.IndexOf(
			"var takeSucceeded = await ServerApiClient.TakePlayerItem",
			StringComparison.Ordinal );
		var unknownTakeBranch = attachBlock.IndexOf( "if ( !takeSucceeded )", StringComparison.Ordinal );
		var attachStateCommit = attachBlock.IndexOf( "AttachmentState.TryReplace", StringComparison.Ordinal );

		var detachBlock = SliceBraceBlock(
			controllerSource,
			"private async Task<WeaponAttachmentTransactionResult> DetachAsync" );
		var giveCall = detachBlock.IndexOf(
			"var givenItem = await ServerApiClient.GivePlayerItem",
			StringComparison.Ordinal );
		var acknowledgedGive = SliceBraceBlock( detachBlock, "if ( givenItem is not null )" );
		var acknowledgedGiveStart = detachBlock.IndexOf(
			"if ( givenItem is not null )",
			StringComparison.Ordinal );
		var detachSettled = acknowledgedGive.IndexOf( "stateDetached = false;", StringComparison.Ordinal );
		var detachRefresh = acknowledgedGive.IndexOf(
			"BroadcastInventoryRefreshBestEffort( owner, detached.ItemId, givenItem.Quantity );",
			StringComparison.Ordinal );
		var detachSuccess = acknowledgedGive.IndexOf(
			"return WeaponAttachmentTransactionResult.Success;",
			StringComparison.Ordinal );
		var detachUnknownFailure = acknowledgedGiveStart < 0
			? -1
			: detachBlock.IndexOf(
				"WeaponAttachmentTransactionFailure.CompensationFailed",
				acknowledgedGiveStart,
				StringComparison.Ordinal );
		var detachReconciliationFlag = detachUnknownFailure < 0
			? -1
			: detachBlock.IndexOf( "true );", detachUnknownFailure, StringComparison.Ordinal );

		var compensation = SliceBraceBlock(
			controllerSource,
			"private static async Task<InventoryCompensationResult> CompensateTakenItemAsync" );

		return !contractSource.Contains(
			       "WeaponAttachmentInventoryReconciliation",
			       StringComparison.Ordinal )
		       && takeCall >= 0
		       && unknownTakeBranch > takeCall
		       && attachStateCommit > unknownTakeBranch
		       && unknownTake.Contains(
			       "WeaponAttachmentTransactionFailure.CompensationFailed",
			       StringComparison.Ordinal )
		       && unknownTake.Contains( "true );", StringComparison.Ordinal )
		       && !unknownTake.Contains(
			       "WeaponAttachmentTransactionFailure.InventoryTakeFailed",
			       StringComparison.Ordinal )
		       && !attachBlock.Contains( "VerifyInventoryQuantityAsync", StringComparison.Ordinal )
		       && !attachBlock.Contains( "takeVerification", StringComparison.Ordinal )
		       && Count( attachBlock, "CompensateTakenItemAsync" ) == 1
		       && giveCall >= 0
		       && !detachBlock.Contains( "VerifyInventoryQuantityAsync", StringComparison.Ordinal )
		       && !detachBlock.Contains( "giveVerification", StringComparison.Ordinal )
		       && detachSettled >= 0
		       && detachRefresh > detachSettled
		       && detachSuccess > detachRefresh
		       && acknowledgedGiveStart > giveCall
		       && detachUnknownFailure > acknowledgedGiveStart
		       && detachReconciliationFlag > detachUnknownFailure
		       && compensation.Contains(
			       "var refunded = await ServerApiClient.GivePlayerItem",
			       StringComparison.Ordinal )
		       && compensation.Contains( "if ( refunded is null )", StringComparison.Ordinal )
		       && compensation.Contains(
			       "return new InventoryCompensationResult( true, refunded.Quantity );",
			       StringComparison.Ordinal )
		       && !compensation.Contains( "GetPlayerInventory", StringComparison.Ordinal );
	}

	private static bool AttachTakeOutcomeCompletionContractValid( string source )
	{
		var attachBlock = SliceBraceBlock(
			source,
			"private async Task<WeaponAttachmentTransactionResult> AttachAsync" );
		var takeMayCommit = attachBlock.IndexOf( "inventoryTaken = true;", StringComparison.Ordinal );
		var takeCall = attachBlock.IndexOf(
			"var takeSucceeded = await ServerApiClient.TakePlayerItem",
			StringComparison.Ordinal );
		var takeResponseBlock = SliceBraceBlock( attachBlock, "if ( !takeSucceeded )" );
		var stateCommit = attachBlock.IndexOf( "AttachmentState.TryReplace", StringComparison.Ordinal );

		return takeMayCommit >= 0
		       && takeMayCommit < takeCall
		       && stateCommit > takeCall
		       && takeResponseBlock.Contains(
			       "WeaponAttachmentTransactionFailure.CompensationFailed",
			       StringComparison.Ordinal )
		       && takeResponseBlock.Contains( "true );", StringComparison.Ordinal )
		       && takeResponseBlock.Contains( "return", StringComparison.Ordinal );
	}

	private static bool DetachAmbiguityPreservesSingleCustodyContractValid( string source )
	{
		var detachBlock = SliceBraceBlock(
			source,
			"private async Task<WeaponAttachmentTransactionResult> DetachAsync" );
		var unresolvedStart = detachBlock.IndexOf(
			"State remains detached to prevent duplicate ownership.",
			StringComparison.Ordinal );
		var unresolvedFailure = unresolvedStart < 0
			? -1
			: detachBlock.IndexOf(
				"WeaponAttachmentTransactionFailure.CompensationFailed",
				unresolvedStart,
				StringComparison.Ordinal );
		var unresolvedFlag = unresolvedFailure < 0
			? -1
			: detachBlock.IndexOf( "true );", unresolvedFailure, StringComparison.Ordinal );

		return Count( detachBlock, "AttachmentState.TryReplace" ) == 1
		       && unresolvedStart >= 0
		       && unresolvedFailure > unresolvedStart
		       && unresolvedFlag > unresolvedFailure;
	}

	private static bool InventoryRefreshDoesNotOwnTransactionOutcomeContractValid( string source )
	{
		var attachBlock = SliceBraceBlock(
			source,
			"private async Task<WeaponAttachmentTransactionResult> AttachAsync" );
		var attachCommit = SliceBraceBlock(
			attachBlock,
			"if ( CanMutate( owner ) && AttachmentState.TryReplace" );
		var detachBlock = SliceBraceBlock(
			source,
			"private async Task<WeaponAttachmentTransactionResult> DetachAsync" );
		var detachCommit = SliceBraceBlock( detachBlock, "if ( givenItem is not null )" );
		var helper = SliceBraceBlock(
			source,
			"private static void BroadcastInventoryRefreshBestEffort" );
		var helperTry = SliceBraceBlock( helper, "try" );

		var attachSettled = attachCommit.IndexOf( "inventoryTaken = false;", StringComparison.Ordinal );
		var attachRefresh = attachCommit.IndexOf(
			"BroadcastInventoryRefreshBestEffort( owner, itemId, expectedQuantityAfterTake );",
			StringComparison.Ordinal );
		var attachSuccess = attachCommit.IndexOf(
			"return WeaponAttachmentTransactionResult.Success;",
			StringComparison.Ordinal );
		var detachSettled = detachCommit.IndexOf( "stateDetached = false;", StringComparison.Ordinal );
		var detachRefresh = detachCommit.IndexOf(
			"BroadcastInventoryRefreshBestEffort( owner, detached.ItemId, givenItem.Quantity );",
			StringComparison.Ordinal );
		var detachSuccess = detachCommit.IndexOf(
			"return WeaponAttachmentTransactionResult.Success;",
			StringComparison.Ordinal );

		return Count( source, "owner.BroadcastInventoryRefresh(" ) == 1
		       && helper.Contains( "if ( !owner.IsValid() )", StringComparison.Ordinal )
		       && helper.Contains( "try", StringComparison.Ordinal )
		       && helper.Contains( "catch ( Exception exception )", StringComparison.Ordinal )
		       && helper.Contains( "Log.Warning", StringComparison.Ordinal )
		       && Count( helperTry, "owner.BroadcastInventoryRefresh(" ) == 1
		       && attachSettled >= 0
		       && attachRefresh > attachSettled
		       && attachSuccess > attachRefresh
		       && detachSettled >= 0
		       && detachRefresh > detachSettled
		       && detachSuccess > detachRefresh;
	}

	private static bool ControllerExceptionalCleanupReturnsToMainThreadContractValid( string source )
	{
		string[] methodMarkers =
		[
			"private async Task<WeaponAttachmentTransactionResult> AttachAsync",
			"private async Task<WeaponAttachmentTransactionResult> DetachAsync",
			"private async Task<WeaponAttachmentTransactionResult> SetLaserEnabledAsync"
		];

		return methodMarkers.All( marker =>
		{
			var methodBlock = SliceBraceBlock( source, marker );
			var catchBlock = SliceBraceBlock( methodBlock, "catch ( Exception exception )" );
			var mainThreadHop = catchBlock.IndexOf(
				"await GameTask.MainThread();",
				StringComparison.Ordinal );
			var engineLog = catchBlock.IndexOf( "Log.Error", StringComparison.Ordinal );
			var failureReturn = catchBlock.IndexOf(
				"return WeaponAttachmentTransactionResult.Failed",
				StringComparison.Ordinal );

			return mainThreadHop >= 0
			       && engineLog > mainThreadHop
			       && failureReturn > mainThreadHop;
		} );
	}

	private static bool ControllerExceptionalReconciliationFlagsContractValid( string source )
	{
		var attachBlock = SliceBraceBlock(
			source,
			"private async Task<WeaponAttachmentTransactionResult> AttachAsync" );
		var attachCatch = SliceBraceBlock( attachBlock, "catch ( Exception exception )" );
		var detachBlock = SliceBraceBlock(
			source,
			"private async Task<WeaponAttachmentTransactionResult> DetachAsync" );
		var detachCatch = SliceBraceBlock( detachBlock, "catch ( Exception exception )" );

		return attachCatch.Contains(
			       "inventoryTaken\n\t\t\t\t\t\t? WeaponAttachmentTransactionFailure.CompensationFailed",
			       StringComparison.Ordinal )
		       && attachCatch.Contains( "inventoryTaken );", StringComparison.Ordinal )
		       && detachCatch.Contains(
			       "stateDetached\n\t\t\t\t\t\t? WeaponAttachmentTransactionFailure.CompensationFailed",
			       StringComparison.Ordinal )
		       && detachCatch.Contains( "stateDetached );", StringComparison.Ordinal );
	}

	private static bool GlobalGateReleaseContractValid( string source )
	{
		return GlobalGateReleaseContractValidFor(
			       source,
			       "private async Task<WeaponAttachmentTransactionResult> AttachAsync" )
		       && GlobalGateReleaseContractValidFor(
			       source,
			       "private async Task<WeaponAttachmentTransactionResult> DetachAsync" )
		       && GlobalGateReleaseContractValidFor(
			       source,
			       "private async Task<WeaponAttachmentTransactionResult> SetLaserEnabledAsync" );
	}

	private static bool GlobalGateReleaseContractValidFor( string source, string methodMarker )
	{
		var block = SliceBraceBlock( source, methodMarker );
		var wait = block.IndexOf( "await TransactionLock.WaitAsync()", StringComparison.Ordinal );
		var tryStart = block.IndexOf( "try", StringComparison.Ordinal );
		var mainThreadHop = block.IndexOf( "await GameTask.MainThread()", StringComparison.Ordinal );
		var dropLock = block.IndexOf( "Equipment.IsDropLocked = true;", StringComparison.Ordinal );
		var finallyStart = block.LastIndexOf( "finally", StringComparison.Ordinal );
		var release = block.LastIndexOf( "TransactionLock.Release()", StringComparison.Ordinal );

		return wait >= 0
		       && tryStart > wait
		       && mainThreadHop > tryStart
		       && dropLock > tryStart
		       && finallyStart > dropLock
		       && release > finallyStart;
	}

	private static bool AttachmentInteractionSourceContractValid(
		string commandSource,
		string controllerSource )
	{
		var executeBlock = SliceBraceBlock( commandSource, "bool ExecuteHost" );
		var resolveBlock = SliceBraceBlock( commandSource, "bool TryResolveController" );
		var attachEntryPoint = SliceBraceBlock(
			controllerSource,
			"Task<WeaponAttachmentTransactionResult> TryAttachForHost" );
		var detachEntryPoint = SliceBraceBlock(
			controllerSource,
			"Task<WeaponAttachmentTransactionResult> TryDetachForHost" );
		var laserEntryPoint = SliceBraceBlock(
			controllerSource,
			"Task<WeaponAttachmentTransactionResult> TrySetLaserEnabledForHost" );
		var attachRpc = SliceBraceBlock( controllerSource, "public void AttachHost" );
		var detachRpc = SliceBraceBlock( controllerSource, "public void DetachHost" );
		var laserRpc = SliceBraceBlock( controllerSource, "public void SetLaserEnabledHost" );

		string[] forbiddenCommandTokens =
		[
			"ServerApiClient",
			"TakePlayerItem",
			"GivePlayerItem",
			"[Rpc."
		];

		return commandSource.Contains(
			       "sealed class WeaponAttachmentCommand : ICommand",
			       StringComparison.Ordinal )
		       && commandSource.Contains(
			       "public string Command => \"weaponattachment\";",
			       StringComparison.Ordinal )
		       && commandSource.Contains( "\"attachment\"", StringComparison.Ordinal )
		       && executeBlock.Contains( "TryResolveController", StringComparison.Ordinal )
		       && executeBlock.Contains( "TryAttachForHost", StringComparison.Ordinal )
		       && executeBlock.Contains( "TryDetachForHost", StringComparison.Ordinal )
		       && executeBlock.Contains( "TrySetLaserEnabledForHost", StringComparison.Ordinal )
		       && resolveBlock.Contains( "caller.CurrentEquipment", StringComparison.Ordinal )
		       && Regex.IsMatch(
			       resolveBlock,
			       @"Components\.Get<WeaponAttachmentController>\s*\(\s*FindMode\.EverythingInSelfAndDescendants\s*\)",
			       RegexOptions.CultureInvariant )
		       && commandSource.Contains( "WeaponAttachmentKind.PistolSuppressor", StringComparison.Ordinal )
		       && commandSource.Contains( "WeaponAttachmentKind.PbsSuppressor", StringComparison.Ordinal )
		       && commandSource.Contains( "WeaponAttachmentKind.RedDot", StringComparison.Ordinal )
		       && commandSource.Contains( "WeaponAttachmentKind.Laser", StringComparison.Ordinal )
		       && commandSource.Contains( "WeaponAttachmentSlot.Muzzle", StringComparison.Ordinal )
		       && commandSource.Contains( "WeaponAttachmentSlot.Optic", StringComparison.Ordinal )
		       && commandSource.Contains( "WeaponAttachmentSlot.Rail", StringComparison.Ordinal )
		       && forbiddenCommandTokens.All(
			       token => !commandSource.Contains( token, StringComparison.Ordinal ) )
		       && attachEntryPoint.Contains( "CanMutate( owner )", StringComparison.Ordinal )
		       && attachEntryPoint.Contains( "WeaponAttachmentSlots.TryGetSlot", StringComparison.Ordinal )
		       && (attachEntryPoint.Contains( "_ = AttachAsync( owner, kind, itemId );", StringComparison.Ordinal )
		           || attachEntryPoint.Contains( "return AttachAsync( owner, kind, itemId );", StringComparison.Ordinal ))
		       && detachEntryPoint.Contains( "CanMutate( owner )", StringComparison.Ordinal )
		       && (detachEntryPoint.Contains( "_ = DetachAsync( owner, slot );", StringComparison.Ordinal )
		           || detachEntryPoint.Contains( "return DetachAsync( owner, slot );", StringComparison.Ordinal ))
		       && laserEntryPoint.Contains( "CanMutate( owner )", StringComparison.Ordinal )
		       && (laserEntryPoint.Contains( "_ = SetLaserEnabledAsync( owner, enabled );", StringComparison.Ordinal )
		           || laserEntryPoint.Contains( "return SetLaserEnabledAsync( owner, enabled );", StringComparison.Ordinal ))
		       && attachRpc.Contains( "TryAttachForHost( owner, kind, itemId );", StringComparison.Ordinal )
		       && detachRpc.Contains( "TryDetachForHost( owner, slot );", StringComparison.Ordinal )
		       && laserRpc.Contains( "TrySetLaserEnabledForHost( owner, enabled );", StringComparison.Ordinal );
	}

	private static bool AttachmentCompletedResultSourceContractValid(
		string commandSource,
		string controllerSource )
	{
		var executeBlock = SliceBraceBlock( commandSource, "bool ExecuteHost" );
		var attachCommand = SliceBraceBlock(
			executeBlock,
			"string.Equals( args[0], \"attach\"" );
		var detachCommand = SliceBraceBlock(
			executeBlock,
			"string.Equals( args[0], \"detach\"" );
		var laserCommand = SliceBraceBlock(
			executeBlock,
			"string.Equals( args[0], \"laser\"" );
		var reportBlock = SliceBraceBlock( commandSource, "async Task ReportTransactionResultAsync" );
		var reportTry = SliceBraceBlock( reportBlock, "try" );
		var reportCatch = SliceBraceBlock( reportBlock, "catch ( Exception exception )" );
		var attachEntryPoint = SliceBraceBlock(
			controllerSource,
			"Task<WeaponAttachmentTransactionResult> TryAttachForHost" );
		var detachEntryPoint = SliceBraceBlock(
			controllerSource,
			"Task<WeaponAttachmentTransactionResult> TryDetachForHost" );
		var laserEntryPoint = SliceBraceBlock(
			controllerSource,
			"Task<WeaponAttachmentTransactionResult> TrySetLaserEnabledForHost" );
		var attachTransaction = SliceBraceBlock(
			controllerSource,
			"async Task<WeaponAttachmentTransactionResult> AttachAsync" );
		var detachTransaction = SliceBraceBlock(
			controllerSource,
			"async Task<WeaponAttachmentTransactionResult> DetachAsync" );
		var laserTransaction = SliceBraceBlock(
			controllerSource,
			"async Task<WeaponAttachmentTransactionResult> SetLaserEnabledAsync" );
		const string attachCall = "AttachAsync( owner, kind, itemId )";
		const string detachCall = "DetachAsync( owner, slot )";
		const string laserCall = "SetLaserEnabledAsync( owner, enabled )";
		var awaitResult = reportTry.IndexOf( "var result = await operation;", StringComparison.Ordinal );
		var mainThreadHop = reportTry.IndexOf( "await GameTask.MainThread();", StringComparison.Ordinal );
		var callerValidation = reportTry.IndexOf( "if ( !caller.IsValid() )", StringComparison.Ordinal );
		var successRead = reportTry.IndexOf( "result.Succeeded", StringComparison.Ordinal );
		var successNotification = reportTry.IndexOf( "caller.Success", StringComparison.Ordinal );
		var errorNotification = reportTry.IndexOf( "caller.Error", StringComparison.Ordinal );
		var catchMainThreadHop = reportCatch.IndexOf(
			"await GameTask.MainThread();",
			StringComparison.Ordinal );
		var catchErrorNotification = reportCatch.IndexOf( "caller.Error", StringComparison.Ordinal );

		return executeBlock.Contains( "ReportTransactionResultAsync", StringComparison.Ordinal )
		       && Count( executeBlock, "ReportTransactionResultAsync" ) == 3
		       && Count( attachCommand, "ReportTransactionResultAsync" ) == 1
		       && attachCommand.Contains( "controller.TryAttachForHost", StringComparison.Ordinal )
		       && Count( detachCommand, "ReportTransactionResultAsync" ) == 1
		       && detachCommand.Contains( "controller.TryDetachForHost", StringComparison.Ordinal )
		       && Count( laserCommand, "ReportTransactionResultAsync" ) == 1
		       && laserCommand.Contains( "controller.TrySetLaserEnabledForHost", StringComparison.Ordinal )
		       && Count( attachEntryPoint, attachCall ) == 1
		       && attachEntryPoint.Contains( $"return {attachCall};", StringComparison.Ordinal )
		       && !attachEntryPoint.Contains( "_ = AttachAsync", StringComparison.Ordinal )
		       && Count( detachEntryPoint, detachCall ) == 1
		       && detachEntryPoint.Contains( $"return {detachCall};", StringComparison.Ordinal )
		       && !detachEntryPoint.Contains( "_ = DetachAsync", StringComparison.Ordinal )
		       && Count( laserEntryPoint, laserCall ) == 1
		       && laserEntryPoint.Contains( $"return {laserCall};", StringComparison.Ordinal )
		       && !laserEntryPoint.Contains( "_ = SetLaserEnabledAsync", StringComparison.Ordinal )
		       && attachTransaction.Contains( "return WeaponAttachmentTransactionResult", StringComparison.Ordinal )
		       && detachTransaction.Contains( "return WeaponAttachmentTransactionResult", StringComparison.Ordinal )
		       && laserTransaction.Contains( "return WeaponAttachmentTransactionResult", StringComparison.Ordinal )
		       && Count( reportTry, "var result = await operation;" ) == 1
		       && Count( reportTry, "result =" ) == 1
		       && awaitResult >= 0
		       && mainThreadHop > awaitResult
		       && callerValidation > mainThreadHop
		       && successRead > callerValidation
		       && successNotification > mainThreadHop
		       && errorNotification > mainThreadHop
		       && reportTry.Contains( "if ( result.CompensationRequired )", StringComparison.Ordinal )
		       && !reportTry.Contains( "if ( !result.CompensationRequired )", StringComparison.Ordinal )
		       && reportTry.Contains( "result.Failure", StringComparison.Ordinal )
		       && catchMainThreadHop >= 0
		       && catchErrorNotification > catchMainThreadHop;
	}

	private static bool DroppedEquipmentSourceContractValid( string source )
	{
		var duplicateStart = source.IndexOf( "if ( existingWeapon != null )", StringComparison.Ordinal );
		var policy = source.IndexOf( "IDroppedWeaponMergePolicy", duplicateStart, StringComparison.Ordinal );
		var ammoMerge = source.IndexOf( "var existingAmmo", duplicateStart, StringComparison.Ordinal );
		return duplicateStart >= 0
		       && policy > duplicateStart
		       && ammoMerge > policy
		       && !source.Contains( "using LifePunch.", StringComparison.Ordinal );
	}

	private static bool DroppedEquipmentLifecycleSourceContractValid( string source )
	{
		var createBlock = SliceBraceBlock( source, "public static DroppedEquipment CreateHost" );
		var copyToDropped = createBlock.IndexOf(
			"state.CopyToDroppedWeapon( droppedWeapon );",
			StringComparison.Ordinal );
		var droppedEvent = createBlock.IndexOf(
			"IEquipmentEvents.Post( x => x.OnEquipmentDropped( droppedWeapon, heldWeapon?.Owner ) );",
			StringComparison.Ordinal );
		var networkSpawn = createBlock.IndexOf( "go.NetworkSpawn();", StringComparison.Ordinal );

		var pickupBlock = SliceBraceBlock( source, "private void DoPickupHost" );
		var duplicateBlock = SliceBraceBlock( pickupBlock, "if ( existingWeapon != null )" );
		var droppedPolicyLookup = duplicateBlock.IndexOf(
			"var mergePolicies = Components.GetAll<IDroppedWeaponMergePolicy>();",
			StringComparison.Ordinal );
		var vetoBlock = SliceBraceBlock(
			duplicateBlock,
			"if ( mergePolicies.Any( policy => !policy.CanMergeDuplicate() ) )" );
		var ownershipMutation = duplicateBlock.IndexOf(
			"existingWeapon.GameObject.Network.AssignOwnership",
			StringComparison.Ordinal );
		var ammoMutation = duplicateBlock.IndexOf( "var existingAmmo", StringComparison.Ordinal );
		var duplicateDestroy = duplicateBlock.IndexOf( "GameObject.Destroy();", StringComparison.Ordinal );

		var copyFromDropped = pickupBlock.IndexOf(
			"state.CopyFromDroppedWeapon( this );",
			StringComparison.Ordinal );
		var pickedUpEvent = pickupBlock.IndexOf(
			"IEquipmentEvents.Post( x => x.OnEquipmentPickedUp( player, this, weapon ) );",
			StringComparison.Ordinal );
		var finalDestroy = pickupBlock.LastIndexOf( "GameObject.Destroy();", StringComparison.Ordinal );

		return copyToDropped >= 0
		       && droppedEvent > copyToDropped
		       && networkSpawn > droppedEvent
		       && droppedPolicyLookup >= 0
		       && vetoBlock.Contains( "return;", StringComparison.Ordinal )
		       && ownershipMutation > droppedPolicyLookup
		       && ammoMutation > droppedPolicyLookup
		       && duplicateDestroy > droppedPolicyLookup
		       && copyFromDropped >= 0
		       && pickedUpEvent > copyFromDropped
		       && finalDestroy > pickedUpEvent;
	}

	private static bool DroppedPresentationSourceContractValid(
		string coreSource,
		string droppedSource,
		string renderSource )
	{
		var coreContractValid = coreSource.Contains(
			"public interface IDroppedWeaponPresentationSource",
			StringComparison.Ordinal )
			&& coreSource.Contains(
				"bool TryCopyPresentationTo( DroppedEquipment dropped );",
				StringComparison.Ordinal )
			&& !coreSource.Contains( "LifePunch.", StringComparison.Ordinal );

		const string stateLoopMarker =
			"foreach ( var state in heldWeapon.Components.GetAll<IDroppedWeaponState>() )";
		const string providerLoopMarker =
			"foreach ( var presentationSource in heldWeapon.Components.GetAll<IDroppedWeaponPresentationSource>( FindMode.EverythingInSelfAndDescendants ) )";
		const string stateCopyToken = "state.CopyToDroppedWeapon( droppedWeapon );";
		const string providerCallToken = "presentationSource.TryCopyPresentationTo( droppedWeapon )";
		var createBlock = SliceBraceBlock( droppedSource, "public static DroppedEquipment CreateHost" );
		var stateLoop = SliceBraceBlock( createBlock, stateLoopMarker );
		var providerLoop = SliceBraceBlock( createBlock, providerLoopMarker );
		var stateLoopStart = createBlock.IndexOf( stateLoopMarker, StringComparison.Ordinal );
		var stateLoopEnd = stateLoopStart < 0 ? -1 : stateLoopStart + stateLoop.Length;
		var providerLoopStart = createBlock.IndexOf( providerLoopMarker, StringComparison.Ordinal );
		var providerLoopEnd = providerLoopStart < 0 ? -1 : providerLoopStart + providerLoop.Length;
		var droppedEvent = createBlock.IndexOf(
			"IEquipmentEvents.Post( x => x.OnEquipmentDropped( droppedWeapon, heldWeapon?.Owner ) );",
			StringComparison.Ordinal );
		var networkSpawn = createBlock.IndexOf( "go.NetworkSpawn();", StringComparison.Ordinal );
		var droppedContractValid = Count( createBlock, stateLoopMarker ) == 1
			&& Count( createBlock, providerLoopMarker ) == 1
			&& Count( stateLoop, stateCopyToken ) == 1
			&& !stateLoop.Contains( "IDroppedWeaponPresentationSource", StringComparison.Ordinal )
			&& providerLoopStart > stateLoopEnd
			&& Count( providerLoop, providerCallToken ) == 1
			&& providerLoop.Contains(
				"if ( presentationSource.TryCopyPresentationTo( droppedWeapon ) )",
				StringComparison.Ordinal )
			&& providerLoop.Contains( "break;", StringComparison.Ordinal )
			&& droppedEvent > providerLoopEnd
			&& networkSpawn > droppedEvent
			&& !droppedSource.Contains( "using LifePunch.", StringComparison.Ordinal );

		var copyBlock = SliceBraceBlock(
			renderSource,
			"public bool TryCopyPresentationTo( DroppedEquipment dropped )" );
		var cloneBlock = SliceBraceBlock(
			renderSource,
			"private static ModelRenderer CloneRendererForDrop" );
		var resolveBlock = SliceBraceBlock( renderSource, "private bool TryResolveContext" );
		var forbiddenAssetTokens = new[]
		{
			"Model.Load",
			".vmdl",
			".prefab",
			"glock",
			"aks74u_original"
		};
		var renderContractValid = renderSource.Contains(
			"Component, IDroppedWeaponPresentationSource",
			StringComparison.Ordinal )
			&& copyBlock.Contains(
				"Perspective != WeaponAttachmentPerspective.ThirdPerson",
				StringComparison.Ordinal )
			&& copyBlock.Contains( "|| !AreRendererBindingsDistinct()", StringComparison.Ordinal )
			&& copyBlock.Contains( "|| !HasRequiredRendererBindings( attachmentState.Current )", StringComparison.Ordinal )
			&& copyBlock.Contains(
				"|| !AreAttachmentRenderersUnder( equipment.GameObject )",
				StringComparison.Ordinal )
			&& copyBlock.Contains( "equipment.ModelRenderer", StringComparison.Ordinal )
			&& copyBlock.Contains( "dropped.Components.Get<ModelRenderer>()", StringComparison.Ordinal )
			&& copyBlock.Contains(
				"dropped.Components.Get<WeaponAttachmentState>( FindMode.EverythingInSelfAndDescendants )",
				StringComparison.Ordinal )
			&& copyBlock.Contains(
				"droppedState.Current != attachmentState.Current",
				StringComparison.Ordinal )
			&& copyBlock.Contains(
				"new GameObject( dropped.GameObject, true, \"weapon_attachment_presentation\" )",
				StringComparison.Ordinal )
			&& Count( copyBlock, "NetworkMode = NetworkMode.Snapshot" ) == 1
			&& copyBlock.Contains(
				"Components.Create<WeaponAttachmentRenderController>( false )",
				StringComparison.Ordinal )
			&& copyBlock.Contains( "WeaponAttachmentPerspective.Dropped", StringComparison.Ordinal )
			&& copyBlock.Contains(
				"!droppedController.AreRendererBindingsDistinct()",
				StringComparison.Ordinal )
			&& copyBlock.Contains(
				"!droppedController.HasRequiredRendererBindings( attachmentState.Current )",
				StringComparison.Ordinal )
			&& copyBlock.Contains( "cloneRoot.Destroy();", StringComparison.Ordinal )
			&& copyBlock.Contains( "droppedController.Enabled = true", StringComparison.Ordinal )
			&& cloneBlock.Contains(
				"new GameObject( cloneRoot, true, sourceRenderer.GameObject.Name )",
				StringComparison.Ordinal )
			&& cloneBlock.Contains( "cloneRenderer.CopyFrom( sourceRenderer );", StringComparison.Ordinal )
			&& cloneBlock.Contains(
				"sourceBaseRenderer.WorldTransform.ToLocal( sourceRenderer.WorldTransform )",
				StringComparison.Ordinal )
			&& Count( cloneBlock, "NetworkMode = NetworkMode.Snapshot" ) == 1
			&& cloneBlock.Contains(
				"Components.Create<ModelRenderer>( false )",
				StringComparison.Ordinal )
			&& cloneBlock.Contains( "cloneRenderer.Enabled = false", StringComparison.Ordinal )
			&& resolveBlock.Contains(
				"Perspective == WeaponAttachmentPerspective.Dropped",
				StringComparison.Ordinal )
			&& resolveBlock.Contains(
				"Components.Get<DroppedEquipment>( FindMode.EverythingInSelfAndAncestors )",
				StringComparison.Ordinal )
			&& resolveBlock.Contains(
				"dropped.Components.Get<WeaponAttachmentState>( FindMode.EverythingInSelfAndDescendants )",
				StringComparison.Ordinal )
			&& resolveBlock.Contains( "dropped.Components.Get<ModelRenderer>()", StringComparison.Ordinal )
			&& resolveBlock.Contains( "droppedBaseModel is not null", StringComparison.Ordinal )
			&& resolveBlock.Contains( "droppedBaseModel.IsValid()", StringComparison.Ordinal )
			&& resolveBlock.Contains( "!droppedBaseModel.IsError", StringComparison.Ordinal )
			&& resolveBlock.Contains(
				"AreAttachmentRenderersUnder( dropped.GameObject )",
				StringComparison.Ordinal )
			&& forbiddenAssetTokens.All( token => !renderSource.Contains( token, StringComparison.Ordinal ) );

		return coreContractValid && droppedContractValid && renderContractValid;
	}

	private static bool RenderControllerSourceContractValid( string source )
	{
		string[] forbiddenTransformTokens =
		[
			"LocalPosition",
			"LocalRotation",
			"LocalScale",
			"WorldPosition",
			"WorldRotation",
			"WorldScale"
		];

		var applyBlock = SliceBraceBlock( source, "private void ApplyRendererState" );
		var resolveBlock = SliceBraceBlock( source, "private bool TryResolveContext" );

		return source.Contains( "class WeaponAttachmentRenderController", StringComparison.Ordinal )
		       && source.Contains( "public WeaponAttachmentPerspective Perspective { get; set; }", StringComparison.Ordinal )
		       && source.Contains( "PistolSuppressorRenderer", StringComparison.Ordinal )
		       && source.Contains( "PbsSuppressorRenderer", StringComparison.Ordinal )
		       && source.Contains( "RedDotRenderer", StringComparison.Ordinal )
		       && source.Contains( "LaserBodyRenderer", StringComparison.Ordinal )
		       && source.Contains( "LaserBeamRenderer", StringComparison.Ordinal )
		       && resolveBlock.Contains(
			       "Components.Get<ViewModel>( FindMode.EverythingInSelfAndAncestors )",
			       StringComparison.Ordinal )
		       && resolveBlock.Contains( "viewModel.Equipment", StringComparison.Ordinal )
		       && resolveBlock.Contains(
			       "Components.Get<Equipment>( FindMode.EverythingInSelfAndAncestors )",
			       StringComparison.Ordinal )
		       && resolveBlock.Contains(
			       "equipment.Components.Get<WeaponAttachmentState>( FindMode.EverythingInSelfAndDescendants )",
			       StringComparison.Ordinal )
		       && resolveBlock.Contains( "viewModel.ModelRenderer.Enabled", StringComparison.Ordinal )
		       && resolveBlock.Contains( "viewModel.RenderingEnabled", StringComparison.Ordinal )
		       && resolveBlock.Contains( "equipment.ModelRenderer.Enabled", StringComparison.Ordinal )
		       && resolveBlock.Contains(
			       "AreAttachmentRenderersUnder( viewModel.AdditionalRendererRoot )",
			       StringComparison.Ordinal )
		       && resolveBlock.Contains(
			       "AreAttachmentRenderersUnder( equipment.GameObject )",
			       StringComparison.Ordinal )
		       && resolveBlock.Contains( "AreRendererBindingsDistinct()", StringComparison.Ordinal )
		       && applyBlock.Contains( "DisableAllRenderers();", StringComparison.Ordinal )
		       && applyBlock.Contains( "HasRequiredRendererBindings( loadout )", StringComparison.Ordinal )
		       && source.Contains(
			       "WeaponAttachmentPresentation.IsAttachmentRendererVisible",
			       StringComparison.Ordinal )
		       && source.Contains(
			       "WeaponAttachmentPresentation.IsLaserVisible( loadout, Perspective )",
			       StringComparison.Ordinal )
		       && source.Contains( "renderer.Enabled = enabled", StringComparison.Ordinal )
		       && source.Contains( "ReferenceEquals( renderers[i], renderers[j] )", StringComparison.Ordinal )
		       && !source.Contains( "WeaponAttachmentState AttachmentState", StringComparison.Ordinal )
		       && !source.Contains( "_lastApplied", StringComparison.Ordinal )
		       && forbiddenTransformTokens.All( token => !source.Contains( token, StringComparison.Ordinal ) );
	}

	private static bool ShootSourceContractValid( string source )
	{
		var localShoot = Slice( source, "private void Shoot()", "private void DoShootHost()" );
		var hostShoot = Slice( source, "private void DoShootHost()", "private float CalculateDamageFalloff" );
		var localProvider = localShoot.IndexOf( "IWeaponFirePresentationProvider", StringComparison.Ordinal );
		var localFallback = localShoot.IndexOf( "if ( presentationProvider is null )", StringComparison.Ordinal );
		var localEffects = localShoot.IndexOf(
			"DoShootEffects( (byte)WeaponFirePresentation.None )",
			StringComparison.Ordinal );
		var hostRejection = hostShoot.IndexOf( "if ( authorization != WeaponHostFireReject.Accepted )", StringComparison.Ordinal );
		var hostProvider = hostShoot.IndexOf( "GetFirePresentation()", StringComparison.Ordinal );
		var hostBroadcast = hostShoot.IndexOf( "BroadcastShootEffects( presentation", StringComparison.Ordinal );
		var hasFallback = source.Contains( "WeaponFirePresentation.None", StringComparison.Ordinal )
		                  || source.Contains( "presentation = 0", StringComparison.Ordinal )
		                  || source.Contains( "?? (byte)0", StringComparison.Ordinal );

		return source.Contains( "IWeaponFirePresentationProvider", StringComparison.Ordinal )
		       && source.Contains( "GetFirePresentation()", StringComparison.Ordinal )
		       && source.Contains( "BroadcastShootEffects( byte", StringComparison.Ordinal )
		       && source.Contains( "DoShootEffects( byte", StringComparison.Ordinal )
		       && source.Contains( "SuppressMuzzleFlash", StringComparison.Ordinal )
		       && source.Contains( "UseSuppressedSound", StringComparison.Ordinal )
		       && hasFallback
		       && localProvider >= 0
		       && localFallback > localProvider
		       && localEffects > localFallback
		       && !localShoot.Contains( "GetFirePresentation()", StringComparison.Ordinal )
		       && hostRejection >= 0
		       && hostProvider > hostRejection
		       && hostBroadcast > hostProvider
		       && !source.Contains( "using LifePunch.", StringComparison.Ordinal );
	}

	private static bool ShootPresentationRulesSourceContractValid( string source )
	{
		var effects = Slice(
			source,
			"private void DoShootEffects( byte presentation )",
			"private void BroadcastBloodEffects" );
		var flashPattern =
			@"if\s*\(\s*!\s*WeaponFirePresentationRules\.ShouldSuppressMuzzleFlash\(\s*presentation\s*\)\s*&&\s*MuzzleFlashPrefab\.IsValid\(\s*\)\s*\)";
		var soundPattern =
			@"var\s+shootSound\s*=\s*WeaponFirePresentationRules\.ShouldUseSuppressedSound\(\s*presentation\s*,\s*SuppressedShootSound\s+is\s+not\s+null\s*\)\s*\?\s*SuppressedShootSound\s*:\s*ShootSound\s*;";

		return Regex.IsMatch( effects, flashPattern, RegexOptions.CultureInvariant )
		       && Regex.IsMatch( effects, soundPattern, RegexOptions.CultureInvariant )
		       && !effects.Contains( "HasFlag( WeaponFirePresentation.SuppressMuzzleFlash )", StringComparison.Ordinal )
		       && !effects.Contains( "HasFlag( WeaponFirePresentation.UseSuppressedSound )", StringComparison.Ordinal );
	}

	private static bool CoreContractSourceValid( string source )
	{
		return source.Contains( "namespace Dxura.RP.Game", StringComparison.Ordinal )
		       && source.Contains( "enum WeaponFirePresentation", StringComparison.Ordinal )
		       && source.Contains( "interface IWeaponFirePresentationProvider", StringComparison.Ordinal )
		       && source.Contains( "interface IDroppedWeaponMergePolicy", StringComparison.Ordinal );
	}

	private static bool IsImmutableValueType( Type type )
	{
		if ( !type.IsValueType )
		{
			return false;
		}

		if ( type.GetFields( BindingFlags.Public | BindingFlags.Instance ).Any( field => !field.IsInitOnly ) )
		{
			return false;
		}

		return type.GetProperties( BindingFlags.Public | BindingFlags.Instance ).All( property =>
		{
			if ( property.SetMethod is null )
			{
				return true;
			}

			return property.SetMethod.ReturnParameter
				.GetRequiredCustomModifiers()
				.Contains( typeof( IsExternalInit ) );
		} );
	}

	private static bool AttachmentSourcesAreStatFree( string source )
	{
		string[] forbidden =
		[
			"BaseDamage",
			"HeadshotDamageMultiplier",
			"BulletSpread",
			"BulletCount",
			"FireRate",
			"MaxRange",
			"RecoilWeaponComponent"
		];

		foreach ( var token in forbidden )
		{
			if ( source.Contains( token, StringComparison.Ordinal ) )
			{
				return false;
			}
		}

		return true;
	}

	private static string ReadSource( string name )
	{
		return File.ReadAllText( Path.Combine( AppContext.BaseDirectory, name ) );
	}

	private static string ReadSourceOrEmpty( string name )
	{
		var path = Path.Combine( AppContext.BaseDirectory, name );
		return File.Exists( path ) ? File.ReadAllText( path ) : string.Empty;
	}

	private static bool TrySnapshotRoundTrip(
		Type codecType,
		WeaponAttachmentLoadout fixture,
		out WeaponAttachmentLoadout restored )
	{
		restored = default;
		if ( codecType is null )
		{
			return false;
		}

		var serialize = codecType.GetMethod( "TrySerialize", BindingFlags.Public | BindingFlags.Static );
		if ( serialize is null )
		{
			return false;
		}

		object[] serializeArguments = [fixture, null];
		if ( serialize.Invoke( null, serializeArguments ) is not true
		     || serializeArguments[1] is not string payload )
		{
			return false;
		}

		return TrySnapshotDeserialize( codecType, payload, out restored );
	}

	private static bool InvokeEquipmentPrefabMatch(
		MethodInfo method,
		string weaponId,
		string prefabPath )
	{
		return method.Invoke( null, [weaponId, prefabPath] ) is true;
	}

	private static bool TrySnapshotDeserialize(
		Type codecType,
		string payload,
		out WeaponAttachmentLoadout loadout )
	{
		loadout = default;
		var deserialize = codecType.GetMethod( "TryDeserialize", BindingFlags.Public | BindingFlags.Static );
		if ( deserialize is null )
		{
			return false;
		}

		object[] deserializeArguments = [payload, default( WeaponAttachmentLoadout )];
		if ( deserialize.Invoke( null, deserializeArguments ) is not true
		     || deserializeArguments[1] is not WeaponAttachmentLoadout restored )
		{
			return false;
		}

		loadout = restored;
		return true;
	}

	private static string Slice( string source, string startMarker, string endMarker )
	{
		var start = source.IndexOf( startMarker, StringComparison.Ordinal );
		if ( start < 0 )
		{
			return string.Empty;
		}

		var end = source.IndexOf( endMarker, start + startMarker.Length, StringComparison.Ordinal );
		return end > start ? source[start..end] : string.Empty;
	}

	private static string ReplaceFirstOrdinal( string source, string value, string replacement )
	{
		var index = source.IndexOf( value, StringComparison.Ordinal );
		return index < 0
			? source
			: string.Concat( source.AsSpan( 0, index ), replacement, source.AsSpan( index + value.Length ) );
	}

	private static string ReplaceLastOrdinal( string source, string value, string replacement )
	{
		var index = source.LastIndexOf( value, StringComparison.Ordinal );
		return index < 0
			? source
			: string.Concat( source.AsSpan( 0, index ), replacement, source.AsSpan( index + value.Length ) );
	}

	private static string SliceBraceBlock( string source, string marker )
	{
		var markerIndex = source.IndexOf( marker, StringComparison.Ordinal );
		var openBrace = markerIndex < 0 ? -1 : source.IndexOf( '{', markerIndex + marker.Length );
		if ( openBrace < 0 )
		{
			return string.Empty;
		}

		var depth = 0;
		for ( var i = openBrace; i < source.Length; i++ )
		{
			if ( source[i] == '{' )
			{
				depth++;
			}
			else if ( source[i] == '}' )
			{
				depth--;
				if ( depth == 0 )
				{
					return source[markerIndex..(i + 1)];
				}
			}
		}

		return string.Empty;
	}

	private static int Count( string source, string value )
	{
		var count = 0;
		var offset = 0;
		while ( (offset = source.IndexOf( value, offset, StringComparison.Ordinal )) >= 0 )
		{
			count++;
			offset += value.Length;
		}

		return count;
	}

	private static void Expect( string name, bool ok )
	{
		if ( ok )
		{
			Console.WriteLine( $"PASS {name}" );
			return;
		}

		_failed++;
		Console.Error.WriteLine( $"FAIL {name}" );
	}

	private sealed class FakeInventory : IWeaponAttachmentInventoryGateway
	{
		private readonly Guid _itemId;

		public FakeInventory( Guid itemId, int quantity )
		{
			_itemId = itemId;
			Quantity = quantity;
		}

		public int Quantity { get; private set; }
		public int TakeCalls { get; private set; }
		public int GiveCalls { get; private set; }
		public bool AllowTake { get; init; } = true;
		public bool AllowGive { get; init; } = true;

		public bool TryTake( Guid itemId, int quantity )
		{
			TakeCalls++;
			if ( !AllowTake || itemId != _itemId || quantity != 1 || Quantity < quantity )
			{
				return false;
			}

			Quantity -= quantity;
			return true;
		}

		public bool TryGive( Guid itemId, int quantity )
		{
			GiveCalls++;
			if ( !AllowGive || itemId != _itemId || quantity != 1 )
			{
				return false;
			}

			Quantity += quantity;
			return true;
		}
	}

	private sealed class FakeState : IWeaponAttachmentStateGateway
	{
		private readonly Queue<bool> _replaceResults;

		public FakeState( WeaponAttachmentLoadout current, params bool[] replaceResults )
		{
			Current = current;
			_replaceResults = new Queue<bool>( replaceResults );
		}

		public WeaponAttachmentLoadout Current { get; private set; }

		public bool TryReplace( WeaponAttachmentLoadout expected, WeaponAttachmentLoadout replacement )
		{
			if ( Current != expected )
			{
				return false;
			}

			var accepted = _replaceResults.Count == 0 || _replaceResults.Dequeue();
			if ( accepted )
			{
				Current = replacement;
			}

			return accepted;
		}
	}
}
