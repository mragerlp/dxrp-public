"""Safety tests for the TEMP-only CS:GO audio staging helper."""

from __future__ import annotations

import hashlib
import io
import json
import struct
import tempfile
import unittest
from contextlib import redirect_stdout
from pathlib import Path
from unittest import mock

import stage_csgo_weapon_audio as staging


class StageCsgoWeaponAudioTests(unittest.TestCase):
    def test_extracted_source_root_verifies_pins_without_staging(self) -> None:
        self.assertTrue(
            hasattr(staging, "validate_source_root"),
            "The organized extraction needs a dedicated read-only verifier",
        )

        pcm = struct.pack(
            "<4sI4s4sIHHIIHH4sIh",
            b"RIFF",
            38,
            b"WAVE",
            b"fmt ",
            16,
            1,
            1,
            8_000,
            16_000,
            2,
            16,
            b"data",
            2,
            0,
        )
        fixture = staging.WaveSpec(
            weapon="glock",
            role="fixture",
            source_entry="bundle/sound.wav",
            output_path="never-written.wav",
            expected_bytes=len(pcm),
            expected_sha256=hashlib.sha256(pcm).hexdigest().upper(),
        )

        with tempfile.TemporaryDirectory(prefix="dxrp_csgo_extracted_verify_") as directory:
            root = Path(directory)
            source = root / "bundle" / "sound.wav"
            source.parent.mkdir(parents=True)
            source.write_bytes(pcm)
            with mock.patch.object(staging, "WAVES", (fixture,)):
                contents, metadata, record = staging.validate_source_root(root)
                output = io.StringIO()
                with (
                    mock.patch.object(
                        staging,
                        "write_exact_temp_files",
                        side_effect=AssertionError("source-root verification must not stage"),
                    ) as writer,
                    redirect_stdout(output),
                ):
                    result = staging.run(
                        ["--source-root", str(root), "--verify-only"]
                    )
                writer.assert_not_called()
                with self.assertRaisesRegex(RuntimeError, "verification only"):
                    staging.run(["--source-root", str(root)])

        self.assertEqual(contents[fixture], pcm)
        self.assertEqual(metadata[fixture].sample_rate, 8_000)
        self.assertEqual(record["kind"], "extracted_source_root")
        self.assertTrue(record["preserved"])
        self.assertEqual(result, 0)
        summary = json.loads(output.getvalue())
        self.assertEqual(summary["mode"], "verify_only_extracted_source_root")
        self.assertEqual(summary["writes"], 0)

    def test_glock_owns_normal_and_suppressed_source_mapping(self) -> None:
        expected_waves = {
            "csgo-master/sound/weapons/glock18/glock_01.wav": (
                "glock_normal_01",
                "game/Assets/addons/lifepunch/lpweapons/glock/sounds/glock_shot_01.wav",
            ),
            "csgo-master/sound/weapons/glock18/glock_02.wav": (
                "glock_normal_02",
                "game/Assets/addons/lifepunch/lpweapons/glock/sounds/glock_shot_02.wav",
            ),
            "csgo-master/sound/weapons/usp/usp_01.wav": (
                "usp_suppressed_01",
                "game/Assets/addons/lifepunch/lpweapons/glock/sounds/glock_suppressed_shot_01.wav",
            ),
            "csgo-master/sound/weapons/usp/usp_02.wav": (
                "usp_suppressed_02",
                "game/Assets/addons/lifepunch/lpweapons/glock/sounds/glock_suppressed_shot_02.wav",
            ),
            "csgo-master/sound/weapons/usp/usp_03.wav": (
                "usp_suppressed_03",
                "game/Assets/addons/lifepunch/lpweapons/glock/sounds/glock_suppressed_shot_03.wav",
            ),
        }
        glock_waves = {
            wave.source_entry: (wave.role, wave.output_path)
            for wave in staging.WAVES
            if wave.weapon == "glock"
        }
        self.assertEqual(glock_waves, expected_waves)
        self.assertFalse(
            any(
                wave.weapon == "sr25"
                and wave.source_entry.startswith("csgo-master/sound/weapons/usp/")
                for wave in staging.WAVES
            )
        )

        glock_events = {
            event.role: (event.output_path, event.sounds)
            for event in staging.EVENTS
            if event.weapon == "glock"
        }
        self.assertEqual(
            glock_events,
            {
                "glock_normal_random_01_02": (
                    "game/Assets/addons/lifepunch/lpweapons/glock/sounds/glock_shot.sound",
                    (
                        "addons/lifepunch/lpweapons/glock/sounds/glock_shot_01.vsnd",
                        "addons/lifepunch/lpweapons/glock/sounds/glock_shot_02.vsnd",
                    ),
                ),
                "usp_suppressed_random_01_02_03": (
                    "game/Assets/addons/lifepunch/lpweapons/glock/sounds/glock_suppressed_shot.sound",
                    (
                        "addons/lifepunch/lpweapons/glock/sounds/glock_suppressed_shot_01.vsnd",
                        "addons/lifepunch/lpweapons/glock/sounds/glock_suppressed_shot_02.vsnd",
                        "addons/lifepunch/lpweapons/glock/sounds/glock_suppressed_shot_03.vsnd",
                    ),
                ),
            },
        )

    def test_cli_exposes_no_product_write_switch(self) -> None:
        parser_args = staging.parse_args(["--verify-only"])
        self.assertTrue(parser_args.verify_only)
        with self.assertRaises(SystemExit):
            staging.parse_args(["--allow-game-write"])
        with self.assertRaises(SystemExit):
            staging.parse_args(["--game-root", str(staging.REPO_ROOT / "game")])

    def test_direct_writer_rejects_repository_output(self) -> None:
        forbidden = staging.REPO_ROOT / "game" / "Assets" / "never-written"
        with self.assertRaisesRegex(RuntimeError, "system TEMP"):
            staging.write_exact_temp_files(forbidden, {"probe.bin": b"probe"})
        self.assertFalse(forbidden.exists())

    def test_direct_writer_rejects_temp_root_itself(self) -> None:
        with self.assertRaisesRegex(RuntimeError, "strict child"):
            staging.write_exact_temp_files(
                Path(tempfile.gettempdir()), {"probe.bin": b"probe"}
            )

    def test_temp_writer_is_deterministic(self) -> None:
        with tempfile.TemporaryDirectory(prefix="dxrp_csgo_audio_unit_") as directory:
            root = Path(directory) / "candidate"
            files = {"nested/a.bin": b"alpha", "b.bin": b"beta"}
            first = staging.write_exact_temp_files(root, files)
            second = staging.write_exact_temp_files(root, files)
            self.assertEqual(first, (2, 0))
            self.assertEqual(second, (0, 2))
            self.assertEqual((root / "nested" / "a.bin").read_bytes(), b"alpha")
            self.assertEqual((root / "b.bin").read_bytes(), b"beta")


if __name__ == "__main__":
    unittest.main()
