from __future__ import annotations

import copy
import json
from pathlib import Path
import tempfile
import unittest
from unittest import mock

import build_weapon_audio_wiring_candidates as builder


class WeaponAudioWiringCandidateTests(unittest.TestCase):
    def test_glock_candidate_owns_normal_and_suppressed_events(self) -> None:
        self.assertIn("glock", builder.SPECS)
        self.assertNotIn("sr25", builder.SPECS)

        spec = builder.SPECS["glock"]
        self.assertEqual(
            spec.event,
            "addons/lifepunch/lpweapons/glock/sounds/glock_shot.sound",
        )
        self.assertEqual(
            spec.suppressed_event,
            "addons/lifepunch/lpweapons/glock/sounds/glock_suppressed_shot.sound",
        )

        source = (builder.REPO_ROOT / spec.source).read_bytes()
        candidate, old_value = builder.build_candidate_bytes(
            source,
            spec.event,
            spec.suppressed_event,
        )
        self.assertEqual(
            old_value,
            "gameplay/equipment/weapons/usp/sounds/usp_shoot.sound",
        )
        document = json.loads(candidate.decode("utf-8-sig"))
        components = [
            node
            for node in builder._walk(document)
            if node.get("__type") == builder.SHOOT_COMPONENT
        ]
        self.assertEqual(len(components), 1)
        self.assertEqual(components[0].get("ShootSound"), spec.event)
        self.assertEqual(
            components[0].get("SuppressedShootSound"),
            spec.suppressed_event,
        )

        with tempfile.TemporaryDirectory() as output_dir:
            record = builder.build_one(spec, Path(output_dir))
            self.assertEqual(record["requested_shoot_sound"], spec.event)
            self.assertEqual(
                record["requested_suppressed_shoot_sound"],
                spec.suppressed_event,
            )
            self.assertFalse(record["accepted"])

    def test_pinned_sources_build_exactly_one_change(self) -> None:
        for spec in builder.SPECS.values():
            source = (builder.REPO_ROOT / spec.source).read_bytes()
            self.assertEqual(builder.sha256(source), spec.source_sha256)
            candidate, old_value = builder.build_candidate_bytes(
                source,
                spec.event,
                spec.suppressed_event,
            )
            self.assertNotEqual(candidate, source)
            self.assertNotEqual(old_value, spec.event)
            self.assertEqual(
                candidate.count(spec.event.encode("utf-8")),
                1,
            )
            match = builder.SHOOT_SOUND_PATTERN.search(source)
            self.assertIsNotNone(match)
            assert match is not None
            if spec.suppressed_event is None:
                self.assertEqual(
                    candidate,
                    source[: match.start(2)]
                    + json.dumps(spec.event).encode("utf-8")
                    + source[match.end(2) :],
                )
            else:
                self.assertEqual(
                    candidate.count(spec.suppressed_event.encode("utf-8")),
                    1,
                )

    def test_output_is_deterministic_and_temp_only(self) -> None:
        with tempfile.TemporaryDirectory() as first_dir, tempfile.TemporaryDirectory() as second_dir:
            first_root = builder.require_temp_output(Path(first_dir))
            second_root = builder.require_temp_output(Path(second_dir))
            for spec in builder.SPECS.values():
                first = builder.build_one(spec, first_root)
                second = builder.build_one(spec, second_root)
                self.assertEqual(
                    first["candidate_sha256"], second["candidate_sha256"]
                )
                self.assertFalse(first["accepted"])

    def test_game_tree_output_is_rejected(self) -> None:
        with self.assertRaisesRegex(ValueError, "outside the repository"):
            builder.require_temp_output(builder.GAME_ROOT / "Assets" / "candidate")

    def test_direct_build_one_cannot_bypass_output_guard(self) -> None:
        forbidden = builder.GAME_ROOT / "Assets" / "never-written"
        with self.assertRaisesRegex(ValueError, "outside the repository"):
            builder.build_one(builder.SPECS["m1911"], forbidden)
        self.assertFalse(forbidden.exists())

    def test_direct_atomic_writer_rejects_game_assets_before_side_effects(self) -> None:
        forbidden = (
            builder.GAME_ROOT
            / "Assets"
            / "_never_written_weapon_audio_atomic.prefab"
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
                builder.write_atomic(forbidden, b"blocked")
        mkdir.assert_not_called()
        shared_writer.assert_not_called()
        self.assertFalse(forbidden.exists())

    def test_non_temp_output_is_rejected(self) -> None:
        forbidden = Path("C:/dxrp-audio-outside-temp")
        if builder._is_relative_to(forbidden.resolve(strict=False), builder.SYSTEM_TEMP_ROOT):
            self.skipTest("probe unexpectedly resolves beneath system temp")
        with self.assertRaisesRegex(ValueError, "system temp"):
            builder.require_temp_output(forbidden)

    def test_cli_exposes_no_product_write_switch(self) -> None:
        options = {
            option
            for action in builder._parser()._actions
            for option in action.option_strings
        }
        self.assertNotIn("--allow-game-write", options)
        self.assertNotIn("--game-root", options)

    def test_missing_or_duplicate_shoot_component_rejects(self) -> None:
        no_component = b'{"RootObject":{"Components":[]}}\n'
        with self.assertRaisesRegex(ValueError, "found 0"):
            builder.build_candidate_bytes(no_component, "x.sound")

        component = (
            b'{"__type":"Dxura.RP.Game.ShootWeaponComponent",'
            b'"ShootSound":null}'
        )
        duplicate = b'{"RootObject":{"Children":[' + component + b',' + component + b']}}'
        with self.assertRaisesRegex(ValueError, "found 2"):
            builder.build_candidate_bytes(duplicate, "x.sound")

    def test_sha_pin_mismatch_rejects_before_output(self) -> None:
        original = builder.SPECS["m1911"]
        wrong = copy.copy(original)
        object.__setattr__(wrong, "source_sha256", "0" * 64)
        with tempfile.TemporaryDirectory() as output_dir:
            with self.assertRaisesRegex(ValueError, "source SHA mismatch"):
                builder.build_one(wrong, Path(output_dir))
            self.assertEqual(list(Path(output_dir).iterdir()), [])

    def test_m870_manifest_discloses_unwired_pump(self) -> None:
        with tempfile.TemporaryDirectory() as output_dir:
            record = builder.build_one(builder.SPECS["m870"], Path(output_dir))
            self.assertEqual(
                record["pump_sound"],
                "addons/lifepunch/lpweapons/m870/sounds/m870_pump.sound",
            )
            self.assertEqual(
                record["pump_wiring"],
                "staged_unwired_no_verified_pump_driver",
            )


if __name__ == "__main__":
    unittest.main()
