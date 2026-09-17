"""Focused tests for the TEMP-ONLY Desert Eagle common-origin builder."""

from __future__ import annotations

import copy
import hashlib
import json
import tempfile
import unittest
from pathlib import Path

import build_deserteagle_common_origin_candidate as builder


class DesertEagleCommonOriginCandidateTests(unittest.TestCase):
    def _manifest_payload(self) -> dict:
        return {
            "version": 1,
            "pose": "idle-bind",
            "coordinate_space": "donor-view",
            "pins": {
                "donor": {
                    "bytes": builder.EXPECTED_DONOR_BYTES,
                    "sha256": builder.EXPECTED_DONOR_SHA256,
                },
                "source": {
                    "bytes": builder.EXPECTED_SOURCE_BYTES,
                    "sha256": builder.EXPECTED_SOURCE_SHA256,
                },
            },
            "transforms": {
                "B_root": {
                    "position": [0.0, 0.0, 0.0],
                    "quaternion": [0.0, 0.0, 0.0, 1.0],
                    "scale": [1.0, 1.0, 1.0],
                },
                "B_slide": {
                    "position": [1.25, -0.5, 2.0],
                    "quaternion": [0.0, 0.0, 0.7071067811865475, 0.7071067811865476],
                    "scale": [1.0, 1.0, 1.0],
                },
                "B_mag": {
                    "position": [-0.75, 1.5, -2.25],
                    "quaternion": [0.0, 0.3826834323650898, 0.0, 0.9238795325112867],
                    "scale": [1.0, 1.0, 1.0],
                },
            },
        }

    def _write_manifest(self, directory: Path, payload: dict | None = None) -> tuple[Path, str]:
        path = directory / "idle-bind.json"
        raw = json.dumps(
            payload if payload is not None else self._manifest_payload(),
            sort_keys=True,
            separators=(",", ":"),
        ).encode("utf-8")
        path.write_bytes(raw)
        return path, hashlib.sha256(raw).hexdigest().upper()

    def _build(self, directory: Path) -> tuple[dict, bytes]:
        manifest_path, manifest_sha = self._write_manifest(directory)
        output_directory = directory / "candidate"
        report = builder.build_candidate(
            output_directory=output_directory,
            idle_bind_path=manifest_path,
            idle_bind_sha256=manifest_sha,
        )
        output = Path(report["output"]["path"])
        return report, output.read_bytes()

    def test_geometry_envelope_derives_one_expected_assembly_A(self) -> None:
        assembly = builder.derive_geometry_assembly()
        self.assertAlmostEqual(assembly.scale[0], 0.0347332382892338, places=15)
        self.assertEqual(assembly.scale, (assembly.scale[0],) * 3)
        self.assertEqual(assembly.quaternion, (0.0, 0.0, 0.0, 1.0))
        for actual, expected in zip(
            assembly.position,
            (0.868258293095228, 0.098958812509626, 0.522650032445715),
        ):
            self.assertAlmostEqual(actual, expected, places=14)
        self.assertEqual(
            hashlib.sha256(builder.GEOMETRY_BOUNDS_RECORD).hexdigest().upper(),
            builder.EXPECTED_GEOMETRY_BOUNDS_SHA256,
        )

    def test_manifest_is_mandatory_complete_and_externally_pinned(self) -> None:
        donor = builder.DEFAULT_DONOR.read_bytes()
        source = builder.DEFAULT_SOURCE.read_bytes()
        with tempfile.TemporaryDirectory(prefix="dxrp_deagle_manifest_") as raw_directory:
            directory = Path(raw_directory)
            path, actual_sha = self._write_manifest(directory)
            with self.assertRaisesRegex(builder.ContractError, "SHA-256 mismatch"):
                builder.load_idle_bind_manifest(
                    path,
                    "0" * 64,
                    donor_bytes=donor,
                    source_bytes=source,
                )

            incomplete = self._manifest_payload()
            del incomplete["transforms"]["B_mag"]
            path, incomplete_sha = self._write_manifest(directory, incomplete)
            with self.assertRaisesRegex(builder.ContractError, "requires B_root/B_slide/B_mag"):
                builder.load_idle_bind_manifest(
                    path,
                    incomplete_sha,
                    donor_bytes=donor,
                    source_bytes=source,
                )

            wrong_context = self._manifest_payload()
            wrong_context["pins"]["source"]["sha256"] = "F" * 64
            path, wrong_context_sha = self._write_manifest(directory, wrong_context)
            with self.assertRaisesRegex(builder.ContractError, "source SHA-256 pin mismatch"):
                builder.load_idle_bind_manifest(
                    path,
                    wrong_context_sha,
                    donor_bytes=donor,
                    source_bytes=source,
                )

            self.assertNotEqual(actual_sha, incomplete_sha)

    def test_compiled_bind_pose_is_explicitly_supported(self) -> None:
        donor = builder.DEFAULT_DONOR.read_bytes()
        source = builder.DEFAULT_SOURCE.read_bytes()
        with tempfile.TemporaryDirectory(prefix="dxrp_deagle_compiled_bind_") as raw_directory:
            directory = Path(raw_directory)
            payload = builder.compiled_bind_manifest_template()
            path, manifest_sha = self._write_manifest(directory, payload)
            matrices, pin = builder.load_idle_bind_manifest(
                path,
                manifest_sha,
                donor_bytes=donor,
                source_bytes=source,
            )
            self.assertEqual(set(matrices), {"B_root", "B_slide", "B_mag"})
            self.assertEqual(pin["pose"], "compiled-bind")
            self.assertEqual(
                pin["compiled_evidence"], builder.compiled_bind_evidence_template()
            )
            report = builder.build_candidate(
                output_directory=directory / "compiled-candidate",
                idle_bind_path=path,
                idle_bind_sha256=manifest_sha,
            )
            self.assertIn(
                "not observed IdlePose", report["proof_ceiling"]
            )

            arbitrary = copy.deepcopy(payload)
            arbitrary["transforms"]["B_root"]["matrix"][0][3] = 123
            path, arbitrary_sha = self._write_manifest(directory, arbitrary)
            with self.assertRaisesRegex(builder.ContractError, "pinned DATA records"):
                builder.load_idle_bind_manifest(
                    path,
                    arbitrary_sha,
                    donor_bytes=donor,
                    source_bytes=source,
                )

            unpinned = copy.deepcopy(payload)
            unpinned["evidence"]["compiled_model"]["sha256"] = "0" * 64
            path, unpinned_sha = self._write_manifest(directory, unpinned)
            with self.assertRaisesRegex(builder.ContractError, "pinned parser/model"):
                builder.load_idle_bind_manifest(
                    path,
                    unpinned_sha,
                    donor_bytes=donor,
                    source_bytes=source,
                )

    def test_copied_locals_across_distinct_moving_parents_are_rejected(self) -> None:
        assembly_matrix = builder.transform_matrix(builder.derive_geometry_assembly())
        bind_matrices = {
            "B_root": builder._identity_matrix(),
            "B_slide": builder.transform_matrix(
                builder.Transform(
                    position=(1.0, 0.0, 0.0),
                    quaternion=(0.0, 0.0, 0.0, 1.0),
                    scale=(1.0, 1.0, 1.0),
                )
            ),
            "B_mag": builder.transform_matrix(
                builder.Transform(
                    position=(0.0, 1.0, 0.0),
                    quaternion=(0.0, 0.0, 0.0, 1.0),
                    scale=(1.0, 1.0, 1.0),
                )
            ),
        }
        copied_locals = {key: assembly_matrix for key in bind_matrices}
        with self.assertRaisesRegex(builder.ContractError, "copied local transform rejected"):
            builder.assert_local_reconstruction(
                bind_matrices, copied_locals, assembly_matrix
            )

    def test_build_is_deterministic_and_preserves_donor_contract(self) -> None:
        donor_bytes_before = builder.DEFAULT_DONOR.read_bytes()
        source_bytes_before = builder.DEFAULT_SOURCE.read_bytes()
        donor = json.loads(donor_bytes_before.decode("utf-8-sig"))
        donor_index = builder.index_prefab(donor)
        source = json.loads(source_bytes_before.decode("utf-8-sig"))
        source_index = builder.index_prefab(source)

        with tempfile.TemporaryDirectory(prefix="dxrp_deagle_candidate_") as raw_directory:
            directory = Path(raw_directory)
            first_directory = directory / "first"
            second_directory = directory / "second"
            first_directory.mkdir()
            second_directory.mkdir()
            report_one, output_one = self._build(first_directory)
            report_two, output_two = self._build(second_directory)

        self.assertEqual(output_one, output_two)
        self.assertEqual(builder.DEFAULT_DONOR.read_bytes(), donor_bytes_before)
        self.assertEqual(builder.DEFAULT_SOURCE.read_bytes(), source_bytes_before)
        self.assertFalse(report_one["game_tree_written"])
        self.assertEqual(report_one["mode"], "TEMP_ONLY")
        self.assertEqual(
            report_one["inputs"]["donor"]["sha256"], builder.EXPECTED_DONOR_SHA256
        )
        self.assertEqual(
            report_one["inputs"]["source"]["sha256"], builder.EXPECTED_SOURCE_SHA256
        )
        self.assertEqual(
            report_one["inputs"]["idle_bind"]["sha256"],
            report_two["inputs"]["idle_bind"]["sha256"],
        )

        candidate = json.loads(output_one.decode("utf-8-sig"))
        candidate_index = builder.index_prefab(candidate)
        self.assertEqual(
            len(candidate_index.all_guids), len(donor_index.all_guids) + 6
        )
        self.assertEqual(len(candidate_index.all_guids), len(set(candidate_index.all_guids)))
        self.assertFalse(candidate_index.all_guids & donor_index.all_guids)
        self.assertFalse(candidate_index.all_guids & source_index.all_guids)
        self.assertGreater(
            builder.assert_references_valid(candidate, candidate_index), 0
        )
        self.assertEqual(
            report_one["transform_report"]["reconstruction"], "PASS"
        )
        self.assertEqual(
            report_one["transform_report"]["copied_local_guard"], "PASS"
        )
        for row in report_one["transform_report"]["parts"].values():
            self.assertLessEqual(row["reconstruction_max_delta"], builder.TRS_TOLERANCE)

        donor_root = donor["RootObject"]
        candidate_root = candidate["RootObject"]
        _, donor_guid_mapping = builder.remap_donor_prefab(donor)
        donor_vm = builder._components_of_type(
            donor_root, "Dxura.RP.Game.ViewModel"
        )[0]
        candidate_vm = builder._components_of_type(
            candidate_root, "Dxura.RP.Game.ViewModel"
        )[0]
        expected_vm = builder._remap_strings(donor_vm, donor_guid_mapping)
        self.assertEqual(candidate_vm, expected_vm)
        self.assertEqual(candidate_vm["Muzzle"], expected_vm["Muzzle"])
        self.assertEqual(candidate_vm["EjectionPort"], expected_vm["EjectionPort"])

        donor_arms = [
            component
            for component in builder._components_of_type(
                donor_root, "Sandbox.SkinnedModelRenderer"
            )
            if component.get("Model")
            == "models/first_person/v_first_person_arms_human.vmdl"
        ][0]
        mapped_arms = builder._remap_strings(donor_arms, donor_guid_mapping)
        candidate_arms = candidate_index.components_by_guid[mapped_arms["__guid"]]
        self.assertEqual(candidate_arms, mapped_arms)

    def test_computed_locals_replace_current_authored_part_locals(self) -> None:
        source = json.loads(builder.DEFAULT_SOURCE.read_text(encoding="utf-8-sig"))
        source_index = builder.index_prefab(source)
        current_locals = {
            spec.role: builder._serialized_transform(
                builder._transform_from_node(
                    next(
                        node
                        for node in source_index.nodes_by_guid.values()
                        if node.get("Name") == spec.node_name
                    ),
                    spec.node_name,
                )
            )
            for spec in builder.PART_SPECS
        }
        with tempfile.TemporaryDirectory(prefix="dxrp_deagle_replacement_") as raw_directory:
            directory = Path(raw_directory)
            report, _ = self._build(directory)
        computed = report["transform_report"]["parts"]
        self.assertNotEqual(computed["root"]["local"], current_locals["root"])
        self.assertNotEqual(computed["slide"]["local"], current_locals["slide"])
        self.assertNotEqual(computed["magazine"]["local"], current_locals["magazine"])

    def test_game_tree_and_non_temp_outputs_have_no_write_mode(self) -> None:
        forbidden = builder.GAME_ASSETS_ROOT / "_never_written" / builder.OUTPUT_NAME
        with self.assertRaisesRegex(builder.ContractError, "game/Assets output is impossible"):
            builder.assert_temp_output_directory(forbidden.parent)
        outside_temp = builder.WORKBENCH_ROOT / "tools" / "weapon_pipeline" / "_never_written"
        with self.assertRaisesRegex(builder.ContractError, "TEMP-ONLY output"):
            builder.assert_temp_output_directory(outside_temp)
        help_text = builder._parser().format_help()
        self.assertNotIn("allow-game-write", help_text)
        self.assertNotIn("--output", help_text)
        idle_bind_action = next(
            action
            for action in builder._parser()._actions
            if action.dest == "idle_bind_json"
        )
        self.assertFalse(idle_bind_action.required)
        self.assertIn("--print-compiled-bind-manifest", help_text)

    def test_duplicate_guid_is_rejected(self) -> None:
        donor = json.loads(builder.DEFAULT_DONOR.read_text(encoding="utf-8-sig"))
        duplicate = copy.deepcopy(donor["RootObject"]["Children"][0])
        donor["RootObject"]["Children"].append(duplicate)
        with self.assertRaisesRegex(builder.ContractError, "duplicate GUID"):
            builder.index_prefab(donor)


if __name__ == "__main__":
    unittest.main()
