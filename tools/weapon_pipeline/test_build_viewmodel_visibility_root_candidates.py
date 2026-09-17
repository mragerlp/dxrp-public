"""Focused tests for build_viewmodel_visibility_root_candidates.py."""

from __future__ import annotations

import copy
import hashlib
import json
import tempfile
import unittest
import uuid
from pathlib import Path
from unittest import mock

import build_viewmodel_visibility_root_candidates as builder


EXPECTED_OUTPUT_PINS: dict[str, tuple[int, str]] = {
    "ak47": (
        76978,
        "EC32DA9D4116A36B7D800A2731DB9821EDB7C17D0201620E6D5DECB76CA3383F",
    ),
    "aks74u": (
        4580,
        "33E301AC348F2301AB6B46938B50F9C6C135D5280235135046FC61AF656578BB",
    ),
    "deserteagle": (
        6343,
        "6FA94E651062F77224D4005E79F48AD268474065D6EEE436DDFA1B9CC52C24C7",
    ),
    "m1911": (
        6273,
        "AFCF9ED73375BD926A0F62DCC46A23374AE2B211A2DF0F9B3368D4A5C4C2F48E",
    ),
    "m870": (
        70839,
        "F9860C726E466007C8624DE2F2BAC68C05034DFEFD826D90FE4BE91F2546703D",
    ),
}


class VisibilityRootCandidateTests(unittest.TestCase):
    def _guid(self, family: str, path: str) -> str:
        return str(
            uuid.uuid5(
                uuid.NAMESPACE_URL,
                f"https://lifepunch.co/dxrp/visibility-root-test/{family}/{path}",
            )
        )

    def _node(self, name: str, guid: str) -> dict:
        return {
            "__guid": guid,
            "__version": 2,
            "Flags": 0,
            "Name": name,
            "Position": "0,0,0",
            "Rotation": "0,0,0,1",
            "Scale": "1,1,1",
            "Tags": "",
            "Enabled": True,
            "NetworkMode": 2,
            "NetworkFlags": 0,
            "NetworkOrphaned": 0,
            "NetworkTransmit": True,
            "OwnerTransfer": 1,
            "Components": [],
            "Children": [],
        }

    def _fixture_prefab(
        self,
        contract: builder.WeaponContract,
        *,
        mutation: str | None = None,
    ) -> dict:
        paths = {
            contract.view_model_owner_path,
            contract.visibility_root_path,
            *(renderer.owner_path for renderer in contract.renderers),
        }
        for path in tuple(paths):
            segments = path.split("/")
            paths.update("/".join(segments[:index]) for index in range(1, len(segments)))
        ordered = sorted(paths, key=lambda value: (value.count("/"), value))
        nodes: dict[str, dict] = {}
        for path in ordered:
            guid = (
                contract.visibility_root_guid
                if path == contract.visibility_root_path
                else self._guid(contract.family, path)
            )
            nodes[path] = self._node(path.rsplit("/", 1)[-1], guid)
            if "/" in path:
                parent = path.rsplit("/", 1)[0]
                nodes[parent]["Children"].append(nodes[path])

        view_model = {
            "__type": builder.VIEW_MODEL_TYPE,
            "__guid": contract.view_model_guid,
            "__enabled": True,
            "Flags": 0,
            "CanADS": True,
        }
        nodes[contract.view_model_owner_path]["Components"].append(view_model)
        for renderer in contract.renderers:
            nodes[renderer.owner_path]["Components"].append(
                {
                    "__type": renderer.component_type,
                    "__guid": renderer.guid,
                    "__enabled": True,
                    "Flags": 0,
                    "Model": renderer.model,
                }
            )

        if mutation == "property-exists":
            view_model["AdditionalRendererRoot"] = {
                "_type": "gameobject",
                "go": contract.visibility_root_guid,
            }
        elif mutation == "viewmodel-type":
            view_model["__type"] = "Tests.NotAViewModel"
        elif mutation == "renderer-model":
            first = contract.renderers[0]
            nodes[first.owner_path]["Components"][0]["Model"] = "models/drift.vmdl"
        elif mutation == "renderer-type":
            first = contract.renderers[0]
            nodes[first.owner_path]["Components"][0]["__type"] = (
                "Sandbox.SkinnedModelRenderer"
            )
        elif mutation == "renderer-outside-root":
            first = contract.renderers[0]
            renderer_component = nodes[first.owner_path]["Components"].pop()
            outsider_path = contract.root_name + "/outsider"
            outsider = self._node("outsider", self._guid(contract.family, outsider_path))
            outsider["Components"].append(renderer_component)
            nodes[contract.root_name]["Children"].append(outsider)
        elif mutation == "root-path":
            nodes[contract.visibility_root_path]["Name"] = "weapon_root_drift"
        elif mutation is not None:
            raise AssertionError(f"unknown fixture mutation {mutation}")

        return {
            "RootObject": nodes[contract.root_name],
            "ResourceVersion": 1,
            "ShowInMenu": False,
            "MenuPath": "",
            "MenuIcon": None,
            "DontBreakAsTemplate": False,
            "__references": [],
            "__version": 1,
        }

    def _write_generated_inputs(
        self,
        root: Path,
        *,
        mutated_family: str | None = None,
        mutation: str | None = None,
    ) -> tuple[dict[str, builder.InputPin], dict[str, bytes]]:
        pins: dict[str, builder.InputPin] = {}
        source_bytes: dict[str, bytes] = {}
        for family in sorted(builder.GENERATED_FAMILIES):
            contract = builder.CONTRACTS_BY_FAMILY[family]
            payload = self._fixture_prefab(
                contract,
                mutation=mutation if family == mutated_family else None,
            )
            raw = (json.dumps(payload, indent=2) + "\n").encode("utf-8")
            path = root / f"{family}.generated.prefab"
            path.write_bytes(raw)
            pins[family] = builder.InputPin(
                path=path,
                expected_bytes=len(raw),
                expected_sha256=builder._sha256(raw),
            )
            source_bytes[family] = raw
        return pins, source_bytes

    def _view_model(self, prefab: dict, family: str) -> dict:
        index = builder._index_prefab(prefab)
        return index.components_by_guid[
            builder.CONTRACTS_BY_FAMILY[family].view_model_guid
        ]

    def test_active_inputs_match_literal_byte_pins(self) -> None:
        for family in ("ak47", "m870"):
            with self.subTest(family=family):
                contract = builder.CONTRACTS_BY_FAMILY[family]
                self.assertIsNotNone(contract.active_path)
                raw = contract.active_path.read_bytes()  # type: ignore[union-attr]
                self.assertEqual(len(raw), contract.active_bytes)
                self.assertEqual(builder._sha256(raw), contract.active_sha256)

    def test_exact_deterministic_output_pins_and_one_property_only(self) -> None:
        with tempfile.TemporaryDirectory(prefix="dxrp-visibility-inputs-") as directory:
            root = Path(directory)
            pins, source_bytes = self._write_generated_inputs(root)
            first = builder.render_candidates(pins)
            second = builder.render_candidates(pins)

            actual_pins = {
                item.contract.family: (len(item.output_bytes), item.output_sha256)
                for item in first
            }
            self.assertEqual(actual_pins, EXPECTED_OUTPUT_PINS)
            self.assertEqual(
                [item.output_bytes for item in first],
                [item.output_bytes for item in second],
            )

            for item in first:
                with self.subTest(family=item.contract.family):
                    source = json.loads(item.source_bytes.decode("utf-8-sig"))
                    output = json.loads(item.output_bytes.decode("utf-8-sig"))
                    view_model = self._view_model(output, item.contract.family)
                    self.assertEqual(
                        view_model.pop("AdditionalRendererRoot"),
                        {
                            "_type": "gameobject",
                            "go": item.contract.visibility_root_guid,
                        },
                    )
                    self.assertEqual(output, source)
                    source_index = builder._index_prefab(source)
                    output_index = builder._index_prefab(
                        json.loads(item.output_bytes.decode("utf-8-sig"))
                    )
                    self.assertEqual(source_index.all_guids, output_index.all_guids)
                    for renderer in item.contract.renderers:
                        owner = output_index.component_owners[renderer.guid]
                        self.assertTrue(
                            builder._is_descendant_or_self(
                                output_index,
                                owner,
                                item.contract.visibility_root_guid,
                            )
                        )

            for family, raw in source_bytes.items():
                self.assertEqual((root / f"{family}.generated.prefab").read_bytes(), raw)

    def test_temp_build_is_create_only_and_does_not_mutate_inputs(self) -> None:
        with tempfile.TemporaryDirectory(prefix="dxrp-visibility-build-") as directory:
            root = Path(directory)
            pins, source_bytes = self._write_generated_inputs(root)
            active_before = {
                family: builder.CONTRACTS_BY_FAMILY[family].active_path.read_bytes()
                for family in ("ak47", "m870")
            }
            output = root / "output"
            output.mkdir()
            report = builder.build_candidates(pins, output_directory=output)

            self.assertEqual(report["result"], "PASS")
            self.assertEqual(report["candidate_count"], 5)
            self.assertEqual(report["mode"], "TEMP_ONLY_CREATE_ONLY_NO_PRODUCT_WRITE_MODE")
            self.assertTrue(report["writes"]["temp_only"])
            self.assertFalse(report["writes"]["game"])
            self.assertEqual(
                {path.name for path in output.iterdir()},
                {contract.output_name for contract in builder.WEAPON_CONTRACTS},
            )
            for row in report["candidates"]:
                self.assertTrue(row["all_custom_renderers_descend_from_root"])
                self.assertEqual(row["semantic_change"], "add AdditionalRendererRoot only")
                path = Path(row["output"]["path"])
                raw = path.read_bytes()
                self.assertEqual(len(raw), row["output"]["bytes"])
                self.assertEqual(builder._sha256(raw), row["output"]["sha256"])

            for family, raw in source_bytes.items():
                self.assertEqual((root / f"{family}.generated.prefab").read_bytes(), raw)
            for family, raw in active_before.items():
                self.assertEqual(
                    builder.CONTRACTS_BY_FAMILY[family].active_path.read_bytes(), raw
                )
            with self.assertRaisesRegex(builder.ContractError, "must be empty"):
                builder.build_candidates(pins, output_directory=output)

    def test_check_mode_writes_nothing(self) -> None:
        with tempfile.TemporaryDirectory(prefix="dxrp-visibility-check-") as directory:
            root = Path(directory)
            pins, source_bytes = self._write_generated_inputs(root)
            before = {path.name for path in root.iterdir()}
            report = builder.check_candidates(pins)
            self.assertFalse(report["wrote_files"])
            self.assertFalse(report["writes"]["temp_only"])
            self.assertEqual({path.name for path in root.iterdir()}, before)
            for family, raw in source_bytes.items():
                self.assertEqual((root / f"{family}.generated.prefab").read_bytes(), raw)

    def test_generated_pin_mismatch_fails_before_output(self) -> None:
        with tempfile.TemporaryDirectory(prefix="dxrp-visibility-pin-") as directory:
            root = Path(directory)
            pins, _ = self._write_generated_inputs(root)
            bad = dict(pins)
            pin = bad["aks74u"]
            bad["aks74u"] = builder.InputPin(
                path=pin.path,
                expected_bytes=pin.expected_bytes,
                expected_sha256="0" * 64,
            )
            output = root / "output"
            output.mkdir()
            with self.assertRaisesRegex(builder.ContractError, "byte pin mismatch"):
                builder.build_candidates(bad, output_directory=output)
            self.assertEqual(list(output.iterdir()), [])

    def test_property_type_path_and_renderer_drift_fail_closed(self) -> None:
        cases = (
            "property-exists",
            "viewmodel-type",
            "renderer-model",
            "renderer-type",
            "renderer-outside-root",
            "root-path",
        )
        for mutation in cases:
            with self.subTest(mutation=mutation), tempfile.TemporaryDirectory(
                prefix=f"dxrp-visibility-{mutation}-"
            ) as directory:
                root = Path(directory)
                pins, _ = self._write_generated_inputs(
                    root,
                    mutated_family="aks74u",
                    mutation=mutation,
                )
                output = root / "output"
                output.mkdir()
                with self.assertRaises(builder.ContractError):
                    builder.build_candidates(pins, output_directory=output)
                self.assertEqual(list(output.iterdir()), [])

    def test_generated_input_must_be_a_strict_system_temp_child(self) -> None:
        contract = builder.CONTRACTS_BY_FAMILY["aks74u"]
        active = builder.CONTRACTS_BY_FAMILY["ak47"].active_path
        self.assertIsNotNone(active)
        raw = active.read_bytes()  # type: ignore[union-attr]
        pin = builder.InputPin(
            path=active,  # type: ignore[arg-type]
            expected_bytes=len(raw),
            expected_sha256=builder._sha256(raw),
        )
        with self.assertRaisesRegex(builder.ContractError, "strict child of system TEMP"):
            builder._read_pinned_input(pin, contract)

    def test_repository_and_non_temp_output_are_rejected_without_writes(self) -> None:
        with self.assertRaises(builder.ContractError):
            builder._verified_output_directory(builder.WORKSPACE_ROOT)
        outside = builder.WORKSPACE_ROOT.parent.resolve()
        if builder._is_within(outside, builder.SYSTEM_TEMP_ROOT):
            self.skipTest("workspace parent unexpectedly lies beneath system TEMP")
        with self.assertRaises(builder.ContractError):
            builder._verified_output_directory(outside)

    def test_create_only_arrival_race_is_rejected_and_external_bytes_survive(self) -> None:
        with tempfile.TemporaryDirectory(prefix="dxrp-visibility-race-") as directory:
            root = Path(directory)
            pins, source_bytes = self._write_generated_inputs(root)
            output = root / "output"
            output.mkdir()
            original_promote = builder.pipeline_io.promote_files
            arrived: list[Path] = []

            def arrive_then_promote(pairs):
                normalized = list(pairs)
                final = Path(normalized[0][1])
                final.write_bytes(b"external-arrival")
                arrived.append(final)
                return original_promote(normalized)

            with mock.patch.object(
                builder.pipeline_io,
                "promote_files",
                side_effect=arrive_then_promote,
            ):
                with self.assertRaisesRegex(builder.ContractError, "changed during staging"):
                    builder.build_candidates(pins, output_directory=output)

            self.assertEqual(len(arrived), 1)
            self.assertEqual(arrived[0].read_bytes(), b"external-arrival")
            self.assertEqual(
                [path for path in output.iterdir() if ".staging" in path.name], []
            )
            for family, raw in source_bytes.items():
                self.assertEqual((root / f"{family}.generated.prefab").read_bytes(), raw)

    def test_manifest_is_exact_and_cli_has_no_product_switch(self) -> None:
        with tempfile.TemporaryDirectory(prefix="dxrp-visibility-manifest-") as directory:
            root = Path(directory)
            pins, _ = self._write_generated_inputs(root)
            payload = {
                "version": 1,
                "generated_inputs": {
                    family: {
                        "path": str(pin.path.resolve()),
                        "bytes": pin.expected_bytes,
                        "sha256": pin.expected_sha256,
                    }
                    for family, pin in pins.items()
                },
            }
            manifest = root / "inputs.json"
            manifest.write_text(json.dumps(payload), encoding="utf-8")
            self.assertEqual(builder.load_generated_input_manifest(manifest), pins)

            invalid = copy.deepcopy(payload)
            invalid["generated_inputs"]["aks74u"]["extra"] = True
            invalid_manifest = root / "invalid.json"
            invalid_manifest.write_text(json.dumps(invalid), encoding="utf-8")
            with self.assertRaisesRegex(builder.ContractError, "exactly"):
                builder.load_generated_input_manifest(invalid_manifest)

        options = {
            option
            for action in builder._parser()._actions
            for option in action.option_strings
        }
        for forbidden in (
            "--allow-game-write",
            "--promote",
            "--portal",
            "--save-scene",
            "--commit",
        ):
            self.assertNotIn(forbidden, options)


if __name__ == "__main__":
    unittest.main()
