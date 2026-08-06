"""Tkinter view for the patcher -> variants -> Dresscode workflow."""

from __future__ import annotations

import threading
import urllib.error
import zipfile
from pathlib import Path
from typing import Callable, List, Optional, Tuple
import tkinter as tk
from tkinter import filedialog, font as tkfont, messagebox, ttk

from frontend_services import (
    PATCHER_RELEASE,
    Part,
    PatcherService,
    WorkflowService,
    patcher_ready,
)
from frontend_translations import UI_TEXT
class VariantDialog:
    def __init__(self, parent: tk.Misc, parts: List[Part], language: str):
        self.language = language
        self.result = None
        self.window = tk.Toplevel(parent)
        self.window.title(self.t("variant_title"))
        self.window.geometry("760x560")
        self.window.configure(bg=Frontend.COLORS["background"])
        self.window.transient(parent)
        self.window.protocol("WM_DELETE_WINDOW", self.finish)
        self.vars = [(part, tk.BooleanVar(value=False)) for part in parts]
        self.primary_model = parts[0].model
        self.variant_name = tk.StringVar()
        ttk.Label(
            self.window, text=self.t("variant_intro")
        ).pack(anchor="w", padx=24, pady=(24, 4))
        ttk.Label(
            self.window,
            text=self.t("variant_rule", model=self.primary_model),
            wraplength=720,
        ).pack(anchor="w", padx=24, pady=(0, 16))
        frame = ttk.Frame(self.window, style="Card.TFrame", padding=12)
        frame.pack(fill="both", expand=True, padx=24)
        canvas = tk.Canvas(
            frame, highlightthickness=0, bg=Frontend.COLORS["card"],
            bd=0, relief="flat",
        )
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
        entry.pack(fill="x", padx=24, pady=16)
        ttk.Label(entry, text=self.t("variant_name")).pack(side="left")
        ttk.Entry(entry, textvariable=self.variant_name, width=35).pack(side="left", padx=6)
        ttk.Button(entry, text=self.t("add_variant"), style="Secondary.TButton",
                   command=self.add).pack(side="left")
        ttk.Button(entry, text=self.t("finish"), style="Accent.TButton",
                   command=self.finish).pack(side="right")
        self.listbox = tk.Listbox(
            self.window, height=4, bg=Frontend.COLORS["input"],
            fg=Frontend.COLORS["text"], selectbackground=Frontend.COLORS["accent"],
            selectforeground=Frontend.COLORS["text"], relief="flat", bd=0,
        )
        self.listbox.pack(fill="x", padx=24, pady=(0, 24))

    def add(self) -> None:
        name = self.variant_name.get().strip()
        if not name:
            messagebox.showerror(
                self.t("variant_name_title"), self.t("enter_variant_name"), parent=self.window
            )
            return
        if any(name.casefold() == old.casefold() for old, _ in self.result or []):
            messagebox.showerror(
                self.t("variant_name_title"), self.t("duplicate_variant"), parent=self.window
            )
            return
        selected = [part.number for part, variable in self.vars if variable.get()]
        if self.result is None:
            self.result = []
        self.result.append((name, selected))
        self.listbox.insert(
            "end",
            f"{name} ({self.t('omit')}: {', '.join(map(str, selected)) or self.t('none')})",
        )
        self.variant_name.set("")

    def t(self, key: str, **values: object) -> str:
        return UI_TEXT[self.language][key].format(**values)

    def finish(self) -> None:
        if self.result is None:
            self.result = []
        self.window.destroy()


class Frontend:
    COLORS = {
        "background": "#111318",
        "card": "#1a1d23",
        "card_alt": "#20242c",
        "input": "#252a33",
        "border": "#303641",
        "text": "#f3f4f6",
        "muted": "#a7adb8",
        "accent": "#4cc2ff",
        "accent_hover": "#75d0ff",
        "success": "#6ed7a0",
        "danger": "#ff8f8f",
    }

    def __init__(self, root: tk.Tk):
        self.root = root
        self.root.title("DressCoder")
        self.root.geometry("900x720")
        self.language = tk.StringVar(value="es")
        self.source = tk.StringVar()
        self.destination = tk.StringVar()
        self.skin_name = tk.StringVar()
        self.tool_status = tk.StringVar()
        self.step_text = tk.StringVar()
        self.progress = tk.DoubleVar(value=0)
        self.current_step = 0
        self.log_lines = []
        self.detail_window = None
        self.detail_log = None
        self.configure_theme()
        self.step_text.set(self.t("ready"))
        self.build_ui()
        self.refresh_tool_status()

    def t(self, key: str, **values: object) -> str:
        return UI_TEXT[self.language.get()][key].format(**values)

    @property
    def workflow_steps(self) -> Tuple[str, ...]:
        return UI_TEXT[self.language.get()]["steps"]

    def configure_theme(self) -> None:
        colors = self.COLORS
        self.root.configure(bg=colors["background"])
        self.fonts = {
            "body": tkfont.Font(self.root, family="Segoe UI", size=10),
            "title": tkfont.Font(
                self.root, family="Segoe UI Semibold", size=24, weight="bold"
            ),
            "subtitle": tkfont.Font(self.root, family="Segoe UI", size=10),
            "section": tkfont.Font(
                self.root, family="Segoe UI Semibold", size=11, weight="bold"
            ),
            "button": tkfont.Font(
                self.root, family="Segoe UI Semibold", size=10, weight="bold"
            ),
        }
        self.root.option_add("*Font", self.fonts["body"])
        style = ttk.Style(self.root)
        style.theme_use("clam")
        style.configure(".", background=colors["background"], foreground=colors["text"])
        style.configure("Card.TFrame", background=colors["card"])
        style.configure("Muted.TLabel", foreground=colors["muted"],
                        background=colors["background"])
        style.configure("Card.TLabel", foreground=colors["text"],
                        background=colors["card"])
        style.configure("CardMuted.TLabel", foreground=colors["muted"],
                        background=colors["card"])
        style.configure("Title.TLabel", font=self.fonts["title"],
                        foreground=colors["text"], background=colors["background"])
        style.configure("Subtitle.TLabel", font=self.fonts["subtitle"],
                        foreground=colors["muted"], background=colors["background"])
        style.configure("Section.TLabel", font=self.fonts["section"],
                        foreground=colors["text"], background=colors["card"])
        style.configure("TEntry", fieldbackground=colors["input"],
                        foreground=colors["text"], insertcolor=colors["text"],
                        bordercolor=colors["border"], lightcolor=colors["border"],
                        darkcolor=colors["border"], padding=8)
        style.map("TEntry", bordercolor=[("focus", colors["accent"])])
        style.configure("TButton", font=self.fonts["button"], padding=(14, 9),
                        background=colors["card_alt"], foreground=colors["text"],
                        bordercolor=colors["border"])
        style.map("TButton", background=[("active", colors["border"])])
        style.configure("Secondary.TButton", background=colors["card_alt"],
                        foreground=colors["text"])
        style.configure("Accent.TButton", background=colors["accent"],
                        foreground="#071016", borderwidth=0)
        style.map("Accent.TButton", background=[("active", colors["accent_hover"])])
        style.configure("Language.TButton", font=self.fonts["body"],
                        padding=(10, 5), background=colors["card_alt"],
                        foreground=colors["muted"], borderwidth=0)
        style.map("Language.TButton", background=[("active", colors["border"])])
        style.configure("SelectedLanguage.TButton", font=self.fonts["button"],
                        padding=(10, 5), background=colors["accent"],
                        foreground="#071016", borderwidth=0)
        style.configure("TCheckbutton", background=colors["card"],
                        foreground=colors["text"], indicatorbackground=colors["input"])
        style.map("TCheckbutton", background=[("active", colors["card"])])
        style.configure("Modern.Horizontal.TProgressbar", troughcolor=colors["input"],
                        background=colors["accent"], bordercolor=colors["input"],
                        lightcolor=colors["accent"], darkcolor=colors["accent"],
                        thickness=8)
        style.configure("TScrollbar", background=colors["card_alt"],
                        troughcolor=colors["card"], bordercolor=colors["card"])

    def build_ui(self) -> None:
        self.root.minsize(760, 600)
        header = ttk.Frame(self.root)
        header.pack(fill="x", padx=32, pady=(28, 12))
        ttk.Label(header, text="DressCoder", style="Title.TLabel").pack(anchor="w")
        ttk.Label(
            header, text=self.t("subtitle"),
            style="Subtitle.TLabel",
        ).pack(anchor="w", pady=(3, 0))

        form = ttk.Frame(self.root, style="Card.TFrame", padding=24)
        form.pack(fill="x", padx=32, pady=(8, 12))
        ttk.Label(form, text=self.t("project_setup"), style="Section.TLabel").grid(
            row=0, column=0, columnspan=3, sticky="w", pady=(0, 16)
        )
        self.add_folder_row(form, self.t("source"), self.source, self.choose_source, 1)
        self.add_folder_row(form, self.t("destination"), self.destination, self.choose_destination, 2)
        ttk.Label(form, text=self.t("skin_name"), style="Card.TLabel").grid(
            row=3, column=0, sticky="w", pady=7
        )
        ttk.Entry(form, textvariable=self.skin_name).grid(
            row=3, column=1, columnspan=2, sticky="ew", padx=(16, 0)
        )
        form.columnconfigure(1, weight=1)
        tools = ttk.Frame(form, style="Card.TFrame")
        tools.grid(row=4, column=0, columnspan=3, sticky="ew", pady=(20, 0))
        ttk.Label(tools, textvariable=self.tool_status, style="CardMuted.TLabel").pack(side="left")
        self.install_button = ttk.Button(
            tools, text=self.t("install_patcher"), style="Secondary.TButton",
            command=self.install_patcher,
        )
        self.install_button.pack(side="right", padx=(6, 0))
        self.dependencies_button = ttk.Button(
            tools, text=self.t("install_dependencies"), style="Secondary.TButton",
            command=self.install_dependencies,
        )
        self.dependencies_button.pack(side="right")
        self.start_button = ttk.Button(
            form, text=self.t("start_conversion"), style="Accent.TButton", command=self.start,
        )
        self.start_button.grid(row=5, column=0, columnspan=3, sticky="e", pady=(24, 0))
        ttk.Label(
            self.root, text=self.t("workflow_description"),
            style="Muted.TLabel",
        ).pack(anchor="w", padx=32, pady=(0, 10))
        status = ttk.Frame(self.root, style="Card.TFrame", padding=24)
        status.pack(fill="x", padx=32, pady=(0, 24))
        ttk.Label(status, text=self.t("workflow_progress"), style="Section.TLabel").pack(anchor="w")
        ttk.Label(status, textvariable=self.step_text, style="CardMuted.TLabel").pack(
            anchor="w", pady=(8, 4)
        )
        ttk.Progressbar(
            status, variable=self.progress, maximum=len(self.workflow_steps),
            mode="determinate", style="Modern.Horizontal.TProgressbar",
        ).pack(fill="x", pady=(4, 14))
        ttk.Button(
            status, text=self.t("view_logs"), style="Secondary.TButton",
            command=self.show_detailed_view,
        ).pack(anchor="e")
        language_bar = ttk.Frame(
            self.root, style="Card.TFrame", padding=3,
        )
        language_bar.pack(anchor="e", padx=32, pady=(0, 20))
        for code, label in (("es", "ES"), ("en", "EN")):
            ttk.Button(
                language_bar, text=label,
                style=(
                    "SelectedLanguage.TButton"
                    if self.language.get() == code else "Language.TButton"
                ),
                command=lambda selected=code: self.change_language(selected),
            ).pack(side="left")

    def add_folder_row(self, parent: ttk.Frame, label: str, variable: tk.StringVar,
                       command: Callable[[], None], row: int) -> None:
        ttk.Label(parent, text=label, style="Card.TLabel").grid(
            row=row, column=0, sticky="w", pady=7
        )
        ttk.Entry(parent, textvariable=variable).grid(
            row=row, column=1, sticky="ew", padx=(16, 8)
        )
        ttk.Button(parent, text=self.t("browse"), style="Secondary.TButton",
                   command=command).grid(row=row, column=2)

    def choose_source(self) -> None:
        path = filedialog.askdirectory(title=self.t("select_source"))
        if path:
            self.source.set(path)
            if not self.skin_name.get().strip():
                self.skin_name.set(Path(path).name)

    def choose_destination(self) -> None:
        path = filedialog.askdirectory(title=self.t("select_destination"))
        if path:
            self.destination.set(path)

    def refresh_tool_status(self) -> None:
        installed = patcher_ready()
        self.tool_status.set(
            f"Patcher {PATCHER_RELEASE}: "
            + (self.t("installed") if installed else self.t("not_installed"))
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
        window.title(self.t("detailed_logs"))
        window.geometry("860x540")
        window.configure(bg=self.COLORS["background"])
        window.transient(self.root)
        frame = ttk.Frame(window, style="Card.TFrame", padding=16)
        frame.pack(fill="both", expand=True)
        log = tk.Text(
            frame, wrap="word", state="disabled",
            bg=self.COLORS["input"], fg=self.COLORS["text"],
            insertbackground=self.COLORS["text"], selectbackground=self.COLORS["accent"],
            relief="flat", bd=0, padx=12, pady=12,
        )
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
        close_button = ttk.Button(
            window, text=self.t("close"), style="Secondary.TButton",
        )
        close_button.pack(anchor="e", padx=16, pady=(0, 16))

        def close() -> None:
            self.detail_window = None
            self.detail_log = None
            window.destroy()

        close_button.configure(command=close)
        window.protocol("WM_DELETE_WINDOW", close)

    def set_step(self, number: int) -> None:
        self.current_step = number
        self.step_text.set(
            self.t(
                "step", number=number, total=len(self.workflow_steps),
                name=self.workflow_steps[number - 1],
            )
        )
        self.progress.set(number - 1)

    def change_language(self, selected: str) -> None:
        if selected == self.language.get():
            return
        self.language.set(selected)
        if self.detail_window is not None and self.detail_window.winfo_exists():
            self.detail_window.destroy()
            self.detail_window = None
            self.detail_log = None
        for child in self.root.winfo_children():
            child.destroy()
        self.build_ui()
        if self.current_step:
            self.set_step(self.current_step)
        else:
            self.step_text.set(self.t("ready"))
        self.refresh_tool_status()

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
                self.t("patcher_missing"), self.t("install_before_start"),
                parent=self.root,
            )
            return
        source = Path(self.source.get().strip())
        destination = Path(self.destination.get().strip())
        name = self.skin_name.get().strip() or source.name
        if not source.is_dir() or not destination.is_dir() or not name:
            messagebox.showerror(self.t("input_required"), self.t("choose_folders"))
            return
        if Path(name).name != name or name in (".", ".."):
            messagebox.showerror(self.t("invalid_name"), self.t("single_folder_name"))
            return
        target = destination / name
        try:
            if target.resolve().is_relative_to(source.resolve()):
                messagebox.showerror(
                    self.t("invalid_folders"), self.t("destination_inside_source"),
                )
                return
        except OSError as exc:
            messagebox.showerror(
                self.t("invalid_folders"), self.t("compare_folders", error=exc)
            )
            return
        if target.exists() and not messagebox.askyesno(
            self.t("destination_exists"),
            self.t("replace_destination", target=target), parent=self.root
        ):
            return
        self.set_busy(True)
        self.set_step(1)
        self.append_log(self.t("starting", source=source, target=target))
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
                self.root.after(0, self.progress.set, len(self.workflow_steps))
                self.ui_call(lambda: messagebox.showinfo(
                    self.t("done"), self.t("conversion_completed"), parent=self.root
                ))
            except Exception as exc:
                self.ui_call(lambda: messagebox.showerror(
                    self.t("process_failed"), str(exc), parent=self.root
                ))
            finally:
                self.root.after(0, lambda: self.set_busy(False))

        threading.Thread(target=worker, daemon=True).start()

    def install_patcher(self) -> None:
        if patcher_ready():
            messagebox.showinfo(
                self.t("patcher_installed"),
                self.t("already_installed", release=PATCHER_RELEASE),
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
                    self.t("install_failed"), str(exc), parent=self.root
                ))
            finally:
                self.root.after(0, self.refresh_tool_status)

        threading.Thread(target=worker, daemon=True).start()

    def install_dependencies(self) -> None:
        if not patcher_ready():
            messagebox.showerror(
            self.t("patcher_missing"), self.t("dependencies_missing"),
                parent=self.root,
            )
            return
        self.set_busy(True)

        def worker() -> None:
            try:
                code = PatcherService(self.ui_log).install_dependencies()
                if code:
                    self.ui_call(lambda: messagebox.showerror(
                        self.t("dependencies_failed"),
                        self.t("pip_failed", code=code), parent=self.root
                    ))
            except RuntimeError as exc:
                self.ui_log("ERROR: dependency installation failed: " + str(exc))
                self.ui_call(lambda: messagebox.showerror(
                    self.t("dependencies_failed"), str(exc), parent=self.root
                ))
            finally:
                self.root.after(0, self.refresh_tool_status)

        threading.Thread(target=worker, daemon=True).start()

    def show_variant_dialog(self, parts):
        dialog = VariantDialog(self.root, parts, self.language.get())
        self.root.wait_window(dialog.window)
        return dialog.result or []


if __name__ == "__main__":
    app = tk.Tk()
    Frontend(app)
    app.mainloop()
