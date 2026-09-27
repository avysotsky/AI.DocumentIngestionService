[CmdletBinding()]
param(
    [string]$Destination = 'D:\AI.Models\multilingual-e5-base'
)

$ErrorActionPreference = 'Stop'
$manifestPath = Join-Path $PSScriptRoot 'model-manifests\multilingual-e5-base.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$baseUri = "https://huggingface.co/$($manifest.repository)/resolve/$($manifest.revision)"

New-Item -ItemType Directory -Path $Destination -Force | Out-Null

foreach ($file in $manifest.files) {
    $destinationPath = Join-Path $Destination $file.target
    $isValid = Test-Path -LiteralPath $destinationPath
    if ($isValid) {
        $existing = Get-Item -LiteralPath $destinationPath
        $isValid = $existing.Length -eq [long]$file.size -and
            (Get-FileHash -LiteralPath $destinationPath -Algorithm SHA256).Hash.ToLowerInvariant() -eq $file.sha256
    }

    if ($isValid) {
        Write-Host "Verified $($file.target)"
        continue
    }

    $temporaryPath = "$destinationPath.download"
    Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
    $sourceUri = "$baseUri/$($file.source)"
    Write-Host "Downloading $sourceUri"
    Invoke-WebRequest -Uri $sourceUri -OutFile $temporaryPath

    $downloaded = Get-Item -LiteralPath $temporaryPath
    $actualHash = (Get-FileHash -LiteralPath $temporaryPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($downloaded.Length -ne [long]$file.size -or $actualHash -ne $file.sha256) {
        Remove-Item -LiteralPath $temporaryPath -Force
        throw "Integrity check failed for $($file.target)."
    }

    Move-Item -LiteralPath $temporaryPath -Destination $destinationPath -Force
}

Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $Destination 'manifest.json') -Force
Write-Host "Model is ready at $Destination"
