# Build-and-launch smoke check for the screensaver itself.
#
# A WinForms screensaver has no headless boundary, so the honest automated check is
# that it builds Release and starts under /s without throwing. This is a LOCAL
# pre-commit / post-build gate, not CI: /s needs an interactive desktop and the repo
# has no CI runner. Run it from a normal PowerShell session on the operator's box:
#   . .\ScreenSaver.Tests\smoke-launch.ps1      (dot-source) or run the lines below.
#
# Exit 0 = built and launched (or dismissed cleanly); non-zero = build or launch crash.

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot   # repo root (this file lives in ScreenSaver.Tests)

Write-Host "[smoke] building ScreenSaver (Release)..."
dotnet build (Join-Path $repo 'ScreenSaver\ScreenSaver.csproj') -c Release -nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw "[smoke] Release build failed (exit $LASTEXITCODE)" }

$relDir = Join-Path $repo 'out\ScreenSaver\bin\Release'
$exe = Get-ChildItem -Path $relDir -Recurse -Filter 'ScreenSaver.exe' -ErrorAction SilentlyContinue |
    Select-Object -First 1
if (-not $exe) { throw "[smoke] ScreenSaver.exe not found under $relDir" }
Write-Host "[smoke] launching $($exe.FullName) /s"

$p = Start-Process -FilePath $exe.FullName -ArgumentList '/s' -PassThru
Start-Sleep -Seconds 4

if ($p.HasExited -and $p.ExitCode -ne 0) {
    throw "[smoke] screensaver crashed on launch (exit $($p.ExitCode))"
}
if (-not $p.HasExited) { $p.Kill() }   # Kill() throws if it already exited -> guard it
Write-Host "[smoke] OK: built and launched without crashing."
