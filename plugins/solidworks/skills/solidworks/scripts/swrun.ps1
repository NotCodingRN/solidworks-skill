# Compiles a C# recipe with the kit against the SOLIDWORKS interop assemblies, then runs it.
#   powershell -NoProfile -ExecutionPolicy Bypass -File swrun.ps1 -Source C:\full\path\recipe.cs -Args2 "arg1","arg2" [-Sim] [-Motion]
param(
    [Parameter(Mandatory = $true)][string]$Source,
    [string[]]$Args2 = @(),
    [switch]$Sim,
    [switch]$Motion
)
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$key = Get-ChildItem 'HKLM:\SOFTWARE\SolidWorks' |
    Where-Object { $_.PSChildName -match '^SOLIDWORKS \d{4}$' } |
    Sort-Object PSChildName | Select-Object -Last 1
if (-not $key) { Write-Error 'SOLIDWORKS is not installed on this machine'; exit 2 }
$sw = (Get-ItemProperty (Join-Path $key.PSPath 'Setup')).'SolidWorks Folder'
$redist = Join-Path $sw 'api\redist'
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$Source = (Resolve-Path $Source).Path

# Build next to the recipe's folder in temp, so the skill folder stays clean.
$out = Join-Path $env:TEMP 'swkit-build'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$exe = Join-Path $out ([IO.Path]::GetFileNameWithoutExtension($Source) + '.exe')

$dlls = @('SolidWorks.Interop.sldworks.dll', 'SolidWorks.Interop.swconst.dll')
$src = @((Join-Path $here 'SwKit.cs'), $Source)
if ($Sim) { $dlls += 'SolidWorks.Interop.cosworks.dll'; $src += (Join-Path $here 'SwSim.cs') }
if ($Motion) { $dlls += 'SolidWorks.Interop.swmotionstudy.dll'; $src += (Join-Path $here 'SwMotion.cs') }
$refs = $dlls | ForEach-Object { "/link:$(Join-Path $redist $_)" }

& $csc /nologo /nowarn:0219,0168,0618 /platform:x64 /out:$exe $refs $src
if ($LASTEXITCODE -ne 0) { exit 1 }
& $exe @Args2
exit $LASTEXITCODE
