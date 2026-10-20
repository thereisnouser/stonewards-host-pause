param(
    [Parameter(Mandatory = $true)] [string] $GameDir,
    [Parameter(Mandatory = $true)] [string] $ProfileDir
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Install a .NET SDK (netstandard2.1 support) before building.'
}
$game = (Resolve-Path -LiteralPath $GameDir -ErrorAction Stop).Path
$profile = (Resolve-Path -LiteralPath $ProfileDir -ErrorAction Stop).Path
$managed = Join-Path $game 'Stonewards_Data\Managed'
$bepRoot = Join-Path $profile 'BepInEx'
$bepCore = Join-Path $bepRoot 'core'
$required = @(
    (Join-Path $managed 'Assembly-CSharp.dll'),
    (Join-Path $managed 'Mirror.dll'),
    (Join-Path $managed 'UnityEngine.dll'),
    (Join-Path $managed 'UnityEngine.CoreModule.dll'),
    (Join-Path $managed 'UnityEngine.IMGUIModule.dll'),
    (Join-Path $managed 'UnityEngine.TextRenderingModule.dll'),
    (Join-Path $managed 'Unity.InputSystem.dll'),
    (Join-Path $bepCore 'BepInEx.dll')
)
foreach ($path in $required) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing dependency: $path"
    }
}
$project = Join-Path $PSScriptRoot 'src\StonewardsHostPause\StonewardsHostPause.csproj'
& dotnet build $project --configuration Release "-p:GameDir=$game" "-p:BepInExDir=$bepRoot"
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }
$dll = Join-Path $PSScriptRoot 'src\StonewardsHostPause\bin\Release\netstandard2.1\StonewardsHostPause.dll'
if (-not (Test-Path -LiteralPath $dll -PathType Leaf)) { throw "Output DLL not found: $dll" }
$dest = Join-Path $bepRoot 'plugins\StonewardsHostPause'
New-Item -Path $dest -ItemType Directory -Force | Out-Null
Copy-Item -LiteralPath $dll -Destination (Join-Path $dest 'StonewardsHostPause.dll') -Force
Write-Host "Installed Host Pause DLL into: $dest" -ForegroundColor Green
Write-Host 'Install only on the host. Guests need no mods.' -ForegroundColor Cyan
