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
$projectPath = Join-Path $root "src/EarthExplorer.App/EarthExplorer.App.csproj"
[xml]$project = Get-Content $projectPath
$version = [string]($project.Project.PropertyGroup.Version | Select-Object -First 1)
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Version is missing in EarthExplorer.App.csproj"
}

try {
    if (Test-Path $dist) {
        Remove-Item $dist -Recurse -Force
    }

    New-Item $dist -ItemType Directory | Out-Null

    dotnet restore (Join-Path $root "EarthExplorer.sln")
    dotnet build (Join-Path $root "EarthExplorer.sln") --configuration Release --no-restore -p:Version=$version
    dotnet test (Join-Path $root "EarthExplorer.sln") --configuration Release --no-build
    dotnet publish $projectPath `
        --configuration Release `
        --runtime $Runtime `
        --self-contained $selfContained `
        --output (Join-Path $dist $Runtime) `
        -p:PublishSingleFile=false `
        -p:Version=$version

    $publishDirectory = Join-Path $dist $Runtime
    $requiredFiles = @(
        (Join-Path $publishDirectory "ExploreEarth.exe"),
        (Join-Path $publishDirectory "Web/globe.html"),
        (Join-Path $publishDirectory "LICENSE"),
        (Join-Path $publishDirectory "THIRD_PARTY_NOTICES.md")
    )
    foreach ($file in $requiredFiles) {
        if (-not (Test-Path $file)) {
            throw "Required publish file is missing: $file"
        }
    }

    $webViewLoader = Get-ChildItem $publishDirectory -Recurse -Filter WebView2Loader.dll | Select-Object -First 1
    if ($null -eq $webViewLoader) {
        throw "WebView2Loader.dll is missing from the publish output"
    }

    $productVersion = (Get-Item (Join-Path $publishDirectory "ExploreEarth.exe")).VersionInfo.ProductVersion
    if (-not $productVersion.StartsWith($version)) {
        throw "ExploreEarth.exe version is '$productVersion', expected '$version'"
    }

    $tessdataDirectory = Join-Path $publishDirectory "tessdata"
    New-Item $tessdataDirectory -ItemType Directory -Force | Out-Null
    Invoke-WebRequest `
        "https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/main/eng.traineddata" `
        -OutFile (Join-Path $tessdataDirectory "eng.traineddata")
    Invoke-WebRequest `
        "https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/main/rus.traineddata" `
        -OutFile (Join-Path $tessdataDirectory "rus.traineddata")

    Get-ChildItem $tessdataDirectory -Filter *.traineddata | ForEach-Object {
        if ($_.Length -lt 1000000) {
            throw "OCR model $($_.Name) is unexpectedly small"
        }
    }

    $prerequisitesDirectory = Join-Path $publishDirectory "prerequisites"
    New-Item $prerequisitesDirectory -ItemType Directory -Force | Out-Null
    $vcRuntimeName = if ($Runtime -eq "win-arm64") { "vc_redist.arm64.exe" } else { "vc_redist.x64.exe" }
    Invoke-WebRequest `
        "https://aka.ms/vs/17/release/$vcRuntimeName" `
        -OutFile (Join-Path $prerequisitesDirectory $vcRuntimeName)
    Invoke-WebRequest `
        "https://go.microsoft.com/fwlink/p/?LinkId=2124703" `
        -OutFile (Join-Path $prerequisitesDirectory "MicrosoftEdgeWebview2Setup.exe")

    @"
@echo off
echo Installing Microsoft Visual C++ Runtime...
"%~dp0prerequisites\$vcRuntimeName" /install /quiet /norestart
echo Installing Microsoft Edge WebView2 Runtime...
"%~dp0prerequisites\MicrosoftEdgeWebview2Setup.exe" /silent /install
echo Prerequisites installation finished.
pause
"@ | Set-Content (Join-Path $publishDirectory "Install-Prerequisites.cmd") -Encoding ascii

    $portableArchive = Join-Path $dist "ExploreEarth-$version-portable-$Runtime.zip"
    Compress-Archive -Path (Join-Path $publishDirectory "*") -DestinationPath $portableArchive -CompressionLevel Optimal
    if ((Get-Item $portableArchive).Length -lt 1000000) {
        throw "Portable archive is unexpectedly small"
    }

    Write-Host "ExploreEarth $version успешно собран: $portableArchive" -ForegroundColor Green
}
catch {
    Write-Error "Сборка ExploreEarth завершилась ошибкой: $($_.Exception.Message)"
    exit 1
}
