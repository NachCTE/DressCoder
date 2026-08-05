"""Small Tkinter frontend for the patcher -> variants -> Dresscode workflow."""

from __future__ import annotations

import json
import re
import shutil
import subprocess
import sys
import threading
import uuid
from dataclasses import dataclass
from pathlib import Path
import tkinter as tk
from tkinter import filedialog, messagebox, ttk
from typing import Callable, List, Optional, Tuple, TypeVar


ROOT = Path(__file__).resolve().parent
PATCHER = ROOT / "tools" / "patcher"
PATCH = PATCHER / "patch.py"
PARTS = PATCHER / "devtools" / "parts.py"
CONVERT = PATCHER / "convert.py"
T = TypeVar("T")


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


class VariantDialog:
    def __init__(self, parent: tk.Misc, parts: list[Part]):
        self.result: Optional[List[Tuple[str, List[int]]]] = None
        self.done = threading.Event()
        self.window = tk.Toplevel(parent)
        self.window.title("Create variants")
        self.window.geometry("760x520")
        self.window.transient(parent)
        self.window.protocol("WM_DELETE_WINDOW", self.finish)
        self.vars = [(part, tk.BooleanVar(value=False)) for part in parts]
        self.variant_name = tk.StringVar()

        ttk.Label(self.window, text="Select parts to omit, then name and add a variant.").pack(
            anchor="w", padx=12, pady=(12, 4)
        )
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
    def __init__(self, root: tk.Tk):
        self.root = root
        self.root.title("DressCoder")
        self.root.geometry("850x650")
        self.source = tk.StringVar()
        self.destination = tk.StringVar()
        self.skin_name = tk.StringVar()
        self.start_button: ttk.Button
        self.log = tk.Text(root, height=20, state="disabled", wrap="word")
        self.build_ui()

    def build_ui(self) -> None:
        form = ttk.Frame(self.root, padding=12)
        form.pack(fill="x")
        self.add_folder_row(form, "Source skin folder:", self.source, self.choose_source, 0)
        self.add_folder_row(form, "Destination root:", self.destination, self.choose_destination, 1)
        ttk.Label(form, text="Skin name:").grid(row=2, column=0, sticky="w", pady=5)
        ttk.Entry(form, textvariable=self.skin_name).grid(row=2, column=1, sticky="ew", padx=6)
        form.columnconfigure(1, weight=1)
        self.start_button = ttk.Button(form, text="Start process", command=self.start)
        self.start_button.grid(row=3, column=0, columnspan=3, pady=(10, 4))
        ttk.Label(
            self.root, text="Patch/copy the skin, optionally add variants, then build Dresscode.",
            padding=(12, 0),
        ).pack(anchor="w")
        self.log.pack(fill="both", expand=True, padx=12, pady=12)

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

    def append_log(self, text: str) -> None:
        self.log.configure(state="normal")
        self.log.insert("end", text.rstrip() + "\n")
        self.log.see("end")
        self.log.configure(state="disabled")

    def ui_call(self, callback: Callable[[], T]) -> Optional[T]:
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
        self.append_log(f"Starting: {source} -> {target}")
        threading.Thread(
            target=self.worker, args=(source, destination, name, target), daemon=True
        ).start()

    def worker(self, source: Path, destination: Path, name: str, target: Path) -> None:
        try:
            if target.exists():
                shutil.rmtree(target)
            work = destination / f".dresscoder-work-{uuid.uuid4().hex}"
            try:
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
                if needs_patch:
                    self.log_command([sys.executable, str(PATCH), "--path", str(source),
                                      "--out", str(target), "--all"])
                else:
                    self.ui_log("No patch required; copying source without modification.")
                    shutil.copytree(source, target)
                if self.ui_call(lambda: messagebox.askyesno(
                    "Variants", "Do you want to add variants?", parent=self.root
                )):
                    self.make_variants(target)
                self.write_metadata(target, name)
                self.log_command([sys.executable, str(CONVERT), str(target),
                                  "--out", str(target / "dresscode"), "--yes"])
                self.ui_log("Finished successfully.")
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

    def make_variants(self, target: Path) -> None:
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
        for variant, omitted in variants or []:
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
