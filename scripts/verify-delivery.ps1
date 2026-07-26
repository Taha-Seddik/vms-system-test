[CmdletBinding()]
param(
    [string]$ApiBaseUrl = "http://localhost:8080",
    [string]$FrontendBaseUrl = "http://localhost:3000",
    [string]$HlsBaseUrl = "http://localhost:8888"
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

function Get-HlsStatus {
    param([string]$CameraId, [string]$Token)
    $arguments = @(
        "-sS",
        "-o", "NUL",
        "-w", "%{http_code}",
        "-b", "cookieCheck=1"
    )
    if ($Token) {
        $arguments += @("-H", "Authorization: Bearer $Token")
    }
    $arguments += "$HlsBaseUrl/$CameraId/index.m3u8?cookieCheck=1"
    [int](& curl.exe @arguments)
}

function Invoke-Logout {
    param([string]$Token)
    Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/auth/logout" `
        -Method Post `
        -Headers (New-AuthHeaders $Token) | Out-Null
}

$sessions = @()
$temporaryCameraId = "camera-step10-secret-test"
$temporaryCameraCreated = $false

try {
    $administrator = Invoke-Login "admin" "Admin123!"
    $sessions += $administrator
    $operator = Invoke-Login "operator" "Operator123!"
    $sessions += $operator
    $viewer = Invoke-Login "viewer" "Viewer123!"
    $sessions += $viewer

    $openApiResponse = Invoke-WebRequest `
        -Uri "$ApiBaseUrl/openapi/v1.json" `
        -UseBasicParsing
    Assert-Equal $openApiResponse.StatusCode 200 `
        "OpenAPI document is available."
    Assert-True `
        ($openApiResponse.Content -match '"/api/cameras"' `
            -and $openApiResponse.Content -match '"Bearer"') `
        "OpenAPI documents VMS routes and JWT authentication."

    $swaggerResponse = Invoke-WebRequest `
        -Uri "$ApiBaseUrl/swagger/index.html" `
        -UseBasicParsing
    Assert-Equal $swaggerResponse.StatusCode 200 `
        "Interactive Swagger UI is available."

    Assert-Equal (Get-HlsStatus "camera-1" "") 401 `
        "Anonymous HLS access is rejected."
    Assert-Equal (Get-HlsStatus "camera-1" $viewer.accessToken) 200 `
        "Viewer can read an assigned HLS camera."
    Assert-Equal (Get-HlsStatus "camera-3" $viewer.accessToken) 401 `
        "Viewer cannot bypass an unassigned HLS camera URL."
    Assert-Equal (Get-HlsStatus "camera-3" $operator.accessToken) 200 `
        "Operator can read an authorized HLS camera."

    $playlist = & curl.exe `
        -sS `
        -b "cookieCheck=1" `
        -H "Authorization: Bearer $($administrator.accessToken)" `
        "$HlsBaseUrl/camera-1/index.m3u8?cookieCheck=1" | Out-String
    Assert-True ($playlist -match "#EXTM3U") `
        "Authorized media response is a real HLS playlist."

    $probe = & docker compose exec -T camera-1 `
        ffprobe `
        -v error `
        -headers "Authorization: Bearer $($administrator.accessToken)`r`n" `
        -select_streams "v:0" `
        -show_entries "stream=codec_name,width,height,r_frame_rate" `
        -of json `
        "http://mediamtx:8888/camera-1/index.m3u8" | Out-String
    Assert-True `
        ($LASTEXITCODE -eq 0 -and $probe -match '"codec_name": "h264"') `
        "Authorized HLS contains decodable H.264 video."

    $corsHeaders = & curl.exe `
        -sS `
        -D - `
        -o NUL `
        -X OPTIONS `
        -H "Origin: http://localhost:3000" `
        -H "Access-Control-Request-Method: GET" `
        -H "Access-Control-Request-Headers: authorization" `
        "$HlsBaseUrl/camera-1/index.m3u8" | Out-String
    Assert-True `
        ($corsHeaders -match "Access-Control-Allow-Origin: http://localhost:3000" `
            -and $corsHeaders -match "Access-Control-Allow-Headers: Authorization, Range") `
        "MediaMTX CORS permits the frontend origin and JWT header."

    $mediaContainer =
        docker compose ps mediamtx --format json | ConvertFrom-Json
    $publishedMediaPorts = @(
        $mediaContainer.Publishers | ForEach-Object TargetPort
    )
    Assert-True `
        ($publishedMediaPorts -notcontains 8554) `
        "RTSP is not published outside the Docker network."
    Assert-True `
        ($publishedMediaPorts -notcontains 9997 `
            -and $publishedMediaPorts -notcontains 9998) `
        "MediaMTX control and metrics ports are internal-only."

    $dashboard = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/command-center" `
        -Headers (New-AuthHeaders $administrator.accessToken)
    Assert-True `
        ($dashboard.cameraHealth.Count -eq 4 `
            -and @($dashboard.cameraHealth | Where-Object {
                $_.hlsUrl -match "/index\.m3u8$"
            }).Count -eq 4) `
        "Command Center supplies four authenticated live-wall sources."

    $apiHeaders = (Invoke-WebRequest `
        -Uri "$ApiBaseUrl/health" `
        -UseBasicParsing).Headers
    $frontendHeaders = (Invoke-WebRequest `
        -Uri "$FrontendBaseUrl/" `
        -UseBasicParsing).Headers
    Assert-True `
        ($apiHeaders["X-Content-Type-Options"] -eq "nosniff" `
            -and $frontendHeaders["X-Content-Type-Options"] -eq "nosniff") `
        "API and frontend return basic security headers."

    $adminHeaders = New-AuthHeaders $administrator.accessToken
    try {
        Invoke-RestMethod `
            -Uri "$ApiBaseUrl/api/cameras/$temporaryCameraId" `
            -Method Delete `
            -Headers $adminHeaders | Out-Null
    }
    catch {
        # A missing cleanup target is the expected state.
    }

    $sourceUser = "step10-user"
    $sourcePassword = "step10-camera-secret"
    $sourceUrl =
        "rtsp://${sourceUser}:${sourcePassword}@mediamtx:8554/camera-1"
    $createdCamera = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/cameras" `
        -Method Post `
        -Headers $adminHeaders `
        -ContentType "application/json" `
        -Body (@{
            id = $temporaryCameraId
            name = "Step 10 Credential Test"
            location = "Secure test"
            rtspUrl = $sourceUrl
            hlsPath = "/$temporaryCameraId/index.m3u8"
            groupId = $null
            isEnabled = $false
        } | ConvertTo-Json)
    $temporaryCameraCreated = $true
    Assert-True `
        ($createdCamera.rtspUrl -notmatch $sourceUser `
            -and $createdCamera.rtspUrl -notmatch $sourcePassword `
            -and $createdCamera.rtspUrl -match "\*\*\*") `
        "Camera management responses redact RTSP credentials."

    $updatedCamera = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/cameras/$temporaryCameraId" `
        -Method Put `
        -Headers $adminHeaders `
        -ContentType "application/json" `
        -Body (@{
            name = "Step 10 Credential Test Updated"
            location = "Secure test"
            rtspUrl = $createdCamera.rtspUrl
            hlsPath = $createdCamera.hlsUrl
            groupId = $null
        } | ConvertTo-Json)
    Assert-Equal $updatedCamera.name "Step 10 Credential Test Updated" `
        "A redacted camera can be edited without replacing its secret."

    $sourceQuery =
        'SELECT "RtspUrl" FROM "Cameras" WHERE "Id"=' `
        + "'$temporaryCameraId';"
    $storedSource = ($sourceQuery | & docker compose exec -T postgres `
        psql `
        -U vms `
        -d vms `
        -tA).Trim()
    Assert-Equal $storedSource $sourceUrl `
        "Redacted updates preserve the stored RTSP source."

    $systemInfo = Invoke-RestMethod -Uri "$ApiBaseUrl/api/system/info"
    Assert-Equal $systemInfo.implementedStep 10 `
        "System metadata reports completed Step 10."
}
finally {
    if ($temporaryCameraCreated -and $administrator) {
        try {
            Invoke-RestMethod `
                -Uri "$ApiBaseUrl/api/cameras/$temporaryCameraId" `
                -Method Delete `
                -Headers (New-AuthHeaders $administrator.accessToken) | Out-Null
            Write-Pass "Temporary credential-test camera was removed."
        }
        catch {
            Write-Warning "Temporary credential-test camera could not be removed."
        }
    }

    foreach ($session in $sessions) {
        try {
            Invoke-Logout $session.accessToken
        }
        catch {
            Write-Warning "A delivery-verification session could not be logged out."
        }
    }
}

Write-Host "Step 10 delivery verification completed successfully."
