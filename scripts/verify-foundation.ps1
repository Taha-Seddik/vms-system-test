[CmdletBinding()]
param(
    [string]$ApiBaseUrl = "http://localhost:8080",
    [string]$FrontendBaseUrl = "http://localhost:3000",
    [string]$HlsBaseUrl = "http://localhost:8888",
    [int]$TimeoutSeconds = 120
)

$ErrorActionPreference = "Stop"

function Wait-ForHttp {
    param(
        [Parameter(Mandatory)]
        [string]$Url,
        [Parameter(Mandatory)]
        [string]$Name
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        try {
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 300) {
                Write-Host "[PASS] $Name responded at $Url"
                return $response
            }
        }
        catch {
            Start-Sleep -Seconds 2
        }
    } while ((Get-Date) -lt $deadline)

    throw "$Name did not become ready at $Url within $TimeoutSeconds seconds."
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker is required. Follow the prerequisite guide in README.md."
}

Wait-ForHttp -Url "$ApiBaseUrl/health" -Name "API" | Out-Null
Wait-ForHttp -Url "$FrontendBaseUrl/health" -Name "Frontend" | Out-Null

foreach ($cameraNumber in 1..4) {
    $cameraPath = "camera-$cameraNumber"
    $playlistUrl = "$HlsBaseUrl/$cameraPath/index.m3u8"
    $playlist = Wait-ForHttp -Url $playlistUrl -Name "$cameraPath HLS playlist"
    $playlistContent = if ($playlist.Content -is [byte[]]) {
        [System.Text.Encoding]::UTF8.GetString($playlist.Content)
    }
    else {
        [string]$playlist.Content
    }

    if ($playlistContent -notmatch "#EXTM3U") {
        throw "$cameraPath returned a response, but it is not an HLS playlist."
    }

    & docker compose exec -T camera-1 `
        ffprobe `
        -v error `
        -rw_timeout 10000000 `
        -select_streams "v:0" `
        -show_entries "stream=codec_name,width,height,r_frame_rate" `
        -of json `
        "http://mediamtx:8888/$cameraPath/index.m3u8"

    if ($LASTEXITCODE -ne 0) {
        throw "ffprobe could not decode video from $cameraPath."
    }

    Write-Host "[PASS] $cameraPath contains decodable video."
}

$containerRows = docker compose ps --format json | ConvertFrom-Json
$unhealthy = @($containerRows | Where-Object {
    $_.State -ne "running" -or ($_.Health -and $_.Health -ne "healthy")
})

if ($unhealthy.Count -gt 0) {
    $unhealthy | Format-Table Name, State, Health
    throw "One or more Compose services are not running and healthy."
}

Write-Host "[PASS] All Compose services are running and healthy."
Write-Host "Step 1 foundation verification completed successfully."
