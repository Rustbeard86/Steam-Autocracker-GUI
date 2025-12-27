# Quick script to generate cs.rin.ru formatted text for a game
# Usage: .\GenerateRinFormat.ps1 "C:\path\to\game" "https://your-pydrive-or-1fichier-link"

param(
    [Parameter(Mandatory=$true)]
    [string]$GamePath,

    [Parameter(Mandatory=$true)]
    [string]$UploadUrl
)

# Find steamapps path
$currentPath = $GamePath
while ($currentPath -and (Split-Path $currentPath -Leaf) -ne "steamapps") {
    $currentPath = Split-Path $currentPath -Parent
}

if (-not $currentPath) {
    Write-Error "Could not find steamapps folder"
    exit 1
}

$gameFolderName = Split-Path $GamePath -Leaf
$acfFiles = Get-ChildItem "$currentPath\appmanifest_*.acf"

foreach ($acf in $acfFiles) {
    $content = Get-Content $acf.FullName -Raw

    # Parse installdir
    if ($content -match '"installdir"\s+"([^"]+)"') {
        $installDir = $matches[1]
        if ($installDir -eq $gameFolderName) {
            # Found the right manifest
            $gameName = if ($content -match '"name"\s+"([^"]+)"') { $matches[1] } else { $gameFolderName }
            $buildId = if ($content -match '"buildid"\s+"(\d+)"') { $matches[1] } else { "?" }
            $lastUpdated = if ($content -match '"LastUpdated"\s+"(\d+)"') { [int64]$matches[1] } else { 0 }

            # Parse depots
            $depots = @()
            if ($content -match '"InstalledDepots"\s*\{([\s\S]*?)\n\t\}') {
                $depotsSection = $matches[1]
                $depotMatches = [regex]::Matches($depotsSection, '"(\d+)"\s*\{[^}]*"manifest"\s+"(\d+)"')
                foreach ($m in $depotMatches) {
                    $depots += "$($m.Groups[1].Value) [Manifest $($m.Groups[2].Value)]"
                }
            }

            # Detect platform by checking for .exe files
            $platform = "Win64"  # Default
            try {
                $exeFiles = Get-ChildItem -Path $GamePath -Filter "*.exe" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 5
                foreach ($exe in $exeFiles) {
                    $bytes = [System.IO.File]::ReadAllBytes($exe.FullName)
                    if ($bytes.Length -gt 0x3C + 4) {
                        # Check PE header to determine architecture
                        $peOffset = [BitConverter]::ToInt32($bytes, 0x3C)
                        if ($peOffset -lt $bytes.Length - 6) {
                            $machineType = [BitConverter]::ToUInt16($bytes, $peOffset + 4)
                            if ($machineType -eq 0x8664) {
                                $platform = "Win64"
                                break
                            } elseif ($machineType -eq 0x014C) {
                                $platform = "Win32"
                                break
                            }
                        }
                    }
                }
            } catch {
                # Keep default Win64 if detection fails
            }

            # Format date
            $versionDate = "Unknown"
            if ($lastUpdated -gt 0) {
                $dt = [DateTimeOffset]::FromUnixTimeSeconds($lastUpdated).UtcDateTime
                $versionDate = $dt.ToString("MMM dd, yyyy - HH:mm:ss") + " UTC [Build $buildId]"
            }

            $depotsText = if ($depots.Count -gt 0) { $depots -join "`n" } else { "No depot info" }

            # Output the formatted text
            Write-Host ""
            Write-Host "=== COPY THIS ===" -ForegroundColor Cyan
            Write-Host ""
            Write-Host "[url=$UploadUrl][color=white][b]$gameName [$platform] [Branch: Public] (Clean Steam Files)[/b][/color][/url]"
            Write-Host "[size=85][color=white][b]Version:[/b] [i]$versionDate[/i][/color][/size]"
            Write-Host ""
            Write-Host "[spoiler=`"[color=white]Depots & Manifests[/color]`"][code=text]$depotsText[/code][/spoiler][color=white][b]Uploaded version:[/b] [i]$versionDate[/i][/color]"
            Write-Host ""
            Write-Host "=== END ===" -ForegroundColor Cyan

            exit 0
        }
    }
}

Write-Error "Could not find matching manifest for: $gameFolderName"
