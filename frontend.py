"""Tkinter view for the patcher -> variants -> Dresscode workflow."""

from __future__ import annotations

import threading
import urllib.error
import zipfile
from pathlib import Path
from typing import Callable, List, Optional, Tuple
import tkinter as tk
from tkinter import filedialog, messagebox, ttk

from frontend_services import (
    PATCHER_RELEASE,
    Part,
    PatcherService,
    WorkflowService,
    patcher_ready,
)


class VariantDialog:
    def __init__(self, parent: tk.Misc, parts: List[Part]):
        self.result = None
        self.window = tk.Toplevel(parent)
        self.window.title("Create variants")
        self.window.geometry("760x520")
        self.window.transient(parent)
        self.window.protocol("WM_DELETE_WINDOW", self.finish)
        self.vars = [(part, tk.BooleanVar(value=False)) for part in parts]
        self.primary_model = parts[0].model
        self.variant_name = tk.StringVar()
        ttk.Label(
            self.window, text="Select parts to omit, then name and add a variant."
        ).pack(anchor="w", padx=12, pady=(12, 4))
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
            messagebox.showerror(
                "Variant name", "That variant name is already listed.", parent=self.window
            )
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


class Frontend:
    WORKFLOW_STEPS = (
        "Checking patch format", "Patching or copying skin",
        "Deciding whether to add variants", "Listing and creating variants",
        "Writing metadata", "Converting and completing",
    )

    def __init__(self, root: tk.Tk):
        self.root = root
        self.root.title("DressCoder")
        self.root.geometry("850x650")
        self.source = tk.StringVar()
        self.destination = tk.StringVar()
        self.skin_name = tk.StringVar()
        self.tool_status = tk.StringVar()
        self.step_text = tk.StringVar(value="Ready to start")
        self.progress = tk.DoubleVar(value=0)
        self.log_lines = []
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
        self.install_button = ttk.Button(tools, text="Install patcher", command=self.install_patcher)
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
            status, variable=self.progress, maximum=len(self.WORKFLOW_STEPS), mode="determinate"
        ).pack(fill="x", pady=6)
        ttk.Button(status, text="Detailed view", command=self.show_detailed_view).pack(anchor="e")

    @staticmethod
    def add_folder_row(parent: ttk.Frame, label: str, variable: tk.StringVar,
                       command: Callable[[], None], row: int) -> None:
        ttk.Label(parent, text=label).grid(row=row, column=0, sticky="w", pady=5)
        ttk.Entry(parent, textvariable=variable).grid(row=row, column=1, sticky="ew", padx=6)
        ttk.Button(parent, text="Browse…", command=command).grid(row=row, column=2)

    def choose_source(self) -> None:
        path = filedialog.askdirectory(title="Select source skin folder")
        if path:
            self.source.set(path)
            if not self.skin_name.get().strip():
                self.skin_name.set(Path(path).name)

    def choose_destination(self) -> None:
        path = filedialog.askdirectory(title="Select destination root")
        if path:
            self.destination.set(path)

    def refresh_tool_status(self) -> None:
        installed = patcher_ready()
        self.tool_status.set(
            f"Patcher {PATCHER_RELEASE}: " + ("installed" if installed else "not installed")
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
        close_button = ttk.Button(window, text="Close")
        close_button.pack(pady=(0, 12))

        def close() -> None:
            self.detail_window = None
            self.detail_log = None
            window.destroy()

        close_button.configure(command=close)
        window.protocol("WM_DELETE_WINDOW", close)

    def set_step(self, number: int) -> None:
        self.step_text.set(
            f"Step {number} of {len(self.WORKFLOW_STEPS)}: {self.WORKFLOW_STEPS[number - 1]}"
        )
        self.progress.set(number - 1)

    def ui_log(self, text: str) -> None:
        self.root.after(0, self.append_log, text)

    def ui_step(self, number: int) -> None:
        self.root.after(0, self.set_step, number)

    def ui_call(self, callback):
        event = threading.Event()
        result = []

        def invoke() -> None:
            try:
                result.append(callback())
            finally:
                event.set()

        self.root.after(0, invoke)
        event.wait()
        return result[0] if result else None

    def set_busy(self, busy: bool) -> None:
        state = "disabled" if busy else "normal"
        self.install_button.configure(state=state)
        self.dependencies_button.configure(state=state if busy else (
            "normal" if patcher_ready() else "disabled"
        ))
        self.start_button.configure(state=state if busy else (
            "normal" if patcher_ready() else "disabled"
        ))

    def start(self) -> None:
        if not patcher_ready():
            messagebox.showerror(
                "Patcher missing", "Install the FFVII Rebirth Mesh Patcher before starting.",
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
                    "Invalid folders",
                    "The destination skin cannot be the source folder or inside it.",
                )
                return
        except OSError as exc:
            messagebox.showerror("Invalid folders", f"Could not compare folders: {exc}")
            return
        if target.exists() and not messagebox.askyesno(
            "Destination exists", f"{target} already exists. Replace it?", parent=self.root
        ):
            return
        self.set_busy(True)
        self.set_step(1)
        self.append_log(f"Starting: {source} -> {target}")
        service = WorkflowService(
            self.ui_log, self.ui_step,
            lambda parts: self.ui_call(lambda: self.show_variant_dialog(parts)),
            lambda title, text: self.ui_call(
                lambda: messagebox.askyesno(title, text, parent=self.root)
            ),
        )

        def worker() -> None:
            try:
                service.run(source, destination, name, target)
                self.root.after(0, self.progress.set, len(self.WORKFLOW_STEPS))
                self.ui_call(lambda: messagebox.showinfo(
                    "Done", "Dresscode conversion completed.", parent=self.root
                ))
            except Exception as exc:
                self.ui_call(lambda: messagebox.showerror(
                    "Process failed", str(exc), parent=self.root
                ))
            finally:
                self.root.after(0, lambda: self.set_busy(False))

        threading.Thread(target=worker, daemon=True).start()

    def install_patcher(self) -> None:
        if patcher_ready():
            messagebox.showinfo(
                "Patcher installed",
                f"FFVII Rebirth Mesh Patcher {PATCHER_RELEASE} is already installed.",
                parent=self.root,
            )
            return
        self.set_busy(True)

        def worker() -> None:
            try:
                PatcherService(self.ui_log).install()
            except (OSError, RuntimeError, urllib.error.URLError, zipfile.BadZipFile) as exc:
                self.ui_log(f"ERROR: patcher installation failed: {exc}")
                self.ui_call(lambda: messagebox.showerror(
                    "Patcher installation failed", str(exc), parent=self.root
                ))
            finally:
                self.root.after(0, self.refresh_tool_status)

        threading.Thread(target=worker, daemon=True).start()

    def install_dependencies(self) -> None:
        if not patcher_ready():
            messagebox.showerror(
                "Patcher missing", "Install the patcher before installing its dependencies.",
                parent=self.root,
            )
            return
        self.set_busy(True)

        def worker() -> None:
            try:
                code = PatcherService(self.ui_log).install_dependencies()
                if code:
                    self.ui_call(lambda: messagebox.showerror(
                        "Dependency installation failed",
                        f"pip exited with code {code}.", parent=self.root
                    ))
            except RuntimeError as exc:
                self.ui_log("ERROR: dependency installation failed: " + str(exc))
                self.ui_call(lambda: messagebox.showerror(
                    "Dependency installation failed", str(exc), parent=self.root
                ))
            finally:
                self.root.after(0, self.refresh_tool_status)

        threading.Thread(target=worker, daemon=True).start()

    def show_variant_dialog(self, parts):
        dialog = VariantDialog(self.root, parts)
        self.root.wait_window(dialog.window)
        return dialog.result or []


if __name__ == "__main__":
    app = tk.Tk()
    Frontend(app)
    app.mainloop()
