#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$BaseUrl = 'http://127.0.0.1:5142',
    [switch]$VerifyLifecycle,
    [string]$ComposeProjectName,
    [string]$EfToolPath = './.local-tools/dotnet-ef'
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$script:step = 'configuración externa'
$repo = Split-Path $PSScriptRoot -Parent
$originalLocation = Get-Location
$savedEnvironment = @{}
$report = [ordered]@{}

function Assert-Check([bool]$Condition) {
    if (-not $Condition) { throw 'La comprobación no produjo el resultado esperado.' }
}

function Request-Api([string]$Path, [int]$Expected, $Body = $null, [string]$Token = '') {
    $options = @{ Uri = $BaseUrl.TrimEnd('/') + $Path; SkipHttpErrorCheck = $true; TimeoutSec = 30 }
    if ($null -ne $Body) {
        $options.Method = 'Post'
        $options.ContentType = 'application/json'
        $options.Body = $Body | ConvertTo-Json -Compress
    }
    if ($Token) { $options.Headers = @{ Authorization = "Bearer $Token" } }
    $response = Invoke-WebRequest @options
    Assert-Check ($response.StatusCode -eq $Expected)
    $report[$script:step] = [int]$response.StatusCode
    if ($response.Content -and $response.Headers['Content-Type'] -like '*json*') {
        return $response.Content | ConvertFrom-Json
    }
}

function Run-Native([string]$Executable, [string[]]$Arguments) {
    $captured = & $Executable @Arguments 2>&1 | Out-String
    Assert-Check ($LASTEXITCODE -eq 0)
}

function Wait-Api {
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        try {
            $response = Invoke-WebRequest ($BaseUrl.TrimEnd('/') + '/healthcheck') -TimeoutSec 2 -SkipHttpErrorCheck
            if ($response.StatusCode -eq 200) { return }
        } catch { }
        Start-Sleep -Seconds 1
    }
    throw 'La API no volvió a estar disponible.'
}

try {
    Set-Location $repo
    $adminUsername = $env:ICS_SMOKE_ADMIN_USERNAME
    $adminPassword = $env:ICS_SMOKE_ADMIN_PASSWORD
    $clientPassword = $env:ICS_SMOKE_CLIENT_PASSWORD
    Assert-Check (-not [string]::IsNullOrWhiteSpace($adminUsername))
    Assert-Check (-not [string]::IsNullOrWhiteSpace($adminPassword))
    Assert-Check (-not [string]::IsNullOrWhiteSpace($clientPassword))
    if ($VerifyLifecycle) { Assert-Check (-not [string]::IsNullOrWhiteSpace($ComposeProjectName)) }
    $runId = [guid]::NewGuid().ToString('N').Substring(0, 12)
    $sku = "SMOKE-$runId"
    $clientUsername = "smoke-$runId"

    $script:step = 'healthcheck'
    $null = Request-Api '/healthcheck' 200
    $script:step = 'swagger-ui'
    $null = Request-Api '/swagger/index.html' 200
    $script:step = 'openapi'
    $openapi = Request-Api '/swagger/v1/swagger.json' 200
    Assert-Check ($null -ne $openapi.paths)
    $script:step = 'catálogo-sin-coincidencias'
    $empty = Request-Api "/api/products?search=$runId" 200
    Assert-Check ($null -ne $empty.items -and $empty.total -eq 0 -and $empty.items.Count -eq 0)
    $script:step = 'login-admin'
    $admin = Request-Api '/api/auth/login' 200 @{ username = $adminUsername; password = $adminPassword }
    Assert-Check ($admin.role -eq 'admin' -and $admin.token)

    $script:step = 'registro-cliente'
    $null = Request-Api '/api/auth/register' 200 @{
        username = $clientUsername; password = $clientPassword; email = "$clientUsername@example.invalid"
        firstName = 'Smoke'; lastName = 'Test'; phoneNumber = '123456789'; address = 'Local test'
    }
    $script:step = 'login-cliente'
    $client = Request-Api '/api/auth/login' 200 @{ username = $clientUsername; password = $clientPassword }
    Assert-Check ($client.role -eq 'user' -and $client.token)
    $product = @{ sku = $sku; internalCode = $sku; name = $sku; description = 'Synthetic local smoke test'; currentUnitPrice = 10; stockQuantity = 1 }
    $script:step = 'creación-sin-token'
    $null = Request-Api '/api/products' 401 $product
    $script:step = 'creación-cliente'
    $null = Request-Api '/api/products' 403 $product $client.token
    $script:step = 'rechazos-sin-persistencia'
    $before = Request-Api "/api/products?search=$sku" 200
    Assert-Check ($before.total -eq 0)
    $script:step = 'creación-admin'
    $created = Request-Api '/api/products' 201 $product $admin.token
    $script:step = 'listado-público'
    $page = Request-Api "/api/products?search=$sku" 200
    Assert-Check ($page.total -eq 1 -and $page.items[0].id -eq $created.id)

    if ($VerifyLifecycle) {
        $script:step = 'configuración-del-proyecto'
        $rawConfig = & docker compose --project-name $ComposeProjectName config --format json 2>$null | Out-String
        Assert-Check ($LASTEXITCODE -eq 0)
        $config = $rawConfig | ConvertFrom-Json
        # Require running API/SQL services in the selected project before changing its lifecycle.
        foreach ($service in @('api', 'sqlserver')) {
            $containerId = & docker compose --project-name $ComposeProjectName ps --quiet $service 2>$null
            Assert-Check ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($containerId))
        }
        $publishedApiPort = $config.services.api.ports[0].published
        $baseUri = [uri]$BaseUrl
        Assert-Check ($baseUri.Host -in @('localhost', '127.0.0.1') -and $baseUri.Port -eq [int]$publishedApiPort)
        $runtime = $config.services.api.environment
        $sqlPort = $config.services.sqlserver.ports[0].published
        $overrides = @{
            ASPNETCORE_ENVIRONMENT = 'Production'
            ConnectionStrings__DefaultConnection = $runtime.ConnectionStrings__DefaultConnection.Replace('Server=sqlserver,1433;', "Server=127.0.0.1,$sqlPort;")
            Jwt__Key = $runtime.Jwt__Key; Jwt__Issuer = $runtime.Jwt__Issuer
            Jwt__Audience = $runtime.Jwt__Audience; Jwt__ExpireInMinutes = $runtime.Jwt__ExpireInMinutes
            Seed__Admin__Enabled = 'true'; Seed__Admin__Username = $adminUsername
            Seed__Admin__Email = $admin.email; Seed__Admin__Password = $adminPassword
        }
        foreach ($name in $overrides.Keys) {
            $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
            [Environment]::SetEnvironmentVariable($name, $overrides[$name], 'Process')
        }
        $script:step = 'inicialización-repetida'
        Run-Native $EfToolPath @('database', 'update', '--project', 'Dsw2025Tpi.Data', '--startup-project', 'Dsw2025Tpi.Api', '--configuration', 'Release', '--no-build')
        $script:step = 'reinicio-con-volumen'
        Run-Native 'docker' @('compose', '--project-name', $ComposeProjectName, 'stop', 'api', 'sqlserver')
        Run-Native 'docker' @('compose', '--project-name', $ComposeProjectName, 'up', '-d', 'api')
        Wait-Api
        $script:step = 'admin-conservado'
        $afterAdmin = Request-Api '/api/auth/login' 200 @{ username = $adminUsername; password = $adminPassword }
        Assert-Check ($afterAdmin.id -eq $admin.id -and $afterAdmin.role -eq 'admin')
        $script:step = 'cliente-conservado'
        $afterClient = Request-Api '/api/auth/login' 200 @{ username = $clientUsername; password = $clientPassword }
        Assert-Check ($afterClient.id -eq $client.id -and $afterClient.role -eq 'user')
        $script:step = 'producto-conservado'
        $afterPage = Request-Api "/api/products?search=$sku" 200
        Assert-Check ($afterPage.total -eq 1 -and $afterPage.items[0].id -eq $created.id)
    }
    $report['lifecycleVerified'] = [bool]$VerifyLifecycle
    $report | ConvertTo-Json
} catch {
    Write-Host "Falló la comprobación: $script:step. No se muestran respuestas ni credenciales." -ForegroundColor Red
    exit 1
} finally {
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process')
    }
    Set-Location $originalLocation
}
