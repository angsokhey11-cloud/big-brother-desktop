#!/usr/bin/env python3
"""Guard clean BIG BROTHER Desktop naming without changing dev-channel identity."""
from pathlib import Path
root=Path(__file__).resolve().parents[1]
project=(root/"src/BBDesktop.csproj").read_text(encoding="utf-8")
ci=(root/".github/workflows/windows-desktop-dev.yml").read_text(encoding="utf-8")
publish=(root/".github/workflows/publish-desktop-github-dev.yml").read_text(encoding="utf-8")
for label, source in [("installer CI", ci), ("release CI", publish)]:
    assert '--packTitle "BIG BROTHER Desktop"' in source, label+": wrong app display name"
    assert '--packTitle "BIG BROTHER Desktop (Development)"' not in source, label+": old display name remains"
    assert '--packId BigBrother.Desktop' in source, label+": package identity was changed"
    assert '--channel dev' in source, label+": updater feed changed unexpectedly"
assert '--packVersion 0.3.33' in ci, "development pack version mismatch"
assert '<Version>0.3.33</Version>' in project, "assembly version mismatch"
assert '--releaseName "BIG BROTHER Desktop $env:VERSION"' in publish, "GitHub release name still includes suffix"
assert '--pre --tag $env:TAG' in publish, "dev prerelease status must remain intact"
print("PASS Windows display name is BIG BROTHER Desktop")
print("PASS packId stays BigBrother.Desktop and update channel stays dev")
print("PASS GitHub prerelease name has no Development suffix")
