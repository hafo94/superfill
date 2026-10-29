<#
.SYNOPSIS
  Runs every tests/integration/*.scr in accoreconsole against a copy of samples/dwg/blank.dwg.
.NOTES
  M0 skeleton: runs the scripts and checks the console output. Dump/size comparison arrives with SFTESTDUMP (M2).
  TODO(owner): confirm the accoreconsole path and supply samples/dwg/blank.dwg.
#>
param(
    [string]$AccoreConsole = "C:\Program Files\Autodesk\AutoCAD 2025\accoreconsole.exe",
    [string]$Configuration = "Debug"
)
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$dll = Join-Path $root "src/SuperFill.Acad/bin/$Configuration/net8.0-windows/SuperFill.Acad.dll"
$blank = Join-Path $root "samples/dwg/blank.dwg"
if (-not (Test-Path $AccoreConsole)) { throw "accoreconsole not found: $AccoreConsole" }
if (-not (Test-Path $dll)) { throw "Build first: $dll" }
if (-not (Test-Path $blank)) { throw "Missing $blank (TODO(owner): provide a blank DWG)" }

$failed = 0
foreach ($scr in Get-ChildItem (Join-Path $root "tests/integration/*.scr")) {
    $work = Join-Path ([IO.Path]::GetTempPath()) ("sf-" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory $work | Out-Null
    Copy-Item $blank (Join-Path $work "test.dwg")
    # Expand the DLL path into a per-run copy of the script.
    $script = Join-Path $work $scr.Name
    (Get-Content $scr.FullName -Raw).Replace("%SF_ACAD_DLL%", $dll) | Set-Content $script
    $out = & $AccoreConsole /i (Join-Path $work "test.dwg") /s $script /l en-US 2>&1 | Out-String
    $ok = $out -match "SFHELLO: SuperFill is alive"
    Write-Host ("{0,-30} {1}" -f $scr.Name, ($(if ($ok) { "PASS" } else { "FAIL" })))
    if (-not $ok) { Write-Host $out; $failed++ }
    Remove-Item $work -Recurse -Force
}
exit $failed
