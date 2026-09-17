"""Contract tests for build_world_hold_ik_candidates.py."""

from __future__ import annotations

import copy
import hashlib
import json
import shutil
import tempfile
import unittest
import uuid
from pathlib import Path, PurePosixPath

import build_world_hold_ik_candidates as builder


class WorldHoldIkCandidateTests(unittest.TestCase):
    def _guid(self, *parts: str) -> str:
        return str(uuid.uuid5(uuid.NAMESPACE_URL, "fixture|" + "|".join(parts)))

    def _node(
        self,
        *,
        guid: str,
        name: str,
        components: list[dict] | None = None,
        children: list[dict] | None = None,
    ) -> dict:
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
            "Components": components or [],
            "Children": children or [],
        }

    def _prefab(self, spec: builder.WeaponSpec) -> tuple[dict, dict[str, str]]:
        root_guid = self._guid(spec.weapon_id, "root")
        model_guid = self._guid(spec.weapon_id, "model")
        renderer_guid = self._guid(spec.weapon_id, "renderer")
        equipment_guid = self._guid(spec.weapon_id, "equipment")
        equipment = {
            "__type": builder.EQUIPMENT_TYPE,
            "__guid": equipment_guid,
            "__enabled": True,
            "Flags": 0,
            "Handedness": "Right",
            "HoldType": spec.hold_type,
            "ModelRenderer": {
                "_type": "component",
                "component_id": renderer_guid,
                "go": model_guid,
                "component_type": "SkinnedModelRenderer",
            },
            "OnComponentDestroy": None,
            "OnComponentDisabled": None,
            "OnComponentEnabled": None,
            "OnComponentFixedUpdate": None,
            "OnComponentStart": None,
            "OnComponentUpdate": None,
        }
        renderer = {
            "__type": "Sandbox.SkinnedModelRenderer",
            "__guid": renderer_guid,
            "__enabled": True,
            "Flags": 0,
            "Model": f"addons/lifepunch/lpweapons/{spec.weapon_id}/models/test.vmdl",
        }
        model = self._node(
            guid=model_guid,
            name="Model",
            components=[renderer],
        )
        root = self._node(
            guid=root_guid,
            name=spec.root_name,
            components=[equipment],
            children=[model],
        )
        payload = {
            "RootObject": root,
            "ResourceVersion": 1,
            "ShowInMenu": False,
            "MenuPath": "",
            "MenuIcon": None,
            "DontBreakAsTemplate": False,
            "__references": [],
            "__version": 1,
        }
        return payload, {
            "root": root_guid,
            "model": model_guid,
            "renderer": renderer_guid,
            "equipment": equipment_guid,
        }

    def _write_repo(
        self,
        root: Path,
        *,
        broken_reference_for: str | None = None,
    ) -> tuple[dict[str, bytes], dict[str, dict[str, str]]]:
        source_bytes: dict[str, bytes] = {}
        guids: dict[str, dict[str, str]] = {}
        for spec in builder.SUPPORTED_WEAPONS:
            payload, weapon_guids = self._prefab(spec)
            if spec.weapon_id == broken_reference_for:
                equipment = payload["RootObject"]["Components"][0]
                equipment["ModelRenderer"]["component_id"] = self._guid(
                    spec.weapon_id, "missing"
                )
            raw = (json.dumps(payload, indent=2) + "\n").encode("utf-8")
            pure_path = PurePosixPath(spec.world_prefab_path)
            path = root.joinpath(*pure_path.parts)
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)
            source_bytes[spec.weapon_id] = raw
            guids[spec.weapon_id] = weapon_guids
        return source_bytes, guids

    def _entry(
        self,
        spec: builder.WeaponSpec,
        raw: bytes,
        parent_guid: str,
        ordinal: int,
    ) -> dict:
        return {
            "weapon_id": spec.weapon_id,
            "accepted": True,
            "source_sha256": hashlib.sha256(raw).hexdigest().upper(),
            "world_prefab_path": spec.world_prefab_path,
            "hold_type": spec.hold_type,
            "left_grip": {
                "parent_guid": parent_guid,
                "position": f"{ordinal + 1}.25,-{ordinal + 2}.5,0.{ordinal + 3}",
                "rotation": "0,0,0,1",
                "scale": "1,1,1",
            },
        }

    def _write_manifest(
        self,
        path: Path,
        entries: list[dict],
    ) -> None:
        path.write_text(
            json.dumps({"version": 1, "weapons": entries}, indent=2) + "\n",
            encoding="utf-8",
        )

    def _fixture_manifest(
        self,
        root: Path,
        source_bytes: dict[str, bytes],
        guids: dict[str, dict[str, str]],
        *,
        reverse: bool = False,
    ) -> Path:
        entries = [
            self._entry(spec, source_bytes[spec.weapon_id], guids[spec.weapon_id]["model"], index)
            for index, spec in enumerate(builder.SUPPORTED_WEAPONS)
        ]
        if reverse:
            entries.reverse()
        path = root / ("accepted-reverse.json" if reverse else "accepted.json")
        self._write_manifest(path, entries)
        return path

    def _output_bytes(self, report: dict) -> dict[str, bytes]:
        output = Path(report["output_directory"])
        return {
            path.name: path.read_bytes()
            for path in output.iterdir()
            if path.is_file()
        }

    def _component(self, candidate: dict) -> tuple[dict, builder.PrefabIndex]:
        prefab_index = builder.index_prefab(candidate)
        matches = [
            component
            for component in prefab_index.components_by_guid.values()
            if component.get("__type") == builder.COMPONENT_TYPE
        ]
        self.assertEqual(len(matches), 1)
        return matches[0], prefab_index

    def test_current_four_world_prefabs_pass_read_only_source_contract(self) -> None:
        reports = builder.inspect_supported_sources()
        self.assertEqual(
            [report["weapon_id"] for report in reports],
            ["aks74u", "ar15", "sr25", "m870"],
        )
        self.assertEqual(
            [report["hold_type"] for report in reports],
            ["Rifle", "Rifle", "Rifle", "Shotgun"],
        )
        self.assertTrue(all(report["reference_closure"] for report in reports))
        self.assertTrue(all(report["guid_count"] > 0 for report in reports))

    def test_four_weapon_output_is_temp_only_closed_and_deterministic(self) -> None:
        with tempfile.TemporaryDirectory(prefix="dxrp-hold-fixture-") as directory:
            repo_root = Path(directory)
            source_bytes, guids = self._write_repo(repo_root)
            manifest = self._fixture_manifest(repo_root, source_bytes, guids)
            reverse_manifest = self._fixture_manifest(
                repo_root,
                source_bytes,
                guids,
                reverse=True,
            )

            first = builder.generate_temp_candidates(manifest, repo_root)
            second = builder.generate_temp_candidates(reverse_manifest, repo_root)
            first_output = Path(first["output_directory"])
            second_output = Path(second["output_directory"])
            self.addCleanup(shutil.rmtree, first_output, True)
            self.addCleanup(shutil.rmtree, second_output, True)

            self.assertTrue(first["temporary_only"])
            self.assertEqual(first["candidate_count"], 4)
            self.assertNotEqual(first_output, second_output)
            self.assertFalse(builder._is_within(first_output, repo_root.resolve()))
            self.assertEqual(self._output_bytes(first), self._output_bytes(second))
            self.assertEqual(
                first["manifest_contract_sha256"],
                second["manifest_contract_sha256"],
            )
            self.assertEqual(first["index_sha256"], second["index_sha256"])

            output_bytes = self._output_bytes(first)
            index_payload = json.loads(output_bytes["candidate-index.json"])
            self.assertTrue(index_payload["temporary_only"])
            self.assertIn("no right-hand IK", index_payload["right_hand_policy"])
            self.assertEqual(
                [item["weapon_id"] for item in index_payload["candidates"]],
                ["aks74u", "ar15", "m870", "sr25"],
            )

            for index, spec in enumerate(builder.SUPPORTED_WEAPONS):
                with self.subTest(weapon=spec.weapon_id):
                    candidate = json.loads(output_bytes[spec.output_name])
                    component, prefab_index = self._component(candidate)
                    equipment = [
                        value
                        for value in prefab_index.components_by_guid.values()
                        if value.get("__type") == builder.EQUIPMENT_TYPE
                    ]
                    self.assertEqual(len(equipment), 1)
                    self.assertEqual(equipment[0]["HoldType"], spec.hold_type)
                    self.assertEqual(equipment[0]["Handedness"], "Right")
                    self.assertEqual(
                        component["Equipment"]["component_id"],
                        guids[spec.weapon_id]["equipment"],
                    )
                    self.assertEqual(
                        component["Equipment"]["go"],
                        guids[spec.weapon_id]["root"],
                    )
                    grip_guid = component["LeftGrip"]["go"]
                    grip = prefab_index.nodes_by_guid[grip_guid].node
                    expected = self._entry(
                        spec,
                        source_bytes[spec.weapon_id],
                        guids[spec.weapon_id]["model"],
                        index,
                    )["left_grip"]
                    self.assertEqual(grip["Name"], "LeftGrip")
                    self.assertEqual(grip["Position"], expected["position"])
                    self.assertEqual(grip["Rotation"], expected["rotation"])
                    self.assertEqual(grip["Scale"], expected["scale"])
                    parent = prefab_index.nodes_by_guid[
                        guids[spec.weapon_id]["model"]
                    ].node
                    self.assertIn(grip, parent["Children"])
                    serialized = json.dumps(component, sort_keys=True)
                    self.assertNotIn("RightGrip", serialized)
                    self.assertNotIn("IkRightHand", serialized)
                    self.assertNotIn("hand_right", serialized)

            for spec in builder.SUPPORTED_WEAPONS:
                source_path = repo_root.joinpath(
                    *PurePosixPath(spec.world_prefab_path).parts
                )
                self.assertEqual(
                    source_path.read_bytes(),
                    source_bytes[spec.weapon_id],
                )

    def test_check_only_renders_hashes_without_creating_output(self) -> None:
        with tempfile.TemporaryDirectory(prefix="dxrp-hold-check-") as directory:
            repo_root = Path(directory)
            source_bytes, guids = self._write_repo(repo_root)
            manifest = self._fixture_manifest(repo_root, source_bytes, guids)
            rendered, index_bytes, manifest_sha = builder.render_candidate_set(
                manifest,
                repo_root,
            )
            report = builder._check_report(rendered, index_bytes, manifest_sha)
            self.assertFalse(report["wrote_files"])
            self.assertEqual(report["candidate_count"], 4)
            self.assertEqual(len(list(repo_root.glob(f"{builder.TEMP_PREFIX}*"))), 0)

    def test_unaccepted_or_incomplete_grips_fail_before_temp_output(self) -> None:
        with (
            tempfile.TemporaryDirectory(prefix="dxrp-hold-repo-") as repo_directory,
            tempfile.TemporaryDirectory(prefix="dxrp-hold-output-") as output_directory,
        ):
            repo_root = Path(repo_directory)
            temp_parent = Path(output_directory)
            source_bytes, guids = self._write_repo(repo_root)
            spec = builder.SUPPORTED_WEAPONS[0]
            valid = self._entry(
                spec,
                source_bytes[spec.weapon_id],
                guids[spec.weapon_id]["model"],
                0,
            )
            cases = []
            unaccepted = copy.deepcopy(valid)
            unaccepted["accepted"] = False
            cases.append(("unaccepted", unaccepted))
            incomplete = copy.deepcopy(valid)
            del incomplete["left_grip"]["scale"]
            cases.append(("incomplete", incomplete))
            malformed = copy.deepcopy(valid)
            malformed["left_grip"]["position"] = "0, 0,0"
            cases.append(("malformed", malformed))
            non_normalized = copy.deepcopy(valid)
            non_normalized["left_grip"]["rotation"] = "0,0,0,2"
            cases.append(("non_normalized", non_normalized))

            for label, entry in cases:
                with self.subTest(case=label):
                    manifest = repo_root / f"{label}.json"
                    self._write_manifest(manifest, [entry])
                    with self.assertRaises(builder.ContractError):
                        builder.generate_temp_candidates(
                            manifest,
                            repo_root,
                            temp_parent=temp_parent,
                        )
                    self.assertEqual(list(temp_parent.iterdir()), [])

    def test_wrong_pin_path_hold_type_or_parent_fails_closed(self) -> None:
        with (
            tempfile.TemporaryDirectory(prefix="dxrp-hold-repo-") as repo_directory,
            tempfile.TemporaryDirectory(prefix="dxrp-hold-output-") as output_directory,
        ):
            repo_root = Path(repo_directory)
            temp_parent = Path(output_directory)
            source_bytes, guids = self._write_repo(repo_root)
            spec = builder.SUPPORTED_WEAPONS[0]
            valid = self._entry(
                spec,
                source_bytes[spec.weapon_id],
                guids[spec.weapon_id]["model"],
                0,
            )
            cases: list[tuple[str, dict]] = []
            wrong_pin = copy.deepcopy(valid)
            wrong_pin["source_sha256"] = "A" * 64
            cases.append(("wrong-pin", wrong_pin))
            wrong_path = copy.deepcopy(valid)
            wrong_path["world_prefab_path"] = (
                "game/Assets/addons/lifepunch/lpweapons/aks74u/equipment/w_other.prefab"
            )
            cases.append(("wrong-path", wrong_path))
            wrong_hold = copy.deepcopy(valid)
            wrong_hold["hold_type"] = "Pistol"
            cases.append(("wrong-hold", wrong_hold))
            wrong_parent = copy.deepcopy(valid)
            wrong_parent["left_grip"]["parent_guid"] = self._guid("missing-parent")
            cases.append(("wrong-parent", wrong_parent))

            for label, entry in cases:
                with self.subTest(case=label):
                    manifest = repo_root / f"{label}.json"
                    self._write_manifest(manifest, [entry])
                    with self.assertRaises(builder.ContractError):
                        builder.generate_temp_candidates(
                            manifest,
                            repo_root,
                            temp_parent=temp_parent,
                        )
                    self.assertEqual(list(temp_parent.iterdir()), [])

    def test_pistol_entries_are_explicitly_rejected_with_no_output(self) -> None:
        with tempfile.TemporaryDirectory(prefix="dxrp-hold-pistol-") as directory:
            root = Path(directory)
            manifest = root / "pistol.json"
            self._write_manifest(
                manifest,
                [
                    {
                        "weapon_id": "m1911",
                        "accepted": True,
                        "source_sha256": "A" * 64,
                        "world_prefab_path": "unused",
                        "hold_type": "Pistol",
                        "left_grip": {
                            "parent_guid": self._guid("pistol"),
                            "position": "0,0,0",
                            "rotation": "0,0,0,1",
                            "scale": "1,1,1",
                        },
                    }
                ],
            )
            with self.assertRaisesRegex(builder.ContractError, "is a pistol"):
                builder.load_manifest(manifest)
            self.assertEqual(
                set(builder.PISTOL_WEAPON_IDS) & set(builder.SPECS_BY_ID),
                set(),
            )

    def test_dangling_source_reference_is_rejected_before_output(self) -> None:
        with (
            tempfile.TemporaryDirectory(prefix="dxrp-hold-repo-") as repo_directory,
            tempfile.TemporaryDirectory(prefix="dxrp-hold-output-") as output_directory,
        ):
            repo_root = Path(repo_directory)
            temp_parent = Path(output_directory)
            source_bytes, guids = self._write_repo(
                repo_root,
                broken_reference_for="ar15",
            )
            spec = builder.SPECS_BY_ID["ar15"]
            entry = self._entry(
                spec,
                source_bytes[spec.weapon_id],
                guids[spec.weapon_id]["model"],
                1,
            )
            manifest = repo_root / "broken-reference.json"
            self._write_manifest(manifest, [entry])
            with self.assertRaisesRegex(builder.ContractError, "Dangling component"):
                builder.generate_temp_candidates(
                    manifest,
                    repo_root,
                    temp_parent=temp_parent,
                )
            self.assertEqual(list(temp_parent.iterdir()), [])

    def test_temp_parent_inside_repository_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory(prefix="dxrp-hold-repo-") as directory:
            repo_root = Path(directory)
            source_bytes, guids = self._write_repo(repo_root)
            spec = builder.SUPPORTED_WEAPONS[0]
            manifest = repo_root / "accepted.json"
            self._write_manifest(
                manifest,
                [
                    self._entry(
                        spec,
                        source_bytes[spec.weapon_id],
                        guids[spec.weapon_id]["model"],
                        0,
                    )
                ],
            )
            forbidden_parent = repo_root / "candidate-output"
            forbidden_parent.mkdir()
            with self.assertRaisesRegex(builder.ContractError, "outside the repository"):
                builder.generate_temp_candidates(
                    manifest,
                    repo_root,
                    temp_parent=forbidden_parent,
                )
            self.assertEqual(list(forbidden_parent.iterdir()), [])

    def test_existing_directory_outside_system_temp_is_rejected(self) -> None:
        system_temp = Path(tempfile.gettempdir()).resolve()
        outside_temp = builder.WORKSPACE_ROOT.parent.resolve()
        if builder._is_within(outside_temp, system_temp):
            self.skipTest("Workspace parent is beneath system temp on this host")
        with self.assertRaisesRegex(
            builder.ContractError,
            "beneath the operating system temp directory",
        ):
            builder._verified_temp_parent(builder.WORKSPACE_ROOT, outside_temp)


if __name__ == "__main__":
    unittest.main()
