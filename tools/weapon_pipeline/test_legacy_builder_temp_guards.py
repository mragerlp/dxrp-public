"""Static regressions for TEMP validation order in Blender-only builders."""

from __future__ import annotations

import ast
from pathlib import Path
import unittest


PIPELINE_ROOT = Path(__file__).resolve().parent
EXPECTED_GUARDS = {
    "build_ar15_native.py": {"output_path"},
    "build_sr25_native.py": {"output_path", "texture_output_path"},
    "build_m1911_graybox.py": {"output_dir"},
    "build_deserteagle_parts.py": {"output_directory"},
}


def _function(tree: ast.Module, name: str) -> ast.FunctionDef:
    for node in tree.body:
        if isinstance(node, ast.FunctionDef) and node.name == name:
            return node
    raise AssertionError(f"Missing function: {name}")


def _attribute_calls(function: ast.FunctionDef, attribute: str) -> list[ast.Call]:
    return [
        node
        for node in ast.walk(function)
        if isinstance(node, ast.Call)
        and isinstance(node.func, ast.Attribute)
        and node.func.attr == attribute
    ]


class LegacyBuilderTempGuardTests(unittest.TestCase):
    def test_temp_guards_precede_every_directory_creation(self) -> None:
        for filename, expected_names in EXPECTED_GUARDS.items():
            with self.subTest(builder=filename):
                source = (PIPELINE_ROOT / filename).read_text(encoding="utf-8")
                tree = ast.parse(source, filename=filename)
                parse_paths = _function(tree, "parse_paths")
                main = _function(tree, "main")

                self.assertEqual(_attribute_calls(parse_paths, "mkdir"), [])
                mkdir_calls = _attribute_calls(main, "mkdir")
                self.assertTrue(mkdir_calls, "main must own intentional TEMP mkdir")

                guard_calls = _attribute_calls(main, "assert_temp_destination")
                guarded_names = {
                    call.args[0].id
                    for call in guard_calls
                    if call.args and isinstance(call.args[0], ast.Name)
                }
                self.assertEqual(guarded_names, expected_names)
                self.assertLess(
                    max(call.lineno for call in guard_calls),
                    min(call.lineno for call in mkdir_calls),
                )
                self.assertNotIn("governed_output", source)
                self.assertNotIn("game/Assets", source)


if __name__ == "__main__":
    unittest.main()
