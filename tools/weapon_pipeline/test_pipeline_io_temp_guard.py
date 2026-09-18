"""TEMP-only destination tests for pipeline_io.py."""

from __future__ import annotations

import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest import mock

import pipeline_io


class PipelineIoTempGuardTests(unittest.TestCase):
    def test_repository_file_destination_is_rejected_before_staging(self) -> None:
        forbidden = pipeline_io.WORKSPACE_ROOT / "game" / "never-written.bin"
        with self.assertRaisesRegex(RuntimeError, "repository destination"):
            pipeline_io.staging_path(forbidden)

    def test_repository_directory_destination_is_rejected_before_staging(self) -> None:
        forbidden = pipeline_io.WORKSPACE_ROOT / "tools" / "weapon_pipeline"
        before = set(forbidden.iterdir())
        with self.assertRaisesRegex(RuntimeError, "repository destination"):
            pipeline_io.staging_directory(forbidden)
        self.assertEqual(set(forbidden.iterdir()), before)

    def test_non_temp_destination_is_rejected(self) -> None:
        forbidden = Path("C:/dxrp-pipeline-outside-temp.bin")
        if pipeline_io._is_within(
            forbidden.resolve(strict=False), pipeline_io.SYSTEM_TEMP_ROOT
        ):
            self.skipTest("probe unexpectedly resolves beneath system temp")
        with self.assertRaisesRegex(RuntimeError, "system TEMP"):
            pipeline_io._assert_temp_destination(forbidden)

    def test_public_guard_rejects_reparse_component(self) -> None:
        forbidden = pipeline_io.WORKSPACE_ROOT / "game" / "never-reparse-written.bin"
        with tempfile.TemporaryDirectory(prefix="dxrp_pipeline_reparse_") as directory:
            link = Path(directory) / "game-link"
            try:
                link.symlink_to(
                    pipeline_io.WORKSPACE_ROOT / "game", target_is_directory=True
                )
            except OSError as exc:
                self.skipTest(f"directory symlink unavailable: {exc}")
            with self.assertRaisesRegex(RuntimeError, "Reparse-point"):
                pipeline_io.assert_temp_destination(link / forbidden.name)
        self.assertFalse(forbidden.exists())

    def test_atomic_writer_preserves_preexisting_unique_stage_collision(self) -> None:
        with tempfile.TemporaryDirectory(prefix="dxrp_pipeline_collision_") as directory:
            root = Path(directory)
            final = root / "candidate.bin"
            token = "0" * 32
            staged = root / f".candidate.{token}.staging.bin"
            staged.write_bytes(b"sentinel")
            with mock.patch.object(
                pipeline_io.uuid,
                "uuid4",
                return_value=SimpleNamespace(hex=token),
            ):
                with self.assertRaisesRegex(RuntimeError, "unexpectedly exists"):
                    pipeline_io.write_bytes_atomic(final, b"candidate")
            self.assertEqual(staged.read_bytes(), b"sentinel")
            self.assertFalse(final.exists())

    def test_atomic_writer_rechecks_parent_identity_before_promotion(self) -> None:
        with tempfile.TemporaryDirectory(prefix="dxrp_pipeline_identity_") as directory:
            root = Path(directory)
            final = root / "candidate.bin"
            real_directory_identity = pipeline_io._directory_identity
            calls = 0

            def changing_identity(path: Path) -> tuple[int, int]:
                nonlocal calls
                calls += 1
                identity = real_directory_identity(path)
                if calls > 1:
                    return identity[0], identity[1] + 1
                return identity

            with mock.patch.object(
                pipeline_io, "_directory_identity", side_effect=changing_identity
            ):
                with self.assertRaisesRegex(RuntimeError, "identity changed"):
                    pipeline_io.write_bytes_atomic(final, b"candidate")
            self.assertFalse(final.exists())
            self.assertEqual(list(root.iterdir()), [])

    def test_temp_staging_and_promotion_succeeds(self) -> None:
        with tempfile.TemporaryDirectory(prefix="dxrp_pipeline_io_unit_") as directory:
            root = Path(directory)
            final = root / "candidate.bin"
            staged = pipeline_io.staging_path(final)
            staged.write_bytes(b"candidate")
            pipeline_io.promote_files([(staged, final)])
            self.assertEqual(final.read_bytes(), b"candidate")
            self.assertFalse(staged.exists())


if __name__ == "__main__":
    unittest.main()
