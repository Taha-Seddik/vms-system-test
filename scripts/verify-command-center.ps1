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

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
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
        [hashtable]$Headers = @{}
    )

    try {
        $response = Invoke-WebRequest `
            -Uri "$ApiBaseUrl$Path" `
            -Method $Method `
            -Headers $Headers `
            -UseBasicParsing `
            -ErrorAction Stop
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
$operator = Invoke-Login "operator" "Operator123!"
$viewer = Invoke-Login "viewer" "Viewer123!"
$operatorHeaders = New-AuthHeaders $operator.accessToken
$viewerHeaders = New-AuthHeaders $viewer.accessToken

$viewerStatus = Get-StatusCode `
    -Path "/api/command-center" `
    -Headers $viewerHeaders
Assert-Equal $viewerStatus 403 "Viewer is excluded from the system-wide command center."

$anonymousHubStatus = Get-StatusCode `
    -Path "/hubs/command-center/negotiate?negotiateVersion=1" `
    -Method "POST"
Assert-Equal $anonymousHubStatus 401 "Anonymous SignalR negotiation is rejected."

$escapedToken = [Uri]::EscapeDataString($operator.accessToken)
$operatorHubStatus = Get-StatusCode `
    -Path "/hubs/command-center/negotiate?negotiateVersion=1&access_token=$escapedToken" `
    -Method "POST"
Assert-Equal $operatorHubStatus 200 "Operator SignalR query-token negotiation succeeds."

$dashboard = $null
for ($attempt = 0; $attempt -lt 12; $attempt++) {
    $dashboard = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/command-center" `
        -Headers $operatorHeaders
    if ($dashboard.metrics.onlineCameras -eq 4) {
        break
    }
    Start-Sleep -Seconds 5
}

Assert-Equal $dashboard.metrics.totalCameras 4 "Dashboard reports four persisted cameras."
Assert-Equal $dashboard.metrics.onlineCameras 4 "Dashboard reports four online cameras."
Assert-Equal $dashboard.metrics.offlineCameras 0 "Dashboard reports no offline cameras."
Assert-Equal $dashboard.metrics.activeLiveStreams 4 "Dashboard reports four probe-confirmed live streams."
Assert-Equal $dashboard.metrics.activeRecordings 0 "Dashboard reports no active recordings before Step 6."
Assert-True `
    ($dashboard.metrics.activeUsers -ge 3) `
    "Dashboard counts distinct recently active users."
Assert-True `
    ($dashboard.metrics.systemUptimeSeconds -gt 0) `
    "Dashboard reports positive API process uptime."
Assert-Equal $dashboard.cameraHealth.Count 4 "Camera-health panel contains all four cameras."
Assert-True `
    ($dashboard.storage.status -ne "Unavailable") `
    "Recording storage filesystem is available."
Assert-True `
    ($dashboard.storage.totalBytes -gt 0) `
    "Storage health reports real filesystem capacity."
Assert-True `
    (@($dashboard.operatorActivity).Count -gt 0) `
    "Operator activity includes recent Operator authentication."

$eventId = [Guid]::NewGuid()
try {
    $insertEvent = @"
INSERT INTO "SystemEvents"
    ("Id", "Type", "Timestamp", "CameraId", "Severity", "Description", "Status")
VALUES
    ('$eventId', 'RecordingFailure', NOW(), 'camera-1', 'Critical',
     'Temporary Step 4 recording failure verification.', 'Open');
"@
    $insertEvent | docker compose exec -T postgres psql `
        -U vms `
        -d vms `
        --quiet | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Temporary dashboard event could not be inserted."
    }

    $eventDashboard = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/command-center" `
        -Headers $operatorHeaders
    Assert-True `
        ($eventId.ToString() -in @(
            $eventDashboard.recordingFailures | ForEach-Object { $_.id }
        )) `
        "Recording failures panel returns persisted failure events."
    Assert-True `
        ($eventId.ToString() -in @(
            $eventDashboard.activeAlarms | ForEach-Object { $_.id }
        )) `
        "Open critical failures appear as active alarms."
    Assert-True `
        ($eventId.ToString() -in @(
            $eventDashboard.recentIncidents | ForEach-Object { $_.id }
        )) `
        "Operational failure events appear as recent incidents."
}
finally {
    $deleteEvent = @"
DELETE FROM "SystemEvents"
WHERE "Id" = '$eventId';
"@
    $deleteEvent | docker compose exec -T postgres psql `
        -U vms `
        -d vms `
        --quiet | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Temporary dashboard event could not be cleaned up."
    }
}
Write-Pass "Temporary command-center event was cleaned up."

$env:VMS_API_BASE_URL = $ApiBaseUrl
node .\frontend\scripts\verify-realtime.mjs
if ($LASTEXITCODE -ne 0) {
    throw "SignalR realtime smoke test failed."
}

Write-Host "Step 4 command-center verification completed successfully." -ForegroundColor Cyan
