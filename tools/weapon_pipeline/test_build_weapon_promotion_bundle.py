"""Focused tests for the fail-closed TEMP weapon promotion bundle."""

from __future__ import annotations

import copy
import importlib.util
import hashlib
import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock


MODULE_PATH = Path(__file__).with_name("build_weapon_promotion_bundle.py")
SPEC = importlib.util.spec_from_file_location("build_weapon_promotion_bundle", MODULE_PATH)
assert SPEC is not None and SPEC.loader is not None
builder = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = builder
SPEC.loader.exec_module(builder)


class PromotionBundleUnitTests(unittest.TestCase):
    def test_cli_exposes_only_temp_output(self) -> None:
        destinations = {action.dest for action in builder._parser()._actions}
        self.assertEqual(destinations, {"help", "output_dir"})
        for forbidden in (
            "product",
            "allow_game_write",
            "game",
            "docs",
            "portal",
            "git",
            "promote",
            "accept",
        ):
            self.assertNotIn(forbidden, destinations)

    def test_pin_verification_rejects_bytes_or_sha_drift(self) -> None:
        with tempfile.TemporaryDirectory(prefix="dxrp_bundle_pin_") as directory:
            path = Path(directory) / "probe.bin"
            path.write_bytes(b"pinned")
            wrong_size = builder.FilePin(path, 99, builder._sha256(b"pinned"))
            with self.assertRaisesRegex(builder.ContractError, "drifted"):
                builder._verify_file_pin(wrong_size, "probe")
            wrong_sha = builder.FilePin(path, 6, "0" * 64)
            with self.assertRaisesRegex(builder.ContractError, "drifted"):
                builder._verify_file_pin(wrong_sha, "probe")

    def test_preflight_builder_drift_fails_before_output_creation(self) -> None:
        original = builder.BUILDER_PINS["pipeline_io"]
        bad = builder.FilePin(original.path, original.bytes, "0" * 64)
        with tempfile.TemporaryDirectory(prefix="dxrp_bundle_parent_") as parent:
            output = Path(parent) / "must_not_exist"
            with mock.patch.dict(builder.BUILDER_PINS, {"pipeline_io": bad}):
                with self.assertRaisesRegex(builder.ContractError, "drifted"):
                    builder.build_temp_bundle(output)
            self.assertFalse(output.exists())

    def test_preflight_blender_or_create_only_drift_fails_before_output(self) -> None:
        blender = builder.SOURCE_PINS["blender_executable"]
        bad_blender = builder.FilePin(blender.path, blender.bytes, "0" * 64)
        with tempfile.TemporaryDirectory(prefix="dxrp_bundle_preflight_") as parent:
            output = Path(parent) / "blender_must_not_exist"
            with mock.patch.dict(
                builder.SOURCE_PINS, {"blender_executable": bad_blender}
            ):
                with self.assertRaisesRegex(builder.ContractError, "drifted"):
                    builder.build_temp_bundle(output)
            self.assertFalse(output.exists())

            original = builder.SUPPORT_ASSET_PINS["aks74u_bolt_vmdl"]
            collision = builder.SupportAssetPin(
                original.source_file,
                original.relative_path,
                original.bytes,
                original.sha256,
                "game/Assets/addons/lifepunch/lpweapons/aks74u/aks74u.vmat",
            )
            output = Path(parent) / "collision_must_not_exist"
            with mock.patch.dict(
                builder.SUPPORT_ASSET_PINS, {"aks74u_bolt_vmdl": collision}
            ):
                with self.assertRaisesRegex(
                    builder.ContractError, "destination already exists"
                ):
                    builder.build_temp_bundle(output)
            self.assertFalse(output.exists())

    def test_temp_guard_rejects_repository_and_temp_root(self) -> None:
        with self.assertRaisesRegex(builder.ContractError, "Repository output"):
            builder.assert_temp_output_directory(
                builder.REPO_ROOT / "tools/weapon_pipeline/not_allowed"
            )
        with self.assertRaisesRegex(builder.ContractError, "strict child"):
            builder.assert_temp_output_directory(builder.SYSTEM_TEMP_ROOT)

    def test_structural_diff_is_deterministic_and_json_pointer_escaped(self) -> None:
        before = {"a/b": {"~key": 1}, "drop": [1, 2], "same": True}
        after = {"a/b": {"~key": 2}, "drop": [1], "add": {"x": 1}, "same": True}
        first = builder.structural_diff(before, after)
        second = builder.structural_diff(before, after)
        self.assertEqual(first, second)
        self.assertEqual(
            first,
            [
                {
                    "op": "add",
                    "path": "/add",
                    "after": {
                        "kind": "object",
                        "keys": 1,
                        "canonical_bytes": 7,
                        "canonical_sha256": builder._sha256(b'{"x":1}'),
                    },
                },
                {
                    "op": "replace",
                    "path": "/a~1b/~0key",
                    "before": 1,
                    "after": 2,
                },
                {"op": "remove", "path": "/drop/1", "before": 2},
            ],
        )

    def test_candidate_and_destination_sets_are_one_to_one(self) -> None:
        self.assertEqual(
            set(builder.CANDIDATE_PINS), set(builder.DESTINATION_PREIMAGE_PINS)
        )
        for name, pin in builder.CANDIDATE_PINS.items():
            self.assertEqual(pin.destination, name)
            self.assertEqual(len(pin.sha256), 64)
            self.assertGreater(pin.bytes, 0)

    def test_change_classifier_keeps_identity_noise_separate(self) -> None:
        rows = [
            ({"op": "replace", "path": "/RootObject/__guid"}, "guid_reference"),
            ({"op": "replace", "path": "/RootObject/Position"}, "transform"),
            ({"op": "replace", "path": "/RootObject/Name"}, "metadata"),
            ({"op": "replace", "path": "/RootObject/__version"}, "schema_version"),
            ({"op": "add", "path": "/RootObject/Children/0"}, "structure"),
            ({"op": "replace", "path": "/RootObject/Enabled"}, "other"),
        ]
        for change, expected in rows:
            with self.subTest(change=change):
                self.assertEqual(builder.classify_change(change), expected)
        with self.assertRaisesRegex(builder.ContractError, "Malformed"):
            builder.classify_change({"op": "replace"})


class PromotionBundleIntegrationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls._temp = tempfile.TemporaryDirectory(prefix="dxrp_bundle_integration_")
        cls.output = Path(cls._temp.name) / "bundle"
        cls.result = builder.build_temp_bundle(cls.output)
        cls.report = cls.result["report"]

    @classmethod
    def tearDownClass(cls) -> None:
        cls._temp.cleanup()

    def test_exact_candidates_are_regenerated(self) -> None:
        self.assertEqual(set(self.report["candidates"]), set(builder.CANDIDATE_PINS))
        for name, expected in builder.CANDIDATE_PINS.items():
            actual = self.report["candidates"][name]
            self.assertEqual(actual["bytes"], expected.bytes)
            self.assertEqual(actual["sha256"], expected.sha256)
            path = Path(actual["path"])
            self.assertTrue(path.is_file())
            self.assertTrue(builder._is_within(path.resolve(), self.output.resolve()))
            self.assertEqual(path.read_bytes(), builder._verify_generated_pin(
                path, expected.bytes, expected.sha256, name
            ))

    def test_candidate_tree_contains_only_eight_promotable_prefabs(self) -> None:
        actual = {
            path.resolve()
            for path in (self.output / "candidates").rglob("*.prefab")
        }
        reported = {
            Path(row["path"]).resolve()
            for row in self.report["candidates"].values()
        }
        self.assertEqual(actual, reported)
        self.assertEqual(len(actual), 8)
        for row in self.report["candidates"].values():
            self.assertTrue(row["promotable"])
            self.assertEqual(row["role"], "PROMOTABLE_REVIEW_CANDIDATE")

    def test_intermediates_are_explicit_non_promotable_evidence(self) -> None:
        self.assertEqual(
            set(self.report["intermediates"]),
            {
                "aks74u",
                "aks74u_bolt_split",
                "aks74u_visibility_base",
                "deserteagle",
                "m1911",
            },
        )
        actual = {
            path.resolve()
            for path in (self.output / "evidence" / "intermediates").rglob(
                "*.prefab"
            )
        }
        reported = {
            Path(row["path"]).resolve()
            for row in self.report["intermediates"].values()
        }
        self.assertEqual(actual, reported)
        self.assertEqual(len(actual), 5)
        expected_roles = {
            "aks74u": "NON_PROMOTABLE_VISIBILITY_INPUT_EVIDENCE",
            "aks74u_bolt_split": "NON_PROMOTABLE_BOLT_BUILDER_EVIDENCE",
            "aks74u_visibility_base": (
                "NON_PROMOTABLE_VISIBILITY_ROOT_BASE_EVIDENCE"
            ),
            "deserteagle": "NON_PROMOTABLE_MINIMAL_BUILDER_EVIDENCE",
            "m1911": "NON_PROMOTABLE_VISIBILITY_INPUT_EVIDENCE",
        }
        for family, row in self.report["intermediates"].items():
            self.assertFalse(row["promotable"])
            self.assertEqual(row["role"], expected_roles[family])
            self.assertTrue(row["relative_path"].startswith("evidence/intermediates/"))

    def test_aks74u_bolt_support_assets_are_exact_create_only_outputs(self) -> None:
        self.assertEqual(set(self.report["support_assets"]), set(builder.SUPPORT_ASSET_PINS))
        actual = {
            path.resolve()
            for path in (self.output / "support_assets").rglob("*")
            if path.is_file()
        }
        reported = {
            Path(row["path"]).resolve()
            for row in self.report["support_assets"].values()
        }
        self.assertEqual(actual, reported)
        self.assertEqual(len(actual), 4)
        for name, expected in builder.SUPPORT_ASSET_PINS.items():
            row = self.report["support_assets"][name]
            path = Path(row["path"])
            source = self.output / row["source_relative_path"]
            self.assertEqual(row["relative_path"], expected.relative_path)
            self.assertEqual(row["product_destination"], expected.destination)
            self.assertEqual(row["bytes"], expected.bytes)
            self.assertEqual(row["sha256"], expected.sha256)
            self.assertEqual(path.read_bytes(), source.read_bytes())
            self.assertTrue(row["create_only"])
            self.assertFalse(row["product_destination_exists"])
            self.assertFalse(row["product_write_performed"])
            self.assertFalse((builder.REPO_ROOT / expected.destination).exists())

        topology_metadata = self.report["support_asset_evidence"]["topology_report"]
        topology_path = Path(topology_metadata["path"])
        expected_bytes, expected_sha = builder.AKS_BOLT_TOPOLOGY_PIN
        self.assertEqual(topology_path.stat().st_size, expected_bytes)
        self.assertEqual(builder._sha256(topology_path.read_bytes()), expected_sha)
        topology = json.loads(topology_path.read_text(encoding="utf-8"))
        self.assertEqual(topology["partition"]["bolt_faces"], 830)
        self.assertEqual(topology["partition"]["body_minus_bolt_faces"], 21_001)
        self.assertTrue(
            topology["post_export_reimport"][
                "face_position_uv_material_multiset_closure"
            ]
        )
        self.assertFalse(
            topology["post_export_reimport"]["duplicate_bolt_in_body"]
        )

    def test_aks74u_bolt_manifest_is_generated_from_this_bundle(self) -> None:
        generated = self.report["generated_aks74u_bolt_inputs"]
        self.assertEqual(
            set(generated),
            {
                "authoritative_fbx_probe",
                "source_topology_evidence",
                "compiled_mp5_data_probe",
                "input_manifest",
            },
        )
        for row in generated.values():
            self.assertTrue(
                builder._is_within(Path(row["path"]).resolve(), self.output.resolve())
            )
        manifest = json.loads(
            Path(generated["input_manifest"]["path"]).read_text(encoding="utf-8")
        )
        base = manifest["inputs"]["base_fit_candidate_prefab"]
        self.assertEqual(
            Path(base["path"]).resolve(),
            Path(
                self.report["intermediates"]["aks74u_visibility_base"]["path"]
            ).resolve(),
        )
        for label in (
            "authoritative_fbx_probe",
            "source_topology_evidence",
            "compiled_mp5_data_probe",
        ):
            self.assertEqual(
                Path(manifest["inputs"][label]["path"]).resolve(),
                Path(generated[label]["path"]).resolve(),
            )

    def test_aks74u_final_is_visibility_root_then_bolt_split_only(self) -> None:
        final = json.loads(
            Path(self.report["candidates"]["aks74u_view"]["path"]).read_text(
                encoding="utf-8"
            )
        )
        base = json.loads(
            Path(
                self.report["intermediates"]["aks74u_visibility_base"]["path"]
            ).read_text(encoding="utf-8")
        )
        final_view_model = next(
            component
            for component in final["RootObject"]["Components"]
            if component.get("__type") == "Dxura.RP.Game.ViewModel"
        )
        base_view_model = next(
            component
            for component in base["RootObject"]["Components"]
            if component.get("__type") == "Dxura.RP.Game.ViewModel"
        )
        self.assertEqual(
            final_view_model["AdditionalRendererRoot"],
            base_view_model["AdditionalRendererRoot"],
        )
        proof = self.report["nested_builder_evidence"]["aks74u_bolt_split"]
        self.assertTrue(proof["first_person_prefab_graph"]["magazine_mapping_byte_semantics_preserved"])
        self.assertTrue(proof["first_person_prefab_graph"]["selector_trigger_stock_unchanged"])
        self.assertTrue(proof["first_person_prefab_graph"]["only_expected_graph_changes"])
        bolt_models = [
            component
            for node in _nodes(final["RootObject"])
            for component in node.get("Components") or []
            if component.get("Model") == "addons/lifepunch/lpweapons/aks74u/aks74u_bolt.vmdl"
        ]
        self.assertEqual(len(bolt_models), 1)
        self.assertEqual(
            proof["scope"],
            {
                "first_person_only": True,
                "split_only_tag_bolt": True,
                "selector_trigger_stock_split": False,
                "third_person_created_or_modified": False,
                "magazine_mapping_changed": False,
            },
        )

    def test_deserteagle_preserves_active_destination_root_name(self) -> None:
        candidate = json.loads(
            Path(self.report["candidates"]["deserteagle_view"]["path"])
            .read_text(encoding="utf-8")
        )
        destination = json.loads(
            builder.DESTINATION_PREIMAGE_PINS["deserteagle_view"].path.read_text(
                encoding="utf-8"
            )
        )
        self.assertEqual(
            candidate["RootObject"]["Name"], destination["RootObject"]["Name"]
        )
        self.assertEqual(candidate["RootObject"]["Name"], "vm_desert_eagle")

    def test_deserteagle_is_the_minimal_active_delta_only(self) -> None:
        candidate_path = Path(
            self.report["candidates"]["deserteagle_view"]["path"]
        )
        candidate = json.loads(candidate_path.read_text(encoding="utf-8"))
        destination = json.loads(
            builder.DESTINATION_PREIMAGE_PINS["deserteagle_view"].path.read_text(
                encoding="utf-8"
            )
        )
        evidence = self.report["nested_builder_evidence"]["deserteagle"]
        proof = evidence["proof"]
        wrapper_guid = proof["wrapper_guid"]
        view_model_guid = "f1e6fcd8-0f8f-4757-b164-300ceeb9a2c5"
        magazine_parent_guid = proof["wrapper_parent_guid"]

        before_guids = _all_guids(destination)
        after_guids = _all_guids(candidate)
        self.assertEqual(after_guids - before_guids, {wrapper_guid})
        self.assertFalse(before_guids - after_guids)
        self.assertEqual(candidate_path.read_bytes(), Path(evidence["output"]["path"]).read_bytes())

        stripped = copy.deepcopy(candidate)
        view_model = _component_by_guid(stripped, view_model_guid)
        self.assertEqual(
            view_model.pop("AdditionalRendererRoot"),
            {"_type": "gameobject", "go": proof["additional_renderer_root"]},
        )
        magazine_parent = _node_by_guid(stripped, magazine_parent_guid)
        wrapper = magazine_parent["Children"][0]
        self.assertEqual(wrapper["__guid"], wrapper_guid)
        self.assertEqual(wrapper["Components"], [])
        self.assertEqual(
            [child["__guid"] for child in wrapper["Children"]],
            [proof["wrapper_child_existing_guid"]],
        )
        magazine_parent["Children"] = wrapper["Children"]
        self.assertEqual(stripped, destination)

    def test_old_broad_deserteagle_candidate_no_longer_feeds_promotion(self) -> None:
        self.assertEqual(
            builder.BUILDER_PINS["deserteagle"].path.name,
            "build_deserteagle_minimal_correction_candidate.py",
        )
        helper = builder.BUILDER_PINS["deserteagle_common_origin_helper"]
        self.assertEqual(helper.path.name, "build_deserteagle_common_origin_candidate.py")
        scope = self.report["nested_builder_evidence"]["visibility_roots"][
            "promotion_bundle_scope"
        ]
        self.assertEqual(scope["excluded_families"], ["deserteagle"])
        visibility_families = {
            row["family"]
            for row in self.report["nested_builder_evidence"]["visibility_roots"][
                "candidates"
            ]
        }
        self.assertNotIn("deserteagle", visibility_families)
        self.assertEqual(
            self.report["nested_builder_evidence"]["deserteagle"]["proof"][
                "semantic_change"
            ],
            "insert one magazine bind wrapper and add AdditionalRendererRoot only",
        )

    def test_executing_orchestrator_is_exactly_pinned(self) -> None:
        raw = MODULE_PATH.read_bytes()
        self.assertEqual(self.report["orchestrator"]["path"], str(MODULE_PATH.resolve()))
        self.assertEqual(self.report["orchestrator"]["bytes"], len(raw))
        self.assertEqual(
            self.report["orchestrator"]["sha256"],
            hashlib.sha256(raw).hexdigest().upper(),
        )

    def test_generic_world_ik_candidates_are_explicitly_excluded(self) -> None:
        coverage = self.report["world_weapon_left_hand_ik_candidates"]
        self.assertFalse(coverage["included"])
        self.assertEqual(coverage["candidate_count"], 0)
        self.assertFalse(coverage["accepted_transforms_supplied"])
        self.assertFalse(coverage["transforms_generated_or_invented"])

    def test_every_builder_source_and_preimage_is_byte_pinned(self) -> None:
        self.assertEqual(set(self.report["builders"]), set(builder.BUILDER_PINS))
        self.assertEqual(set(self.report["sources"]), set(builder.SOURCE_PINS))
        self.assertEqual(
            set(self.report["destination_preimages"]),
            set(builder.DESTINATION_PREIMAGE_PINS),
        )
        for section, pins in (
            ("builders", builder.BUILDER_PINS),
            ("sources", builder.SOURCE_PINS),
            ("destination_preimages", builder.DESTINATION_PREIMAGE_PINS),
        ):
            for name, expected in pins.items():
                actual = self.report[section][name]
                self.assertEqual(actual["bytes"], expected.bytes)
                self.assertEqual(actual["sha256"], expected.sha256)

    def test_generated_manifests_are_exact_and_inside_bundle(self) -> None:
        self.assertEqual(
            set(self.report["generated_bind_manifests"]), set(builder.MANIFEST_PINS)
        )
        for name, (expected_bytes, expected_sha) in builder.MANIFEST_PINS.items():
            actual = self.report["generated_bind_manifests"][name]
            self.assertEqual(actual["bytes"], expected_bytes)
            self.assertEqual(actual["sha256"], expected_sha)
            self.assertTrue(
                builder._is_within(Path(actual["path"]).resolve(), self.output.resolve())
            )

    def test_structural_diffs_are_reviewable_json_and_pinned(self) -> None:
        self.assertEqual(
            set(self.report["structural_diffs"]), set(builder.CANDIDATE_PINS)
        )
        for name, pin in self.report["structural_diffs"].items():
            path = Path(pin["path"])
            raw = path.read_bytes()
            self.assertEqual(len(raw), pin["bytes"])
            self.assertEqual(builder._sha256(raw), pin["sha256"])
            document = json.loads(raw.decode("utf-8"))
            self.assertEqual(document["kind"], "unified_json_structural_diff_summary")
            self.assertEqual(document["candidate"], name)
            self.assertEqual(document["change_count"], len(document["changes"]))
            self.assertGreater(document["change_count"], 0)
            self.assertEqual(
                sum(document["operation_counts"].values()), document["change_count"]
            )
            self.assertEqual(
                sum(document["change_categories"].values()),
                document["change_count"],
            )
            self.assertEqual(
                document["change_categories"],
                self.report["structural_diffs"][name]["change_categories"],
            )
            self.assertEqual(
                document["acceptance"],
                {"measured": False, "visual": False, "runtime": False, "portal": False},
            )

    def test_change_categories_match_the_reviewed_candidate_shapes(self) -> None:
        expected = {
            "ak47_view": {
                "guid_reference": 0,
                "transform": 0,
                "metadata": 0,
                "schema_version": 0,
                "structure": 1,
                "other": 0,
            },
            "aks74u_view": {
                "guid_reference": 0,
                "transform": 5,
                "metadata": 0,
                "schema_version": 0,
                "structure": 2,
                "other": 1,
            },
            "aks74u_world": {
                "guid_reference": 0,
                "transform": 2,
                "metadata": 0,
                "schema_version": 0,
                "structure": 0,
                "other": 0,
            },
            "ar15_view": {
                "guid_reference": 0,
                "transform": 0,
                "metadata": 0,
                "schema_version": 0,
                "structure": 15,
                "other": 0,
            },
            "deserteagle_view": {
                "guid_reference": 1,
                "transform": 3,
                "metadata": 0,
                "schema_version": 0,
                "structure": 3,
                "other": 1,
            },
            "m1911_view": {
                "guid_reference": 0,
                "transform": 3,
                "metadata": 1,
                "schema_version": 1,
                "structure": 14,
                "other": 0,
            },
            "m870_view": {
                "guid_reference": 0,
                "transform": 0,
                "metadata": 0,
                "schema_version": 0,
                "structure": 1,
                "other": 0,
            },
            "sr25_view": {
                "guid_reference": 1,
                "transform": 1,
                "metadata": 0,
                "schema_version": 0,
                "structure": 23,
                "other": 1,
            },
        }
        actual = {
            name: row["change_categories"]
            for name, row in self.report["structural_diffs"].items()
        }
        self.assertEqual(actual, expected)

    def test_structural_diff_bytes_are_stable_across_temp_roots(self) -> None:
        second_output = Path(self._temp.name) / "bundle_second"
        second = builder.build_temp_bundle(second_output)["report"]
        for name in builder.CANDIDATE_PINS:
            self.assertEqual(
                self.report["structural_diffs"][name]["bytes"],
                second["structural_diffs"][name]["bytes"],
            )
            self.assertEqual(
                self.report["structural_diffs"][name]["sha256"],
                second["structural_diffs"][name]["sha256"],
            )

    def test_report_has_no_acceptance_or_product_write_claim(self) -> None:
        self.assertEqual(
            self.report["acceptance"],
            {"measured": False, "visual": False, "runtime": False, "portal": False},
        )
        self.assertEqual(
            self.report["writes"],
            {
                "temp_only": True,
                "game_tree": False,
                "docs": False,
                "portal": False,
                "git": False,
            },
        )
        self.assertFalse(
            self.report["nested_builder_evidence"]["aks74u"]["first_person"][
                "static_magazine_motion_compatibility"
            ]["rendered_motion_verified"]
        )
        self.assertFalse(
            self.report["nested_builder_evidence"]["m4_current_body"][
                "measured_and_accepted"
            ]
        )
        ejection = self.report["nested_builder_evidence"]["m4_current_body"][
            "sr25_ejection_owner"
        ]
        self.assertEqual(
            ejection["new_parent_guid"],
            "57250000-5a25-4000-8000-000000000036",
        )
        self.assertLessEqual(ejection["serialized_round_trip_max_delta"], 1e-7)
        self.assertTrue(ejection["reference_preserved"])
        self.assertFalse(ejection["final_candidate_original_hierarchy_preserved"])

    def test_visibility_builder_evidence_is_complete_and_temp_only(self) -> None:
        evidence = self.report["nested_builder_evidence"]["visibility_roots"]
        self.assertEqual(evidence["candidate_count"], 4)
        rows = {row["family"]: row for row in evidence["candidates"]}
        self.assertEqual(
            set(rows), {"ak47", "aks74u", "m1911", "m870"}
        )
        self.assertEqual(rows["ak47"]["source_kind"], "active")
        self.assertEqual(rows["m870"]["source_kind"], "active")
        for family in ("aks74u", "m1911"):
            self.assertEqual(rows[family]["source_kind"], "generated")
        self.assertEqual(
            evidence["writes"],
            {
                "temp_only": True,
                "game": False,
                "docs": False,
                "portal": False,
                "git": False,
                "scene": False,
            },
        )
        visibility_root = (self.output / "candidates" / "visibility_roots").resolve()
        for family, row in rows.items():
            expected_root = (
                self.output / "evidence" / "intermediates" / "aks74u_visibility_base"
                if family == "aks74u"
                else visibility_root
            ).resolve()
            self.assertTrue(
                builder._is_within(Path(row["output"]["path"]).resolve(), expected_root)
            )

    def test_consolidated_report_is_pinned_and_temp_only(self) -> None:
        pin = self.result["report_pin"]
        path = Path(pin["path"])
        self.assertEqual(path, self.output / builder.REPORT_FILE)
        raw = path.read_bytes()
        self.assertEqual(len(raw), pin["bytes"])
        self.assertEqual(builder._sha256(raw), pin["sha256"])
        self.assertTrue(builder._is_within(path.resolve(), builder.SYSTEM_TEMP_ROOT))
        self.assertFalse(builder._is_within(path.resolve(), builder.REPO_ROOT.resolve()))


def _nodes(root: dict):
    yield root
    for child in root.get("Children") or []:
        yield from _nodes(child)


def _all_guids(prefab: dict) -> set[str]:
    result: set[str] = set()
    for node in _nodes(prefab["RootObject"]):
        result.add(node["__guid"])
        result.update(component["__guid"] for component in node.get("Components") or [])
    return result


def _node_by_guid(prefab: dict, guid: str) -> dict:
    return next(node for node in _nodes(prefab["RootObject"]) if node["__guid"] == guid)


def _component_by_guid(prefab: dict, guid: str) -> dict:
    return next(
        component
        for node in _nodes(prefab["RootObject"])
        for component in node.get("Components") or []
        if component["__guid"] == guid
    )


if __name__ == "__main__":
    unittest.main()
