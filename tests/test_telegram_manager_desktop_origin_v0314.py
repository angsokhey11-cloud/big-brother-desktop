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

# Bank Review uses its own permission-gated native route, not Bank History.
dashboard=directory/"big-brother-dashboard"
index=(dashboard/"index.html").read_text(encoding="utf-8")
sections=(dashboard/"desktop-admin-work-sections-v1.js").read_text(encoding="utf-8")
adapter=(dashboard/"supabase-dashboard-adapter-core-v1.3.js").read_text(encoding="utf-8")
assert "navAdminBankReview" in index and "loadWorkspace(\\'admin-bank-review\\')" in index
assert '"admin-bank-review": "https://app.bigbrother.test/big-brother-notification-center/bank-transaction-bulk-review.html?dashboard=1' in index
assert "moduleName === 'admin-bank-review'" in index
assert "'admin-bank-review':{module:'admin_work',action:'view',adminOnly:true}" in adapter
assert 'window.loadWorkspace("admin-bank-history",false)' not in sections
assert (directory/"big-brother-notification-center/bank-transaction-bulk-review.html").is_file()
print("PASS Bank Review native route, local origin, and admin guard")
