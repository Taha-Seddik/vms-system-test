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
    if (-not $Condition) { throw $Message }
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
    param([string]$AccessToken)
    @{ Authorization = "Bearer $AccessToken" }
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
        [int]$response.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            return [int]$_.Exception.Response.StatusCode
        }
        throw
    }
}

$sessions = @()
$eventIds = 1..8 | ForEach-Object { [Guid]::NewGuid() }
$requiredTypes = @(
    "CameraOffline",
    "MotionDetected",
    "RecordingStarted",
    "RecordingStopped",
    "StorageFull",
    "CameraReconnected",
    "UserLogin",
    "UserLogout"
)

try {
    $admin = Invoke-Login "admin" "Admin123!"
    $operator = Invoke-Login "operator" "Operator123!"
    $viewer = Invoke-Login "viewer" "Viewer123!"
    $sessions += $admin, $operator, $viewer
    $operatorHeaders = New-AuthHeaders $operator.accessToken
    $viewerHeaders = New-AuthHeaders $viewer.accessToken

    Assert-Equal `
        (Get-StatusCode "/api/events" "GET" $viewerHeaders) `
        403 `
        "Viewer cannot access system-wide events."
    Assert-Equal `
        (Get-StatusCode "/api/events/$($eventIds[4])/close" "POST" $viewerHeaders) `
        403 `
        "Viewer cannot close an event."
    Assert-Equal `
        (Get-StatusCode "/api/events?from=2026-07-27T00:00:00Z&to=2026-07-26T00:00:00Z" "GET" $operatorHeaders) `
        400 `
        "Reversed event dates are rejected."

    $values = @(
        "('$($eventIds[0])', 'CameraOffline', NOW() - INTERVAL '8 seconds', 'camera-1', 'Warning', 'Step 8 Camera Offline verification.', 'Open')",
        "('$($eventIds[1])', 'MotionDetected', NOW() - INTERVAL '7 seconds', 'camera-1', 'Warning', 'Step 8 Motion Detected verification.', 'Open')",
        "('$($eventIds[2])', 'RecordingStarted', NOW() - INTERVAL '6 seconds', 'camera-1', 'Information', 'Step 8 Recording Started verification.', 'Closed')",
        "('$($eventIds[3])', 'RecordingStopped', NOW() - INTERVAL '5 seconds', 'camera-1', 'Information', 'Step 8 Recording Stopped verification.', 'Closed')",
        "('$($eventIds[4])', 'StorageFull', NOW() - INTERVAL '4 seconds', NULL, 'Critical', 'Step 8 Storage Full verification.', 'Open')",
        "('$($eventIds[5])', 'CameraReconnected', NOW() - INTERVAL '3 seconds', 'camera-1', 'Information', 'Step 8 Camera Reconnected verification.', 'Closed')",
        "('$($eventIds[6])', 'UserLogin', NOW() - INTERVAL '2 seconds', NULL, 'Information', 'Step 8 User Login verification.', 'Closed')",
        "('$($eventIds[7])', 'UserLogout', NOW() - INTERVAL '1 second', NULL, 'Information', 'Step 8 User Logout verification.', 'Closed')"
    )
    $insertSql = @"
INSERT INTO "SystemEvents"
    ("Id", "Type", "Timestamp", "CameraId", "Severity", "Description", "Status")
VALUES
    $($values -join ",`n    ");
"@
    $insertSql | docker compose exec -T postgres psql `
        -U vms -d vms --quiet | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Temporary Step 8 events could not be inserted."
    }

    $events = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/events?take=200" `
        -Headers $operatorHeaders
    foreach ($index in 0..7) {
        $event = @($events.items | Where-Object { $_.id -eq $eventIds[$index].ToString() })
        Assert-Equal $event.Count 1 "$($requiredTypes[$index]) is returned by the event API."
        Assert-True ($null -ne $event[0].timestamp) "$($requiredTypes[$index]) includes a timestamp."
        Assert-True (-not [string]::IsNullOrWhiteSpace($event[0].description)) "$($requiredTypes[$index]) includes a description."
        Assert-True ($null -ne $event[0].severity) "$($requiredTypes[$index]) includes severity."
        Assert-True ($null -ne $event[0].status) "$($requiredTypes[$index]) includes Open/Closed status."
    }

    $cameraEvent = $events.items | Where-Object { $_.id -eq $eventIds[0].ToString() }
    Assert-Equal $cameraEvent.cameraName "Entrance" "Camera-linked events include the camera identity."
    $loginEvent = $events.items | Where-Object { $_.id -eq $eventIds[6].ToString() }
    Assert-Equal $loginEvent.isIncident $false "Authentication activity is not classified as an incident."
    $storageEvent = $events.items | Where-Object { $_.id -eq $eventIds[4].ToString() }
    Assert-Equal $storageEvent.isIncident $true "Operational events are classified as incidents."
    Assert-Equal $storageEvent.isActiveAlarm $true "Open critical events are classified as active alarms."

    $filtered = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/events?type=StorageFull&severity=Critical&status=Open&take=200" `
        -Headers $operatorHeaders
    Assert-True `
        ($eventIds[4].ToString() -in @($filtered.items | ForEach-Object { $_.id })) `
        "Type, severity, and status filters return the matching Storage Full event."

    $details = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/events/$($eventIds[4])" `
        -Headers $operatorHeaders
    Assert-Equal $details.id $eventIds[4].ToString() "Event details return the selected event."

    $dashboard = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/command-center" `
        -Headers $operatorHeaders
    Assert-True `
        ($eventIds[4].ToString() -in @($dashboard.activeAlarms | ForEach-Object { $_.id })) `
        "The same open critical event appears in command-center active alarms."
    Assert-True `
        ($eventIds[4].ToString() -in @($dashboard.recentIncidents | ForEach-Object { $_.id })) `
        "The same operational event appears as a recent incident."

    $env:VMS_API_BASE_URL = $ApiBaseUrl
    $env:VMS_EVENT_ID = $eventIds[4].ToString()
    node .\frontend\scripts\verify-event-realtime.mjs
    if ($LASTEXITCODE -ne 0) {
        throw "Event SignalR verification failed."
    }

    $closed = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/events/$($eventIds[4])" `
        -Headers $operatorHeaders
    Assert-Equal $closed.status "Closed" "The close action persists Closed status."
    Assert-Equal $closed.isActiveAlarm $false "A closed event is removed from active-alarm classification."
}
finally {
    Remove-Item Env:VMS_EVENT_ID -ErrorAction SilentlyContinue
    $quotedIds = $eventIds | ForEach-Object { "'$_'" }
    $deleteSql = @"
DELETE FROM "SystemEvents"
WHERE "Id" IN ($($quotedIds -join ","));
"@
    $deleteSql | docker compose exec -T postgres psql `
        -U vms -d vms --quiet | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Pass "Temporary Step 8 events were cleaned up."
    }

    foreach ($session in $sessions) {
        try {
            Invoke-RestMethod `
                -Uri "$ApiBaseUrl/api/auth/logout" `
                -Method Post `
                -Headers (New-AuthHeaders $session.accessToken) | Out-Null
        }
        catch {
            Write-Warning "A verification session could not be logged out."
        }
    }
}

Write-Host "Step 8 event-management verification completed successfully." -ForegroundColor Cyan
