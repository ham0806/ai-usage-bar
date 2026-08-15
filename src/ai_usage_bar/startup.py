from __future__ import annotations

import sys
from pathlib import Path

STARTUP_NAME = "AI Usage Bar.lnk"


def _pythonw_path() -> Path:
    exe = Path(sys.executable)
    if exe.name.lower() == "python.exe":
        pythonw = exe.with_name("pythonw.exe")
        if pythonw.exists():
            return pythonw
    return exe


def startup_shortcut_path() -> Path:
    appdata = Path.home() / "AppData" / "Roaming"
    return appdata / "Microsoft" / "Windows" / "Start Menu" / "Programs" / "Startup" / STARTUP_NAME


def is_startup_enabled() -> bool:
    return startup_shortcut_path().exists()


def set_startup_enabled(enabled: bool) -> None:
    path = startup_shortcut_path()
    if not enabled:
        if path.exists():
            path.unlink()
        return

    import win32com.client

    pythonw = _pythonw_path()
    shell = win32com.client.Dispatch("WScript.Shell")
    shortcut = shell.CreateShortCut(str(path))
    shortcut.Targetpath = str(pythonw)
    shortcut.Arguments = "-m ai_usage_bar"
    shortcut.WorkingDirectory = str(Path(sys.executable).resolve().parent)
    shortcut.WindowStyle = 7
    shortcut.Description = "AI Usage Bar"
    shortcut.save()
