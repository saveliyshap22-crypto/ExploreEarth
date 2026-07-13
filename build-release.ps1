[CmdletBinding()]
param(
    [ValidateSet("win-x64", "win-arm64")]
    [string]$Runtime = "win-x64",
    [switch]$FrameworkDependent
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$dist = Join-Path $root "dist"
$selfContained = -not $FrameworkDependent

try {
    if (Test-Path $dist) {
        Remove-Item $dist -Recurse -Force
    }

    New-Item $dist -ItemType Directory | Out-Null

    dotnet restore (Join-Path $root "EarthExplorer.sln")
    dotnet test (Join-Path $root "EarthExplorer.sln") --configuration Release --no-restore
    dotnet build (Join-Path $root "EarthExplorer.sln") --configuration Release --no-restore
    dotnet publish (Join-Path $root "src/EarthExplorer.App/EarthExplorer.App.csproj") `
        --configuration Release `
        --runtime $Runtime `
        --self-contained $selfContained `
        --output (Join-Path $dist $Runtime) `
        -p:PublishSingleFile=false

    Write-Host "ExploreEarth успешно собран: $dist" -ForegroundColor Green
}
catch {
    Write-Error "Сборка ExploreEarth завершилась ошибкой: $($_.Exception.Message)"
    exit 1
}
