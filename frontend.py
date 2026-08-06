"""Small Tkinter frontend for the patcher -> variants -> Dresscode workflow."""

from __future__ import annotations

import json
import hashlib
import re
import shutil
import subprocess
import sys
import tempfile
import threading
import urllib.error
import uuid
import urllib.request
import zipfile
from dataclasses import dataclass
from pathlib import Path
import tkinter as tk
from tkinter import filedialog, messagebox, ttk
from typing import Callable


ROOT = Path(__file__).resolve().parent
PATCHER = ROOT / "tools" / "patcher"
PATCH = PATCHER / "patch.py"
PARTS = PATCHER / "devtools" / "parts.py"
CONVERT = PATCHER / "convert.py"
PATCHER_RELEASE = "v1.5.0"
PATCHER_DOWNLOAD_URL = (
    "https://github.com/nikolaybutnik/FFVII-Rebirth-Mesh-Patcher/releases/"
    f"download/{PATCHER_RELEASE}/FFVII-Rebirth-Mesh-Patcher-v1.5.0.zip"
)
PATCHER_SHA256 = "353e90aaa4f3b5b8cda26a7f82451836d23e67260c3260da8b9b700dfe53b3d4"


@dataclass
class Part:
    number: int
    name: str
    model: str


def parse_parts(output: str) -> list[Part]:
    """Parse parts.py's model headers and flat, numbered part rows."""
    result: list[Part] = []
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


def run_command(args: list[str]) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        args, cwd=PATCHER, text=True, encoding="utf-8",
        errors="replace", capture_output=True, check=False,
    )


def patcher_ready() -> bool:
    return PATCH.exists() and PARTS.exists() and CONVERT.exists()


def safe_extract(zip_path: Path, destination: Path) -> None:
    destination = destination.resolve()
    with zipfile.ZipFile(zip_path) as archive:
        for member in archive.infolist():
            target = (destination / member.filename).resolve()
            if target != destination and destination not in target.parents:
                raise RuntimeError(f"archive contains an unsafe path: {member.filename}")
        archive.extractall(destination)


class VariantDialog:
    def __init__(self, parent: tk.Misc, parts: list[Part]):
        self.result = None
        self.done = threading.Event()
        self.window = tk.Toplevel(parent)
        self.window.title("Create variants")
        self.window.geometry("760x520")
        self.window.transient(parent)
        self.window.protocol("WM_DELETE_WINDOW", self.finish)
        self.vars = [(part, tk.BooleanVar(value=False)) for part in parts]
        self.primary_model = parts[0].model
        self.variant_name = tk.StringVar()

        ttk.Label(self.window, text="Select parts to omit, then name and add a variant.").pack(
            anchor="w", padx=12, pady=(12, 4)
        )
        ttk.Label(
            self.window,
            text=(
                f"Only the primary model ({self.primary_model}) can be changed. "
                "Condition and secondary models stay identical between outfits."
            ),
            wraplength=720,
        ).pack(anchor="w", padx=12, pady=(0, 8))
        frame = ttk.Frame(self.window)
        frame.pack(fill="both", expand=True, padx=12)
        canvas = tk.Canvas(frame, highlightthickness=0)
        scroll = ttk.Scrollbar(frame, orient="vertical", command=canvas.yview)
        inner = ttk.Frame(canvas)
        inner.bind("<Configure>", lambda e: canvas.configure(scrollregion=canvas.bbox("all")))
        canvas.create_window((0, 0), window=inner, anchor="nw")
        canvas.configure(yscrollcommand=scroll.set)
        canvas.pack(side="left", fill="both", expand=True)
        scroll.pack(side="right", fill="y")
        for part, variable in self.vars:
            ttk.Checkbutton(
                inner, text=f"{part.number}: {part.name}  [{part.model}]",
                variable=variable,
                state="normal" if part.model == self.primary_model else "disabled",
            ).pack(anchor="w", pady=1)

        entry = ttk.Frame(self.window)
        entry.pack(fill="x", padx=12, pady=8)
        ttk.Label(entry, text="Variant name:").pack(side="left")
        ttk.Entry(entry, textvariable=self.variant_name, width=35).pack(side="left", padx=6)
        ttk.Button(entry, text="Add variant", command=self.add).pack(side="left")
        ttk.Button(entry, text="Finish", command=self.finish).pack(side="right")
        self.listbox = tk.Listbox(self.window, height=4)
        self.listbox.pack(fill="x", padx=12, pady=(0, 12))

    def add(self) -> None:
        name = self.variant_name.get().strip()
        if not name:
            messagebox.showerror("Variant name", "Enter a variant name.", parent=self.window)
            return
        if any(name.casefold() == old.casefold() for old, _ in self.result or []):
            messagebox.showerror("Variant name", "That variant name is already listed.", parent=self.window)
            return
        selected = [part.number for part, variable in self.vars if variable.get()]
        if self.result is None:
            self.result = []
        self.result.append((name, selected))
        self.listbox.insert("end", f"{name} (omit: {', '.join(map(str, selected)) or 'none'})")
        self.variant_name.set("")

    def finish(self) -> None:
        if self.result is None:
            self.result = []
        self.window.destroy()
        self.done.set()


class Frontend:
    WORKFLOW_STEPS = (
        "Checking patch format",
        "Patching or copying skin",
        "Deciding whether to add variants",
        "Listing and creating variants",
        "Writing metadata",
        "Converting and completing",
    )

    def __init__(self, root: tk.Tk):
        self.root = root
        self.root.title("DressCoder")
        self.root.geometry("850x650")
        self.source = tk.StringVar()
        self.destination = tk.StringVar()
        self.skin_name = tk.StringVar()
        self.start_button: ttk.Button
        self.install_button: ttk.Button
        self.dependencies_button: ttk.Button
        self.tool_status = tk.StringVar()
        self.step_text = tk.StringVar(value="Ready to start")
        self.progress = tk.DoubleVar(value=0)
        self.log_lines: list[str] = []
        self.detail_window = None
        self.detail_log = None
        self.build_ui()
        self.refresh_tool_status()

    def build_ui(self) -> None:
        form = ttk.Frame(self.root, padding=12)
        form.pack(fill="x")
        self.add_folder_row(form, "Source skin folder:", self.source, self.choose_source, 0)
        self.add_folder_row(form, "Destination root:", self.destination, self.choose_destination, 1)
        ttk.Label(form, text="Skin name:").grid(row=2, column=0, sticky="w", pady=5)
        ttk.Entry(form, textvariable=self.skin_name).grid(row=2, column=1, sticky="ew", padx=6)
        form.columnconfigure(1, weight=1)
        tools = ttk.Frame(form)
        tools.grid(row=3, column=0, columnspan=3, sticky="ew", pady=(8, 0))
        ttk.Label(tools, textvariable=self.tool_status).pack(side="left")
        self.install_button = ttk.Button(
            tools, text="Install patcher", command=self.install_patcher
        )
        self.install_button.pack(side="right", padx=(6, 0))
        self.dependencies_button = ttk.Button(
            tools, text="Install dependencies", command=self.install_dependencies
        )
        self.dependencies_button.pack(side="right")
        self.start_button = ttk.Button(form, text="Start process", command=self.start)
        self.start_button.grid(row=4, column=0, columnspan=3, pady=(10, 4))
        ttk.Label(
            self.root, text="Patch/copy the skin, optionally add variants, then build Dresscode.",
            padding=(12, 0),
        ).pack(anchor="w")
        status = ttk.Frame(self.root, padding=12)
        status.pack(fill="x")
        ttk.Label(status, textvariable=self.step_text).pack(anchor="w")
        ttk.Progressbar(
            status, variable=self.progress, maximum=len(self.WORKFLOW_STEPS),
            mode="determinate",
        ).pack(fill="x", pady=(6, 6))
        ttk.Button(status, text="Detailed view", command=self.show_detailed_view).pack(
            anchor="e"
        )

    @staticmethod
    def add_folder_row(
        parent: ttk.Frame,
        label: str,
        variable: tk.StringVar,
        command: Callable[[], None],
        row: int,
    ) -> None:
        ttk.Label(parent, text=label).grid(row=row, column=0, sticky="w", pady=5)
        ttk.Entry(parent, textvariable=variable).grid(row=row, column=1, sticky="ew", padx=6)
        ttk.Button(parent, text="Browse…", command=command).grid(row=row, column=2)

    def choose_source(self):
        path = filedialog.askdirectory(title="Select source skin folder")
        if path:
            self.source.set(path)
            if not self.skin_name.get().strip():
                self.skin_name.set(Path(path).name)

    def choose_destination(self):
        path = filedialog.askdirectory(title="Select destination root")
        if path:
            self.destination.set(path)

    def refresh_tool_status(self) -> None:
        installed = patcher_ready()
        self.tool_status.set(
            f"Patcher {PATCHER_RELEASE}: "
            + ("installed" if installed else "not installed")
        )
        if hasattr(self, "install_button"):
            self.install_button.configure(state="disabled" if installed else "normal")
            self.dependencies_button.configure(state="normal" if installed else "disabled")
            self.start_button.configure(state="normal" if installed else "disabled")

    def append_log(self, text: str) -> None:
        line = text.rstrip()
        if not line:
            return
        self.log_lines.append(line)
        if self.detail_log is not None:
            self.detail_log.configure(state="normal")
            self.detail_log.insert("end", line + "\n")
            self.detail_log.see("end")
            self.detail_log.configure(state="disabled")

    def show_detailed_view(self) -> None:
        if self.detail_window is not None and self.detail_window.winfo_exists():
            self.detail_window.deiconify()
            self.detail_window.lift()
            return
        window = tk.Toplevel(self.root)
        self.detail_window = window
        window.title("Detailed logs")
        window.geometry("800x500")
        window.transient(self.root)
        frame = ttk.Frame(window, padding=12)
        frame.pack(fill="both", expand=True)
        log = tk.Text(frame, wrap="word", state="disabled")
        scroll = ttk.Scrollbar(frame, orient="vertical", command=log.yview)
        log.configure(yscrollcommand=scroll.set)
        log.pack(side="left", fill="both", expand=True)
        scroll.pack(side="right", fill="y")
        self.detail_log = log
        if self.log_lines:
            log.configure(state="normal")
            log.insert("end", "\n".join(self.log_lines) + "\n")
            log.see("end")
            log.configure(state="disabled")
        close_button = ttk.Button(window, text="Close", command=window.destroy)
        close_button.pack(pady=(0, 12))

        def clear_detail_references() -> None:
            self.detail_window = None
            self.detail_log = None

        def close() -> None:
            clear_detail_references()
            window.destroy()

        window.protocol("WM_DELETE_WINDOW", close)
        close_button.configure(command=close)

    def set_step(self, number: int) -> None:
        self.step_text.set(f"Step {number} of {len(self.WORKFLOW_STEPS)}: {self.WORKFLOW_STEPS[number - 1]}")
        self.progress.set(number - 1)

    def ui_step(self, number: int) -> None:
        self.root.after(0, self.set_step, number)

    def ui_call(self, callback):
        event = threading.Event()
        result = []

        def invoke():
            try:
                result.append(callback())
            finally:
                event.set()

        self.root.after(0, invoke)
        event.wait()
        return result[0] if result else None

    def start(self) -> None:
        if not patcher_ready():
            messagebox.showerror(
                "Patcher missing",
                "Install the FFVII Rebirth Mesh Patcher before starting.",
                parent=self.root,
            )
            return
        source = Path(self.source.get().strip())
        destination = Path(self.destination.get().strip())
        name = self.skin_name.get().strip() or source.name
        if not source.is_dir() or not destination.is_dir() or not name:
            messagebox.showerror("Input required", "Choose existing source and destination folders.")
            return
        if Path(name).name != name or name in (".", ".."):
            messagebox.showerror("Invalid skin name", "Use a single folder name for the skin.")
            return
        target = destination / name
        try:
            if target.resolve().is_relative_to(source.resolve()):
                messagebox.showerror(
                    "Invalid folders", "The destination skin cannot be the source folder or inside it."
                )
                return
        except OSError as exc:
            messagebox.showerror("Invalid folders", f"Could not compare folders: {exc}")
            return
        if target.exists() and not messagebox.askyesno(
            "Destination exists", f"{target} already exists. Replace it?", parent=self.root
        ):
            return
        self.start_button.configure(state="disabled")
        self.set_step(1)
        self.append_log(f"Starting: {source} -> {target}")
        threading.Thread(
            target=self.worker, args=(source, destination, name, target), daemon=True
        ).start()

    def install_patcher(self) -> None:
        if patcher_ready():
            messagebox.showinfo(
                "Patcher installed",
                f"FFVII Rebirth Mesh Patcher {PATCHER_RELEASE} is already installed.",
                parent=self.root,
            )
            return
        self.install_button.configure(state="disabled")
        self.dependencies_button.configure(state="disabled")
        self.start_button.configure(state="disabled")
        threading.Thread(target=self.install_patcher_worker, daemon=True).start()

    def install_patcher_worker(self) -> None:
        archive_path = None
        staging = None
        try:
            self.ui_log(f"Downloading FFVII Rebirth Mesh Patcher {PATCHER_RELEASE}...")
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
            patch_candidates = [
                path for path in staging.rglob("patch.py")
                if (path.parent / "convert.py").is_file()
                and (path.parent / "devtools" / "parts.py").is_file()
            ]
            if len(patch_candidates) != 1:
                raise RuntimeError(
                    "the release archive does not contain exactly one valid patcher"
                )
            PATCHER.mkdir(parents=True, exist_ok=True)
            shutil.copytree(patch_candidates[0].parent, PATCHER, dirs_exist_ok=True)
            if not patcher_ready():
                raise RuntimeError("patcher installation is incomplete")
            self.ui_log(f"Patcher {PATCHER_RELEASE} installed.")
            self.root.after(0, self.refresh_tool_status)
        except (OSError, RuntimeError, urllib.error.URLError, zipfile.BadZipFile) as exc:
            self.ui_log(f"ERROR: patcher installation failed: {exc}")
            error_message = str(exc)
            self.root.after(
                0,
                lambda: messagebox.showerror(
                    "Patcher installation failed", error_message, parent=self.root
                ),
            )
        finally:
            if archive_path and archive_path.exists():
                archive_path.unlink()
            if staging and staging.exists():
                shutil.rmtree(staging, ignore_errors=True)
            self.root.after(0, self.refresh_tool_status)

    def install_dependencies(self) -> None:
        if not patcher_ready():
            messagebox.showerror(
                "Patcher missing",
                "Install the patcher before installing its dependencies.",
                parent=self.root,
            )
            return
        requirements = PATCHER / "requirements.txt"
        if not requirements.is_file():
            messagebox.showerror(
                "Requirements missing",
                f"Could not find {requirements}.",
                parent=self.root,
            )
            return
        self.install_button.configure(state="disabled")
        self.dependencies_button.configure(state="disabled")
        self.start_button.configure(state="disabled")
        threading.Thread(
            target=self.install_dependencies_worker,
            args=(requirements,),
            daemon=True,
        ).start()

    def install_dependencies_worker(self, requirements: Path) -> None:
        command = [
            sys.executable,
            "-m",
            "pip",
            "install",
            "--disable-pip-version-check",
            "-r",
            str(requirements),
        ]
        self.ui_log("$ " + subprocess.list2cmdline(command))
        completed = subprocess.run(
            command,
            cwd=PATCHER,
            text=True,
            encoding="utf-8",
            errors="replace",
            capture_output=True,
            check=False,
        )
        output = (completed.stdout + completed.stderr).strip()
        if output:
            self.ui_log(output)
        if completed.returncode == 0:
            self.ui_log("Dependencies installed.")
        else:
            self.ui_log(
                f"ERROR: dependency installation failed with exit code "
                f"{completed.returncode}"
            )
            self.root.after(
                0,
                lambda: messagebox.showerror(
                    "Dependency installation failed",
                    f"pip exited with code {completed.returncode}.",
                    parent=self.root,
                ),
            )
        self.root.after(0, self.refresh_tool_status)

    def worker(self, source: Path, destination: Path, name: str, target: Path) -> None:
        try:
            if target.exists():
                shutil.rmtree(target)
            work = destination / f".dresscoder-work-{uuid.uuid4().hex}"
            try:
                self.ui_step(1)
                listing_args = [
                    sys.executable, str(PATCH), "--path", str(source),
                    "--out", str(work), "--list",
                ]
                self.ui_log("$ " + subprocess.list2cmdline(listing_args))
                listing = run_command(listing_args)
                self.ui_log((listing.stdout + listing.stderr).strip())
                if listing.returncode != 0:
                    raise RuntimeError(f"patch listing failed with exit code {listing.returncode}")
                needs_patch = "needs patching" in listing.stdout.lower()
                self.ui_step(2)
                if needs_patch:
                    self.log_command([sys.executable, str(PATCH), "--path", str(source),
                                      "--out", str(target), "--all"])
                else:
                    self.ui_log("No patch required; copying source without modification.")
                    shutil.copytree(source, target)
                self.ui_step(3)
                if self.ui_call(lambda: messagebox.askyesno(
                    "Variants", "Do you want to add variants?", parent=self.root
                )):
                    self.make_variants(target)
                else:
                    self.ui_step(4)
                self.ui_step(5)
                self.write_metadata(target, name)
                self.ui_step(6)
                self.convert_to_dresscode(target)
                self.ui_log("Finished successfully.")
                self.root.after(0, self.progress.set, len(self.WORKFLOW_STEPS))
                self.ui_call(lambda: messagebox.showinfo("Done", "Dresscode conversion completed.",
                                                         parent=self.root))
            finally:
                if work.exists():
                    try:
                        shutil.rmtree(work)
                    except OSError as exc:
                        self.ui_log(f"WARNING: could not remove work folder {work}: {exc}")
        except Exception as exc:
            self.ui_log(f"ERROR: {type(exc).__name__}: {exc}")
            self.ui_call(lambda: messagebox.showerror("Process failed", str(exc), parent=self.root))
        finally:
            self.root.after(0, lambda: self.start_button.configure(state="normal"))

    def ui_log(self, text: str) -> None:
        self.root.after(0, self.append_log, text)

    def log_command(self, args: list[str]) -> None:
        self.ui_log("$ " + subprocess.list2cmdline(args))
        completed = run_command(args)
        output = (completed.stdout + completed.stderr).strip()
        if output:
            self.ui_log(output)
        if completed.returncode != 0:
            raise RuntimeError(f"command failed with exit code {completed.returncode}")

    def convert_to_dresscode(self, target: Path) -> None:
        """Run the official converter and relocate its sibling output."""
        staging = Path(tempfile.mkdtemp(
            prefix=".dresscoder-convert-", dir=str(target.parent)
        ))
        staged_source = staging / target.name
        output = None
        try:
            shutil.move(str(target), str(staged_source))
            command = [sys.executable, str(CONVERT), str(staged_source), "--yes"]
            self.log_command(command)
            candidates = sorted(
                path for path in staging.iterdir()
                if path.is_dir() and path.name.endswith(" (Dresscode)")
            )
            if len(candidates) != 1:
                raise RuntimeError(
                    "the converter did not produce exactly one Dresscode folder"
                )
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

    def make_variants(self, target: Path) -> None:
        self.ui_step(4)
        command = [sys.executable, str(PARTS), str(target), "--list"]
        self.ui_log("$ " + subprocess.list2cmdline(command))
        listed = run_command(command)
        self.ui_log((listed.stdout + listed.stderr).strip())
        if listed.returncode != 0:
            raise RuntimeError(f"parts listing failed with exit code {listed.returncode}")
        parts = parse_parts(listed.stdout + listed.stderr)
        if not parts:
            raise RuntimeError("parts.py returned no editable parts.")
        variants = self.ui_call(lambda: self.show_variant_dialog(parts))
        primary_model = parts[0].model
        allowed_numbers = {
            part.number for part in parts if part.model == primary_model
        }
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
                allowed = self.ui_call(lambda: messagebox.askyesno(
                    "Variant exists", f"{out} already exists. Replace it?", parent=self.root
                ))
                if not allowed:
                    raise RuntimeError(f"refused to overwrite existing variant: {out}")
                shutil.rmtree(out)
            args = [sys.executable, str(PARTS), str(target), "--omit",
                    ",".join(map(str, omitted)) or "none", "--out", str(out)]
            self.log_command(args)

    def show_variant_dialog(self, parts):
        dialog = VariantDialog(self.root, parts)
        self.root.wait_window(dialog.window)
        return dialog.result or []

    @staticmethod
    def write_metadata(target: Path, name: str) -> None:
        path = target / "dresscode.json"
        data = {}
        if path.is_file():
            with path.open("r", encoding="utf-8") as stream:
                data = json.load(stream)
        data.update({
            "name": name,
            "author": data.get("author", ""),
            "description": data.get("description", ""),
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


if __name__ == "__main__":
    app = tk.Tk()
    Frontend(app)
    app.mainloop()
