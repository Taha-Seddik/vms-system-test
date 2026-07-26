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
$temporaryUserId = $null
$username = "verify.step9.$([Guid]::NewGuid().ToString('N').Substring(0, 8))"

try {
    $admin = Invoke-Login "admin" "Admin123!"
    $operator = Invoke-Login "operator" "Operator123!"
    $viewer = Invoke-Login "viewer" "Viewer123!"
    $sessions += $admin, $operator, $viewer
    $adminHeaders = New-AuthHeaders $admin.accessToken
    $operatorHeaders = New-AuthHeaders $operator.accessToken
    $viewerHeaders = New-AuthHeaders $viewer.accessToken

    Assert-Equal `
        (Get-StatusCode "/api/users" "GET" $viewerHeaders) `
        403 `
        "Viewer cannot access Administrator user management."
    Assert-Equal `
        (Get-StatusCode "/api/audit-logs" "GET" $viewerHeaders) `
        403 `
        "Viewer cannot access audit logs."
    Assert-Equal `
        (Get-StatusCode "/api/search" "GET" $viewerHeaders) `
        403 `
        "Viewer cannot access system-wide search."
    Assert-Equal `
        (Get-StatusCode "/api/users" "GET" $operatorHeaders) `
        403 `
        "Operator cannot manage users."
    Assert-Equal `
        (Get-StatusCode "/api/audit-logs" "GET" $operatorHeaders) `
        403 `
        "Operator cannot read full audit logs."

    $existingUsers = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/users" `
        -Headers $adminHeaders
    Assert-True `
        (@($existingUsers).Count -ge 3) `
        "Administrator can list the seeded users."

    $invalidViewerStatus = Get-StatusCode `
        "/api/users" `
        "POST" `
        $adminHeaders
    Assert-True `
        ($invalidViewerStatus -in @(400, 415)) `
        "Malformed user creation is rejected."

    $createBody = @{
        username = $username
        displayName = "Step 9 Verification Viewer"
        password = "Viewer123!"
        role = "Viewer"
        assignedCameraIds = @("camera-3")
    } | ConvertTo-Json
    $created = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/users" `
        -Method Post `
        -Headers $adminHeaders `
        -ContentType "application/json" `
        -Body $createBody
    $temporaryUserId = $created.id
    Assert-Equal $created.role "Viewer" "Administrator created a Viewer role."
    Assert-Equal `
        @($created.assignedCameras).Count `
        1 `
        "Created Viewer has exactly one camera assignment."
    Assert-Equal `
        $created.assignedCameras[0].id `
        "camera-3" `
        "Created Viewer assignment is persisted."

    $filteredUsers = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/users?search=$([Uri]::EscapeDataString($username))&role=Viewer&isEnabled=true&cameraId=camera-3" `
        -Headers $adminHeaders
    Assert-True `
        ($temporaryUserId -in @($filteredUsers | ForEach-Object { $_.id })) `
        "User search, role, status, and camera filters find the account."

    $temporaryViewer = Invoke-Login $username "Viewer123!"
    $sessions += $temporaryViewer
    $temporaryViewerHeaders = New-AuthHeaders $temporaryViewer.accessToken
    $assigned = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/cameras/accessible" `
        -Headers $temporaryViewerHeaders
    Assert-Equal @($assigned).Count 1 "New Viewer receives only assigned cameras."
    Assert-Equal $assigned[0].id "camera-3" "Viewer cannot see an unassigned camera."

    $updateBody = @{
        displayName = "Step 9 Updated Viewer"
        role = "Viewer"
        isEnabled = $true
        assignedCameraIds = @("camera-4")
        newPassword = "Changed123!"
    } | ConvertTo-Json
    $updated = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/users/$temporaryUserId" `
        -Method Put `
        -Headers $adminHeaders `
        -ContentType "application/json" `
        -Body $updateBody
    Assert-Equal `
        $updated.assignedCameras[0].id `
        "camera-4" `
        "Administrator changed the Viewer camera assignment."
    Assert-Equal `
        (Get-StatusCode "/api/auth/me" "GET" $temporaryViewerHeaders) `
        401 `
        "Password change immediately revokes the previous session."

    $changedViewer = Invoke-Login $username "Changed123!"
    $sessions += $changedViewer
    $changedViewerHeaders = New-AuthHeaders $changedViewer.accessToken
    $changedAssigned = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/cameras/accessible" `
        -Headers $changedViewerHeaders
    Assert-Equal $changedAssigned[0].id "camera-4" "New password and assignment take effect."

    $operatorBody = @{
        displayName = "Step 9 Verification Operator"
        role = "Operator"
        isEnabled = $true
        assignedCameraIds = @()
        newPassword = $null
    } | ConvertTo-Json
    $promoted = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/users/$temporaryUserId" `
        -Method Put `
        -Headers $adminHeaders `
        -ContentType "application/json" `
        -Body $operatorBody
    Assert-Equal $promoted.role "Operator" "Administrator changed the user role."
    Assert-Equal `
        @($promoted.assignedCameras).Count `
        0 `
        "Non-Viewer roles do not retain camera assignments."

    $allSearch = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/search?take=20" `
        -Headers $adminHeaders
    Assert-True (@($allSearch.cameras).Count -gt 0) "Global search returns cameras."
    Assert-True (@($allSearch.recordings).Count -gt 0) "Global search returns recordings."
    Assert-True (@($allSearch.events).Count -gt 0) "Global search returns events."
    Assert-True (@($allSearch.users).Count -gt 0) "Administrator global search returns users."

    $cameraSearch = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/search?q=Entrance&cameraId=camera-1&take=20" `
        -Headers $adminHeaders
    Assert-True `
        ("camera-1" -in @($cameraSearch.cameras | ForEach-Object { $_.id })) `
        "Text and camera filters find Entrance."

    $eventSearch = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/search?eventType=UserLogin&status=Closed&take=20" `
        -Headers $adminHeaders
    Assert-True `
        (@($eventSearch.events).Count -gt 0) `
        "Event type and status filters return login events."

    $operatorSearch = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/search?q=admin" `
        -Headers $operatorHeaders
    Assert-Equal `
        @($operatorSearch.users).Count `
        0 `
        "Operator search does not expose user records."

    Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/cameras/camera-1/test-connection" `
        -Method Post `
        -Headers $operatorHeaders | Out-Null
    $audit = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/audit-logs?take=200" `
        -Headers $adminHeaders
    Assert-True `
        (@($audit.items | Where-Object {
            $_.actorUsername -eq "admin" -and
            $_.resourceType -eq "User" -and
            $_.action -in @("Created", "Updated")
        }).Count -ge 2) `
        "User create and update operations are audited."
    Assert-True `
        (@($audit.items | Where-Object {
            $_.actorUsername -eq "operator" -and
            $_.resourceType -eq "Camera" -and
            $_.action -eq "Executed" -and
            $_.resourceId -eq "camera-1"
        }).Count -ge 1) `
        "Operator camera action is audited with actor and resource."

    $dashboard = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/command-center" `
        -Headers $adminHeaders
    Assert-True `
        (@($dashboard.operatorActivity | Where-Object {
            $_.displayName -eq "Security Operator" -and
            $_.action -eq "Executed"
        }).Count -ge 1) `
        "Command center displays recent audited Operator activity."

    Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/users/$temporaryUserId" `
        -Method Delete `
        -Headers $adminHeaders | Out-Null
    $deletedStatus = Get-StatusCode `
        "/api/users/$temporaryUserId" `
        "GET" `
        $adminHeaders
    Assert-Equal $deletedStatus 404 "Administrator deleted the temporary user."
    $temporaryUserId = $null
}
finally {
    if ($temporaryUserId) {
        try {
            Invoke-RestMethod `
                -Uri "$ApiBaseUrl/api/users/$temporaryUserId" `
                -Method Delete `
                -Headers $adminHeaders | Out-Null
        }
        catch {
            Write-Warning "Temporary Step 9 user could not be removed."
        }
    }

    foreach ($session in $sessions) {
        try {
            Invoke-RestMethod `
                -Uri "$ApiBaseUrl/api/auth/logout" `
                -Method Post `
                -Headers (New-AuthHeaders $session.accessToken) | Out-Null
        }
        catch {
            # Revoked and deleted sessions are expected during this workflow.
        }
    }
}

Write-Host "Step 9 users, search, and audit verification completed successfully." -ForegroundColor Cyan
