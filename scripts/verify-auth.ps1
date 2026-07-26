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

function Invoke-Login {
    param(
        [string]$Username,
        [string]$Password
    )

    $body = @{
        username = $Username
        password = $Password
    } | ConvertTo-Json

    return Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/auth/login" `
        -Method Post `
        -ContentType "application/json" `
        -Body $body
}

function New-AuthHeaders {
    param([string]$AccessToken)
    return @{ Authorization = "Bearer $AccessToken" }
}

$anonymousStatus = Get-StatusCode -Path "/api/auth/me"
Assert-Equal $anonymousStatus 401 "Anonymous requests are rejected."

$invalidBody = @{ username = "admin"; password = "incorrect" } | ConvertTo-Json
$invalidStatus = Get-StatusCode `
    -Path "/api/auth/login" `
    -Method "POST" `
    -Body $invalidBody
Assert-Equal $invalidStatus 401 "Invalid credentials are rejected."

$administrator = Invoke-Login "admin" "Admin123!"
$administratorHeaders = New-AuthHeaders $administrator.accessToken
Assert-Equal $administrator.user.role "Administrator" "Administrator login returns the Administrator role."
$administratorCameras = Invoke-RestMethod `
    -Uri "$ApiBaseUrl/api/cameras/accessible" `
    -Headers $administratorHeaders
Assert-Equal $administratorCameras.Count 4 "Administrator can access all four cameras."
$adminStatus = Get-StatusCode `
    -Path "/api/access/admin" `
    -Headers $administratorHeaders
Assert-Equal $adminStatus 200 "Administrator-only API policy allows the Administrator."

$operator = Invoke-Login "operator" "Operator123!"
$operatorHeaders = New-AuthHeaders $operator.accessToken
$operatorAdminStatus = Get-StatusCode `
    -Path "/api/access/admin" `
    -Headers $operatorHeaders
Assert-Equal $operatorAdminStatus 403 "Administrator-only API policy rejects the Operator."
$operatorStatus = Get-StatusCode `
    -Path "/api/access/operator" `
    -Headers $operatorHeaders
Assert-Equal $operatorStatus 200 "Operator policy allows the Operator."

$viewer = Invoke-Login "viewer" "Viewer123!"
$viewerHeaders = New-AuthHeaders $viewer.accessToken
$viewerCameras = Invoke-RestMethod `
    -Uri "$ApiBaseUrl/api/cameras/accessible" `
    -Headers $viewerHeaders
$viewerIds = @($viewerCameras | ForEach-Object { $_.id }) -join ","
Assert-Equal $viewerIds "camera-1,camera-2" "Viewer receives only assigned cameras."
$viewerOperatorStatus = Get-StatusCode `
    -Path "/api/access/operator" `
    -Headers $viewerHeaders
Assert-Equal $viewerOperatorStatus 403 "Operator policy rejects the Viewer."

$logoutStatus = Get-StatusCode `
    -Path "/api/auth/logout" `
    -Method "POST" `
    -Headers $viewerHeaders
Assert-Equal $logoutStatus 204 "Logout succeeds."
$revokedStatus = Get-StatusCode `
    -Path "/api/auth/me" `
    -Headers $viewerHeaders
Assert-Equal $revokedStatus 401 "A logged-out token is revoked server-side."

$activity = Invoke-RestMethod `
    -Uri "$ApiBaseUrl/api/auth/activity" `
    -Headers $administratorHeaders
$activityTypes = @($activity.recentEvents | ForEach-Object { $_.type })
if ("UserLogin" -notin $activityTypes -or "UserLogout" -notin $activityTypes) {
    throw "Administrator activity did not include both login and logout events."
}
Write-Pass "Administrator activity contains login and logout events."

$databaseQuery = @'
SELECT COUNT(*) FILTER (
    WHERE "PasswordHash" IN ('Admin123!', 'Operator123!', 'Viewer123!')
), (
    SELECT COUNT(*)
    FROM "UserCameraAssignments"
    WHERE "UserId" = '10000000-0000-0000-0000-000000000003'
)
FROM "Users";
'@
$databaseEvidence = $databaseQuery | docker compose exec -T postgres psql `
    -U vms `
    -d vms `
    --tuples-only `
    --no-align
if ($LASTEXITCODE -ne 0) {
    throw "PostgreSQL verification query failed."
}
Assert-Equal $databaseEvidence.Trim() "0|2" "Passwords are hashed and the demo Viewer has two persisted assignments."

Write-Host "Step 2 authentication verification completed successfully." -ForegroundColor Cyan
