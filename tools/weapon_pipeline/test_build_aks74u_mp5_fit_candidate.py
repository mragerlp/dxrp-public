"""Focused contracts for the TEMP-ONLY AKS-74U/MP5 fit candidate tool."""

from __future__ import annotations

import copy
import json
import math
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import build_aks74u_mp5_fit_candidate as builder


class Aks74uMp5FitCandidateTests(unittest.TestCase):
    def _manifest_payload(
        self, transform: builder.Transform | None = None
    ) -> dict:
        payload = copy.deepcopy(builder.manifest_template())
        payload["sample"]["observed"] = True
        payload["sample"]["sensor"] = "unit-test explicit idle-bind fixture"
        payload["sample"]["transform"] = builder.transform_payload(
            transform
            or builder.Transform(
                (3.125, -0.25, 6.5),
                (0.0, math.sqrt(0.5), 0.0, math.sqrt(0.5)),
                (1.0, 1.0, 1.0),
            )
        )
        return payload

    def _write_manifest(
        self,
        directory: Path,
        transform: builder.Transform | None = None,
        *,
        name: str = "idle-bind.json",
    ) -> Path:
        path = directory / name
        path.write_bytes(builder._serialize_json(self._manifest_payload(transform)))
        return path

    def test_current_sources_and_ownership_pass_independent_audit(self) -> None:
        audit = builder.audit_current_sources()

        self.assertEqual(
            audit["node_counts"],
            {
                "donor_vm": 82,
                "target_vm": 84,
                "donor_world": 5,
                "target_world": 6,
            },
        )
        self.assertTrue(
            builder._almost_equal_transform(
                audit["fp_fit"], audit["current_fp_body"]
            )
        )
        self.assertTrue(
            builder._almost_equal_transform(audit["tp_fit"], audit["current_tp"])
        )
        self.assertEqual(
            audit["fp_anchor_owners"],
            {
                "muzzle": "weapon_root/weapon_root_children/muzzle",
                "ejection": "weapon_root/weapon_root_children/bolt",
            },
        )
        self.assertEqual(
            audit["world_anchor_owners"],
            {"muzzle": "Model/Muzzle", "ejection": "Model/EjectionPort"},
        )
        self.assertEqual(audit["left_grip_nodes"], [])
        self.assertEqual(audit["ik_components"], [])
        self.assertEqual(
            builder._bounds_union(builder.AKS_BODY_BOUNDS, builder.AKS_MAG_BOUNDS),
            builder.AKS_COMBINED_BOUNDS,
        )

    def test_bounds_fit_reproduces_current_nonfinal_seeds(self) -> None:
        fp = builder.derive_centered_length_fit(
            builder.AKS_COMBINED_BOUNDS, builder.MP5_VM_BOUNDS
        )
        tp = builder.derive_centered_length_fit(
            builder.AKS_COMBINED_BOUNDS, builder.MP5_W_BOUNDS
        )

        self.assertEqual(
            builder.transform_payload(fp),
            {
                "position": "1.0136986,0.13541961,7.1526102",
                "rotation": "0,0,0,1",
                "scale": "0.89572925,0.89572925,0.89572925",
            },
        )
        self.assertEqual(
            builder.transform_payload(tp),
            {
                "position": "1.00402247,0.13560436,7.18615047",
                "rotation": "0,0,0,1",
                "scale": "0.89645458,0.89645458,0.89645458",
            },
        )

    def test_inverse_bind_law_round_trips_nontrivial_parent(self) -> None:
        parent = builder.Transform(
            (3.125, -0.25, 6.5),
            (0.0, math.sqrt(0.5), 0.0, math.sqrt(0.5)),
            (1.0, 1.0, 1.0),
        )
        desired = builder.derive_centered_length_fit(
            builder.AKS_COMBINED_BOUNDS, builder.MP5_VM_BOUNDS
        )

        local = builder.inverse_parent_compose(parent, desired)
        reassembled = builder.compose(parent, local)

        self.assertTrue(
            builder._almost_equal_transform(reassembled, desired, tolerance=1e-7)
        )
        self.assertFalse(
            builder._almost_equal_transform(
                builder.compose(parent, desired), desired, tolerance=1e-3
            )
        )

    def test_manifest_is_explicit_pinned_and_nonplaceholder(self) -> None:
        with tempfile.TemporaryDirectory(prefix="dxrp_aks74u_manifest_") as directory:
            root = Path(directory)
            valid_path = self._write_manifest(root)
            manifest = builder.load_idle_bind_manifest(valid_path)
            self.assertEqual(manifest.sensor, "unit-test explicit idle-bind fixture")
            self.assertEqual(manifest.sample_kind, "observed_idle_bind")

            compiled_payload = builder.compiled_bind_manifest_template()
            compiled_path = root / "compiled-bind.json"
            compiled_path.write_bytes(builder._serialize_json(compiled_payload))
            compiled = builder.load_idle_bind_manifest(compiled_path)
            self.assertEqual(compiled.sample_kind, "compiled_model_bind")
            self.assertEqual(
                compiled.evidence, builder.compiled_bind_evidence_template()
            )

            arbitrary_payload = copy.deepcopy(compiled_payload)
            arbitrary_payload["sample"]["transform"]["position"] = "123,456,789"
            arbitrary_path = root / "arbitrary-compiled-bind.json"
            arbitrary_path.write_bytes(builder._serialize_json(arbitrary_payload))
            with self.assertRaisesRegex(builder.ContractError, "pinned DATA record"):
                builder.load_idle_bind_manifest(arbitrary_path)

            unpinned_evidence = copy.deepcopy(compiled_payload)
            unpinned_evidence["sample"]["evidence"]["compiled_model"][
                "sha256"
            ] = "0" * 64
            unpinned_path = root / "unpinned-compiled-evidence.json"
            unpinned_path.write_bytes(builder._serialize_json(unpinned_evidence))
            with self.assertRaisesRegex(builder.ContractError, "pinned parser/model"):
                builder.load_idle_bind_manifest(unpinned_path)

            incomplete = root / "incomplete.json"
            incomplete.write_bytes(builder._serialize_json(builder.manifest_template()))
            with self.assertRaisesRegex(builder.ContractError, "sample.observed"):
                builder.load_idle_bind_manifest(incomplete)

            wrong_pin_payload = self._manifest_payload()
            wrong_pin_payload["pins"]["donor_vm_prefab"]["sha256"] = "0" * 64
            wrong_pin = root / "wrong-pin.json"
            wrong_pin.write_bytes(builder._serialize_json(wrong_pin_payload))
            with self.assertRaisesRegex(builder.ContractError, "source pins"):
                builder.load_idle_bind_manifest(wrong_pin)

    def test_candidate_build_is_temp_only_deterministic_and_nonmutating(self) -> None:
        source_before = {
            name: (builder.REPO_ROOT / pin.relative_path).read_bytes()
            for name, pin in builder.SOURCE_PINS.items()
        }
        parent = builder.Transform(
            (3.125, -0.25, 6.5),
            (0.0, math.sqrt(0.5), 0.0, math.sqrt(0.5)),
            (1.0, 1.0, 1.0),
        )
        with tempfile.TemporaryDirectory(prefix="dxrp_aks74u_build_test_") as directory:
            root = Path(directory)
            manifest = self._write_manifest(root, parent)
            first = builder.build_temp_candidates(manifest, root / "candidate-one")
            second = builder.build_temp_candidates(manifest, root / "candidate-two")

            for key in ("viewmodel", "world", "report"):
                first_path = Path(first["output_directory"]) / first["files"][key]
                second_path = Path(second["output_directory"]) / second["files"][key]
                self.assertEqual(first_path.read_bytes(), second_path.read_bytes())
                self.assertTrue(first_path.is_relative_to(root))

            report = first["report"]
            self.assertEqual(report["mode"], "TEMP_ONLY_NO_PRODUCT_WRITE_MODE")
            self.assertIn(
                "magazine_parent_bind_P", report["first_person"]
            )
            self.assertNotIn(
                "magazine_idle_parent_P", report["first_person"]
            )
            self.assertTrue(
                report["first_person"]["static_magazine_motion_compatibility"][
                    "compatible"
                ]
            )
            self.assertFalse(
                report["first_person"]["static_magazine_motion_compatibility"][
                    "rendered_motion_verified"
                ]
            )
            self.assertFalse(report["first_person"]["anchors_rewired"])
            self.assertFalse(report["third_person"]["anchors_rewired"])
            self.assertFalse(report["third_person"]["left_hand_ik_wired"])
            self.assertNotIn(str(root), json.dumps(report, sort_keys=True))

            vm_path = Path(first["output_directory"]) / first["files"]["viewmodel"]
            vm = json.loads(vm_path.read_text(encoding="utf-8"))
            vm_index = builder.index_prefab(vm, "test candidate VM")
            expected_body = builder.derive_centered_length_fit(
                builder.AKS_COMBINED_BOUNDS, builder.MP5_VM_BOUNDS
            )
            expected_mag = builder.inverse_parent_compose(parent, expected_body)
            self.assertTrue(
                builder._almost_equal_transform(
                    builder._transform_from_node(
                        vm_index.nodes[builder.VM_BODY_PATH], "candidate body"
                    ),
                    expected_body,
                    tolerance=1e-7,
                )
            )
            self.assertTrue(
                builder._almost_equal_transform(
                    builder._transform_from_node(
                        vm_index.nodes[builder.VM_MAG_PATH], "candidate magazine"
                    ),
                    expected_mag,
                    tolerance=1e-7,
                )
            )

        source_after = {
            name: (builder.REPO_ROOT / pin.relative_path).read_bytes()
            for name, pin in builder.SOURCE_PINS.items()
        }
        self.assertEqual(source_after, source_before)

    def test_repository_and_nonempty_output_paths_are_rejected(self) -> None:
        with self.assertRaisesRegex(builder.ContractError, "TEMP-ONLY"):
            builder.assert_temp_output_directory(
                builder.REPO_ROOT / "tools" / "weapon_pipeline" / "forbidden-output"
            )

        with tempfile.TemporaryDirectory(prefix="dxrp_aks74u_nonempty_") as directory:
            root = Path(directory)
            output = root / "candidate"
            output.mkdir()
            (output / "occupied.txt").write_text("occupied", encoding="utf-8")
            with self.assertRaisesRegex(builder.ContractError, "must be empty"):
                builder.assert_temp_output_directory(output)

    def test_cli_exposes_no_product_write_switch(self) -> None:
        destinations = {action.dest for action in builder._parser()._actions}
        self.assertNotIn("allow_game_write", destinations)
        self.assertNotIn("product", destinations)


if __name__ == "__main__":
    unittest.main()
