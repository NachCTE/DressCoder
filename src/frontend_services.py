"""Tkinter-free services for the DressCoder frontend."""

from __future__ import annotations

import hashlib
import importlib.util
import json
import re
import shutil
import subprocess
import sys
import tempfile
import urllib.error
import urllib.request
import uuid
import zipfile
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Dict, List, Optional, Tuple


ROOT = (
    Path(sys.executable).resolve().parent
    if getattr(sys, "frozen", False)
    else Path(__file__).resolve().parent
)
PATCHER = ROOT / "tools" / "patcher"
PATCH = PATCHER / "patch.py"
PARTS = PATCHER / "devtools" / "parts.py"
CONVERT = PATCHER / "convert.py"
UNPATCH = PATCHER / "unpatch.py"
PATCHER_RELEASE = "v1.5.0"
PATCHER_DOWNLOAD_URL = (
    "https://github.com/nikolaybutnik/FFVII-Rebirth-Mesh-Patcher/releases/"
    f"download/{PATCHER_RELEASE}/FFVII-Rebirth-Mesh-Patcher-v1.5.0.zip"
)
PATCHER_SHA256 = "353e90aaa4f3b5b8cda26a7f82451836d23e67260c3260da8b9b700dfe53b3d4"

LogCallback = Callable[[str], None]
StepCallback = Callable[[int], None]
VariantCallback = Callable[
    [List["Part"]], Optional[List[Tuple[str, List[int]]]]
]
ConfirmCallback = Callable[[str, str], bool]


@dataclass
class Part:
    number: int
    name: str
    model: str


def parse_parts(output: str) -> List[Part]:
    """Parse parts.py's model headers and flat, numbered part rows."""
    result = []
    model = "unknown model"
    header = re.compile(r"^\s{2,}(.+?)\s+\(([^()]*)\)\s*$")
    row = re.compile(
        r"^\s+(\d+)\s{2,}(.+?)\s{2,}[\d,]+\s{3,}(.+?)(?:\s+\[left out\])?\s*$"
    )
    for line in output.splitlines():
        match = header.match(line)
        if match and not match.group(1).strip().startswith(("#", "(")):
            model = match.group(1).strip()
            continue
        match = row.match(line)
        if match:
            result.append(Part(int(match.group(1)), match.group(2).strip(), model))
    return result


def safe_extract(zip_path: Path, destination: Path) -> None:
    destination = destination.resolve()
    with zipfile.ZipFile(zip_path) as archive:
        for member in archive.infolist():
            target = (destination / member.filename).resolve()
            if target != destination and destination not in target.parents:
                raise RuntimeError("archive contains an unsafe path: " + member.filename)
        archive.extractall(destination)


def patcher_ready() -> bool:
    return PATCH.exists() and PARTS.exists() and CONVERT.exists() and UNPATCH.exists()


def patcher_dependencies_ready() -> bool:
    return importlib.util.find_spec("numpy") is not None


def python_command() -> str:
    if not getattr(sys, "frozen", False):
        return sys.executable
    command = shutil.which("python")
    if command is None:
        raise RuntimeError("Python was not found in PATH.")
    return command


def is_dresscode_folder(source: Path) -> bool:
    return any(source.rglob("*.uplugin"))


class PatcherService:
    """Installs the official patcher and its Python dependencies."""

    def __init__(self, log: LogCallback):
        self.log = log

    def install(self) -> None:
        archive_path = None
        staging = None
        try:
            self.log("Downloading FFVII Rebirth Mesh Patcher " + PATCHER_RELEASE + "...")
            with tempfile.NamedTemporaryFile(
                prefix="dresscoder-patcher-", suffix=".zip", delete=False
            ) as stream:
                archive_path = Path(stream.name)
                with urllib.request.urlopen(PATCHER_DOWNLOAD_URL, timeout=60) as response:
                    while True:
                        chunk = response.read(1024 * 1024)
                        if not chunk:
                            break
                        stream.write(chunk)
            digest = hashlib.sha256(archive_path.read_bytes()).hexdigest()
            if digest != PATCHER_SHA256:
                raise RuntimeError(
                    f"download checksum mismatch: expected {PATCHER_SHA256}, got {digest}"
                )
            staging = Path(tempfile.mkdtemp(prefix="dresscoder-patcher-"))
            safe_extract(archive_path, staging)
            candidates = [
                path for path in staging.rglob("patch.py")
                if (path.parent / "convert.py").is_file()
                and (path.parent / "devtools" / "parts.py").is_file()
            ]
            if len(candidates) != 1:
                raise RuntimeError(
                    "the release archive does not contain exactly one valid patcher"
                )
            PATCHER.mkdir(parents=True, exist_ok=True)
            shutil.copytree(candidates[0].parent, PATCHER, dirs_exist_ok=True)
            if not patcher_ready():
                raise RuntimeError("patcher installation is incomplete")
            self.log("Patcher " + PATCHER_RELEASE + " installed.")
        finally:
            if archive_path and archive_path.exists():
                archive_path.unlink()
            if staging and staging.exists():
                shutil.rmtree(staging, ignore_errors=True)

    def install_dependencies(self) -> int:
        requirements = PATCHER / "requirements.txt"
        if not requirements.is_file():
            raise RuntimeError(f"Could not find {requirements}.")
        command = [
            python_command(), "-m", "pip", "install",
            "--disable-pip-version-check", "-r", str(requirements),
        ]
        self.log("$ " + subprocess.list2cmdline(command))
        completed = subprocess.run(
            command, cwd=PATCHER, text=True, encoding="utf-8",
            errors="replace", capture_output=True, check=False,
        )
        output = (completed.stdout + completed.stderr).strip()
        if output:
            self.log(output)
        if completed.returncode == 0:
            self.log("Dependencies installed.")
        else:
            self.log(
                "ERROR: dependency installation failed with exit code "
                + str(completed.returncode)
            )
        return completed.returncode


class BatchPatchService:
    """Patches or unpatches independent skin folders into a destination root."""

    def __init__(self, log: LogCallback, step: StepCallback):
        self.log = log
        self.step = step

    @staticmethod
    def _reason(output: str, fallback: str) -> str:
        for line in output.splitlines():
            text = line.strip()
            lowered = text.lower()
            if any(word in lowered for word in (
                "nothing", "already", "unaffected", "not touched", "no character",
            )):
                return text
        return fallback

    def run(
        self, sources: List[Path], destination: Path, reverse: bool
    ) -> List[Dict[str, str]]:
        tool = UNPATCH if reverse else PATCH
        needs_label = "needs unpatching" if reverse else "needs patching"
        results = []
        for index, source in enumerate(sources, start=1):
            source = source.resolve()
            batch_output_name = source.name
            target = destination.resolve() / batch_output_name
            self.log(f"Batch source: {source}")
            self.log(f"Batch output: {target}")
            if target.exists():
                shutil.rmtree(target)
            self.step(index)
            try:
                shutil.copytree(source, target)
                inspect_command = [
                    python_command(), str(tool), "--path", str(target), "--list",
                ]
                self.log("$ " + subprocess.list2cmdline(inspect_command))
                inspected = subprocess.run(
                    inspect_command, cwd=PATCHER, text=True, encoding="utf-8",
                    errors="replace", capture_output=True, check=False,
                )
                inspection = (inspected.stdout + inspected.stderr).strip()
                if inspection:
                    self.log(inspection)
                if inspected.returncode != 0:
                    results.append({
                        "name": batch_output_name, "status": "failed",
                        "reason": f"inspection exited with code {inspected.returncode}",
                    })
                    continue
                if needs_label not in inspection.lower():
                    results.append({
                        "name": batch_output_name, "status": "skipped",
                        "reason": self._reason(inspection, "already in the target format"),
                    })
                    continue
                command = [
                    python_command(), str(tool), "--path", str(target),
                    "--all", "--no-backup",
                ]
                self.log("$ " + subprocess.list2cmdline(command))
                completed = subprocess.run(
                    command, cwd=PATCHER, text=True, encoding="utf-8",
                    errors="replace", capture_output=True, check=False,
                )
                output = (completed.stdout + completed.stderr).strip()
                if output:
                    self.log(output)
                if completed.returncode != 0:
                    results.append({
                        "name": batch_output_name, "status": "failed",
                        "reason": f"patcher exited with code {completed.returncode}",
                    })
                else:
                    results.append({
                        "name": batch_output_name, "status": "patched",
                        "reason": "patched successfully",
                    })
            except OSError as exc:
                results.append({
                    "name": batch_output_name, "status": "failed", "reason": str(exc),
                })
        return results


class WorkflowService:
    """Runs the patch, variant, metadata, and converter workflow."""

    WORKFLOW_STEP_COUNT = 6

    def __init__(
        self,
        log: LogCallback,
        step: StepCallback,
        choose_variants: VariantCallback,
        confirm: ConfirmCallback,
    ):
        self.log = log
        self.step = step
        self.choose_variants = choose_variants
        self.confirm = confirm

    def run(
        self, source: Path, destination: Path, name: str, target: Path,
        author: str = "", description: str = "", photo: Optional[Path] = None,
        skip_patch: bool = False,
    ) -> None:
        try:
            if is_dresscode_folder(source):
                raise RuntimeError(
                    "the selected source already appears to be a Dresscode mod"
                )
            if target.exists():
                shutil.rmtree(target)
            work = destination / (".dresscoder-work-" + uuid.uuid4().hex)
            try:
                self.step(1)
                self.step(2)
                if skip_patch:
                    self.log("Skipping V1.005 patch; copying source unchanged.")
                    shutil.copytree(source, target)
                else:
                    listing_args = [
                        python_command(), str(PATCH), "--path", str(source),
                        "--list",
                    ]
                    listing = self.run_logged(listing_args)
                    if listing.returncode != 0:
                        raise RuntimeError(
                            f"patch listing failed with exit code {listing.returncode}"
                        )
                    needs_patch = "needs patching" in (
                        listing.stdout + listing.stderr
                    ).lower()
                    if needs_patch:
                        shutil.copytree(source, target)
                        patched = self.run_logged([
                            python_command(), str(PATCH), "--path", str(target),
                            "--all", "--no-backup",
                        ])
                        if patched.returncode != 0:
                            raise RuntimeError(
                                f"patch failed with exit code {patched.returncode}"
                            )
                    else:
                        self.log("No patch required; copying source without modification.")
                        shutil.copytree(source, target)
                self.step(3)
                if self.confirm("Variants", "Do you want to add variants?"):
                    self.make_variants(target)
                else:
                    self.step(4)
                self.step(5)
                self.write_metadata(target, name, author, description, photo)
                self.step(6)
                self.convert_to_dresscode(target)
                self.log("Finished successfully.")
            finally:
                if work.exists():
                    try:
                        shutil.rmtree(work)
                    except OSError as exc:
                        self.log(f"WARNING: could not remove work folder {work}: {exc}")
        except Exception as exc:
            self.log(f"ERROR: {type(exc).__name__}: {exc}")
            raise

    def run_logged(self, args: List[str]) -> subprocess.CompletedProcess:
        self.log("$ " + subprocess.list2cmdline(args))
        completed = subprocess.run(
            args, cwd=PATCHER, text=True, encoding="utf-8",
            errors="replace", capture_output=True, check=False,
        )
        output = (completed.stdout + completed.stderr).strip()
        if output:
            self.log(output)
        return completed

    def make_variants(self, target: Path) -> None:
        self.step(4)
        listed = self.run_logged([python_command(), str(PARTS), str(target), "--list"])
        if listed.returncode != 0:
            raise RuntimeError(f"parts listing failed with exit code {listed.returncode}")
        parts = parse_parts(listed.stdout + listed.stderr)
        if not parts:
            raise RuntimeError("parts.py returned no editable parts.")
        variants = self.choose_variants(parts)
        primary_model = parts[0].model
        allowed_numbers = {part.number for part in parts if part.model == primary_model}
        for variant, omitted in variants or []:
            invalid = sorted(set(omitted) - allowed_numbers)
            if invalid:
                raise RuntimeError(
                    "variant selection contains parts from a secondary model: "
                    + ", ".join(map(str, invalid))
                )
            if Path(variant).name != variant or variant in (".", ".."):
                raise RuntimeError(f"invalid variant name: {variant!r}")
            out = target / "Variants" / variant
            if out.exists():
                if not self.confirm("Variant exists", f"{out} already exists. Replace it?"):
                    raise RuntimeError(f"refused to overwrite existing variant: {out}")
                shutil.rmtree(out)
            self.run_logged([
                python_command(), str(PARTS), str(target), "--omit",
                ",".join(map(str, omitted)) or "none", "--out", str(out),
            ])

    def convert_to_dresscode(self, target: Path) -> None:
        """Run the official converter and relocate its sibling output."""
        staging = Path(tempfile.mkdtemp(prefix=".dresscoder-convert-", dir=str(target.parent)))
        staged_source = staging / target.name
        output = None
        try:
            shutil.move(str(target), str(staged_source))
            converted = self.run_logged([
                python_command(), str(CONVERT), str(staged_source), "--yes"
            ])
            if converted.returncode != 0:
                raise RuntimeError(
                    f"converter failed with exit code {converted.returncode}"
                )
            candidates = sorted(
                path for path in staging.iterdir()
                if path.is_dir() and path.name.endswith(" (Dresscode)")
            )
            if len(candidates) != 1:
                raise RuntimeError("the converter did not produce exactly one Dresscode folder")
            output = candidates[0]
            shutil.move(str(staged_source), str(target))
            destination = target / "dresscode"
            if destination.exists():
                shutil.rmtree(destination)
            shutil.move(str(output), str(destination))
        except Exception:
            if not target.exists() and staged_source.exists():
                shutil.move(str(staged_source), str(target))
            raise
        finally:
            if staging.exists():
                shutil.rmtree(staging, ignore_errors=True)

    @staticmethod
    def write_metadata(
        target: Path, name: str, author: str = "", description: str = "",
        photo: Optional[Path] = None,
    ) -> None:
        if photo is not None:
            target_photo = target / ("icon" + photo.suffix.lower())
            shutil.copy2(photo, target_photo)
        path = target / "dresscode.json"
        data = {}
        if path.is_file():
            with path.open("r", encoding="utf-8") as stream:
                data = json.load(stream)
        data.update({
            "name": name,
            "author": author,
            "description": description,
            "category": data.get("category", "Outfit"),
            "version": data.get("version", "1.0.0"),
            "stackable": data.get("stackable", False),
        })
        outfits = [{"folder": ".", "name": name, "description": ""}]
        variants = target / "Variants"
        if variants.is_dir():
            for folder in sorted(variants.iterdir(), key=lambda p: p.name.casefold()):
                if folder.is_dir():
                    outfits.append({
                        "folder": f"Variants/{folder.name}",
                        "name": folder.name,
                        "description": "",
                    })
        data["outfits"] = outfits
        with path.open("w", encoding="utf-8") as stream:
            json.dump(data, stream, indent=2, ensure_ascii=False)
            stream.write("\n")
