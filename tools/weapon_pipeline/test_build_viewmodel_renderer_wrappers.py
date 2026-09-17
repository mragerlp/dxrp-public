"""Focused contract tests for build_viewmodel_renderer_wrappers.py."""

from __future__ import annotations

import copy
import json
import tempfile
import unittest
import uuid
from pathlib import Path

import build_viewmodel_renderer_wrappers as builder


class ViewModelRendererWrapperTests(unittest.TestCase):
    def _load(self, spec: builder.PrefabSpec) -> tuple[dict, bytes]:
        raw = spec.default_source.read_bytes()
        return json.loads(raw.decode("utf-8-sig")), raw

    def _identity_transforms(
        self, spec: builder.PrefabSpec
    ) -> dict[str, builder.WrapperTransform]:
        return {node.path: builder.IDENTITY_TRANSFORM for node in spec.renderer_nodes}

    def test_current_prefabs_preserve_original_structure_and_contracts(self) -> None:
        for spec in builder.PREFAB_SPECS:
            with self.subTest(family=spec.family):
                source, raw_before = self._load(spec)
                before_index = builder.index_prefab(source)
                before_hierarchy = builder._hierarchy_snapshot(before_index)
                view_model_before = builder._view_model_contract(source)

                candidate, report = builder.transform_prefab(
                    source, spec, self._identity_transforms(spec)
                )
                after_index = builder.index_prefab(candidate)

                self.assertEqual(report["renderers_moved"], len(spec.renderer_nodes))
                self.assertTrue(report["original_hierarchy_preserved"])
                self.assertTrue(report["donor_transforms_preserved"])
                self.assertTrue(report["renderer_components_preserved"])
                self.assertTrue(report["references_valid"])
                self.assertEqual(
                    set(report["identity_moving_parts"]), set(spec.moving_paths)
                )
                self.assertEqual(view_model_before, builder._view_model_contract(candidate))
                self.assertEqual(
                    len(after_index.all_guids),
                    len(before_index.all_guids) + len(spec.renderer_nodes),
                )

                wrapper_guids = set(report["new_wrapper_guids"])
                for guid, original in before_hierarchy.items():
                    current = builder._hierarchy_snapshot(after_index)[guid]
                    for field in ("parent_guid", "Position", "Rotation", "Scale", "Name"):
                        self.assertEqual(current[field], original[field])
                    self.assertEqual(
                        tuple(
                            child
                            for child in current["children"]
                            if child not in wrapper_guids
                        ),
                        original["children"],
                    )

                self.assertEqual(spec.default_source.read_bytes(), raw_before)

    def test_component_reference_owner_follows_moved_renderer(self) -> None:
        spec = builder.PREFAB_SPECS[0]
        source, _ = self._load(spec)
        matches = builder._find_renderer_matches(source, spec)
        target = matches[0]
        reference_component = {
            "__type": "Tests.RendererReferenceHolder",
            "__guid": str(uuid.uuid4()),
            "__enabled": True,
            "Target": {
                "_type": "component",
                "component_id": target.renderer["__guid"],
                "go": target.donor.node["__guid"],
                "component_type": "ModelRenderer",
            },
        }
        source["RootObject"]["Components"].append(reference_component)

        candidate, report = builder.transform_prefab(
            source, spec, self._identity_transforms(spec)
        )
        index = builder.index_prefab(candidate)
        expected_owner = index.component_owners[target.renderer["__guid"]]
        holder = index.components_by_guid[reference_component["__guid"]]
        self.assertEqual(holder["Target"]["go"], expected_owner)
        self.assertEqual(report["reference_owner_updates"], 1)

    def test_external_manifest_requires_every_moving_part(self) -> None:
        spec = builder.PREFAB_SPECS[0]
        one_path = next(iter(spec.moving_paths))
        partial = {spec.family: {one_path: builder.IDENTITY_TRANSFORM}}
        with self.assertRaisesRegex(builder.ContractError, "missing common-origin moving"):
            builder._resolve_transforms(spec, partial, external_manifest=True)

    def test_manifest_transform_lands_on_wrapper_not_donor(self) -> None:
        spec = builder.PREFAB_SPECS[0]
        source, _ = self._load(spec)
        target_path = sorted(spec.moving_paths)[0]
        manifest_payload = {
            "version": 1,
            "families": {
                spec.family: {
                    path: {
                        "position": "1.25,-2.5,3.75" if path == target_path else "0,0,0",
                        "quaternion": "0,0,0.7071068,0.7071068"
                        if path == target_path
                        else "0,0,0,1",
                        "scale": "0.9,0.9,0.9" if path == target_path else "1,1,1",
                    }
                    for path in spec.moving_paths
                }
            },
        }
        with tempfile.TemporaryDirectory(prefix="dxrp_wrapper_manifest_") as directory:
            manifest_path = Path(directory) / "transforms.json"
            manifest_path.write_text(json.dumps(manifest_payload), encoding="utf-8")
            manifest = builder.load_transform_manifest(manifest_path)

        transforms = builder._resolve_transforms(
            spec, manifest.families, external_manifest=True
        )
        before = builder._find_renderer_matches(source, spec)
        donor_before = next(
            copy.deepcopy(match.donor.node)
            for match in before
            if match.contract.path == target_path
        )
        candidate, report = builder.transform_prefab(source, spec, transforms)
        after_index = builder.index_prefab(candidate)
        row = next(
            wrapper for wrapper in report["wrappers"] if wrapper["donor_path"] == target_path
        )
        wrapper = after_index.nodes_by_guid[row["wrapper_guid"]].node
        donor = after_index.nodes_by_guid[row["donor_guid"]].node

        self.assertEqual(wrapper["Position"], "1.25,-2.5,3.75")
        self.assertEqual(wrapper["Rotation"], "0,0,0.7071068,0.7071068")
        self.assertEqual(wrapper["Scale"], "0.9,0.9,0.9")
        for field in ("Position", "Rotation", "Scale"):
            self.assertEqual(donor[field], donor_before[field])

    def test_duplicate_guid_is_rejected(self) -> None:
        spec = builder.PREFAB_SPECS[0]
        source, _ = self._load(spec)
        duplicate = copy.deepcopy(source["RootObject"]["Children"][0])
        source["RootObject"]["Children"].append(duplicate)
        with self.assertRaisesRegex(builder.ContractError, "Duplicate GUID"):
            builder.index_prefab(source)

    def test_game_assets_output_is_always_rejected(self) -> None:
        forbidden = builder.GAME_ASSETS_ROOT / "_temp_wrapper_negative_control"
        with self.assertRaisesRegex(builder.ContractError, "repository output"):
            builder.assert_output_paths_allowed([forbidden])

    def test_cli_exposes_no_product_write_switch(self) -> None:
        option_strings = {
            option
            for action in builder._parser()._actions
            for option in action.option_strings
        }
        self.assertNotIn("--allow-game-write", option_strings)

    def test_non_temp_output_is_rejected(self) -> None:
        forbidden = Path("C:/dxrp-wrapper-outside-temp")
        if builder._is_within(forbidden, builder.SYSTEM_TEMP_ROOT):
            self.skipTest("probe unexpectedly resolves beneath system temp")
        with self.assertRaisesRegex(builder.ContractError, "system temp"):
            builder.assert_output_paths_allowed([forbidden])

    def test_nonunit_quaternion_is_rejected(self) -> None:
        with self.assertRaisesRegex(builder.ContractError, "must be normalized"):
            builder._parse_transform(
                {
                    "position": "0,0,0",
                    "quaternion": "0,0,0,2",
                    "scale": "1,1,1",
                },
                "nonunit-probe",
            )

    def test_negative_and_nonuniform_scale_are_rejected(self) -> None:
        for scale, message in (("-1,-1,-1", "positive"), ("1,1.01,1", "uniform")):
            with self.subTest(scale=scale):
                with self.assertRaisesRegex(builder.ContractError, message):
                    builder._parse_transform(
                        {
                            "position": "0,0,0",
                            "quaternion": "0,0,0,1",
                            "scale": scale,
                        },
                        "scale-probe",
                    )

    def test_product_acceptance_marker_and_pins_load_from_json(self) -> None:
        expected_families = {
            spec.family: self._identity_transforms(spec)
            for spec in builder.PREFAB_SPECS
        }
        expected_pins = {
            spec.family: builder.ProductPin(
                source_path=str(spec.default_source.resolve()),
                source_sha256=builder._sha256(spec.default_source.read_bytes()),
                output_path=str((builder.SYSTEM_TEMP_ROOT / spec.output_name).resolve()),
                output_sha256="A" * 64,
            )
            for spec in builder.PREFAB_SPECS
        }
        payload = {
            "version": 1,
            "measured_and_accepted": True,
            "pins": {
                family: {
                    "source_path": pin.source_path,
                    "source_sha256": pin.source_sha256,
                    "output_path": pin.output_path,
                    "output_sha256": pin.output_sha256,
                }
                for family, pin in expected_pins.items()
            },
            "families": {
                family: {
                    path: {
                        "position": transform.position,
                        "quaternion": transform.quaternion,
                        "scale": transform.scale,
                    }
                    for path, transform in transforms.items()
                }
                for family, transforms in expected_families.items()
            },
        }
        with tempfile.TemporaryDirectory(prefix="dxrp_wrapper_acceptance_") as directory:
            manifest_path = Path(directory) / "accepted.json"
            manifest_path.write_text(json.dumps(payload), encoding="utf-8")
            actual = builder.load_transform_manifest(manifest_path)

        self.assertIs(actual.measured_and_accepted, True)
        self.assertEqual(actual.pins, expected_pins)
        self.assertEqual(actual.families, expected_families)
        self.assertEqual(len(actual.sha256 or ""), 64)

    def test_accepted_manifest_pins_are_enforced_during_build(self) -> None:
        with tempfile.TemporaryDirectory(prefix="dxrp_wrapper_pins_") as directory:
            root = Path(directory)
            preview = root / "preview"
            preview_reports = builder.build_candidates(output_directory=preview)
            accepted = root / "accepted"
            payload = {
                "version": 1,
                "measured_and_accepted": True,
                "pins": {},
                "families": {},
            }
            preview_sha = {
                report["family"]: report["output_sha256"]
                for report in preview_reports
            }
            for spec in builder.PREFAB_SPECS:
                payload["pins"][spec.family] = {
                    "source_path": str(spec.default_source.resolve()),
                    "source_sha256": builder._sha256(spec.default_source.read_bytes()),
                    "output_path": str((accepted / spec.output_name).resolve()),
                    "output_sha256": preview_sha[spec.family],
                }
                payload["families"][spec.family] = {
                    path: {
                        "position": builder.IDENTITY_TRANSFORM.position,
                        "quaternion": builder.IDENTITY_TRANSFORM.quaternion,
                        "scale": builder.IDENTITY_TRANSFORM.scale,
                    }
                    for path in spec.moving_paths
                }
            manifest_path = root / "accepted.json"
            manifest_path.write_text(json.dumps(payload), encoding="utf-8")

            reports = builder.build_candidates(
                output_directory=accepted,
                transform_manifest_path=manifest_path,
            )
            self.assertTrue(all(report["measured_and_accepted"] for report in reports))

    def test_stale_manifest_pin_fails_before_output(self) -> None:
        with tempfile.TemporaryDirectory(prefix="dxrp_wrapper_bad_pin_") as directory:
            root = Path(directory)
            output = root / "candidate"
            payload = {
                "version": 1,
                "measured_and_accepted": True,
                "pins": {},
                "families": {},
            }
            for spec in builder.PREFAB_SPECS:
                payload["pins"][spec.family] = {
                    "source_path": str(spec.default_source.resolve()),
                    "source_sha256": "0" * 64,
                    "output_path": str((output / spec.output_name).resolve()),
                    "output_sha256": "0" * 64,
                }
                payload["families"][spec.family] = {
                    path: {
                        "position": builder.IDENTITY_TRANSFORM.position,
                        "quaternion": builder.IDENTITY_TRANSFORM.quaternion,
                        "scale": builder.IDENTITY_TRANSFORM.scale,
                    }
                    for path in spec.moving_paths
                }
            manifest_path = root / "stale.json"
            manifest_path.write_text(json.dumps(payload), encoding="utf-8")
            with self.assertRaisesRegex(builder.ContractError, "source SHA-256"):
                builder.build_candidates(
                    output_directory=output,
                    transform_manifest_path=manifest_path,
                )
            self.assertFalse(output.exists())

    def test_temp_build_writes_candidates_without_mutating_sources(self) -> None:
        source_bytes = {
            spec.family: spec.default_source.read_bytes()
            for spec in builder.PREFAB_SPECS
        }
        with tempfile.TemporaryDirectory(prefix="dxrp_wrapper_test_") as directory:
            output_directory = Path(directory)
            reports = builder.build_candidates(output_directory=output_directory)
            self.assertEqual(len(reports), len(builder.PREFAB_SPECS))
            for spec, report in zip(builder.PREFAB_SPECS, reports):
                output = Path(report["output"])
                self.assertTrue(output.is_file())
                self.assertTrue(builder._is_within(output, output_directory))
                self.assertFalse(builder._is_within(output, builder.GAME_ASSETS_ROOT))
                self.assertEqual(
                    report["manifest_mode"], "builtin_identity_structural_only"
                )
                self.assertIn("inverse(donor_idle_local P)", report["transform_semantics"])
                self.assertIn("W = inverse(P) * B", report["proof_ceiling"])
                builder.index_prefab(json.loads(output.read_text(encoding="utf-8")))
                self.assertEqual(spec.default_source.read_bytes(), source_bytes[spec.family])


    def test_nonempty_temp_output_directory_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory(prefix="dxrp_wrapper_nonempty_") as directory:
            output_directory = Path(directory)
            (output_directory / "sentinel.txt").write_text("keep", encoding="utf-8")
            with self.assertRaisesRegex(builder.ContractError, "must be empty"):
                builder.build_candidates(output_directory=output_directory)


if __name__ == "__main__":
    unittest.main()
