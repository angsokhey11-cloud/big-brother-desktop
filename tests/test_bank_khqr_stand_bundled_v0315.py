#!/usr/bin/env python3
"""Retain KHQR Hash-vs-merchant-stand safeguards in packaged Desktop review."""
from pathlib import Path
import json
root=Path(__file__).resolve().parents[1]
lock=json.loads((root/"config/modules.lock.json").read_text(encoding="utf-8"))
pin=next(m["sha"] for m in lock["modules"] if m["name"]=="big-brother-notification-center")
assert pin=="3923939f778d26d3af42b3991f52d93ea1765e8b","Wrong bank review revision"
site=root/"build/web/big-brother-notification-center"
bulk=(site/"bank-transaction-bulk-review.html").read_text(encoding="utf-8")
single=(site/"bank-transaction-batch-review.html").read_text(encoding="utf-8")
private=(site/"bank-new-invoice-review.html").read_text(encoding="utf-8")
checks={
 "Bulk registration refuses STAND-like numeric KHQR references":
   'row.original?.bank_channel' in bulk and 'STAND is merchant ID — enter the KHQR Hash instead' in bulk,
 "Bulk transaction input explicitly says never STAND":
   "Transaction ID / Hash (never STAND)" in bulk,
 "Single transaction review rejects merchant STAND":
   "STAND identifies the merchant QR stand, not the payment" in single,
 "Private invoice register review rejects merchant STAND":
   "STAND is the merchant QR stand, not the transaction" in private,
 "Existing bank registration RPC unchanged":
   "bb_bank_register_invoice_tx_v4" in bulk and "bb_bank_register_invoice_tx_v4" in private
}
for label,ok in checks.items():
 assert ok,"FAILED: "+label
 print("PASS",label)
print("PASS Desktop Bank Review KHQR merchant STAND safeguards")
