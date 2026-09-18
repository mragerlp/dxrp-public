"""Focused tests for the narrow Desert Eagle TEMP correction builder."""

from __future__ import annotations

import copy
import json
import tempfile
import unittest
from pathlib import Path
from unittest import mock

import build_deserteagle_minimal_correction_candidate as builder


EXPECTED_OUTPUT_BYTES = 74_373
EXPECTED_OUTPUT_SHA256 = (
    "27CDBFD846AF3A08637A05C8577428BC3C30B4420259DD5CDE5502C4E4FFB6C7"
)
EXPECTED_WRAPPER_TRANSFORM = {
    "Position": "-0.81244256471166221,0,-0.15842932801374754",
    "Rotation": "0,-0.79863533644524842,0,0.60181525352967336",
    "Scale": "1.0000000000001106,1,1.0000000000001104",
}


class DesertEagleMinimalCorrectionCandidateTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.active_bytes = builder.ACTIVE_PREFAB_PATH.read_bytes()
        cls.active = builder.common_origin._decode_prefab(
            cls.active_bytes, "test active Desert Eagle"
        )

    def _candidate_without_writing(self) -> tuple[dict, dict]:
        return builder.transform_prefab(copy.deepcopy(self.active), self.active_bytes)

    def test_live_input_pins_and_active_contract_pass(self) -> None:
        payloads = builder.validate_source_pins()
        self.assertEqual(set(payloads), set(builder.SOURCE_PINS))
        self.assertEqual(
            builder._sha256(self.active_bytes), builder.EXPECTED_ACTIVE_SHA256
        )
        self.assertEqual(len(self.active_bytes), builder.EXPECTED_ACTIVE_BYTES)
        index, view_model = builder.validate_active_prefab(
            copy.deepcopy(self.active), self.active_bytes
        )
        self.assertNotIn("AdditionalRendererRoot", view_model)
        self.assertEqual(index.nodes_by_path[builder.CUSTOM_ROOT_PATH]["__guid"], builder.CUSTOM_ROOT_GUID)
        self.assertEqual(
            index.nodes_by_path[builder.SLIDE_RENDERER_PATH]["Position"],
            builder.EXPECTED_SLIDE_TRANSFORM["Position"],
        )

    def test_candidate_is_deterministic_temp_only_and_exactly_scoped(self) -> None:
        active_before = builder.ACTIVE_PREFAB_PATH.read_bytes()
        with tempfile.TemporaryDirectory(prefix="dxrp-deagle-minimal-test-") as root:
            root_path = Path(root)
            first = builder.build_candidate(root_path / "first")
            second = builder.build_candidate(root_path / "second")

            first_path = Path(first["output"]["path"])
            second_path = Path(second["output"]["path"])
            first_bytes = first_path.read_bytes()
            second_bytes = second_path.read_bytes()
            self.assertEqual(first_bytes, second_bytes)
            self.assertEqual(len(first_bytes), EXPECTED_OUTPUT_BYTES)
            self.assertEqual(builder._sha256(first_bytes), EXPECTED_OUTPUT_SHA256)
            self.assertEqual(first["output"]["bytes"], EXPECTED_OUTPUT_BYTES)
            self.assertEqual(first["output"]["sha256"], EXPECTED_OUTPUT_SHA256)
            self.assertEqual(
                first["mode"], "TEMP_ONLY_NO_PRODUCT_WRITE_MODE"
            )
            self.assertEqual(
                first["writes"],
                {
                    "system_temp": True,
                    "game": False,
                    "docs": False,
                    "editor": False,
                    "portal": False,
                    "git": False,
                },
            )
            self.assertTrue(first_path.is_relative_to(Path(tempfile.gettempdir())))

            candidate = json.loads(first_bytes.decode("utf-8"))
            source = copy.deepcopy(self.active)
            before_index = builder.common_origin.index_prefab(source)
            after_index = builder.common_origin.index_prefab(candidate)
            self.assertEqual(
                after_index.all_guids - before_index.all_guids,
                {builder.WRAPPER_GUID},
            )
            self.assertFalse(before_index.all_guids - after_index.all_guids)
            self.assertEqual(
                before_index.component_owners, after_index.component_owners
            )

            view_model = after_index.components_by_guid[builder.VIEW_MODEL_GUID]
            self.assertEqual(
                view_model["AdditionalRendererRoot"],
                {"_type": "gameobject", "go": builder.CUSTOM_ROOT_GUID},
            )
            wrapper = after_index.nodes_by_guid[builder.WRAPPER_GUID]
            self.assertEqual(wrapper["Name"], builder.WRAPPER_NAME)
            self.assertEqual(
                builder._node_transform_payload(wrapper),
                EXPECTED_WRAPPER_TRANSFORM,
            )
            self.assertEqual(wrapper["Components"], [])
            self.assertEqual(
                [child["__guid"] for child in wrapper["Children"]],
                [builder.MAGAZINE_RENDERER_GUID],
            )
            self.assertEqual(
                after_index.parent_by_guid[builder.WRAPPER_GUID],
                builder.MAGAZINE_PARENT_GUID,
            )
            self.assertEqual(
                after_index.parent_by_guid[builder.MAGAZINE_RENDERER_GUID],
                builder.WRAPPER_GUID,
            )

            # Remove precisely the two allowed structural changes.  The exact
            # active object must then be recovered, including the coherent slide.
            stripped = copy.deepcopy(candidate)
            stripped_index = builder.common_origin.index_prefab(stripped)
            stripped_index.components_by_guid[builder.VIEW_MODEL_GUID].pop(
                "AdditionalRendererRoot"
            )
            parent = stripped_index.nodes_by_guid[builder.MAGAZINE_PARENT_GUID]
            parent["Children"] = parent["Children"][0]["Children"]
            self.assertEqual(stripped, source)
            self.assertEqual(
                after_index.nodes_by_guid[
                    before_index.nodes_by_path[builder.BODY_RENDERER_PATH]["__guid"]
                ]["Position"],
                builder.EXPECTED_BODY_TRANSFORM["Position"],
            )
            self.assertEqual(
                after_index.nodes_by_guid[
                    before_index.nodes_by_path[builder.SLIDE_RENDERER_PATH]["__guid"]
                ]["Position"],
                builder.EXPECTED_SLIDE_TRANSFORM["Position"],
            )

        self.assertEqual(builder.ACTIVE_PREFAB_PATH.read_bytes(), active_before)

    def test_p_times_w_times_r_reconstructs_existing_body_assembly(self) -> None:
        candidate, proof = self._candidate_without_writing()
        index = builder.common_origin.index_prefab(candidate)
        wrapper = index.nodes_by_guid[builder.WRAPPER_GUID]
        body = index.nodes_by_path[builder.BODY_RENDERER_PATH]
        magazine = index.nodes_by_guid[builder.MAGAZINE_RENDERER_GUID]

        root_bind = builder.common_origin._parse_manifest_transform(
            builder.common_origin.COMPILED_BIND_TRANSFORMS["B_root"], "B_root"
        )
        parent_p = builder.common_origin._parse_manifest_transform(
            builder.common_origin.COMPILED_BIND_TRANSFORMS["B_mag"], "P"
        )
        wrapper_w = builder.common_origin.transform_matrix(
            builder.common_origin._transform_from_node(wrapper, "W")
        )
        renderer_r = builder.common_origin.transform_matrix(
            builder.common_origin._transform_from_node(magazine, "R")
        )
        body_r = builder.common_origin.transform_matrix(
            builder.common_origin._transform_from_node(body, "R_body")
        )
        expected_a = builder.common_origin.matrix_multiply(root_bind, body_r)
        actual_a = builder.common_origin.matrix_multiply(
            builder.common_origin.matrix_multiply(parent_p, wrapper_w), renderer_r
        )
        delta = builder.common_origin.matrix_max_delta(actual_a, expected_a)
        self.assertLess(delta, 1e-14)
        self.assertEqual(
            proof["transform"]["law"],
            "P * W * R = A; W = inverse(P) * A * inverse(R)",
        )
        self.assertAlmostEqual(
            proof["transform"]["reconstruction_max_delta"], delta, places=18
        )
        self.assertAlmostEqual(
            proof["transform"][
                "preserved_slide_compiled_bind_to_body_max_delta"
            ],
            0.0006807000000002006,
            places=15,
        )

    def test_structural_drift_is_rejected_even_if_reparsed(self) -> None:
        slide_drift = copy.deepcopy(self.active)
        slide_index = builder.common_origin.index_prefab(slide_drift)
        slide_index.nodes_by_path[builder.SLIDE_RENDERER_PATH]["Position"] = "1,2,3"
        slide_bytes = builder.common_origin._serialize_prefab(
            slide_drift, self.active_bytes
        )
        with self.assertRaisesRegex(builder.ContractError, "local transform drifted"):
            builder.validate_active_prefab(slide_drift, slide_bytes)

        property_drift = copy.deepcopy(self.active)
        property_index = builder.common_origin.index_prefab(property_drift)
        property_index.components_by_guid[builder.VIEW_MODEL_GUID][
            "AdditionalRendererRoot"
        ] = {"_type": "gameobject", "go": builder.CUSTOM_ROOT_GUID}
        property_bytes = builder.common_origin._serialize_prefab(
            property_drift, self.active_bytes
        )
        with self.assertRaisesRegex(
            builder.ContractError, "already has AdditionalRendererRoot"
        ):
            builder.validate_active_prefab(property_drift, property_bytes)

    def test_byte_pin_drift_fails_closed(self) -> None:
        with self.assertRaisesRegex(builder.ContractError, "byte pin mismatch"):
            builder._verify_file_pin(
                builder.ACTIVE_PREFAB_PATH,
                builder.EXPECTED_ACTIVE_BYTES + 1,
                builder.EXPECTED_ACTIVE_SHA256,
                "active Desert Eagle prefab",
            )
        with tempfile.TemporaryDirectory(prefix="dxrp-deagle-pin-drift-") as root:
            path = Path(root) / "material.vmat"
            path.write_bytes(b"drift")
            with self.assertRaisesRegex(builder.ContractError, "SHA-256 pin mismatch"):
                builder._verify_file_pin(
                    path,
                    5,
                    "0" * 64,
                    "source material",
                )

    def test_source_failure_creates_no_output(self) -> None:
        with tempfile.TemporaryDirectory(prefix="dxrp-deagle-fail-closed-") as root:
            output = Path(root) / "must-not-exist"
            with mock.patch.object(
                builder,
                "validate_source_pins",
                side_effect=builder.ContractError("injected source drift"),
            ):
                with self.assertRaisesRegex(builder.ContractError, "source drift"):
                    builder.build_candidate(output)
            self.assertFalse(output.exists())

    def test_repository_temp_root_and_nonempty_outputs_are_rejected(self) -> None:
        forbidden = builder.WORKBENCH_ROOT / "tools/weapon_pipeline/forbidden-output"
        with self.assertRaises(RuntimeError):
            builder._prepare_output_directory(forbidden)
        self.assertFalse(forbidden.exists())

        with self.assertRaises(builder.ContractError):
            builder._prepare_output_directory(Path(tempfile.gettempdir()))

        with tempfile.TemporaryDirectory(prefix="dxrp-deagle-nonempty-") as root:
            output = Path(root) / "output"
            output.mkdir()
            (output / "occupied.txt").write_text("occupied", encoding="utf-8")
            with self.assertRaisesRegex(builder.ContractError, "must be empty"):
                builder._prepare_output_directory(output)

    def test_cli_exposes_no_product_write_switch(self) -> None:
        parser = builder._parser()
        option_strings = {
            option
            for action in parser._actions
            for option in action.option_strings
        }
        self.assertEqual(
            option_strings,
            {"-h", "--help", "--output-directory"},
        )
        with self.assertRaises(SystemExit):
            parser.parse_args(["--product-write"])


if __name__ == "__main__":
    unittest.main()
