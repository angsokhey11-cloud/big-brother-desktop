"""Regression guard for installed Desktop v0.3.11 (no DB / browser needed)."""
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
WEB = ROOT / "build" / "web"

invoice_dir = WEB / "big-brother-invoice-generator"
dashboard_dir = WEB / "big-brother-dashboard"
adapter = (invoice_dir / "supabase-adapter.js").read_text(encoding="utf-8")
invoice_loader = (invoice_dir / "index.html").read_text(encoding="utf-8")
mobile_loader = (invoice_dir / "mobile.html").read_text(encoding="utf-8")
side = (dashboard_dir / "dashboard-sidebar-toggle-v1.js").read_text(encoding="utf-8")
dash = (dashboard_dir / "index.html").read_text(encoding="utf-8")

def check(condition, description):
    assert condition, description
    print("PASS:", description)

check(
    "restoredBatchSelect.dispatchEvent(new Event('change', { bubbles: true }))" in adapter,
    "Batch restoration triggers the existing invoice-number RPC change handler",
)
check(
    "restoredBatch?.batchId === restoredBatchId" in adapter,
    "Only a loaded valid Batch triggers automatic Batch invoice-number lookup",
)
check(
    "await window.loadNextInvoiceNumber();" in adapter,
    "Direct Sale still loads the normal invoice-number sequence",
)
check(
    "if(request!==seqRequest||seqBatch.value!==batch)return;" in adapter,
    "Stale Batch invoice-number RPC responses cannot replace a newer selection",
)
check(
    "supabase-adapter.js?v=20261123-batch-restore-invoice-no1" in invoice_loader,
    "Desktop generator uses revised Supabase adapter",
)
check(
    "supabase-adapter.js?v=20261123-batch-restore-invoice-no1" in mobile_loader,
    "Mobile PWA generator uses revised Supabase adapter",
)
check(
    'body.bb-sidebar-collapsed #moduleWorkspace:has(#moduleFrame[data-module="invoice"])' in side,
    "Sidebar collapse docks only the Invoice Generator iframe",
)
check(
    "width:calc(100% - 430px)" in side and "@media(min-width:1440px)" in side,
    "Wide desktop screens reserve 430px right-side browser workspace",
)
check(
    "dashboard-sidebar-toggle-v1.js?v=20261123-invoice-sidecar1" in dash,
    "Installed Dashboard reloads the revised sidebar stylesheet",
)
check(
    "big-brother-invoice-generator/index.html?embed=1&v=20261123-batch-restore-sidecar1" in dash,
    "Installed Dashboard loads the corrected Invoice Generator version",
)
print("All installed Invoice resume and right-browser-dock regression checks passed.")
