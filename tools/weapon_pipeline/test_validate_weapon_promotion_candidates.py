"""Focused tests for the read-only weapon promotion candidate validator."""

from __future__ import annotations

import hashlib
import json
import tempfile
import unittest
from contextlib import contextmanager
from pathlib import Path

import build_weapon_promotion_bundle as promotion
import validate_weapon_promotion_candidates as validator


LEGACY_ACTIVE_GATE = Path(__file__).with_name("Test-WeaponAnimationHierarchyContract.ps1")
LEGACY_ACTIVE_GATE_BYTES = 39423
LEGACY_ACTIVE_GATE_SHA256 = "AD4C78B98DED0D93411D70F68D1760C3D15A2E5F4A79D9505DAB4D186BACC7F8"


class PromotionCandidateValidatorTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls._temporary = tempfile.TemporaryDirectory(prefix="dxrp_candidate_validator_test_")
        cls.bundle_root = Path(cls._temporary.name) / "bundle"
        promotion.build_temp_bundle(cls.bundle_root)

    @classmethod
    def tearDownClass(cls) -> None:
        cls._temporary.cleanup()

    def _candidate(self, key: str) -> dict:
        contract = validator.CANDIDATES[key]
        return json.loads((self.bundle_root / contract.relative_path).read_text(encoding="utf-8-sig"))

    @contextmanager
    def _fresh_report(self):
        report_path = self.bundle_root / validator.REPORT_FILE
        original = report_path.read_bytes()
        report = json.loads(original.decode("utf-8-sig"))
        try:
            yield self.bundle_root, report_path, report
        finally:
            if report_path.exists() or report_path.is_symlink():
                report_path.unlink()
            report_path.write_bytes(original)

    @staticmethod
    def _write_report(path: Path, report: dict) -> None:
        path.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")

    def test_exact_generated_bundle_passes(self) -> None:
        result = validator.validate_bundle(self.bundle_root)
        self.assertEqual(result["result"], "PASS")
        self.assertEqual(result["candidate_count"], 8)
        self.assertEqual(result["candidate_pins"], 8)
        self.assertEqual(result["destination_preimage_pins"], 8)
        self.assertEqual(result["support_asset_count"], 4)
        self.assertEqual(result["support_asset_pins"], 4)
        self.assertTrue(all(row["references_closed"] for row in result["candidates"].values()))
        aks_delta = result["candidates"]["aks74u_view"]["bolt_delta"]
        self.assertTrue(aks_delta["additional_renderer_root_preserved"])
        self.assertTrue(aks_delta["magazine_unchanged"])
        self.assertTrue(aks_delta["selector_trigger_stock_unchanged"])
        self.assertTrue(aks_delta["one_bolt_renderer_under_mp5_bolt"])
        self.assertTrue(result["aks74u_bolt_evidence"]["fbx_round_trip_closed"])
        deagle_delta = result["candidates"]["deserteagle_view"]["minimal_delta"]
        self.assertEqual(
            deagle_delta["added_guids"],
            [validator.DESERTEAGLE_WRAPPER_GUID],
        )
        self.assertEqual(deagle_delta["removed_guids"], [])
        self.assertTrue(deagle_delta["existing_guid_graph_preserved"])
        self.assertTrue(deagle_delta["existing_component_owners_preserved"])
        self.assertTrue(deagle_delta["existing_locals_preserved"])

    def test_aks74u_bolt_delta_is_fail_closed(self) -> None:
        base = json.loads(
            Path(
                json.loads(
                    (self.bundle_root / validator.REPORT_FILE).read_text(
                        encoding="utf-8-sig"
                    )
                )["intermediates"]["aks74u_visibility_base"]["path"]
            ).read_text(encoding="utf-8-sig")
        )
        candidate = self._candidate("aks74u_view")
        proof = validator._validate_aks74u_bolt_delta(candidate, base)
        self.assertTrue(proof["magazine_unchanged"])

        magazine_drift = self._candidate("aks74u_view")
        magazine = next(
            node
            for node in _nodes(magazine_drift["RootObject"])
            if node.get("Name") == "aks74u_magazine"
        )
        magazine["Position"] = "1,2,3"
        with self.assertRaisesRegex(validator.ContractError, "out-of-scope node"):
            validator._validate_aks74u_bolt_delta(magazine_drift, base)

        visibility_drift = self._candidate("aks74u_view")
        view_model = next(
            component
            for component in visibility_drift["RootObject"]["Components"]
            if component.get("__type") == "Dxura.RP.Game.ViewModel"
        )
        view_model["AdditionalRendererRoot"]["go"] = (
            "00000000-0000-0000-0000-000000000001"
        )
        with self.assertRaisesRegex(validator.ContractError, "visibility-root"):
            validator._validate_aks74u_bolt_delta(visibility_drift, base)

    def test_aks74u_support_and_scope_claims_are_fail_closed(self) -> None:
        with self._fresh_report() as (root, report_path, report):
            report["support_assets"]["aks74u_bolt_fbx"]["product_destination"] = (
                "game/Assets/evil.fbx"
            )
            self._write_report(report_path, report)
            with self.assertRaisesRegex(
                validator.ContractError, "create-only support metadata drifted"
            ):
                validator.validate_bundle(root)

        manifest_path = (
            self.bundle_root
            / "evidence/intermediates/aks74u_bolt_split/bundle_manifest.json"
        )
        original = manifest_path.read_bytes()
        try:
            manifest = json.loads(original)
            manifest["scope"]["selector_trigger_stock_split"] = True
            mutated = (json.dumps(manifest, indent=2) + "\n").encode("utf-8")
            manifest_path.write_bytes(mutated)
            with self._fresh_report() as (root, report_path, report):
                report["support_asset_evidence"]["bundle_manifest"]["bytes"] = len(mutated)
                report["support_asset_evidence"]["bundle_manifest"]["sha256"] = (
                    hashlib.sha256(mutated).hexdigest().upper()
                )
                self._write_report(report_path, report)
                with self.assertRaisesRegex(
                    validator.ContractError, "bolt manifest scope drifted"
                ):
                    validator.validate_bundle(root)
        finally:
            manifest_path.write_bytes(original)

    def test_deserteagle_existing_graph_and_locals_are_fail_closed(self) -> None:
        destination = json.loads(
            (
                validator.REPO_ROOT
                / validator.CANDIDATES["deserteagle_view"].destination
            ).read_text(encoding="utf-8-sig")
        )
        candidate = self._candidate("deserteagle_view")
        result = validator._validate_deserteagle_minimal_delta(
            candidate, destination
        )
        self.assertTrue(result["existing_locals_preserved"])

        local_drift = self._candidate("deserteagle_view")
        body = next(
            node
            for node in _nodes(local_drift["RootObject"])
            if node.get("Name") == "desert_eagle_body"
        )
        body["Position"] = "1,2,3"
        with self.assertRaisesRegex(
            validator.ContractError,
            "changed existing locals, GUID graph, references, or data",
        ):
            validator._validate_deserteagle_minimal_delta(
                local_drift, destination
            )

        wrapper_drift = self._candidate("deserteagle_view")
        wrapper = next(
            node
            for node in _nodes(wrapper_drift["RootObject"])
            if node.get("__guid") == validator.DESERTEAGLE_WRAPPER_GUID
        )
        wrapper["__guid"] = "00000000-0000-0000-0000-000000000001"
        with self.assertRaisesRegex(
            validator.ContractError, "added more than its one wrapper GUID"
        ):
            validator._validate_deserteagle_minimal_delta(
                wrapper_drift, destination
            )

    def test_old_broad_deserteagle_builder_or_visibility_scope_is_rejected(self) -> None:
        with self._fresh_report() as (root, report_path, report):
            helper = promotion.BUILDER_PINS["deserteagle_common_origin_helper"]
            report["builders"]["deserteagle"] = {
                "path": str(helper.path.resolve()),
                "bytes": helper.bytes,
                "sha256": helper.sha256,
            }
            self._write_report(report_path, report)
            with self.assertRaisesRegex(
                validator.ContractError,
                "does not pin the minimal Desert Eagle builder",
            ):
                validator.validate_bundle(root)

        with self._fresh_report() as (root, report_path, report):
            report["nested_builder_evidence"]["visibility_roots"][
                "promotion_bundle_scope"
            ]["excluded_families"] = []
            self._write_report(report_path, report)
            with self.assertRaisesRegex(
                validator.ContractError,
                "visibility-builder exclusion drifted",
            ):
                validator.validate_bundle(root)

    def test_legacy_active_animation_gate_remains_byte_pinned(self) -> None:
        data = LEGACY_ACTIVE_GATE.read_bytes()
        self.assertEqual(len(data), LEGACY_ACTIVE_GATE_BYTES)
        self.assertEqual(hashlib.sha256(data).hexdigest().upper(), LEGACY_ACTIVE_GATE_SHA256)

    def test_non_temp_bundle_root_is_rejected(self) -> None:
        with self.assertRaisesRegex(validator.ContractError, "strict child of system TEMP"):
            validator.assert_temp_bundle_root(validator.REPO_ROOT)

    def test_report_write_claim_is_rejected(self) -> None:
        with self._fresh_report() as (root, report_path, report):
            report["writes"]["game_tree"] = True
            self._write_report(report_path, report)
            with self.assertRaisesRegex(validator.ContractError, "writes contract drifted"):
                validator.validate_bundle(root)

    def test_report_version_acceptance_and_world_ik_claims_are_rejected(self) -> None:
        mutations = (
            (lambda report: report.__setitem__("version", 999), "version drifted"),
            (lambda report: report.pop("version"), "version drifted"),
            (
                lambda report: report["acceptance"].__setitem__("runtime", True),
                "acceptance contract drifted",
            ),
            (
                lambda report: report["world_weapon_left_hand_ik_candidates"].__setitem__("included", True),
                "world-weapon IK exclusion contract drifted",
            ),
            (
                lambda report: report["world_weapon_left_hand_ik_candidates"].__setitem__(
                    "transforms_generated_or_invented", True
                ),
                "world-weapon IK exclusion contract drifted",
            ),
        )
        for mutate, message in mutations:
            with self.subTest(message=message):
                with self._fresh_report() as (root, report_path, report):
                    mutate(report)
                    self._write_report(report_path, report)
                    with self.assertRaisesRegex(validator.ContractError, message):
                        validator.validate_bundle(root)

        with self._fresh_report() as (root, report_path, report):
            report.pop("writes")
            self._write_report(report_path, report)
            with self.assertRaisesRegex(validator.ContractError, "writes contract drifted"):
                validator.validate_bundle(root)

    def test_report_output_directory_drift_is_rejected(self) -> None:
        with self._fresh_report() as (root, report_path, report):
            report["output_directory"] = str(validator.REPO_ROOT)
            self._write_report(report_path, report)
            with self.assertRaisesRegex(validator.ContractError, "output_directory"):
                validator.validate_bundle(root)

    def test_candidate_destination_and_path_drift_are_rejected(self) -> None:
        with self._fresh_report() as (root, report_path, report):
            report["candidates"]["m1911_view"]["destination_preimage"] = "evil"
            self._write_report(report_path, report)
            with self.assertRaisesRegex(validator.ContractError, "destination-preimage identity drifted"):
                validator.validate_bundle(root)

        with self._fresh_report() as (root, report_path, report):
            report["candidates"]["m1911_view"]["path"] = str(validator.REPO_ROOT / "game")
            self._write_report(report_path, report)
            with self.assertRaisesRegex(validator.ContractError, "report candidate path drifted"):
                validator.validate_bundle(root)

    def test_inner_paths_must_remain_strict_children_of_bundle(self) -> None:
        with tempfile.TemporaryDirectory(prefix="dxrp_candidate_validator_escape_") as directory:
            parent = Path(directory)
            root = parent / "bundle"
            root.mkdir()
            outside = parent / "outside.prefab"
            outside.write_text("{}\n", encoding="utf-8")
            with self.assertRaisesRegex(validator.ContractError, "escaped bundle root"):
                validator._strict_child(root.resolve(), outside, "candidate")

    def test_report_symlink_escape_is_rejected_when_supported(self) -> None:
        with self._fresh_report() as (root, report_path, _report):
            outside = root.parent / "outside-report.json"
            outside.write_bytes(report_path.read_bytes())
            report_path.unlink()
            try:
                report_path.symlink_to(outside)
            except OSError as exc:
                self.skipTest(f"symlink privilege unavailable: {exc}")
            with self.assertRaisesRegex(validator.ContractError, "escaped bundle root"):
                validator.validate_bundle(root)

    def test_additional_renderer_root_escape_is_rejected(self) -> None:
        prefab = self._candidate("m870_view")
        view_model = next(
            component for component in prefab["RootObject"]["Components"]
            if component.get("__type") == "Dxura.RP.Game.ViewModel"
        )
        body = next(
            child
            for node in _nodes(prefab["RootObject"])
            for child in node.get("Children") or []
            if child.get("Name") == "m870_body"
        )
        body["Name"] = "weapon_root"
        view_model["AdditionalRendererRoot"]["go"] = body["__guid"]
        with self.assertRaisesRegex(validator.ContractError, "outside AdditionalRendererRoot"):
            validator._validate_candidate_structure("m870_view", prefab)

    def test_motion_owner_drift_is_rejected(self) -> None:
        prefab = self._candidate("m1911_view")
        magazine = next(node for node in _nodes(prefab["RootObject"]) if node.get("Name") == "magazine")
        custom = next(child for child in magazine["Children"] if child.get("Name") == "m1911_magazine")
        magazine["Children"].remove(custom)
        next(node for node in _nodes(prefab["RootObject"]) if node.get("Name") == "slide")["Children"].append(custom)
        with self.assertRaisesRegex(validator.ContractError, "motion owner slide != magazine"):
            validator._validate_candidate_structure("m1911_view", prefab)

    def test_duplicate_guid_and_dangling_reference_are_rejected(self) -> None:
        duplicate = self._candidate("ak47_view")
        children = duplicate["RootObject"]["Children"]
        children[1]["__guid"] = children[0]["__guid"]
        with self.assertRaisesRegex(validator.ContractError, "Duplicate/colliding GUID"):
            validator._validate_candidate_structure("ak47_view", duplicate)

        dangling = self._candidate("ak47_view")
        view_model = next(
            component for component in dangling["RootObject"]["Components"]
            if component.get("__type") == "Dxura.RP.Game.ViewModel"
        )
        view_model["Muzzle"]["go"] = "00000000-0000-0000-0000-000000000000"
        with self.assertRaisesRegex(validator.ContractError, "dangling gameobject reference"):
            validator._validate_candidate_structure("ak47_view", dangling)

    def test_component_reference_owner_mismatch_is_rejected(self) -> None:
        prefab = self._candidate("ak47_view")
        view_model = next(
            component for component in prefab["RootObject"]["Components"]
            if component.get("__type") == "Dxura.RP.Game.ViewModel"
        )
        real_owner = view_model["Arms"]["go"]
        other_owner = next(
            node["__guid"] for node in _nodes(prefab["RootObject"])
            if node["__guid"] != real_owner
        )
        view_model["Arms"]["go"] = other_owner
        with self.assertRaisesRegex(validator.ContractError, "component owner mismatch"):
            validator._validate_candidate_structure("ak47_view", prefab)


def _nodes(root: dict):
    yield root
    for child in root.get("Children") or []:
        yield from _nodes(child)


if __name__ == "__main__":
    unittest.main()
