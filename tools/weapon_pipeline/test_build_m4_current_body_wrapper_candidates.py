"""Focused contracts for the TEMP-only M4/current-body wrapper orchestrator."""

from __future__ import annotations

import json
import sys
import tempfile
import unittest
from dataclasses import replace
from pathlib import Path
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parent))

import build_m4_current_body_wrapper_candidates as builder


class M4CurrentBodyWrapperCandidateTests(unittest.TestCase):
    def test_live_external_pins_records_and_body_baselines(self) -> None:
        sample = builder.collect_compiled_bind_sample()
        _, baselines = builder.collect_body_baselines()

        self.assertEqual(sample.evidence["sample_kind"], "compiled_model_bind")
        self.assertEqual(
            sample.evidence["parser"],
            {
                "path": str(builder.PARSER_PIN.path.resolve()),
                "bytes": 108_603_232,
                "sha256": builder.PARSER_PIN.sha256,
                "version": builder.PARSER_VERSION,
            },
        )
        self.assertEqual(
            sample.evidence["compiled_model"],
            {
                "path": str(builder.M4_MODEL_PIN.path.resolve()),
                "bytes": 2_019_792,
                "sha256": builder.M4_MODEL_PIN.sha256,
            },
        )
        self.assertEqual(sample.evidence["bone_count"], 80)
        self.assertEqual(
            sample.evidence["data_stdout"],
            {
                "bytes": builder.EXPECTED_DATA_STDOUT_BYTES,
                "sha256": builder.EXPECTED_DATA_STDOUT_SHA256,
            },
        )
        self.assertEqual(
            sample.evidence["required_records_sha256"],
            builder.EXPECTED_REQUIRED_RECORDS_SHA256,
        )
        self.assertEqual(
            sample.evidence["required_parent_local_records"],
            builder._records_payload(builder.EXPECTED_DATA_RECORDS),
        )
        self.assertEqual(set(baselines), {"ar15", "sr25"})
        for family, baseline in baselines.items():
            self.assertTrue(builder._transform_is_identity(baseline.transform))
            self.assertEqual(
                baseline.evidence["baseline_label"], "current-body baseline"
            )
            self.assertIs(baseline.evidence["measured_and_accepted"], False)
            self.assertIs(baseline.evidence["geometry_derived"], False)
            self.assertIs(baseline.evidence["visually_accepted"], False)
            self.assertEqual(
                baseline.evidence["node_path"], builder.BODY_NODE_PATHS[family]
            )

    def test_exact_wrapper_locals_cover_every_moving_path_and_round_trip(self) -> None:
        _, baselines = builder.collect_body_baselines()
        families, derivations = builder.derive_wrapper_manifest(
            builder.EXPECTED_DATA_RECORDS, baselines
        )
        expected_by_bone = {
            "stock": {
                "position": "-5.628703,-0.0000028,-0.72176969",
                "quaternion": "-0.04619199,-0.026089,-0.99859184,0",
                "scale": "1,1,1",
            },
            "trigger": {
                "position": "-0.74836983,-0.00000073,0.22847937",
                "quaternion": "0.018448,-0.7387731,0.018447,0.67344909",
                "scale": "1,1,1",
            },
            "magazine": {
                "position": "-2.36220729,-0.00000393,-2.67715457",
                "quaternion": "0.01844799,-0.73877259,0.01844799,0.67344963",
                "scale": "1,1,1",
            },
            "mode_selector": {
                "position": "-1.30160256,0.56296352,0.55252997",
                "quaternion": "-0.04619199,-0.026089,-0.99859184,0",
                "scale": "1,1,1",
            },
            "bolt_flap": {
                "position": "0.47122571,2.35123171,0.89209158",
                "quaternion": "0.53581307,-0.57736007,-0.46011206,0.40970305",
                "scale": "1,1,1",
            },
            "bolt": {
                "position": "-1.51465058,0.00000086,-0.72178197",
                "quaternion": "0.02609,-0.04619099,0,0.99859186",
                "scale": "1,1,1",
            },
            "charging_handle": {
                "position": "2.87400807,0.000001,-1.69290874",
                "quaternion": "0.026089,-0.04619099,0,0.99859189",
                "scale": "1,1,1",
            },
        }
        for family, paths in builder.FAMILY_MOVING_BONES.items():
            self.assertEqual(set(families[family]), set(paths))
            for path, bone in paths.items():
                self.assertEqual(families[family][path], expected_by_bone[bone])
                self.assertLessEqual(
                    derivations[family][path]["round_trip_max_delta"], 1e-7
                )
                self.assertLessEqual(
                    derivations[family][path]["serialized_round_trip_max_delta"],
                    1e-7,
                )
                self.assertEqual(
                    derivations[family][path]["serialized_round_trip_P_times_W"],
                    {
                        "position": "0,0,0",
                        "quaternion": "0,0,0,1",
                        "scale": "1,1,1",
                    },
                )

    def test_arbitrary_transform_and_parent_records_are_rejected(self) -> None:
        arbitrary = dict(builder.EXPECTED_DATA_RECORDS)
        stock = arbitrary["stock"]
        arbitrary["stock"] = replace(
            stock,
            transform=replace(stock.transform, position=(123.0, 456.0, 789.0)),
        )
        with self.assertRaisesRegex(builder.ContractError, "record mismatch for stock"):
            builder.validate_required_records(arbitrary)

        wrong_parent = dict(builder.EXPECTED_DATA_RECORDS)
        magazine = wrong_parent["magazine"]
        wrong_parent["magazine"] = replace(magazine, parent="weapon_root")
        with self.assertRaisesRegex(
            builder.ContractError, "record mismatch for magazine"
        ):
            builder.validate_required_records(wrong_parent)

        sample = builder.collect_compiled_bind_sample()
        tampered_stdout = sample.data_stdout.replace(
            b"[ -5.538097, 0.051171, 1.236977 ]",
            b"[ 123.0, 0.051171, 1.236977 ]",
            1,
        )
        self.assertNotEqual(tampered_stdout, sample.data_stdout)
        with self.assertRaisesRegex(builder.ContractError, "record mismatch for stock"):
            builder.validate_required_records(
                builder.parse_compiled_data_records(tampered_stdout)
            )

    def test_tampered_file_and_version_pins_are_rejected(self) -> None:
        with self.assertRaisesRegex(builder.ContractError, "byte mismatch"):
            builder._verify_file_pin(
                replace(builder.PARSER_PIN, bytes=builder.PARSER_PIN.bytes + 1),
                "tampered parser",
            )
        with self.assertRaisesRegex(builder.ContractError, "SHA-256 mismatch"):
            builder._verify_file_pin(
                replace(builder.M4_MODEL_PIN, sha256="0" * 64),
                "tampered model",
            )
        with self.assertRaisesRegex(builder.ContractError, "SHA-256 mismatch"):
            builder._verify_file_pin(
                replace(
                    builder.SOURCE_PREFAB_PINS["ar15"], sha256="F" * 64
                ),
                "tampered source prefab",
            )
        with self.assertRaisesRegex(builder.ContractError, "SHA-256 mismatch"):
            builder._verify_file_pin(
                replace(builder.WRAPPER_BUILDER_PIN, sha256="0" * 64),
                "tampered wrapper builder",
            )
        with self.assertRaisesRegex(builder.ContractError, "version mismatch"):
            builder._validate_parser_version(b"Version: arbitrary\r\n")

    def test_nonidentity_body_baseline_is_rejected(self) -> None:
        family = "ar15"
        pin = builder.SOURCE_PREFAB_PINS[family]
        prefab = json.loads(pin.path.read_text(encoding="utf-8-sig"))
        nodes = builder._index_nodes(prefab, family)
        nodes[builder.BODY_NODE_PATHS[family]]["Position"] = "1,0,0"
        with self.assertRaisesRegex(builder.ContractError, "baseline B is not identity"):
            builder.verify_body_baseline(family, prefab, pin)

    def test_build_is_temp_only_exact_and_nonmutating(self) -> None:
        source_before = {
            family: pin.path.read_bytes()
            for family, pin in builder.SOURCE_PREFAB_PINS.items()
        }
        with tempfile.TemporaryDirectory(prefix="dxrp_m4_wrapper_test_") as directory:
            output = Path(directory) / "candidate"
            result = builder.build_temp_candidates(output)
            self.assertEqual(Path(result["output_directory"]), output.resolve())
            self.assertEqual(
                result["report"]["mode"], "TEMP_ONLY_NO_PRODUCT_WRITE_MODE"
            )
            self.assertIs(result["report"]["game_tree_written"], False)
            self.assertIs(result["report"]["measured_and_accepted"], False)
            self.assertEqual(
                result["report"]["transform_law"],
                "W = inverse(P) * B; P * W = B",
            )
            self.assertTrue(result["report"]["source_inputs_unchanged"])
            self.assertIn("NO product-tree write", result["report"]["proof_ceiling"])

            for family, expected in builder.EXPECTED_CANDIDATE_PINS.items():
                path = output / result["files"][family]
                self.assertTrue(path.is_relative_to(Path(tempfile.gettempdir())))
                data = path.read_bytes()
                self.assertEqual(len(data), expected.bytes)
                self.assertEqual(builder._sha256(data), expected.sha256)
                wrapper_report = result["report"]["wrapper_builder_reports"][family]
                self.assertEqual(wrapper_report["identity_moving_parts"], [])
                self.assertTrue(wrapper_report["original_hierarchy_preserved"])
                self.assertTrue(wrapper_report["donor_transforms_preserved"])
                self.assertTrue(wrapper_report["renderer_components_preserved"])
                self.assertTrue(wrapper_report["references_valid"])

            ar15 = json.loads(
                (output / result["files"]["ar15"]).read_text(encoding="utf-8")
            )
            nodes = builder._index_nodes(ar15, "ar15 candidate")
            view_models = [
                component
                for node in nodes.values()
                for component in node["Components"]
                if component.get("__type") == "Dxura.RP.Game.ViewModel"
            ]
            self.assertEqual(
                view_models[0]["AdditionalRendererRoot"],
                {
                    "_type": "gameobject",
                    "go": builder.AR15_ADDITIONAL_RENDERER_ROOT_GUID,
                },
            )
            renderer_paths = {
                component["__guid"]: path
                for path, node in nodes.items()
                for component in node["Components"]
                if component.get("__type") == "Sandbox.ModelRenderer"
            }
            self.assertEqual(
                set(renderer_paths), builder.AR15_CUSTOM_MODEL_RENDERER_GUIDS
            )
            self.assertEqual(len(renderer_paths), 7)
            self.assertTrue(
                all(
                    path.startswith(builder.AR15_ADDITIONAL_RENDERER_ROOT_PATH + "/")
                    for path in renderer_paths.values()
                )
            )
            self.assertEqual(
                result["report"]["ar15_additional_renderer_root"]
                ["custom_model_renderer_paths"],
                {guid: renderer_paths[guid] for guid in sorted(renderer_paths)},
            )

            sr25 = json.loads(
                (output / result["files"]["sr25"]).read_text(encoding="utf-8")
            )
            wrapper_builder, _ = builder._load_pinned_wrapper_builder()
            sr25_index = wrapper_builder.index_prefab(sr25)
            marker = sr25_index.nodes_by_guid[builder.SR25_EJECTION_PORT_GUID]
            self.assertEqual(marker.parent_guid, builder.SR25_BOLT_FLAP_GUID)
            self.assertEqual(
                marker.path,
                builder.SR25_BOLT_FLAP_PATH + "/sr25_ejection_port",
            )
            self.assertEqual(
                builder._node_transform(marker.node, "final marker"),
                builder.Transform(
                    position=(0.97884475, 8.24603638, 0.02181413),
                    quaternion=(
                        0.53581307,
                        -0.57736007,
                        -0.46011206,
                        0.40970305,
                    ),
                    scale=(1.0, 1.0, 1.0),
                ),
            )
            view_model = sr25_index.components_by_guid[
                builder.SR25_VIEW_MODEL_COMPONENT_GUID
            ]
            self.assertEqual(
                view_model["EjectionPort"],
                {
                    "_type": "gameobject",
                    "go": builder.SR25_EJECTION_PORT_GUID,
                },
            )
            donor = sr25_index.nodes_by_guid[builder.SR25_BOLT_FLAP_GUID].node
            self.assertEqual(
                builder._child_guid_tuple(donor, "final donor"),
                (
                    builder.SR25_BOLT_FLAP_WRAPPER_GUID,
                    builder.SR25_EJECTION_PORT_GUID,
                ),
            )
            ejection_report = result["report"]["sr25_ejection_owner"]
            self.assertLessEqual(
                ejection_report["serialized_round_trip_max_delta"], 1e-7
            )
            self.assertTrue(ejection_report["guid_set_preserved"])
            self.assertTrue(ejection_report["component_owners_preserved"])
            self.assertTrue(ejection_report["reference_preserved"])
            self.assertTrue(ejection_report["native_m4_source_unchanged"])
            self.assertTrue(
                ejection_report[
                    "renderer_wrapper_intermediate_original_hierarchy_preserved"
                ]
            )
            self.assertFalse(
                ejection_report["final_candidate_original_hierarchy_preserved"]
            )

            report_path = output / result["files"]["report"]
            self.assertEqual(
                json.loads(report_path.read_text(encoding="utf-8")), result["report"]
            )
            self.assertEqual(
                builder._sha256(report_path.read_bytes()),
                result["report_pin"]["sha256"],
            )

        source_after = {
            family: pin.path.read_bytes()
            for family, pin in builder.SOURCE_PREFAB_PINS.items()
        }
        self.assertEqual(source_after, source_before)

    def test_two_independent_builds_are_byte_identical(self) -> None:
        with tempfile.TemporaryDirectory(prefix="dxrp_m4_repeatability_") as directory:
            root = Path(directory)
            first = builder.build_temp_candidates(root / "first")
            second = builder.build_temp_candidates(root / "second")
            for family in builder.EXPECTED_CANDIDATE_PINS:
                first_bytes = (root / "first" / first["files"][family]).read_bytes()
                second_bytes = (root / "second" / second["files"][family]).read_bytes()
                self.assertEqual(first_bytes, second_bytes)

    def test_native_m4_ejection_owner_drift_is_rejected(self) -> None:
        native = json.loads(
            builder.M4_SOURCE_PREFAB_PIN.path.read_text(encoding="utf-8-sig")
        )
        wrapper_builder, _ = builder._load_pinned_wrapper_builder()
        native_index = wrapper_builder.index_prefab(native)
        native_index.components_by_guid[builder.M4_VIEW_MODEL_COMPONENT_GUID][
            "EjectionPort"
        ]["go"] = native["RootObject"]["__guid"]

        with tempfile.TemporaryDirectory(prefix="dxrp_m4_owner_drift_") as directory:
            root = Path(directory)
            tampered_path = root / "vm_m4a1.prefab"
            tampered_data = wrapper_builder._serialize_prefab(native, "\n")
            tampered_path.write_bytes(tampered_data)
            tampered_pin = builder.FilePin(
                path=tampered_path,
                bytes=len(tampered_data),
                sha256=builder._sha256(tampered_data),
            )
            with mock.patch.object(builder, "M4_SOURCE_PREFAB_PIN", tampered_pin):
                with self.assertRaisesRegex(
                    builder.ContractError,
                    "EjectionPort no longer points to bolt_flap",
                ):
                    builder.build_temp_candidates(root / "candidate")

    def test_repository_nonempty_and_product_switches_are_rejected(self) -> None:
        with self.assertRaisesRegex(builder.ContractError, "repository output"):
            builder.assert_temp_output_directory(
                builder.REPO_ROOT / "tools/weapon_pipeline/forbidden"
            )
        with tempfile.TemporaryDirectory(prefix="dxrp_m4_nonempty_") as directory:
            output = Path(directory) / "candidate"
            output.mkdir()
            (output / "occupied.txt").write_text("occupied", encoding="utf-8")
            with self.assertRaisesRegex(builder.ContractError, "must be empty"):
                builder.assert_temp_output_directory(output)

        destinations = {action.dest for action in builder._parser()._actions}
        self.assertNotIn("allow_game_write", destinations)
        self.assertNotIn("product", destinations)
        self.assertEqual(destinations, {"help", "output_dir"})


if __name__ == "__main__":
    unittest.main()
