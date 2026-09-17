from __future__ import annotations

import importlib.util
import io
import json
import math
import struct
import sys
import tempfile
import unittest
from contextlib import redirect_stdout
from pathlib import Path
from unittest import mock


MODULE_PATH = Path(__file__).with_name("audit_weapon_donor_animation_channels.py")
SPEC = importlib.util.spec_from_file_location("donor_animation_audit", MODULE_PATH)
assert SPEC is not None and SPEC.loader is not None
audit = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = audit
SPEC.loader.exec_module(audit)


class SyntheticGltf:
    def __init__(self, root: Path) -> None:
        self.root = root
        self.gltf = root / "fixture.gltf"
        self.binary = root / "fixture.bin"
        self.payload = bytearray()
        self.document = {
            "asset": {"version": "2.0"},
            "buffers": [{"uri": "fixture.bin", "byteLength": 0}],
            "bufferViews": [],
            "accessors": [],
            "nodes": [],
            "animations": [],
        }

    def accessor(self, rows: list[tuple[float, ...]], kind: str) -> int:
        offset = len(self.payload)
        for row in rows:
            self.payload.extend(struct.pack(f"<{len(row)}f", *row))
        byte_length = len(self.payload) - offset
        view_index = len(self.document["bufferViews"])
        self.document["bufferViews"].append(
            {"buffer": 0, "byteOffset": offset, "byteLength": byte_length}
        )
        accessor_index = len(self.document["accessors"])
        self.document["accessors"].append(
            {
                "bufferView": view_index,
                "componentType": 5126,
                "count": len(rows),
                "type": kind,
            }
        )
        return accessor_index

    def add_channel(
        self,
        animation_name: str,
        node_name: str,
        path: str,
        values: list[tuple[float, ...]],
    ) -> None:
        node_index = len(self.document["nodes"])
        self.document["nodes"].append({"name": node_name})
        times = [(float(index),) for index in range(len(values))]
        input_accessor = self.accessor(times, "SCALAR")
        output_accessor = self.accessor(values, "VEC4" if path == "rotation" else "VEC3")
        self.document["animations"].append(
            {
                "name": animation_name,
                "channels": [
                    {"sampler": 0, "target": {"node": node_index, "path": path}}
                ],
                "samplers": [
                    {
                        "input": input_accessor,
                        "output": output_accessor,
                        "interpolation": "LINEAR",
                    }
                ],
            }
        )

    def write(self) -> dict:
        self.document["buffers"][0]["byteLength"] = len(self.payload)
        self.binary.write_bytes(self.payload)
        self.gltf.write_text(json.dumps(self.document), encoding="utf-8")
        return self.document


class DonorAnimationAuditTests(unittest.TestCase):
    def test_translation_and_rotation_proofs_parse_binary_accessors(self) -> None:
        with tempfile.TemporaryDirectory(dir=audit.SYSTEM_TEMP_ROOT) as raw:
            root = Path(raw)
            fixture = SyntheticGltf(root)
            fixture.add_channel(
                "Reload", "magazine", "translation", [(0.0, 0.0, 0.0), (2.0, 0.0, 0.0)]
            )
            fixture.add_channel(
                "Reload_Shell",
                "carrier",
                "rotation",
                [(0.0, 0.0, 0.0, 1.0), (0.0, 0.0, math.sin(0.25), math.cos(0.25))],
            )
            document = fixture.write()
            translation = audit._prove_requirement(
                document,
                fixture.gltf,
                root,
                audit.ChannelRequirement("Reload", "magazine", "translation", "translation"),
            )
            rotation = audit._prove_requirement(
                document,
                fixture.gltf,
                root,
                audit.ChannelRequirement("Reload_Shell", "carrier", "rotation", "rotation"),
            )
            self.assertEqual(translation["observed"], 2.0)
            self.assertGreater(rotation["observed"], 28.0)

    def test_constant_target_channel_fails_closed(self) -> None:
        with tempfile.TemporaryDirectory(dir=audit.SYSTEM_TEMP_ROOT) as raw:
            root = Path(raw)
            fixture = SyntheticGltf(root)
            fixture.add_channel(
                "Reload", "magazine", "translation", [(1.0, 2.0, 3.0), (1.0, 2.0, 3.0)]
            )
            document = fixture.write()
            with self.assertRaisesRegex(audit.ContractError, "translation is constant"):
                audit._prove_requirement(
                    document,
                    fixture.gltf,
                    root,
                    audit.ChannelRequirement(
                        "Reload", "magazine", "translation", "translation"
                    ),
                )

    def test_inventory_observation_records_constant_without_claiming_motion(self) -> None:
        with tempfile.TemporaryDirectory(dir=audit.SYSTEM_TEMP_ROOT) as raw:
            root = Path(raw)
            fixture = SyntheticGltf(root)
            fixture.add_channel(
                "Fire_Hold_delta",
                "charging_handle",
                "translation",
                [(0.0, 0.0, 0.0)],
            )
            document = fixture.write()
            observed = audit._observe_requirement(
                document,
                fixture.gltf,
                root,
                audit.ChannelRequirement(
                    "Fire_Hold_delta",
                    "charging_handle",
                    "translation",
                    "translation",
                ),
            )
            self.assertFalse(observed["nonconstant"])
            self.assertEqual(observed["observed"], 0.0)

    def test_external_buffer_escape_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory(dir=audit.SYSTEM_TEMP_ROOT) as raw:
            root = Path(raw)
            fixture = SyntheticGltf(root)
            fixture.add_channel(
                "Reload", "magazine", "translation", [(0.0, 0.0, 0.0), (1.0, 0.0, 0.0)]
            )
            document = fixture.write()
            document["buffers"][0]["uri"] = "../outside.bin"
            with self.assertRaisesRegex(audit.ContractError, "escapes its glTF directory"):
                audit._read_accessor(document, 0, fixture.gltf, root)

    def test_spaghelli_action_node_absence_is_fail_closed(self) -> None:
        clean = {"nodes": [{"name": "bolt"}, {"name": "carrier"}, {"name": "shell01"}]}
        result = audit._prove_no_spaghelli_action_node(clean)
        self.assertFalse(result["dedicated_pump_foreend_action_node"])
        with self.assertRaisesRegex(audit.ContractError, "dedicated action node"):
            audit._prove_no_spaghelli_action_node(
                {"nodes": [{"name": "shotgun_fore-end"}]}
            )

    def test_pin_mismatch_rejected(self) -> None:
        with tempfile.TemporaryDirectory(dir=audit.SYSTEM_TEMP_ROOT) as raw:
            path = Path(raw) / "pin.bin"
            path.write_bytes(b"abc")
            pin = audit.FilePin(path, 3, "0" * 64)
            with self.assertRaisesRegex(audit.ContractError, "SHA-256 drift"):
                audit._verify_pin(pin, "fixture")

    def test_non_temp_and_root_output_are_rejected(self) -> None:
        with self.assertRaisesRegex(audit.ContractError, "strict system TEMP child"):
            audit.audit_to_temp(audit.SYSTEM_TEMP_ROOT)
        with self.assertRaisesRegex(audit.ContractError, "strict system TEMP child"):
            audit.audit_to_temp(Path(__file__).resolve().parent)

    def test_strict_child_rejects_existing_temp_sibling(self) -> None:
        with tempfile.TemporaryDirectory(dir=audit.SYSTEM_TEMP_ROOT) as raw_root:
            with tempfile.TemporaryDirectory(dir=audit.SYSTEM_TEMP_ROOT) as raw_sibling:
                root = Path(raw_root)
                sibling_file = Path(raw_sibling) / "outside.bin"
                sibling_file.write_bytes(b"outside")
                with self.assertRaisesRegex(audit.ContractError, "not a strict TEMP child"):
                    audit._strict_child(
                        root, sibling_file, "sibling fixture", must_exist=True
                    )

    def test_unapproved_export_exception_is_rejected(self) -> None:
        spec = audit.DONORS[0]
        result = mock.Mock(returncode=0, stdout="System.Exception: surprise", stderr="")
        with self.assertRaisesRegex(audit.ContractError, "unapproved exception"):
            audit._validate_export_diagnostics(spec, result)

    def test_exact_known_usp_physics_sidecar_exception_is_recorded(self) -> None:
        spec = next(item for item in audit.DONORS if item.key == "usp")
        result = mock.Mock(
            returncode=0,
            stdout=(
                "System.NullReferenceException: Object reference not set to an instance of an object.\n"
                "at ValveResourceFormat.IO.GltfModelExporter.LoadPhysicsMeshes\n"
                "at ValveResourceFormat.IO.GltfModelExporter.ExportToFile"
            ),
            stderr="",
        )
        self.assertTrue(audit._validate_export_diagnostics(spec, result))

    def test_cli_exposes_no_output_or_promotion_argument(self) -> None:
        with self.assertRaisesRegex(audit.ContractError, "accepts no output or promotion"):
            audit.main(["--output", "game"])

    def test_main_removes_temp_before_printing_report(self) -> None:
        captured_path: Path | None = None

        def fake_audit(output_root: Path) -> dict:
            nonlocal captured_path
            captured_path = output_root
            self.assertTrue(output_root.exists())
            return {"status": "PASS"}

        output = io.StringIO()
        with mock.patch.object(audit, "audit_to_temp", side_effect=fake_audit):
            with redirect_stdout(output):
                self.assertEqual(audit.main([]), 0)
        self.assertIsNotNone(captured_path)
        assert captured_path is not None
        self.assertFalse(captured_path.exists())
        report = json.loads(output.getvalue())
        self.assertTrue(report["temporary_output"]["removed_before_report"])


if __name__ == "__main__":
    unittest.main()
