[CmdletBinding()]
param(
    [string]$ApiBaseUrl = "http://localhost:8080"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http

function Write-Pass {
    param([string]$Message)
    Write-Host "[PASS] $Message" -ForegroundColor Green
}

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
    Write-Pass $Message
}

function Assert-Equal {
    param($Actual, $Expected, [string]$Message)
    if ($Actual -ne $Expected) {
        throw "$Message Expected '$Expected', received '$Actual'."
    }
    Write-Pass $Message
}

function Invoke-Login {
    param([string]$Username, [string]$Password)
    Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/auth/login" `
        -Method Post `
        -ContentType "application/json" `
        -Body (@{ username = $Username; password = $Password } | ConvertTo-Json)
}

function New-AuthHeaders {
    param([string]$Token)
    @{ Authorization = "Bearer $Token" }
}

function Get-StatusCode {
    param(
        [string]$Path,
        [hashtable]$Headers = @{}
    )
    try {
        $response = Invoke-WebRequest `
            -Uri "$ApiBaseUrl$Path" `
            -Headers $Headers `
            -UseBasicParsing
        return [int]$response.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            return [int]$_.Exception.Response.StatusCode
        }
        throw
    }
}

function Invoke-Logout {
    param([string]$Token)
    Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/auth/logout" `
        -Method Post `
        -Headers (New-AuthHeaders $Token) | Out-Null
}

$operator = $null
$viewer = $null

try {
    $operator = Invoke-Login "operator" "Operator123!"
    $viewer = Invoke-Login "viewer" "Viewer123!"
    $operatorHeaders = New-AuthHeaders $operator.accessToken
    $viewerHeaders = New-AuthHeaders $viewer.accessToken

    Assert-Equal `
        (Get-StatusCode "/api/recordings") `
        401 `
        "Anonymous users cannot browse recordings."
    Assert-Equal `
        (Get-StatusCode "/api/recordings" $viewerHeaders) `
        403 `
        "Viewers cannot browse the playback library."
    Assert-Equal `
        (Get-StatusCode "/api/recordings/$([Guid]::NewGuid())/media" $viewerHeaders) `
        403 `
        "Viewers cannot fetch protected recording media."

    $completed = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/recordings?state=Completed&take=100" `
        -Headers $operatorHeaders
    $longRecording = @($completed) |
        Where-Object {
            $_.mode -eq "Manual" `
                -and [double]$_.durationSeconds -gt 31
        } |
        Select-Object -First 1

    if (-not $longRecording) {
        $cameras = Invoke-RestMethod `
            -Uri "$ApiBaseUrl/api/cameras/accessible" `
            -Headers $operatorHeaders
        $camera = @($cameras) |
            Where-Object {
                $_.connectionStatus -eq "Online" `
                    -and $_.recordingStatus -eq "NotRecording"
            } |
            Select-Object -First 1
        if (-not $camera) {
            throw "No online, idle camera is available for playback verification."
        }

        $started = Invoke-RestMethod `
            -Uri "$ApiBaseUrl/api/cameras/$($camera.id)/recordings/manual/start" `
            -Method Post `
            -Headers $operatorHeaders
        Write-Host "Capturing a 35-second verification recording..."
        Start-Sleep -Seconds 35
        $stopped = Invoke-RestMethod `
            -Uri "$ApiBaseUrl/api/cameras/$($camera.id)/recordings/stop" `
            -Method Post `
            -Headers $operatorHeaders
        $longRecording = $stopped.recording
    }

    Assert-True `
        ($longRecording.state -eq "Completed" `
            -and [double]$longRecording.durationSeconds -gt 30) `
        "A completed recording longer than 30 seconds is available."

    $details = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/recordings/$($longRecording.id)" `
        -Headers $operatorHeaders
    $timestamps = @($details.keyframes | ForEach-Object timestampSeconds)
    Assert-True `
        (0 -in $timestamps -and 30 -in $timestamps) `
        "The keyframe timeline contains real 0-second and 30-second previews."

    $recordingPath = "/api/recordings/$($longRecording.id)"
    $httpClient = [System.Net.Http.HttpClient]::new()
    $rangeRequest = [System.Net.Http.HttpRequestMessage]::new(
        [System.Net.Http.HttpMethod]::Get,
        "$ApiBaseUrl$recordingPath/media")
    $rangeRequest.Headers.Authorization =
        [System.Net.Http.Headers.AuthenticationHeaderValue]::new(
            "Bearer",
            $operator.accessToken)
    $rangeRequest.Headers.Range =
        [System.Net.Http.Headers.RangeHeaderValue]::new(0, 1023)
    try {
        $range = $httpClient.SendAsync($rangeRequest).GetAwaiter().GetResult()
        Assert-Equal `
            ([int]$range.StatusCode) `
            206 `
            "Protected media supports HTTP byte ranges for seeking."
        Assert-True `
            ($range.Content.Headers.ContentType.MediaType -eq "video/mp4") `
            "Protected media returns the MP4 content type."
    }
    finally {
        if ($range) {
            $range.Dispose()
        }
        $rangeRequest.Dispose()
        $httpClient.Dispose()
    }

    $download = Invoke-WebRequest `
        -Uri "$ApiBaseUrl$recordingPath/download" `
        -Headers $operatorHeaders `
        -UseBasicParsing
    Assert-True `
        ($download.Headers["Content-Disposition"] -like "attachment*") `
        "Download returns an MP4 attachment with a safe server filename."

    foreach ($timestamp in @(0, 30)) {
        $keyframe = $details.keyframes |
            Where-Object timestampSeconds -eq $timestamp |
            Select-Object -First 1
        $image = Invoke-WebRequest `
            -Uri "$ApiBaseUrl$recordingPath/keyframes/$($keyframe.id)" `
            -Headers $operatorHeaders `
            -UseBasicParsing
        Assert-True `
            ($image.Headers["Content-Type"] -like "image/jpeg*") `
            "The ${timestamp}-second keyframe is served as a protected JPEG."
    }

    $compactId = $longRecording.id -replace "-", ""
    $mediaProbe = docker compose exec -T api ffprobe `
        -v error `
        -select_streams v:0 `
        -show_entries stream=codec_name `
        -of default=noprint_wrappers=1:nokey=1 `
        "/var/lib/vms/recordings/$compactId.mp4" | Out-String
    Assert-True `
        ($LASTEXITCODE -eq 0 -and $mediaProbe.Trim() -eq "h264") `
        "The playback source is a real H.264 MP4."

    foreach ($timestamp in @(0, 30)) {
        $fileName = "{0:D6}.jpg" -f $timestamp
        $imageProbe = docker compose exec -T api ffprobe `
            -v error `
            -select_streams v:0 `
            -show_entries stream=codec_name `
            -of default=noprint_wrappers=1:nokey=1 `
            "/var/lib/vms/recordings/keyframes/$compactId/$fileName" | Out-String
        Assert-True `
            ($LASTEXITCODE -eq 0 -and $imageProbe.Trim() -eq "mjpeg") `
            "The ${timestamp}-second preview is a decodable JPEG keyframe."
    }

    $filtered = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/recordings?cameraId=$($longRecording.cameraId)&mode=Manual&state=Completed&take=100" `
        -Headers $operatorHeaders
    Assert-True `
        ($longRecording.id -in @($filtered | ForEach-Object id)) `
        "Camera, recording-type, and status filters return the expected recording."

    $invalidDates = Get-StatusCode `
        "/api/recordings?from=2026-07-27T00%3A00%3A00Z&to=2026-07-26T00%3A00%3A00Z" `
        $operatorHeaders
    Assert-Equal `
        $invalidDates `
        400 `
        "Invalid recording date ranges are rejected."

    Write-Host "Step 7 playback verification completed successfully." `
        -ForegroundColor Cyan
}
finally {
    if ($operator) {
        Invoke-Logout $operator.accessToken
    }
    if ($viewer) {
        Invoke-Logout $viewer.accessToken
    }
}
