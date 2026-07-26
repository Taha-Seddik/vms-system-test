[CmdletBinding()]
param(
    [string]$ApiBaseUrl = "http://localhost:8080"
)

$ErrorActionPreference = "Stop"

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

function Invoke-Logout {
    param([string]$Token)
    Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/auth/logout" `
        -Method Post `
        -Headers (New-AuthHeaders $Token) | Out-Null
}

function Get-StatusCode {
    param(
        [string]$Path,
        [string]$Method = "GET",
        [hashtable]$Headers = @{}
    )
    try {
        $response = Invoke-WebRequest `
            -Uri "$ApiBaseUrl$Path" `
            -Method $Method `
            -Headers $Headers `
            -UseBasicParsing
        [int]$response.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            return [int]$_.Exception.Response.StatusCode
        }
        throw
    }
}

function Wait-Recording {
    param(
        [Guid]$RecordingId,
        [hashtable]$Headers,
        [int]$Seconds = 20
    )
    for ($attempt = 0; $attempt -lt $Seconds; $attempt++) {
        $rows = Invoke-RestMethod `
            -Uri "$ApiBaseUrl/api/recordings?take=100" `
            -Headers $Headers
        $recording = $rows | Where-Object id -eq $RecordingId.ToString()
        if ($recording -and $recording.state -ne "Recording") {
            return $recording
        }
        Start-Sleep -Seconds 1
    }
    throw "Recording '$RecordingId' did not finish within $Seconds seconds."
}

function Assert-Playable {
    param($Recording, [string]$Message)
    $fileName = ($Recording.id -replace "-", "") + ".mp4"
    $probeOutput = docker compose exec -T api ffprobe `
        -v error `
        -select_streams v:0 `
        -show_entries stream=codec_name,width,height `
        -show_entries format=duration,size `
        -of json `
        "/var/lib/vms/recordings/$fileName" | Out-String
    if ($LASTEXITCODE -ne 0) {
        throw "$Message FFprobe could not decode '$fileName'."
    }
    $probe = $probeOutput | ConvertFrom-Json
    Assert-True `
        ($probe.streams.Count -gt 0 -and $probe.streams[0].codec_name -eq "h264") `
        "$Message contains a decodable H.264 video stream."
    Assert-True `
        ([double]$probe.format.duration -gt 0 -and [long]$probe.format.size -gt 0) `
        "$Message has positive duration and file size."
}

$sessions = @()
$camera4Original = $null
try {
    $administrator = Invoke-Login "admin" "Admin123!"
    $sessions += $administrator
    $operator = Invoke-Login "operator" "Operator123!"
    $sessions += $operator
    $viewer = Invoke-Login "viewer" "Viewer123!"
    $sessions += $viewer
    $adminHeaders = New-AuthHeaders $administrator.accessToken
    $operatorHeaders = New-AuthHeaders $operator.accessToken
    $viewerHeaders = New-AuthHeaders $viewer.accessToken

    $viewerCameras = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/cameras/accessible" `
        -Headers $viewerHeaders
    Assert-Equal $viewerCameras.Count 2 "Viewer receives exactly two assigned live cameras."
    Assert-Equal `
        (Get-StatusCode `
            "/api/cameras/camera-1/recordings/manual/start" `
            "POST" `
            $viewerHeaders) `
        403 `
        "Viewer cannot start a recording."

    for ($attempt = 0; $attempt -lt 12; $attempt++) {
        $cameras = Invoke-RestMethod `
            -Uri "$ApiBaseUrl/api/cameras/accessible" `
            -Headers $operatorHeaders
        if (($cameras | Where-Object connectionStatus -ne "Online").Count -eq 0) {
            break
        }
        Start-Sleep -Seconds 5
    }
    Assert-Equal `
        ($cameras | Where-Object connectionStatus -eq "Online").Count `
        4 `
        "All four HLS wall cameras are probe-confirmed online."

    $manual = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/cameras/camera-1/recordings/manual/start" `
        -Method Post `
        -Headers $operatorHeaders
    Assert-Equal $manual.recording.mode "Manual" "Manual recording starts."
    Assert-Equal `
        (Get-StatusCode `
            "/api/cameras/camera-1/recordings/continuous/start" `
            "POST" `
            $operatorHeaders) `
        409 `
        "Conflicting recording modes are rejected."
    Start-Sleep -Seconds 4
    $manualStopped = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/cameras/camera-1/recordings/stop" `
        -Method Post `
        -Headers $operatorHeaders
    Assert-Equal $manualStopped.recording.state "Completed" "Manual recording finalizes."
    Assert-Playable $manualStopped.recording "Manual recording"

    $motion = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/cameras/camera-2/motion/simulate" `
        -Method Post `
        -Headers $operatorHeaders
    Assert-True `
        (-not [string]::IsNullOrWhiteSpace($motion.recording.triggerEventId)) `
        "Simulated motion creates a persisted trigger event."
    $eventRecording = Wait-Recording `
        ([Guid]$motion.recording.id) `
        $operatorHeaders
    Assert-Equal $eventRecording.state "Completed" "Event recording completes automatically."
    Assert-Playable $eventRecording "Event recording"

    $continuous = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/cameras/camera-3/recordings/continuous/start" `
        -Method Post `
        -Headers $operatorHeaders
    Start-Sleep -Seconds 22
    Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/cameras/camera-3/recordings/stop" `
        -Method Post `
        -Headers $operatorHeaders | Out-Null
    $allContinuousRows = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/recordings?cameraId=camera-3&take=100" `
        -Headers $operatorHeaders
    $continuousRows = @($allContinuousRows) | Where-Object {
            $_.mode -eq "Continuous" `
                -and ([DateTimeOffset]$_.startedAt) -ge `
                    ([DateTimeOffset]$continuous.recording.startedAt)
        }
    Assert-True `
        (@($continuousRows).Count -ge 2) `
        "Continuous recording creates at least two finalized segments."
    foreach ($segment in $continuousRows) {
        Assert-Equal $segment.state "Completed" "Continuous segment finalizes successfully."
        Assert-Playable $segment "Continuous segment"
    }

    $camera4Original = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/cameras/manage/camera-4" `
        -Headers $adminHeaders
    $brokenCamera = @{
        name = $camera4Original.name
        location = $camera4Original.location
        rtspUrl = "rtsp://mediamtx:8554/step-6-missing-source"
        hlsPath = $camera4Original.hlsUrl
        groupId = $camera4Original.group.id
    } | ConvertTo-Json
    Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/cameras/camera-4" `
        -Method Put `
        -Headers $adminHeaders `
        -ContentType "application/json" `
        -Body $brokenCamera | Out-Null
    $failedStart = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/cameras/camera-4/recordings/manual/start" `
        -Method Post `
        -Headers $operatorHeaders
    $failedRecording = Wait-Recording `
        ([Guid]$failedStart.recording.id) `
        $operatorHeaders
    Assert-Equal $failedRecording.state "Failed" "A real FFmpeg source failure is persisted."
    $dashboard = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/command-center" `
        -Headers $operatorHeaders
    Assert-True `
        ($failedRecording.id -in @(
            $dashboard.recordingFailures |
                Where-Object cameraId -eq "camera-4" |
                ForEach-Object id
        ) -or @($dashboard.recordingFailures | Where-Object cameraId -eq "camera-4").Count -gt 0) `
        "Recording failure is visible in the command center."
}
finally {
    if ($camera4Original) {
        try {
            $restoreCamera = @{
                name = $camera4Original.name
                location = $camera4Original.location
                rtspUrl = $camera4Original.rtspUrl
                hlsPath = $camera4Original.hlsUrl
                groupId = $camera4Original.group.id
            } | ConvertTo-Json
            Invoke-RestMethod `
                -Uri "$ApiBaseUrl/api/cameras/camera-4" `
                -Method Put `
                -Headers $adminHeaders `
                -ContentType "application/json" `
                -Body $restoreCamera | Out-Null
            Invoke-RestMethod `
                -Uri "$ApiBaseUrl/api/cameras/camera-4/test-connection" `
                -Method Post `
                -Headers $operatorHeaders | Out-Null
            Write-Pass "Camera 4 source was restored and reconnected."
        }
        catch {
            Write-Warning "Camera 4 could not be restored automatically: $($_.Exception.Message)"
        }
    }

    foreach ($session in $sessions) {
        try {
            Invoke-Logout $session.accessToken
        }
        catch {
            Write-Warning "A verification session could not be logged out."
        }
    }
}

Write-Host "Steps 5 and 6 live/recording verification completed successfully." -ForegroundColor Cyan
