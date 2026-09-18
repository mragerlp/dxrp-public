"""Focused tests for the TEMP-only M1911 common-origin candidate builder."""

from __future__ import annotations

import copy
import hashlib
import json
import tempfile
import unittest
from contextlib import redirect_stdout
from io import StringIO
from pathlib import Path
from unittest import mock

import build_m1911_common_origin_candidate as builder


EXPECTED_LOCALS = {
    "body": {
        "Position": "-0.88245009999999979,0.074942410000000001,3.4594364",
        "Rotation": "0,0,0,1",
        "Scale": "1,1,1",
    },
    "slide": {
        "Position": "-2.2053200999999998,0.074942410000000001,1.8289033999999997",
        "Rotation": "0,0,0,1",
        "Scale": "1,1,1",
    },
    "magazine": {
        "Position": "-3.8946310928797931,0.074942410000000001,-1.9602429314329672",
        "Rotation": "0,-0.79863533644524842,0,0.60181525352967336",
        "Scale": "1.0000000000001104,1,1.0000000000001104",
    },
}


class M1911CommonOriginCandidateTests(unittest.TestCase):
    def _write_manifest(
        self, directory: Path, payload: dict[str, object] | None = None
    ) -> tuple[Path, str, bytes]:
        path = directory / "m1911-compiled-bind.json"
        if payload is None:
            raw = builder.compiled_bind_manifest_bytes()
        else:
            raw = (
                json.dumps(payload, sort_keys=True, separators=(",", ":")) + "\n"
            ).encode("utf-8")
        path.write_bytes(raw)
        return path, hashlib.sha256(raw).hexdigest().upper(), raw

    def _build(self, directory: Path) -> tuple[dict[str, object], bytes]:
        manifest, manifest_sha, _ = self._write_manifest(directory)
        report = builder.build_candidate(
            output_directory=directory / "candidate",
            compiled_bind_path=manifest,
            compiled_bind_sha256=manifest_sha,
        )
        output = Path(report["output"]["path"])
        return report, output.read_bytes()

    def test_manifest_template_pins_m1911_and_keeps_seed_unaccepted(self) -> None:
        payload = builder.compiled_bind_manifest_template()
        self.assertEqual(payload["pose"], "compiled-bind")
        self.assertEqual(payload["coordinate_space"], "donor-view")
        self.assertEqual(payload["acceptance"], {"accepted": False, "visual_accepted": False})
        self.assertEqual(payload["body_seed"], builder.BODY_SEED)
        self.assertEqual(
            payload["pins"]["donor"],
            {
                "bytes": builder.EXPECTED_DONOR_BYTES,
                "sha256": builder.EXPECTED_DONOR_SHA256,
            },
        )
        self.assertEqual(
            payload["pins"]["source"],
            {
                "bytes": builder.EXPECTED_SOURCE_BYTES,
                "sha256": builder.EXPECTED_SOURCE_SHA256,
            },
        )
        self.assertEqual(
            payload["pins"]["materializer"],
            {
                "bytes": builder.EXPECTED_MATERIALIZER_BYTES,
                "sha256": builder.EXPECTED_MATERIALIZER_SHA256,
            },
        )
        self.assertEqual(
            payload["evidence"], builder.common_origin.compiled_bind_evidence_template()
        )
        self.assertEqual(
            payload["transforms"], builder.common_origin.COMPILED_BIND_TRANSFORMS
        )
        self.assertEqual(
            builder.compiled_bind_manifest_bytes(),
            builder.compiled_bind_manifest_bytes(),
        )

    def test_build_is_deterministic_exact_and_nonmutating(self) -> None:
        input_paths = (
            builder.DEFAULT_DONOR,
            builder.DEFAULT_SOURCE,
            builder.MATERIALIZER_PATH,
            builder.COMMON_ORIGIN_HELPERS_PATH,
        )
        before = {path: path.read_bytes() for path in input_paths}
        with tempfile.TemporaryDirectory(prefix="dxrp_m1911_common_origin_test_") as raw:
            directory = Path(raw)
            first_dir = directory / "first"
            second_dir = directory / "second"
            first_dir.mkdir()
            second_dir.mkdir()
            report_one, candidate_one = self._build(first_dir)
            report_two, candidate_two = self._build(second_dir)

        self.assertEqual(candidate_one, candidate_two)
        self.assertEqual(len(candidate_one), builder.EXPECTED_CANDIDATE_BYTES)
        self.assertEqual(
            hashlib.sha256(candidate_one).hexdigest().upper(),
            builder.EXPECTED_CANDIDATE_SHA256,
        )
        self.assertEqual(report_one["output"]["sha256"], builder.EXPECTED_CANDIDATE_SHA256)
        self.assertEqual(report_two["output"]["sha256"], builder.EXPECTED_CANDIDATE_SHA256)
        self.assertFalse(report_one["game_tree_written"])
        self.assertEqual(report_one["mode"], "TEMP_ONLY")
        self.assertEqual(report_one["source_nonmutation"], "PASS")
        self.assertEqual(report_one["acceptance"], builder.BODY_SEED_ACCEPTANCE)
        self.assertIn("visual false", report_one["proof_ceiling"])
        for path, data in before.items():
            self.assertEqual(path.read_bytes(), data)

        candidate = json.loads(candidate_one.decode("utf-8-sig"))
        index, contract = builder.validate_materialized_contract(
            candidate, require_authored_seed=False
        )
        self.assertEqual(contract["view_model_components"], 1)
        self.assertEqual(contract["view_model_guid"], builder.EXPECTED_VIEW_MODEL_GUID)
        root = candidate["RootObject"]
        self.assertEqual(root["__guid"], builder.EXPECTED_ROOT_GUID)
        root_view_models = [
            component
            for component in root["Components"]
            if component.get("__type") == "Dxura.RP.Game.ViewModel"
        ]
        self.assertEqual(len(root_view_models), 1)
        self.assertEqual(root_view_models[0]["__guid"], builder.EXPECTED_VIEW_MODEL_GUID)

        part_report = report_one["transform_report"]["parts"]
        for spec in builder.PART_SPECS:
            self.assertEqual(part_report[spec.role]["local"], EXPECTED_LOCALS[spec.role])
            node = index.nodes_by_guid[spec.object_guid]
            self.assertEqual(index.parent_by_guid[spec.object_guid], spec.parent_instance_guid)
            self.assertEqual(node["Components"][0]["__guid"], spec.renderer_guid)
            self.assertEqual(node["Components"][0]["Model"], spec.model)
            self.assertLessEqual(
                part_report[spec.role]["reconstruction_max_delta"],
                builder.common_origin.TRS_TOLERANCE,
            )
        self.assertEqual(report_one["transform_report"]["reconstruction"], "PASS")
        self.assertEqual(report_one["transform_report"]["copied_local_guard"], "PASS")
        self.assertLessEqual(
            report_one["transform_report"]["reconstruction_max_delta"],
            builder.common_origin.TRS_TOLERANCE,
        )

        for key, expected_hash in (
            ("donor", builder.EXPECTED_DONOR_SHA256),
            ("source", builder.EXPECTED_SOURCE_SHA256),
            ("materializer", builder.EXPECTED_MATERIALIZER_SHA256),
            ("common_origin_helpers", builder.EXPECTED_COMMON_ORIGIN_HELPERS_SHA256),
        ):
            self.assertEqual(report_one["inputs"][key]["sha256"], expected_hash)
        self.assertEqual(
            report_one["inputs"]["parser"]["sha256"],
            builder.common_origin.COMPILED_BIND_PARSER_SHA256,
        )
        self.assertEqual(
            report_one["inputs"]["compiled_model"]["sha256"],
            builder.common_origin.COMPILED_BIND_MODEL_SHA256,
        )

    def test_manifest_requires_full_sha_exact_pins_and_exact_usp_records(self) -> None:
        donor = builder.DEFAULT_DONOR.read_bytes()
        source = builder.DEFAULT_SOURCE.read_bytes()
        materializer_bytes = builder.MATERIALIZER_PATH.read_bytes()
        helper_bytes = builder.COMMON_ORIGIN_HELPERS_PATH.read_bytes()
        kwargs = {
            "donor_bytes": donor,
            "source_bytes": source,
            "materializer_bytes": materializer_bytes,
            "helper_bytes": helper_bytes,
        }
        with tempfile.TemporaryDirectory(prefix="dxrp_m1911_bind_contract_") as raw:
            directory = Path(raw)
            path, manifest_sha, _ = self._write_manifest(directory)
            with self.assertRaisesRegex(builder.ContractError, "SHA-256 mismatch"):
                builder.load_compiled_bind_manifest(path, "0" * 64, **kwargs)

            accepted = builder.compiled_bind_manifest_template()
            matrices, manifest_pin, _ = builder.load_compiled_bind_manifest(
                path, manifest_sha, **kwargs
            )
            self.assertEqual(set(matrices), {"B_root", "B_slide", "B_mag"})
            self.assertEqual(manifest_pin["sha256"], manifest_sha)

            mutations = []
            wrong_materializer = copy.deepcopy(accepted)
            wrong_materializer["pins"]["materializer"]["sha256"] = "F" * 64
            mutations.append((wrong_materializer, "pins.materializer"))
            falsely_accepted = copy.deepcopy(accepted)
            falsely_accepted["acceptance"]["accepted"] = True
            mutations.append((falsely_accepted, "explicitly unaccepted"))
            wrong_transform = copy.deepcopy(accepted)
            wrong_transform["transforms"]["B_slide"]["matrix"][0][3] = 99
            mutations.append((wrong_transform, "transforms changed"))
            wrong_parser = copy.deepcopy(accepted)
            wrong_parser["evidence"]["parser"]["sha256"] = "0" * 64
            mutations.append((wrong_parser, "parser/model evidence"))
            for payload, message in mutations:
                with self.subTest(message=message):
                    changed_path, changed_sha, _ = self._write_manifest(directory, payload)
                    with self.assertRaisesRegex(builder.ContractError, message):
                        builder.load_compiled_bind_manifest(
                            changed_path, changed_sha, **kwargs
                        )

    def test_exact_part_and_root_contract_drift_is_rejected(self) -> None:
        materialized, _ = builder.materializer.materialize(
            builder.DEFAULT_DONOR, builder.DEFAULT_SOURCE
        )
        index = builder.index_prefab(materialized)

        wrong_model = copy.deepcopy(materialized)
        wrong_model_index = builder.index_prefab(wrong_model)
        wrong_model_index.nodes_by_guid[
            builder.PART_SPECS[0].object_guid
        ]["Components"][0]["Model"] = "wrong.vmdl"
        with self.assertRaisesRegex(builder.ContractError, "model changed"):
            builder.validate_materialized_contract(
                wrong_model, require_authored_seed=True
            )

        wrong_guid = copy.deepcopy(materialized)
        wrong_guid_index = builder.index_prefab(wrong_guid)
        wrong_guid_index.nodes_by_guid[
            builder.PART_SPECS[1].object_guid
        ]["__guid"] = "00000000-0000-0000-0000-000000000001"
        with self.assertRaisesRegex(builder.ContractError, "missing exact object GUID"):
            builder.validate_materialized_contract(
                wrong_guid, require_authored_seed=True
            )

        no_view_model = copy.deepcopy(materialized)
        no_view_model["RootObject"]["Components"] = [
            component
            for component in no_view_model["RootObject"]["Components"]
            if component.get("__type") != "Dxura.RP.Game.ViewModel"
        ]
        with self.assertRaisesRegex(builder.ContractError, "single ViewModel"):
            builder.validate_materialized_contract(
                no_view_model, require_authored_seed=True
            )

        self.assertIn(builder.PART_SPECS[2].object_guid, index.nodes_by_guid)

    def test_copied_locals_across_distinct_parents_are_rejected(self) -> None:
        binds = {
            key: builder.common_origin._parse_manifest_transform(value, key)
            for key, value in builder.common_origin.COMPILED_BIND_TRANSFORMS.items()
        }
        copied = {key: builder._body_seed_transform() for key in binds}
        with self.assertRaisesRegex(builder.ContractError, "copied local transform rejected"):
            builder._assert_distinct_moving_parent_locals(binds, copied)

    def test_output_guards_and_cli_expose_no_write_bypass(self) -> None:
        forbidden_directory = (
            builder.WORKBENCH_ROOT / "tools" / "weapon_pipeline" / "_never_m1911"
        )
        with self.assertRaisesRegex(builder.ContractError, "repository output"):
            builder.assert_temp_output_directory(forbidden_directory)

        outside_temp = Path("C:/dxrp-m1911-common-origin-outside-temp")
        if builder._is_within(outside_temp.resolve(strict=False), builder.SYSTEM_TEMP_ROOT):
            self.skipTest("outside-temp probe unexpectedly resolves beneath system TEMP")
        with self.assertRaisesRegex(builder.ContractError, "system TEMP"):
            builder.assert_temp_output_directory(outside_temp)

        with tempfile.TemporaryDirectory(prefix="dxrp_m1911_nonempty_") as raw:
            directory = Path(raw)
            (directory / "sentinel.txt").write_text("keep", encoding="utf-8")
            with self.assertRaisesRegex(builder.ContractError, "must be empty"):
                builder.assert_temp_output_directory(directory)

        help_text = builder._parser().format_help()
        self.assertNotIn("--output", help_text)
        self.assertNotIn("allow-game-write", help_text)
        self.assertNotIn("accept-visual", help_text)
        self.assertIn("--print-compiled-bind-manifest", help_text)

    def test_direct_writer_rejects_repository_before_side_effects(self) -> None:
        forbidden = (
            builder.WORKBENCH_ROOT
            / "tools"
            / "weapon_pipeline"
            / "_never_written_m1911_common_origin.prefab"
        )
        with (
            mock.patch.object(
                Path, "mkdir", side_effect=AssertionError("mkdir must not run")
            ) as mkdir,
            mock.patch.object(
                builder.pipeline_io,
                "staging_path",
                side_effect=AssertionError("shared staging must not run"),
            ) as shared_staging,
        ):
            with self.assertRaisesRegex(RuntimeError, "repository destination"):
                builder.write_candidate(forbidden, b"blocked")
        mkdir.assert_not_called()
        shared_staging.assert_not_called()
        self.assertFalse(forbidden.exists())

    def test_direct_writer_preserves_destination_that_appears_during_staging(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory(prefix="dxrp_m1911_writer_race_") as raw:
            directory = Path(raw) / "output"
            directory.mkdir()
            destination = directory / builder.OUTPUT_NAME
            external_bytes = b"external-owner"
            original_staging_path = builder.pipeline_io.staging_path

            def stage_then_create(final_path: Path) -> Path:
                staged = original_staging_path(final_path)
                destination.write_bytes(external_bytes)
                return staged

            with mock.patch.object(
                builder.pipeline_io,
                "staging_path",
                side_effect=stage_then_create,
            ):
                with self.assertRaisesRegex(
                    builder.ContractError, "appeared during create-only staging"
                ):
                    builder.write_candidate(destination, b"candidate")

            self.assertEqual(destination.read_bytes(), external_bytes)
            self.assertEqual(list(directory.iterdir()), [destination])

    def test_cli_prints_manifest_without_exposing_an_output_path(self) -> None:
        capture = StringIO()
        with redirect_stdout(capture):
            result = builder.main(["--print-compiled-bind-manifest"])
        self.assertEqual(result, 0)
        self.assertEqual(
            json.loads(capture.getvalue()), builder.compiled_bind_manifest_template()
        )

    def test_cli_build_uses_an_internal_temp_output_only(self) -> None:
        with tempfile.TemporaryDirectory(prefix="dxrp_m1911_cli_test_") as raw:
            directory = Path(raw)
            manifest, manifest_sha, _ = self._write_manifest(directory)
            output_directory = directory / "cli-output"
            output_directory.mkdir()
            capture = StringIO()
            with (
                mock.patch.object(
                    builder.tempfile, "mkdtemp", return_value=str(output_directory)
                ),
                redirect_stdout(capture),
            ):
                result = builder.main(
                    [
                        "--compiled-bind-json",
                        str(manifest),
                        "--compiled-bind-sha256",
                        manifest_sha,
                    ]
                )
            self.assertEqual(result, 0)
            prefix = "DXRP_M1911_COMMON_ORIGIN="
            self.assertTrue(capture.getvalue().startswith(prefix))
            report = json.loads(capture.getvalue()[len(prefix) :])
            self.assertEqual(report["result"], "PASS")
            self.assertEqual(report["output"]["sha256"], builder.EXPECTED_CANDIDATE_SHA256)
            self.assertEqual(Path(report["output"]["path"]).parent, output_directory)


if __name__ == "__main__":
    unittest.main()
