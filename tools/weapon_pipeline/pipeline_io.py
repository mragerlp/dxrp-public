"""Fail-closed staging and promotion helpers for DXRP asset builders."""

from __future__ import annotations

import atexit
import os
import shutil
import stat
import sys
import tempfile
import uuid
from pathlib import Path
from typing import Iterable


_staged_files: set[Path] = set()
_staged_directories: set[Path] = set()
_file_destinations: dict[
    Path, tuple[Path, tuple[int, int], tuple[object, ...]]
] = {}
_directory_destinations: dict[
    Path, tuple[Path, tuple[int, int], dict[Path, tuple[object, ...]]]
] = {}
WORKSPACE_ROOT = Path(__file__).resolve().parents[2]
SYSTEM_TEMP_ROOT = Path(tempfile.gettempdir()).resolve()


def _absolute(path: Path) -> Path:
    """Return an absolute lexical path without following the leaf."""

    return Path(os.path.abspath(os.fspath(path)))


def _is_reparse_point(path: Path, result: os.stat_result) -> bool:
    attributes = getattr(result, "st_file_attributes", 0)
    reparse_flag = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0)
    return path.is_symlink() or bool(attributes & reparse_flag)


def _assert_no_reparse_components(path: Path) -> None:
    """Reject symlinks and Windows junctions in every existing component."""

    current = _absolute(path)
    while True:
        try:
            result = current.lstat()
        except FileNotFoundError:
            pass
        else:
            if _is_reparse_point(current, result):
                raise RuntimeError(f"Reparse-point output path is not allowed: {current}")
        parent = current.parent
        if parent == current:
            break
        current = parent


def _directory_identity(path: Path) -> tuple[int, int]:
    _assert_no_reparse_components(path)
    result = path.lstat()
    if not stat.S_ISDIR(result.st_mode):
        raise RuntimeError(f"Output parent is not a directory: {path}")
    return result.st_dev, result.st_ino


def _leaf_snapshot(path: Path) -> tuple[object, ...]:
    _assert_no_reparse_components(path)
    try:
        result = path.lstat()
    except FileNotFoundError:
        return ("missing",)
    if not stat.S_ISREG(result.st_mode):
        raise RuntimeError(f"Final output is not a regular file: {path}")
    return (
        "file",
        result.st_dev,
        result.st_ino,
        result.st_size,
        result.st_mtime_ns,
    )


def _canonical_leaf(path: Path) -> Path:
    lexical = _absolute(path)
    _assert_no_reparse_components(lexical)
    parent = lexical.parent.resolve(strict=True)
    _assert_no_reparse_components(parent)
    return parent / lexical.name


def _is_within(path: Path, parent: Path) -> bool:
    try:
        path.relative_to(parent)
        return True
    except ValueError:
        return False


def _assert_temp_destination(path: Path) -> None:
    resolved = path.resolve(strict=False)
    if _is_within(resolved, WORKSPACE_ROOT):
        raise RuntimeError(
            "TEMP-only pipeline refuses every repository destination: " + str(resolved)
        )
    if resolved == SYSTEM_TEMP_ROOT or not _is_within(resolved, SYSTEM_TEMP_ROOT):
        raise RuntimeError(
            "TEMP-only pipeline requires a strict child of system TEMP: "
            + str(resolved)
        )


def assert_temp_destination(path: Path) -> Path:
    """Validate and normalize a destination before any filesystem mutation."""

    absolute = _absolute(path)
    _assert_no_reparse_components(absolute)
    resolved = absolute.resolve(strict=False)
    _assert_temp_destination(resolved)
    return resolved


def write_bytes_atomic(final_path: Path, data: bytes) -> Path:
    """Write bytes through unique exclusive staging and guarded promotion."""

    final_path = assert_temp_destination(final_path)
    final_path.parent.mkdir(parents=True, exist_ok=True)
    staged = staging_path(final_path)
    stage_created = False
    try:
        with staged.open("xb") as stream:
            stage_created = True
            stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())
        if staged.read_bytes() != data:
            raise RuntimeError(f"Staged output bytes changed before promotion: {staged}")
        promote_files([(staged, final_path)])
    finally:
        cleanup_complete = not (staged.exists() or staged.is_symlink())
        if stage_created and (staged.exists() or staged.is_symlink()):
            try:
                staged.unlink()
            except OSError as exc:
                print(
                    f"DXRP_PIPELINE_IO_CLEANUP_WARNING: staged output remains at "
                    f"{staged}: {exc}",
                    file=sys.stderr,
                )
            else:
                cleanup_complete = True
        if cleanup_complete:
            _staged_files.discard(staged)
            _file_destinations.pop(staged, None)
    return final_path


def staging_path(final_path: Path) -> Path:
    final_path = _canonical_leaf(final_path)
    _assert_temp_destination(final_path)
    parent_identity = _directory_identity(final_path.parent)
    final_snapshot = _leaf_snapshot(final_path)
    token = uuid.uuid4().hex
    path = final_path.with_name(
        f".{final_path.stem}.{token}.staging{final_path.suffix}"
    )
    if path.exists() or path.is_symlink():
        raise RuntimeError(f"Staging path unexpectedly exists: {path}")
    _staged_files.add(path)
    _file_destinations[path] = (final_path, parent_identity, final_snapshot)
    return path


def staging_directory(final_directory: Path) -> Path:
    final_directory = _absolute(final_directory)
    _assert_no_reparse_components(final_directory)
    if not final_directory.is_dir():
        raise RuntimeError(f"Final output directory does not exist: {final_directory}")
    final_directory = final_directory.resolve(strict=True)
    _assert_temp_destination(final_directory)
    final_identity = _directory_identity(final_directory)
    child_snapshots = {
        child.relative_to(final_directory): _leaf_snapshot(child)
        for child in final_directory.iterdir()
    }
    token = uuid.uuid4().hex
    path = final_directory.parent / f".{final_directory.name}.{token}.staging"
    if path.exists() or path.is_symlink():
        raise RuntimeError(f"Staging directory unexpectedly exists: {path}")
    path.mkdir(parents=False, exist_ok=False)
    _staged_directories.add(path)
    _directory_destinations[path] = (
        final_directory,
        final_identity,
        child_snapshots,
    )
    return path


def _registered_destination(
    staged: Path, final: Path
) -> tuple[tuple[int, int], tuple[object, ...]]:
    registration = _file_destinations.get(staged)
    if registration is not None:
        expected, parent_identity, final_snapshot = registration
        if final != expected:
            raise RuntimeError(
                f"Staged output destination changed: expected {expected}, got {final}"
            )
        return parent_identity, final_snapshot

    for staged_directory, (
        final_directory,
        directory_identity,
        child_snapshots,
    ) in (
        _directory_destinations.items()
    ):
        try:
            relative = staged.relative_to(staged_directory)
        except ValueError:
            continue
        expected = final_directory / relative
        if final != expected:
            raise RuntimeError(
                f"Staged directory destination changed: expected {expected}, got {final}"
            )
        return directory_identity, child_snapshots.get(relative, ("missing",))

    raise RuntimeError(f"Unregistered staged output: {staged}")


def _assert_stage_file(path: Path) -> tuple[object, ...]:
    _assert_no_reparse_components(path)
    result = path.lstat()
    if not stat.S_ISREG(result.st_mode):
        raise RuntimeError(f"Staged output is not a regular file: {path}")
    return (
        "file",
        result.st_dev,
        result.st_ino,
        result.st_size,
        result.st_mtime_ns,
    )


def _rollback(promoted: list[Path], backups: dict[Path, Path]) -> list[str]:
    failures: list[str] = []
    for final in reversed(promoted):
        try:
            if final.exists() or final.is_symlink():
                final.unlink()
        except OSError as exc:
            failures.append(f"remove promoted {final}: {exc}")
    for final, backup in reversed(list(backups.items())):
        try:
            if backup.exists() or backup.is_symlink():
                os.replace(backup, final)
        except OSError as exc:
            failures.append(f"restore {backup} -> {final}: {exc}")
    return failures


def promote_files(pairs: Iterable[tuple[Path, Path]]) -> None:
    normalized = [(_absolute(staged), _absolute(final)) for staged, final in pairs]
    destinations = [final for _, final in normalized]
    if not normalized:
        raise RuntimeError("Promotion set must contain at least one output")
    if len(destinations) != len(set(destinations)):
        raise RuntimeError("Duplicate final destination in promotion set")
    for final in destinations:
        _assert_temp_destination(final)

    snapshots: dict[Path, tuple[object, ...]] = {}
    stage_snapshots: dict[Path, tuple[object, ...]] = {}
    parent_identities: dict[Path, tuple[int, int]] = {}
    for staged, final in normalized:
        parent_identity, initial_snapshot = _registered_destination(staged, final)
        parent_identities[final] = parent_identity
        if _directory_identity(final.parent) != parent_identities[final]:
            raise RuntimeError(f"Output directory identity changed: {final.parent}")
        stage_snapshots[staged] = _assert_stage_file(staged)
        snapshots[final] = _leaf_snapshot(final)
        if snapshots[final] != initial_snapshot:
            raise RuntimeError(f"Final output changed during staging: {final}")

    token = uuid.uuid4().hex
    backups: dict[Path, Path] = {}
    promoted: list[Path] = []
    try:
        for _, final in normalized:
            if _directory_identity(final.parent) != parent_identities[final]:
                raise RuntimeError(f"Output directory identity changed: {final.parent}")
            if _leaf_snapshot(final) != snapshots[final]:
                raise RuntimeError(f"Final output changed during build: {final}")
            if snapshots[final][0] == "file":
                backup = final.with_name(f".{final.name}.{token}.backup")
                if backup.exists() or backup.is_symlink():
                    raise RuntimeError(f"Backup path unexpectedly exists: {backup}")
                os.replace(final, backup)
                backups[final] = backup

        for staged, final in normalized:
            if _directory_identity(final.parent) != parent_identities[final]:
                raise RuntimeError(f"Output directory identity changed: {final.parent}")
            if _leaf_snapshot(final) != ("missing",):
                raise RuntimeError(f"Final output reappeared before promotion: {final}")
            if _assert_stage_file(staged) != stage_snapshots[staged]:
                raise RuntimeError(f"Staged output changed before promotion: {staged}")
            os.replace(staged, final)
            _staged_files.discard(staged)
            promoted.append(final)
    except BaseException as exc:
        rollback_failures = _rollback(promoted, backups)
        if rollback_failures:
            note = "Promotion rollback was incomplete; recovery paths remain: " + "; ".join(
                rollback_failures
            )
            if hasattr(exc, "add_note"):
                exc.add_note(note)
            print(f"DXRP_PIPELINE_IO_ROLLBACK_WARNING: {note}", file=sys.stderr)
        raise
    else:
        for backup in backups.values():
            try:
                if backup.exists() or backup.is_symlink():
                    backup.unlink()
            except OSError as exc:
                print(
                    f"DXRP_PIPELINE_IO_CLEANUP_WARNING: committed output; "
                    f"backup remains at {backup}: {exc}",
                    file=sys.stderr,
                )


def _cleanup() -> None:
    for path in list(_staged_files):
        try:
            if path.exists() and not path.is_symlink():
                path.unlink()
        except OSError:
            pass
    for path in sorted(_staged_directories, key=lambda item: len(item.parts), reverse=True):
        try:
            if path.exists() and not path.is_symlink():
                shutil.rmtree(path)
        except OSError:
            pass


atexit.register(_cleanup)
