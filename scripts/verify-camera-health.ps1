[CmdletBinding()]
param(
    [string]$ApiBaseUrl = "http://localhost:8080"
)

$ErrorActionPreference = "Stop"

function Write-Pass {
    param([string]$Message)
    Write-Host "[PASS] $Message" -ForegroundColor Green
}

function Assert-Equal {
    param(
        $Actual,
        $Expected,
        [string]$Message
    )

    if ($Actual -ne $Expected) {
        throw "$Message Expected '$Expected', received '$Actual'."
    }

    Write-Pass $Message
}

function Invoke-Login {
    param(
        [string]$Username,
        [string]$Password
    )

    return Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/auth/login" `
        -Method Post `
        -ContentType "application/json" `
        -Body (@{
            username = $Username
            password = $Password
        } | ConvertTo-Json)
}

function New-AuthHeaders {
    param([string]$AccessToken)
    return @{ Authorization = "Bearer $AccessToken" }
}

function Get-StatusCode {
    param(
        [string]$Path,
        [string]$Method = "GET",
        [hashtable]$Headers = @{},
        [string]$Body
    )

    try {
        $parameters = @{
            Uri = "$ApiBaseUrl$Path"
            Method = $Method
            Headers = $Headers
            UseBasicParsing = $true
            ErrorAction = "Stop"
        }
        if ($Body) {
            $parameters.Body = $Body
            $parameters.ContentType = "application/json"
        }

        $response = Invoke-WebRequest @parameters
        return [int]$response.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            return [int]$_.Exception.Response.StatusCode
        }
        throw
    }
}

$administrator = Invoke-Login "admin" "Admin123!"
$administratorHeaders = New-AuthHeaders $administrator.accessToken
$operator = Invoke-Login "operator" "Operator123!"
$operatorHeaders = New-AuthHeaders $operator.accessToken
$viewer = Invoke-Login "viewer" "Viewer123!"
$viewerHeaders = New-AuthHeaders $viewer.accessToken

$managedCameras = Invoke-RestMethod `
    -Uri "$ApiBaseUrl/api/cameras/manage" `
    -Headers $administratorHeaders
Assert-Equal $managedCameras.Count 4 "Four persisted demo cameras are available to Administrators."

$cameraGroups = Invoke-RestMethod `
    -Uri "$ApiBaseUrl/api/camera-groups" `
    -Headers $administratorHeaders
if ($cameraGroups.Count -lt 2) {
    throw "Expected at least the Perimeter and Operations camera groups."
}
Write-Pass "Camera groups are persisted and returned by the API."

$viewerCameras = Invoke-RestMethod `
    -Uri "$ApiBaseUrl/api/cameras" `
    -Headers $viewerHeaders
Assert-Equal `
    (@($viewerCameras | ForEach-Object { $_.id }) -join ",") `
    "camera-1,camera-2" `
    "Viewer assignment filtering still limits camera access."

$viewerProbeStatus = Get-StatusCode `
    -Path "/api/cameras/camera-1/test-connection" `
    -Method "POST" `
    -Headers $viewerHeaders
Assert-Equal $viewerProbeStatus 403 "Viewers cannot run operational connection probes."

$operatorProbe = Invoke-RestMethod `
    -Uri "$ApiBaseUrl/api/cameras/camera-1/test-connection" `
    -Method Post `
    -Headers $operatorHeaders
Assert-Equal $operatorProbe.succeeded $true "Operator FFprobe connection test succeeds."
Assert-Equal $operatorProbe.resolution "640x360" "FFprobe reports the expected generated resolution."
Assert-Equal $operatorProbe.framesPerSecond 10 "FFprobe reports the expected generated FPS."

$allOnline = $false
for ($attempt = 0; $attempt -lt 10; $attempt++) {
    $managedCameras = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/cameras/manage" `
        -Headers $administratorHeaders
    $healthyCount = @(
        $managedCameras | Where-Object {
            $_.connectionStatus -eq "Online" -and $_.lastHeartbeatAt
        }
    ).Count
    if ($healthyCount -eq 4) {
        $allOnline = $true
        break
    }
    Start-Sleep -Seconds 3
}
Assert-Equal $allOnline $true "Background monitoring marks four cameras online with heartbeats."

$suffix = [Guid]::NewGuid().ToString("N").Substring(0, 8)
$verificationCameraId = "camera-verify-$suffix"
$createdGroupId = $null
$cameraCreated = $false

try {
    $group = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/camera-groups" `
        -Method Post `
        -Headers $administratorHeaders `
        -ContentType "application/json" `
        -Body (@{
            name = "Verification $suffix"
            description = "Temporary Step 3 verification group"
        } | ConvertTo-Json)
    $createdGroupId = $group.id

    $createdCamera = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/cameras" `
        -Method Post `
        -Headers $administratorHeaders `
        -ContentType "application/json" `
        -Body (@{
            id = $verificationCameraId
            name = "Verification Camera"
            location = "Automated test"
            rtspUrl = "rtsp://mediamtx:8554/missing-$suffix"
            hlsPath = "/$verificationCameraId/index.m3u8"
            groupId = $createdGroupId
            isEnabled = $true
        } | ConvertTo-Json)
    $cameraCreated = $true
    Assert-Equal $createdCamera.group.id $createdGroupId "Camera create persists its group assignment."

    $offlineProbe = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/cameras/$verificationCameraId/test-connection" `
        -Method Post `
        -Headers $administratorHeaders
    Assert-Equal $offlineProbe.succeeded $false "An unavailable RTSP source is detected as offline."
    Assert-Equal $offlineProbe.status "Offline" "Failed probe changes camera status to Offline."

    $updatedCamera = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/cameras/$verificationCameraId" `
        -Method Put `
        -Headers $administratorHeaders `
        -ContentType "application/json" `
        -Body (@{
            name = "Verification Camera"
            location = "Automated test"
            rtspUrl = "rtsp://mediamtx:8554/camera-1"
            hlsPath = "/$verificationCameraId/index.m3u8"
            groupId = $createdGroupId
        } | ConvertTo-Json)
    Assert-Equal $updatedCamera.connectionStatus "Offline" "Editing preserves the prior state until a fresh health check."

    $onlineProbe = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/cameras/$verificationCameraId/test-connection" `
        -Method Post `
        -Headers $administratorHeaders
    Assert-Equal $onlineProbe.succeeded $true "The repaired RTSP source reconnects."
    Assert-Equal $onlineProbe.status "Online" "Successful probe changes camera status to Online."

    $disabledCamera = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/cameras/$verificationCameraId/enabled" `
        -Method Patch `
        -Headers $administratorHeaders `
        -ContentType "application/json" `
        -Body (@{ isEnabled = $false } | ConvertTo-Json)
    Assert-Equal $disabledCamera.connectionStatus "Disabled" "Disable operation persists Disabled status."

    $enabledCamera = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/cameras/$verificationCameraId/enabled" `
        -Method Patch `
        -Headers $administratorHeaders `
        -ContentType "application/json" `
        -Body (@{ isEnabled = $true } | ConvertTo-Json)
    Assert-Equal $enabledCamera.connectionStatus "Unknown" "Enable operation schedules a fresh health check."

    $databaseQuery = @"
SELECT COUNT(*) FILTER (WHERE "Type" = 'CameraOffline'),
       COUNT(*) FILTER (WHERE "Type" = 'CameraReconnected')
FROM "SystemEvents"
WHERE "CameraId" = '$verificationCameraId';
"@
    $eventEvidence = $databaseQuery | docker compose exec -T postgres psql `
        -U vms `
        -d vms `
        --tuples-only `
        --no-align
    if ($LASTEXITCODE -ne 0) {
        throw "PostgreSQL event verification query failed."
    }
    Assert-Equal `
        $eventEvidence.Trim() `
        "1|1" `
        "Offline and reconnected transitions each persist one real system event."
}
finally {
    if ($cameraCreated) {
        Invoke-WebRequest `
            -Uri "$ApiBaseUrl/api/cameras/$verificationCameraId" `
            -Method Delete `
            -Headers $administratorHeaders `
            -UseBasicParsing | Out-Null
    }
    if ($createdGroupId) {
        Invoke-WebRequest `
            -Uri "$ApiBaseUrl/api/camera-groups/$createdGroupId" `
            -Method Delete `
            -Headers $administratorHeaders `
            -UseBasicParsing | Out-Null
    }
    $cleanupQuery = @"
DELETE FROM "SystemEvents"
WHERE "CameraId" = '$verificationCameraId';
"@
    $cleanupQuery | docker compose exec -T postgres psql `
        -U vms `
        -d vms `
        --quiet | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Temporary verification events could not be cleaned up."
    }
}

Write-Pass "Temporary camera, group, and transition events were cleaned up."
Write-Host "Step 3 camera-management verification completed successfully." -ForegroundColor Cyan
