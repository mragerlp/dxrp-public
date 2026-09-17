"""Focused contracts for the strict-TEMP AKS-74U bolt-split candidate."""

from __future__ import annotations

import copy
import json
import math
import os
import tempfile
import unittest
from pathlib import Path
from unittest import mock

import build_aks74u_bolt_split_candidate as builder


HERE = Path(__file__).resolve().parent
BUNDLE_ROOT = HERE.parents[1]
INPUT_MANIFEST = Path(
    os.environ.get(
        "DXRP_AKS74U_BOLT_INPUT_MANIFEST",
        BUNDLE_ROOT / "aks74u_bolt_split_inputs.json",
    )
)


class Aks74uBoltSplitCandidateTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        if not INPUT_MANIFEST.is_file():
            raise unittest.SkipTest(
                "set DXRP_AKS74U_BOLT_INPUT_MANIFEST to a byte-pinned strict-TEMP manifest"
            )
        cls.contract = builder.load_input_contract(INPUT_MANIFEST)
        cls.base_bytes = cls.contract.pins["base_fit_candidate_prefab"].path.read_bytes()
        cls.base = json.loads(cls.base_bytes)

    def test_inverse_parent_math_round_trips_nontrivial_transform(self) -> None:
        root_half = math.sqrt(0.5)
        parent = builder.Transform(
            (3.25, -2.0, 7.5),
            (0.0, root_half, 0.0, root_half),
            (2.0, 3.0, 4.0),
        )
        desired = builder.Transform(
            (-1.5, 8.0, 2.25),
            (root_half, 0.0, 0.0, root_half),
            (0.5, 0.75, 1.25),
        )
        local = builder.inverse_parent_compose(parent, desired)
        self.assertTrue(
            builder._almost_equal(builder.compose(parent, local), desired, tolerance=1e-7)
        )
        self.assertFalse(
            builder._almost_equal(builder.compose(parent, desired), desired, tolerance=1e-3)
        )

    def test_exact_bolt_bind_produces_expected_wrapper(self) -> None:
        candidate, graph = builder.build_candidate_prefab(
            self.base, self.contract.compiled_bind
        )
        self.assertEqual(graph["base_node_count"], 84)
        self.assertEqual(graph["candidate_node_count"], 85)
        self.assertEqual(
            graph["bolt_renderer_local_W"],
            {
                "position": "-3.0849904,0.13541761,4.4021212",
                "rotation": "0,0,0,1",
                "scale": "0.89572925,0.89572925,0.89572925",
            },
        )
        index = builder.index_prefab(candidate, "unit candidate")
        builder._renderer(
            index.nodes[builder.BODY_PATH], builder.BODY_MINUS_MODEL, "unit body"
        )
        builder._renderer(
            index.nodes[builder.BOLT_PARENT_PATH + "/aks74u_bolt"],
            builder.BOLT_MODEL,
            "unit bolt",
        )
        base_index = builder.index_prefab(self.base, "unit base")
        self.assertEqual(
            index.nodes[builder.MAGAZINE_PATH],
            base_index.nodes[builder.MAGAZINE_PATH],
        )
        for path in (builder.SELECTOR_PATH, builder.TRIGGER_PATH, builder.STOCK_PATH):
            self.assertEqual(index.nodes[path], base_index.nodes[path])

    def test_graph_rejects_missing_or_prepopulated_bolt_node(self) -> None:
        missing = copy.deepcopy(self.base)
        index = builder.index_prefab(missing, "missing fixture")
        parent = index.nodes[builder.ASSEMBLY_PATH]
        parent["Children"] = [child for child in parent["Children"] if child["Name"] != "bolt"]
        with self.assertRaisesRegex(builder.ContractError, "missing required nodes"):
            builder.audit_base_prefab(missing)

        occupied = copy.deepcopy(self.base)
        index = builder.index_prefab(occupied, "occupied fixture")
        index.nodes[builder.BOLT_PARENT_PATH]["Children"].append(
            copy.deepcopy(index.nodes[builder.MAGAZINE_PATH])
        )
        with self.assertRaisesRegex(builder.ContractError, "duplicate path/GUID|not empty"):
            builder.audit_base_prefab(occupied)

    def test_graph_rejects_wrong_body_model_and_duplicate_guid(self) -> None:
        wrong_model = copy.deepcopy(self.base)
        index = builder.index_prefab(wrong_model, "wrong model fixture")
        renderer = builder._renderer(
            index.nodes[builder.BODY_PATH], builder.CURRENT_BODY_MODEL, "fixture body"
        )
        renderer["Model"] = "invented/body.vmdl"
        with self.assertRaisesRegex(builder.ContractError, "expected exactly one renderer"):
            builder.audit_base_prefab(wrong_model)

        duplicate = copy.deepcopy(self.base)
        index = builder.index_prefab(duplicate, "duplicate fixture setup")
        index.nodes[builder.BODY_PATH]["__guid"] = index.nodes[builder.BOLT_PARENT_PATH][
            "__guid"
        ]
        with self.assertRaisesRegex(builder.ContractError, "duplicate path/GUID"):
            builder.index_prefab(duplicate, "duplicate fixture")

    def test_input_manifest_pin_bind_and_topology_fail_closed(self) -> None:
        payload = json.loads(INPUT_MANIFEST.read_bytes())
        with tempfile.TemporaryDirectory(prefix="dxrp_aks74u_bolt_pin_test_") as directory:
            root = Path(directory)
            wrong_pin = copy.deepcopy(payload)
            wrong_pin["inputs"]["authoritative_fbx"]["sha256"] = "0" * 64
            wrong_pin_path = root / "wrong-pin.json"
            wrong_pin_path.write_bytes(builder._json_bytes(wrong_pin))
            with self.assertRaisesRegex(builder.ContractError, "SHA-256 mismatch"):
                builder.load_input_contract(wrong_pin_path)

            wrong_bind = copy.deepcopy(payload)
            wrong_bind["compiled_bolt_bind"]["transform"]["position"] = "0,0,0"
            wrong_bind_path = root / "wrong-bind.json"
            wrong_bind_path.write_bytes(builder._json_bytes(wrong_bind))
            with self.assertRaisesRegex(builder.ContractError, "bolt bind"):
                builder.load_input_contract(wrong_bind_path)

            wrong_topology = copy.deepcopy(payload)
            wrong_topology["topology_contract"]["bolt_faces"] = 829
            wrong_topology_path = root / "wrong-topology.json"
            wrong_topology_path.write_bytes(builder._json_bytes(wrong_topology))
            with self.assertRaisesRegex(builder.ContractError, "topology contract"):
                builder.load_input_contract(wrong_topology_path)

    def test_output_containment_rejects_non_temp_root_nonempty_and_reparse(self) -> None:
        with self.assertRaisesRegex(builder.ContractError, "strict child"):
            builder.assert_temp_output_directory(
                Path(r"D:\Cavelux\dxrp-workbench\tools\weapon_pipeline\forbidden")
            )
        with self.assertRaisesRegex(builder.ContractError, "strict child"):
            builder.assert_temp_output_directory(Path(tempfile.gettempdir()))
        with tempfile.TemporaryDirectory(prefix="dxrp_aks74u_bolt_nonempty_") as directory:
            root = Path(directory)
            output = root / "candidate"
            output.mkdir()
            (output / "occupied.txt").write_text("occupied", encoding="utf-8")
            with self.assertRaisesRegex(builder.ContractError, "must be empty"):
                builder.assert_temp_output_directory(output)

            link = root / "reparse"
            try:
                link.symlink_to(root, target_is_directory=True)
            except OSError:
                simulated = root / "simulated-reparse" / "candidate"
                with mock.patch.object(
                    builder,
                    "_is_reparse",
                    side_effect=lambda path: path.name == "simulated-reparse",
                ):
                    with self.assertRaisesRegex(builder.ContractError, "Reparse-point"):
                        builder.assert_temp_output_directory(simulated)
            else:
                with self.assertRaisesRegex(builder.ContractError, "Reparse-point"):
                    builder.assert_temp_output_directory(link / "candidate")

    def test_modeldocs_are_static_common_origin_and_material_pinned(self) -> None:
        body = builder._modeldoc(
            builder.BODY_FBX_ASSET, "SK_Rif_SLR_AK47_body_minus_bolt"
        ).decode("utf-8")
        bolt = builder._modeldoc(
            builder.BOLT_FBX_ASSET, "SK_Rif_SLR_AK47_bolt"
        ).decode("utf-8")
        for text in (body, bolt):
            self.assertIn('import_translation = [ 0.0, 0.0, 0.0 ]', text)
            self.assertIn('import_rotation = [ 0.0, 0.0, 0.0 ]', text)
            self.assertIn('import_scale = 0.3937008', text)
            self.assertIn(f'from = "{builder.SOURCE_MATERIAL}"', text)
            self.assertIn(f'to = "{builder.BODY_MATERIAL}"', text)
            self.assertNotIn("tag_fireselector", text)
            self.assertNotIn("tag_trigger", text)
            self.assertNotIn("tag_stock", text)

    def test_cli_has_no_product_write_switch(self) -> None:
        destinations = {action.dest for action in builder._parser()._actions}
        self.assertEqual(destinations, {"help", "input_manifest", "output_dir"})
        self.assertNotIn("allow_game_write", destinations)
        self.assertNotIn("product", destinations)

    @unittest.skipUnless(
        os.environ.get("DXRP_RUN_BLENDER_INTEGRATION") == "1",
        "set DXRP_RUN_BLENDER_INTEGRATION=1 for the pinned Blender round trip",
    )
    def test_live_build_is_deterministic_and_complete(self) -> None:
        with tempfile.TemporaryDirectory(prefix="dxrp_aks74u_bolt_live_test_") as directory:
            root = Path(directory)
            first = builder.build_bundle(INPUT_MANIFEST, root / "first")
            second = builder.build_bundle(INPUT_MANIFEST, root / "second")
            first_root = Path(first["output_directory"])
            second_root = Path(second["output_directory"])
            first_files = {
                path.relative_to(first_root).as_posix(): path.read_bytes()
                for path in first_root.rglob("*")
                if path.is_file()
            }
            second_files = {
                path.relative_to(second_root).as_posix(): path.read_bytes()
                for path in second_root.rglob("*")
                if path.is_file()
            }
            self.assertEqual(first_files, second_files)
            manifest = first["manifest"]
            self.assertTrue(manifest["scope"]["first_person_only"])
            self.assertTrue(manifest["scope"]["split_only_tag_bolt"])
            self.assertFalse(manifest["scope"]["third_person_created_or_modified"])
            self.assertFalse(manifest["scope"]["magazine_mapping_changed"])
            self.assertEqual(
                manifest["topology_result"]["partition"]["bolt_faces"], 830
            )
            self.assertTrue(
                manifest["topology_result"]["post_export_reimport"][
                    "face_position_uv_material_multiset_closure"
                ]
            )


if __name__ == "__main__":
    unittest.main()
