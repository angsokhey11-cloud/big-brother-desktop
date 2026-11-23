"""Regression for Windows Desktop v0.3.16 admin A/R bank payment reversal."""
from pathlib import Path
import json
root=Path(__file__).resolve().parents[1]
lock=json.loads((root/"config/modules.lock.json").read_text(encoding="utf-8"))
assert len(lock["modules"])==32,"Expected all 32 accounting modules"
rev=next(m["sha"] for m in lock["modules"] if m["name"]=="big-brother-ar")
assert rev=="00df73a52c26c64e3381a88b38db83470d999700","Reversal module revision changed"
base=root/"build/web/big-brother-ar"
loader=(base/"index.html").read_text(encoding="utf-8")
ui=(base/"receivable-ar-payment-reversal-v1.js").read_text(encoding="utf-8")
sql=(base/"sql/20261123_ar_bank_payment_reversal_v1.sql")
checks={
 "All Receivable loader includes reversal screen": "receivable-ar-payment-reversal-v1.js?v=20261123-ar-payment-reversal1" in loader,
 "Standalone admin reversal screen packaged": len(ui)>5000,
 "Admin-only gated mounting": "profile?.role==='admin'&&profile?.permissions?.canEdit" in ui,
 "Preview then token-protected reversal": "rpc('preview'" in ui and "p_token:p.token" in ui,
 "User types payment ID to confirm": "arRevTypedId" in ui and "selected?.paymentId" in ui,
 "Audit reason and explicit confirmation": "p_reason:reason" in ui and "window.confirm(confirmation)" in ui,
 "Close modal using Escape": "e.key==='Escape'" in ui,
 "Refresh A/R after approved action": "window.loadAR(true)" in ui,
 "Server RPC source captured in pinned bundle": sql.is_file() and "bb_ar_payment_reversal_v1" in sql.read_text(encoding="utf-8"),
 "Preserve existing desktop Telegram routing": (root/"build/web/big-brother-notification-center/app.js").is_file()
}
for label,ok in checks.items():
 assert ok,label
 print("PASS",label)
print("PASS A/R bank reversal Desktop v0.3.16 guard")
