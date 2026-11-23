#!/usr/bin/env python3
"""Telegram Manager Desktop host-only URL fix regression (no DB/Telegram usage)."""
from pathlib import Path
import importlib.util
root=Path(__file__).resolve().parents[1]
bundle=root/"tools/bundle_web.py"
spec=importlib.util.spec_from_file_location("desktop_bundle", bundle)
mod=importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)

directory=root/"build/web"
manager=directory/"big-brother-notification-center/app.js"
assert manager.is_file(), "Bundled Telegram Manager missing"
code=manager.read_text(encoding="utf-8")
checks={
    "Local approved-tool origin": "const TOOL_ORIGIN = 'https://app.bigbrother.test';" in code,
    "No Cloud Pages tool origin remains": "const TOOL_ORIGIN = 'https://angsokhey11-cloud.github.io';" not in code,
    "Existing same-origin tool allow-list preserved":
        "locationURL.origin===TOOL_ORIGIN" in code
        and "toolKeyFromPath(locationURL.pathname)" in code,
    "Customer Bank Directory points to bundled local file":
        "'customer-bank-directory': {title:'Customer Bank Directory',path:'/big-brother-admin-work/customer-bank-directory.html'}" in code
        and (directory/"big-brother-admin-work/customer-bank-directory.html").is_file(),
    "Other Telegram tools are bundled":
        (directory/"big-brother-notification-center/invoice-photo-organizer.html").is_file()
        and (directory/"big-brother-admin-work/bank-assistant-setup.html").is_file(),
    "Dark tool theme still bundled":
        (directory/"big-brother-notification-center/telegram-workspace-tools-v1.css").is_file(),
    "Non-Telegram source is never rewritten":
        mod.patch_desktop_telegram_tool_origin(
            "big-brother-admin-work","app.js",
            "const TOOL_ORIGIN = 'https://angsokhey11-cloud.github.io';"
        )=="const TOOL_ORIGIN = 'https://angsokhey11-cloud.github.io';",
}
for label,ok in checks.items():
    assert ok,"FAILED "+label
    print("PASS",label)
# A source marker change must fail closed rather than silently breaking tools.
try:
    mod.patch_desktop_telegram_tool_origin(
        "big-brother-notification-center","app.js","const TOOL_ORIGIN = 'https://somewhere.invalid';")
except RuntimeError:
    print("PASS Origin build guard rejects unrecognized source")
else:
    raise AssertionError("Origin build guard missing")
print("PASS Desktop Telegram Manager embedded routing regression")
