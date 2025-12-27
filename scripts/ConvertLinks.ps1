# Paste your links in the format: [url=LINK]GameName[/url]
# One per line. Run: .\ConvertLinks.ps1

param(
    [string]$InputFile = "",
    [string]$SteamPaths = "C:\Program Files (x86)\Steam;G:\SteamLibrary"
)

$links = @'
[url=https://example.com/share/ABC123]Game Name 1[/url]
[url=https://example.com/share/DEF456]Game Name 2[/url]
[url=https://example.com/share/GHI789]Game Name 3[/url]
'@

# If input file provided, use that instead
if ($InputFile -and (Test-Path $InputFile)) {
    $links = Get-Content $InputFile -Raw
}

$steamPathList = $SteamPaths -split ";"

# Build game manifest cache
$gameManifests = @{}
foreach ($steamPath in $steamPathList) {
    $steamapps = Join-Path $steamPath "steamapps"
    if (!(Test-Path $steamapps)) { continue }

    Get-ChildItem "$steamapps\appmanifest_*.acf" -ErrorAction SilentlyContinue | ForEach-Object {
        $content = Get-Content $_.FullName -Raw
        if ($content -match '"name"\s+"([^"]+)"') {
            $gameName = $matches[1]
            $gameManifests[$gameName.ToLower()] = $content
        }
    }
}

Write-Host "`nFound $($gameManifests.Count) installed games`n" -ForegroundColor Cyan

# Parse and convert each link
$output = @()
$linkPattern = '\[url=([^\]]+)\]([^\[]+)\[/url\]'

foreach ($match in [regex]::Matches($links, $linkPattern)) {
    $url = $match.Groups[1].Value
    $gameName = $match.Groups[2].Value

    # Find matching manifest (fuzzy match)
    $manifest = $null
    $matchedName = $null
    foreach ($key in $gameManifests.Keys) {
        if ($key -like "*$($gameName.ToLower())*" -or $gameName.ToLower() -like "*$key*") {
            $manifest = $gameManifests[$key]
            # Get actual game name from manifest
            if ($manifest -match '"name"\s+"([^"]+)"') {
                $matchedName = $matches[1]
            }
            break
        }
    }

    if (!$manifest) {
        Write-Host "NOT FOUND: $gameName" -ForegroundColor Red
        continue
    }

    # Extract info
    $buildId = if ($manifest -match '"buildid"\s+"(\d+)"') { $matches[1] } else { "?" }
    $lastUpdated = if ($manifest -match '"LastUpdated"\s+"(\d+)"') { [int64]$matches[1] } else { 0 }

    # Get depots
    $depots = @()
    if ($manifest -match '"InstalledDepots"\s*\{([\s\S]*?)\n\t\}') {
        $depotsSection = $matches[1]
        $depotMatches = [regex]::Matches($depotsSection, '"(\d+)"\s*\{[^}]*"manifest"\s+"(\d+)"')
        foreach ($d in $depotMatches) {
            $depots += "$($d.Groups[1].Value) [Manifest $($d.Groups[2].Value)]"
        }
    }

    # Format date
    $versionDate = "Unknown"
    if ($lastUpdated -gt 0) {
        $dt = [DateTimeOffset]::FromUnixTimeSeconds($lastUpdated).UtcDateTime
        $versionDate = $dt.ToString("MMM dd, yyyy - HH:mm:ss") + " UTC [Build $buildId]"
    }

    $depotsText = if ($depots.Count -gt 0) { $depots -join "`n" } else { "No depot info" }

    $formatted = @"
[url=$url][color=white][b]$matchedName [Win64] [Branch: Public] (Clean Steam Files)[/b][/color][/url]
[size=85][color=white][b]Version:[/b] [i]$versionDate[/i][/color][/size]

[spoiler="[color=white]Depots & Manifests[/color]"][code=text]$depotsText[/code][/spoiler][color=white][b]Uploaded version:[/b] [i]$versionDate[/i][/color]
"@

    $output += $formatted
    Write-Host "OK: $matchedName" -ForegroundColor Green
}

# Output all
Write-Host "`n========== FORMATTED OUTPUT ==========`n" -ForegroundColor Cyan
$result = $output -join "`n`n"
Write-Host $result

# Copy to clipboard
$result | Set-Clipboard
Write-Host "`n`n[Copied to clipboard!]" -ForegroundColor Yellow
