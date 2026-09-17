"""Focused TEMP-only tests for build_m1911_view_prefab.py."""

from __future__ import annotations

import json
import tempfile
import unittest
from contextlib import redirect_stdout
from io import StringIO
from pathlib import Path
from unittest import mock

import build_m1911_view_prefab as builder


class M1911ViewPrefabTests(unittest.TestCase):
    def test_current_inputs_materialize_to_the_pinned_candidate(self) -> None:
        donor_before = builder.DEFAULT_DONOR.read_bytes()
        patch_before = builder.DEFAULT_PATCH.read_bytes()
        document, summary = builder.build_document(
            builder.DEFAULT_DONOR, builder.DEFAULT_PATCH
        )
        payload = builder.serialize_document(document)
        self.assertEqual(
            builder.sha256_bytes(payload), builder.EXPECTED_MATERIALIZED_SHA256
        )
        self.assertEqual(summary["rootName"], "vm_m1911")
        self.assertEqual(summary["viewModelComponents"], 1)
        self.assertEqual(len(summary["parts"]), 3)
        self.assertTrue(summary["documentEnvelopePreserved"])
        self.assertEqual(list(document), builder.EXPECTED_DOCUMENT_KEYS)
        self.assertEqual(
            {
                key: document[key]
                for key in builder.EXPECTED_DOCUMENT_KEYS
                if key != "RootObject"
            },
            builder.EXPECTED_DOCUMENT_ENVELOPE,
        )
        self.assertEqual(
            summary["externalPackageReferences"],
            [
                "facepunch.v_first_person_arms_human#205798",
                "facepunch.v_usp#278998",
            ],
        )
        self.assertEqual(builder.DEFAULT_DONOR.read_bytes(), donor_before)
        self.assertEqual(builder.DEFAULT_PATCH.read_bytes(), patch_before)

    def test_cli_exposes_no_product_write_switch(self) -> None:
        options = {
            option
            for action in builder._parser()._actions
            for option in action.option_strings
        }
        self.assertNotIn("--allow-game-write", options)
        self.assertNotIn("--source-backup", options)

    def test_repository_output_is_rejected(self) -> None:
        with self.assertRaisesRegex(ValueError, "repository output"):
            builder.assert_temp_output_path(builder.DEFAULT_PATCH)

    def test_non_temp_output_is_rejected(self) -> None:
        candidate = Path("C:/dxrp-m1911-outside-temp.prefab")
        if builder.is_within(candidate, builder.SYSTEM_TEMP_ROOT):
            self.skipTest("probe unexpectedly resolves beneath system temp")
        with self.assertRaisesRegex(ValueError, "system temp"):
            builder.assert_temp_output_path(candidate)

    def test_direct_atomic_writer_rejects_game_assets_before_side_effects(self) -> None:
        forbidden = (
            builder.WORKBENCH_ROOT
            / "game"
            / "Assets"
            / "_never_written_m1911_atomic.prefab"
        )
        with (
            mock.patch.object(
                Path, "mkdir", side_effect=AssertionError("mkdir must not run")
            ) as mkdir,
            mock.patch.object(
                builder.pipeline_io,
                "write_bytes_atomic",
                side_effect=AssertionError("shared writer must not run"),
            ) as shared_writer,
        ):
            with self.assertRaisesRegex(RuntimeError, "repository destination"):
                builder.write_bytes_atomic(forbidden, b"blocked", forbidden.parent)
        mkdir.assert_not_called()
        shared_writer.assert_not_called()
        self.assertFalse(forbidden.exists())

    def test_temp_build_is_deterministic_and_nonmutating(self) -> None:
        donor_before = builder.DEFAULT_DONOR.read_bytes()
        patch_before = builder.DEFAULT_PATCH.read_bytes()
        with tempfile.TemporaryDirectory(prefix="dxrp_m1911_materialize_") as directory:
            output = Path(directory) / "vm_m1911.materialized_candidate.prefab"
            capture = StringIO()
            with redirect_stdout(capture):
                result = builder.main(["--output", str(output)])
            self.assertEqual(result, 0)
            report = json.loads(capture.getvalue())
            self.assertEqual(report["result"], "PASS")
            self.assertFalse(report["gameTreeWritten"])
            self.assertEqual(
                report["outputSha256"], builder.EXPECTED_MATERIALIZED_SHA256
            )
            self.assertEqual(
                builder.sha256_file(output), builder.EXPECTED_MATERIALIZED_SHA256
            )
            output_document = json.loads(output.read_text(encoding="utf-8"))
            self.assertEqual(list(output_document), builder.EXPECTED_DOCUMENT_KEYS)
            self.assertEqual(
                output_document["__references"],
                [
                    "facepunch.v_first_person_arms_human#205798",
                    "facepunch.v_usp#278998",
                ],
            )
        self.assertEqual(builder.DEFAULT_DONOR.read_bytes(), donor_before)
        self.assertEqual(builder.DEFAULT_PATCH.read_bytes(), patch_before)

    def test_nonempty_temp_output_directory_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory(prefix="dxrp_m1911_nonempty_") as directory:
            root = Path(directory)
            (root / "sentinel.txt").write_text("keep", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "must be empty"):
                builder.assert_temp_output_path(root / "candidate.prefab")


if __name__ == "__main__":
    unittest.main()
