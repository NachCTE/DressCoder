"""Tkinter view for the patcher -> variants -> Dresscode workflow."""

from __future__ import annotations

import json
import os
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
        header = ttk.Frame(self.window, style="Card.TFrame", padding=(24, 20))
        header.pack(fill="x", padx=24, pady=(24, 12))
        ttk.Label(
            header, text=self.t("variant_intro"), style="Section.TLabel"
        ).pack(anchor="w")
        ttk.Label(
            header,
            text=self.t("variant_rule", model=self.primary_model),
            style="CardMuted.TLabel",
            wraplength=720,
        ).pack(anchor="w", pady=(8, 0))
        frame = ttk.Frame(self.window, padding=12)
        frame.pack(fill="both", expand=True, padx=24, pady=(0, 12))
        canvas = tk.Canvas(
            frame, highlightthickness=0, bg=Frontend.COLORS["background"],
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
            checkbutton = ttk.Checkbutton(
                inner, text=f"{part.number}: {part.name}  [{part.model}]",
                variable=variable,
                style="Variant.TCheckbutton",
                state="normal" if part.model == self.primary_model else "disabled",
            )
            checkbutton.pack(anchor="w", fill="x", pady=3)
            checkbutton.bind("<MouseWheel>", lambda event: self._scroll_parts(canvas, event))
        inner.bind("<MouseWheel>", lambda event: self._scroll_parts(canvas, event))
        canvas.bind("<MouseWheel>", lambda event: self._scroll_parts(canvas, event))
        self.window.geometry("760x620")
        entry = ttk.Frame(self.window, style="Card.TFrame", padding=(16, 12))
        entry.pack(fill="x", padx=24, pady=(0, 12))
        ttk.Label(
            entry, text=self.t("variant_name"), style="Card.TLabel"
        ).pack(side="left")
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
        self.listbox.pack(fill="x", padx=24, pady=(0, 24), ipady=4)

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

    @staticmethod
    def _scroll_parts(canvas: tk.Canvas, event: tk.Event) -> str:
        canvas.yview_scroll(int(-1 * (event.delta / 120)), "units")
        return "break"

    def finish(self) -> None:
        if self.result is None:
            self.result = []
        self.window.destroy()


class Frontend:
    CACHE_PATH = (
        Path.home() / "AppData" / "Local" / "DressCoder" / "settings.json"
    )
    # Windows 11 dark-mode neutral palette (Mica-inspired surfaces + system accent).
    COLORS = {
        "background": "#202020",
        "card": "#2c2c2c",
        "card_alt": "#333333",
        "input": "#2b2b2b",
        "border": "#3d3d3d",
        "divider": "#3a3a3a",
        "text": "#ffffff",
        "muted": "#c5c5c5",
        "subtle": "#9a9a9a",
        "accent": "#60cdff",
        "accent_hover": "#7fd4ff",
        "accent_pressed": "#4cc2ff",
        "success": "#6ccb5f",
        "danger": "#ff99a4",
    }

    def __init__(self, root: tk.Tk):
        self.root = root
        self.root.title("DressCoder")
        self.root.geometry("900x1000")
        self.language = tk.StringVar(value="es")
        self.source = tk.StringVar()
        self.destination = tk.StringVar()
        self.skin_name = tk.StringVar()
        self.author = tk.StringVar()
        self.description = tk.StringVar()
        self.photo = tk.StringVar()
        self.tool_status = tk.StringVar()
        self.step_text = tk.StringVar()
        self.progress = tk.DoubleVar(value=0)
        self.current_step = 0
        self.log_lines = []
        self.detail_window = None
        self.detail_log = None
        self.status_dot = None
        self.load_cache()
        self.configure_theme()
        self.step_text.set(self.t("ready"))
        self.build_ui()
        self.refresh_tool_status()
        self.root.protocol("WM_DELETE_WINDOW", self.close)
        self.root.after(10, lambda: self._use_dark_titlebar(self.root))

    def load_cache(self) -> None:
        if not self.CACHE_PATH.is_file():
            return
        try:
            with self.CACHE_PATH.open("r", encoding="utf-8") as stream:
                data = json.load(stream)
        except (OSError, json.JSONDecodeError):
            return
        if not isinstance(data, dict):
            return
        values = {
            "source": self.source,
            "destination": self.destination,
            "skin_name": self.skin_name,
            "author": self.author,
            "description": self.description,
            "photo": self.photo,
        }
        for key, variable in values.items():
            value = data.get(key)
            if isinstance(value, str):
                variable.set(value)
        language = data.get("language")
        if language in ("es", "en"):
            self.language.set(language)

    def save_cache(self) -> None:
        data = {
            "language": self.language.get(),
            "source": self.source.get(),
            "destination": self.destination.get(),
            "skin_name": self.skin_name.get(),
            "author": self.author.get(),
            "description": self.description.get(),
            "photo": self.photo.get(),
        }
        self.CACHE_PATH.parent.mkdir(parents=True, exist_ok=True)
        temporary = self.CACHE_PATH.with_suffix(".tmp")
        with temporary.open("w", encoding="utf-8") as stream:
            json.dump(data, stream, indent=2, ensure_ascii=False)
            stream.write("\n")
        temporary.replace(self.CACHE_PATH)

    def close(self) -> None:
        try:
            self.save_cache()
        except OSError as exc:
            self.append_log("WARNING: could not save settings: " + str(exc))
        self.root.destroy()

    @staticmethod
    def _use_dark_titlebar(window: tk.Misc) -> None:
        """Enable the native Windows 11 dark title bar for a top-level window."""
        try:
            import ctypes
            window.update_idletasks()
            handle = ctypes.windll.user32.GetParent(window.winfo_id())
            value = ctypes.c_int(1)
            for attribute in (20, 19):  # DWMWA_USE_IMMERSIVE_DARK_MODE (20 modern, 19 legacy)
                ctypes.windll.dwmapi.DwmSetWindowAttribute(
                    handle, attribute, ctypes.byref(value), ctypes.sizeof(value)
                )
        except (AttributeError, OSError):
            pass  # Non-Windows platform or API unavailable; ignore silently.

    def t(self, key: str, **values: object) -> str:
        return UI_TEXT[self.language.get()][key].format(**values)

    @property
    def workflow_steps(self) -> Tuple[str, ...]:
        return UI_TEXT[self.language.get()]["steps"]

    def configure_theme(self) -> None:
        colors = self.COLORS
        self.root.configure(bg=colors["background"])
        available = set(tkfont.families(self.root))
        def pick(*candidates: str) -> str:
            for name in candidates:
                if name in available:
                    return name
            return "Segoe UI"
        display_family = pick("Segoe UI Variable Display", "Segoe UI Semibold", "Segoe UI")
        text_family = pick("Segoe UI Variable Text", "Segoe UI")
        semibold_family = pick(
            "Segoe UI Variable Text Semibold", "Segoe UI Semibold", "Segoe UI"
        )
        self.fonts = {
            "body": tkfont.Font(self.root, family=text_family, size=10),
            "title": tkfont.Font(self.root, family=display_family, size=26),
            "subtitle": tkfont.Font(self.root, family=text_family, size=10),
            "section": tkfont.Font(self.root, family=semibold_family, size=11),
            "button": tkfont.Font(self.root, family=semibold_family, size=10),
            "small": tkfont.Font(self.root, family=text_family, size=9),
        }
        self.root.option_add("*Font", self.fonts["body"])
        style = ttk.Style(self.root)
        style.theme_use("clam")
        style.configure(".", background=colors["background"], foreground=colors["text"])
        style.configure("Card.TFrame", background=colors["card"])
        style.configure("Muted.TLabel", foreground=colors["muted"],
                        background=colors["background"])
        style.configure("Card.TLabel", foreground=colors["text"],
                        background=colors["card"], font=self.fonts["body"])
        style.configure("CardMuted.TLabel", foreground=colors["muted"],
                        background=colors["card"])
        style.configure("CardSubtle.TLabel", foreground=colors["subtle"],
                        background=colors["card"], font=self.fonts["small"])
        style.configure("Title.TLabel", font=self.fonts["title"],
                        foreground=colors["text"], background=colors["background"])
        style.configure("Subtitle.TLabel", font=self.fonts["subtitle"],
                        foreground=colors["muted"], background=colors["background"])
        style.configure("Section.TLabel", font=self.fonts["section"],
                        foreground=colors["text"], background=colors["card"])
        style.configure("Divider.TSeparator", background=colors["divider"])
        style.configure("TEntry", fieldbackground=colors["input"],
                        foreground=colors["text"], insertcolor=colors["text"],
                        bordercolor=colors["border"], lightcolor=colors["border"],
                        darkcolor=colors["border"], padding=9, relief="flat")
        style.map("TEntry", bordercolor=[("focus", colors["accent"])],
                  lightcolor=[("focus", colors["accent"])],
                  darkcolor=[("focus", colors["accent"])])
        style.configure("TButton", font=self.fonts["button"], padding=(16, 9),
                        background=colors["card_alt"], foreground=colors["text"],
                        bordercolor=colors["border"], relief="flat", borderwidth=1)
        style.map("TButton", background=[("active", colors["border"]), ("disabled", colors["card"])],
                  foreground=[("disabled", colors["subtle"])])
        style.configure("Secondary.TButton", background=colors["card_alt"],
                        foreground=colors["text"])
        style.map("Secondary.TButton", background=[("active", colors["border"])])
        style.configure("Accent.TButton", background=colors["accent"],
                        foreground="#0a0a0a", borderwidth=0)
        style.map("Accent.TButton",
                  background=[("disabled", colors["card_alt"]),
                              ("pressed", colors["accent_pressed"]),
                              ("active", colors["accent_hover"])],
                  foreground=[("disabled", colors["subtle"])])
        style.configure("Language.TButton", font=self.fonts["small"],
                        padding=(12, 6), background=colors["card"],
                        foreground=colors["muted"], borderwidth=0, relief="flat")
        style.map("Language.TButton", background=[("active", colors["border"])])
        style.configure("SelectedLanguage.TButton", font=self.fonts["button"],
                        padding=(12, 6), background=colors["accent"],
                        foreground="#0a0a0a", borderwidth=0, relief="flat")
        style.configure("Pill.TFrame", background=colors["card"])
        style.configure("TCheckbutton", background=colors["card"],
                        foreground=colors["text"], indicatorbackground=colors["input"],
                        indicatorcolor=colors["input"])
        style.map("TCheckbutton", background=[("active", colors["card"])],
                  indicatorbackground=[("selected", colors["accent"])])
        style.configure(
            "Variant.TCheckbutton",
            background=colors["background"],
            foreground=colors["text"],
            indicatorbackground=colors["input"],
            indicatorcolor=colors["input"],
            padding=(6, 3),
        )
        style.map(
            "Variant.TCheckbutton",
            background=[("active", colors["background"])],
            foreground=[("disabled", colors["subtle"])],
            indicatorbackground=[
                ("disabled", colors["card_alt"]),
                ("selected", colors["accent"]),
            ],
        )
        style.configure("Modern.Horizontal.TProgressbar", troughcolor="#171717",
                        background=colors["accent"], bordercolor=colors["input"],
                        lightcolor=colors["accent"], darkcolor=colors["accent"],
                        thickness=6)
        style.configure("TScrollbar", background=colors["card_alt"],
                        troughcolor=colors["card"], bordercolor=colors["card"],
                        arrowcolor=colors["muted"])

    def build_ui(self) -> None:
        self.root.minsize(780, 760)
        self.root.unbind_all("<MouseWheel>")  # avoid stacking handlers across rebuilds
        colors = self.COLORS

        # --- Footer: language switcher (packed first so it always stays visible) --
        footer = ttk.Frame(self.root)
        footer.pack(fill="x", side="bottom", padx=36, pady=(8, 20))
        ttk.Separator(footer, style="Divider.TSeparator").pack(fill="x", pady=(0, 14))
        language_bar = ttk.Frame(footer, style="Pill.TFrame", padding=3)
        language_bar.pack(anchor="e")
        for code, label in (("es", "ES"), ("en", "EN")):
            ttk.Button(
                language_bar, text=label,
                style=(
                    "SelectedLanguage.TButton"
                    if self.language.get() == code else "Language.TButton"
                ),
                command=lambda selected=code: self.change_language(selected),
            ).pack(side="left")

        # --- Scrollable content area -------------------------------------------
        # Wrapped in a canvas so the window can shrink without hiding the footer;
        # content scrolls instead of being clipped.
        outer = ttk.Frame(self.root)
        outer.pack(fill="both", expand=True, side="top")
        canvas = tk.Canvas(outer, bg=colors["background"], highlightthickness=0, bd=0)
        vscroll = ttk.Scrollbar(outer, orient="vertical", command=canvas.yview)
        content = ttk.Frame(canvas)
        content_window = canvas.create_window((0, 0), window=content, anchor="nw")

        def _on_content_configure(_event=None) -> None:
            canvas.configure(scrollregion=canvas.bbox("all"))

        def _on_canvas_configure(event) -> None:
            canvas.itemconfigure(content_window, width=event.width)

        content.bind("<Configure>", _on_content_configure)
        canvas.bind("<Configure>", _on_canvas_configure)
        canvas.configure(yscrollcommand=vscroll.set)

        def _on_mousewheel(event) -> None:
            if content.winfo_reqheight() > canvas.winfo_height():
                canvas.yview_scroll(int(-1 * (event.delta / 120)), "units")

        def _update_scrollbar(_event=None) -> None:
            canvas.update_idletasks()
            if content.winfo_reqheight() > canvas.winfo_height():
                if not vscroll.winfo_ismapped():
                    vscroll.pack(side="right", fill="y")
            elif vscroll.winfo_ismapped():
                vscroll.pack_forget()
            canvas.configure(scrollregion=canvas.bbox("all"))

        canvas.bind_all("<MouseWheel>", _on_mousewheel)
        content.bind("<Configure>", _update_scrollbar, add="+")
        canvas.bind("<Configure>", _update_scrollbar, add="+")
        canvas.pack(side="left", fill="both", expand=True)
        self.root.after_idle(_update_scrollbar)

        header = ttk.Frame(content)
        header.pack(fill="x", padx=36, pady=(32, 16))
        ttk.Button(
            header, text=self.t("help"), style="Secondary.TButton",
            command=self.show_help,
        ).pack(side="right", anchor="n")
        ttk.Label(header, text="DressCoder", style="Title.TLabel").pack(anchor="w")
        ttk.Label(
            header, text=self.t("subtitle"), style="Subtitle.TLabel",
        ).pack(anchor="w", pady=(4, 0))

        # --- Project setup card -------------------------------------------------
        form = ttk.Frame(content, style="Card.TFrame", padding=(24, 20))
        form.pack(fill="x", padx=36, pady=(0, 16))
        ttk.Label(form, text=self.t("project_setup"), style="Section.TLabel").grid(
            row=0, column=0, columnspan=3, sticky="w", pady=(0, 14)
        )
        self.add_folder_row(form, self.t("source"), self.source, self.choose_source, 1)
        ttk.Separator(form, style="Divider.TSeparator").grid(
            row=2, column=0, columnspan=3, sticky="ew", pady=10
        )
        self.add_folder_row(form, self.t("destination"), self.destination, self.choose_destination, 3)
        ttk.Separator(form, style="Divider.TSeparator").grid(
            row=4, column=0, columnspan=3, sticky="ew", pady=10
        )
        ttk.Label(form, text=self.t("skin_name"), style="Card.TLabel").grid(
            row=5, column=0, sticky="w", pady=7
        )
        ttk.Entry(form, textvariable=self.skin_name).grid(
            row=5, column=1, columnspan=2, sticky="ew", padx=(16, 0)
        )
        ttk.Separator(form, style="Divider.TSeparator").grid(
            row=6, column=0, columnspan=3, sticky="ew", pady=10
        )
        ttk.Label(form, text=self.t("author"), style="Card.TLabel").grid(
            row=7, column=0, sticky="w", pady=(11, 6)
        )
        ttk.Entry(form, textvariable=self.author).grid(
            row=7, column=1, columnspan=2, sticky="ew", padx=(16, 0), pady=(11, 6)
        )
        ttk.Label(form, text=self.t("description"), style="Card.TLabel").grid(
            row=8, column=0, sticky="w", pady=(6, 6)
        )
        ttk.Entry(form, textvariable=self.description).grid(
            row=8, column=1, columnspan=2, sticky="ew", padx=(16, 0), pady=(6, 6)
        )
        ttk.Label(form, text=self.t("photo"), style="Card.TLabel").grid(
            row=9, column=0, sticky="w", pady=(6, 11)
        )
        ttk.Entry(form, textvariable=self.photo, state="readonly").grid(
            row=9, column=1, sticky="ew", padx=(16, 8), pady=(6, 11)
        )
        photo_actions = ttk.Frame(form, style="Card.TFrame")
        photo_actions.grid(row=9, column=2, sticky="e", pady=(6, 11))
        ttk.Button(
            photo_actions, text=self.t("photo_browse"), style="Secondary.TButton",
            command=self.choose_photo,
        ).pack(side="left")
        ttk.Button(
            photo_actions, text=self.t("remove_photo"), style="Secondary.TButton",
            command=self.clear_photo,
        ).pack(side="left", padx=(8, 0))
        form.columnconfigure(1, weight=1)

        # --- Patcher status card -------------------------------------------------
        tools = ttk.Frame(content, style="Card.TFrame", padding=(24, 16))
        tools.pack(fill="x", padx=36, pady=(0, 16))
        status_row = ttk.Frame(tools, style="Card.TFrame")
        status_row.pack(fill="x")
        self.status_dot = tk.Canvas(
            status_row, width=10, height=10, bg=colors["card"],
            highlightthickness=0, bd=0,
        )
        self.status_dot.pack(side="left", padx=(0, 10))
        ttk.Label(status_row, textvariable=self.tool_status, style="CardMuted.TLabel").pack(
            side="left"
        )
        self.install_button = ttk.Button(
            status_row, text=self.t("install_patcher"), style="Secondary.TButton",
            command=self.install_patcher,
        )
        self.install_button.pack(side="right", padx=(8, 0))
        self.dependencies_button = ttk.Button(
            status_row, text=self.t("install_dependencies"), style="Secondary.TButton",
            command=self.install_dependencies,
        )
        self.dependencies_button.pack(side="right")

        self.start_button = ttk.Button(
            content, text=self.t("start_conversion"), style="Accent.TButton", command=self.start,
        )
        self.start_button.pack(anchor="e", padx=36, pady=(0, 6))
        ttk.Label(
            content, text=self.t("workflow_description"), style="Muted.TLabel",
        ).pack(anchor="w", padx=36, pady=(4, 12))

        # --- Progress card --------------------------------------------------------
        status = ttk.Frame(content, style="Card.TFrame", padding=(24, 20))
        status.pack(fill="x", padx=36, pady=(0, 16))
        ttk.Label(status, text=self.t("workflow_progress"), style="Section.TLabel").pack(anchor="w")
        ttk.Label(status, textvariable=self.step_text, style="CardMuted.TLabel").pack(
            anchor="w", pady=(10, 6)
        )
        ttk.Progressbar(
            status, variable=self.progress, maximum=len(self.workflow_steps),
            mode="determinate", style="Modern.Horizontal.TProgressbar",
        ).pack(fill="x", pady=(2, 14))
        ttk.Button(
            status, text=self.t("view_logs"), style="Secondary.TButton",
            command=self.show_detailed_view,
        ).pack(anchor="e", pady=(0, 8))

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

    def choose_photo(self) -> None:
        path = filedialog.askopenfilename(
            title=self.t("select_photo"),
            filetypes=[
                ("Image files", "*.png *.jpg *.jpeg"),
                ("PNG files", "*.png"),
                ("JPEG files", "*.jpg *.jpeg"),
                ("All files", "*.*"),
            ],
        )
        if path:
            self.photo.set(path)

    def clear_photo(self) -> None:
        self.photo.set("")

    def show_help(self) -> None:
        messagebox.showinfo(
            self.t("help_title"),
            self.t("help_message"),
            parent=self.root,
        )

    def refresh_tool_status(self) -> None:
        installed = patcher_ready()
        self.tool_status.set(
            f"Patcher {PATCHER_RELEASE}: "
            + (self.t("installed") if installed else self.t("not_installed"))
        )
        if self.status_dot is not None and self.status_dot.winfo_exists():
            self.status_dot.delete("all")
            color = self.COLORS["success"] if installed else self.COLORS["danger"]
            self.status_dot.create_oval(1, 1, 9, 9, fill=color, outline="")
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
        author = self.author.get().strip()
        description = self.description.get().strip()
        photo_text = self.photo.get().strip()
        photo = Path(photo_text) if photo_text else None
        if not source.is_dir() or not destination.is_dir() or not name:
            messagebox.showerror(self.t("input_required"), self.t("choose_folders"))
            return
        if photo is not None and not photo.is_file():
            messagebox.showerror(self.t("input_required"), self.t("select_photo"))
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
                service.run(
                    source, destination, name, target,
                    author, description, photo,
                )
                self.root.after(0, self.progress.set, len(self.workflow_steps))
                self.ui_call(lambda: messagebox.showinfo(
                    self.t("done"), self.t("conversion_completed"), parent=self.root
                ))
                if self.ui_call(lambda: messagebox.askyesno(
                    self.t("open_folder_title"),
                    self.t("open_folder_question"),
                    parent=self.root,
                )):
                    try:
                        os.startfile(str(target / "dresscode"))
                    except OSError as exc:
                        self.ui_call(lambda: messagebox.showerror(
                            self.t("open_folder_failed"),
                            str(exc),
                            parent=self.root,
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
