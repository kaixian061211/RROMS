param(
    [string]$SharedSecretsPath,
    [string]$LocalSecretsPath,
    [switch]$Check
)

$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($SharedSecretsPath)) {
    $SharedSecretsPath = Join-Path $projectDirectory 'secrets.shared.json'
}
if ([string]::IsNullOrWhiteSpace($LocalSecretsPath)) {
    $LocalSecretsPath = Join-Path $projectDirectory 'secrets.local.json'
}

$sharedNames = @(
    'Stripe__SecretKey', 'Stripe__WebhookSecret', 'Stripe__Currency',
    'Stripe__CheckoutExpiryMinutes', 'Smtp__Host', 'Smtp__Port',
    'Smtp__TimeoutSeconds', 'Smtp__UserName', 'Smtp__Password',
    'Smtp__FromName', 'Smtp__FromEmail', 'Notifications__WebPush__AppId',
    'Notifications__WebPush__ApiKey', 'Notifications__WebPush__AliasSecret',
    'Notifications__WebPush__PublicBaseUrl'
)
$localNames = $sharedNames + @(
    'ConnectionStrings__DefaultConnection', 'DefaultAdmin__Email', 'DefaultAdmin__Password'
)

function Read-SettingsFile([string]$Path, [string[]]$AllowedNames) {
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Missing $Path. Follow the local setup steps in README.md."
    }
    try {
        $document = Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
    }
    catch {
        # JSON parser errors can include input containing credentials.
        throw "Cannot read $Path. Ensure it contains valid JSON."
    }
    if ($document -isnot [System.Management.Automation.PSCustomObject]) {
        throw "Expected a JSON object in $Path."
    }

    $values = @{}
    foreach ($property in $document.PSObject.Properties) {
        if ($AllowedNames -notcontains $property.Name) {
            throw "Unsupported setting in $Path. Keep database and administrator settings in secrets.local.json."
        }
        if ($null -ne $property.Value -and $property.Value -isnot [string]) {
            throw "Setting values in $Path must be strings."
        }
        $values[$property.Name] = [string]$property.Value
    }
    return $values
}

$sharedSettings = Read-SettingsFile $SharedSecretsPath $sharedNames
$localSettings = Read-SettingsFile $LocalSecretsPath $localNames
$databaseConnection = $localSettings['ConnectionStrings__DefaultConnection']
if ([string]::IsNullOrWhiteSpace($databaseConnection) -or
    $databaseConnection -match 'YOUR_SQL_SERVER|YOUR_DATABASE') {
    throw 'Set your own ConnectionStrings__DefaultConnection in secrets.local.json before starting.'
}

# Each launch uses the files rather than stale settings inherited from an IDE.
# Optional blank values disable the corresponding environment overrides.
$launchSettings = @{}
foreach ($name in $localNames) { $launchSettings[$name] = '' }
foreach ($name in $sharedSettings.Keys) { $launchSettings[$name] = $sharedSettings[$name] }
foreach ($name in $localSettings.Keys) { $launchSettings[$name] = $localSettings[$name] }

$previousSettings = @{}
try {
    foreach ($name in $launchSettings.Keys) {
        $previousSettings[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        [Environment]::SetEnvironmentVariable($name, $launchSettings[$name], 'Process')
    }
    if ($Check) {
        Write-Host 'Secrets files loaded; your local database connection is configured. No app was started.'
    }
    else {
        & dotnet run --project (Join-Path $projectDirectory 'PaymentModule.csproj') --launch-profile https
        if ($LASTEXITCODE -ne 0) { throw "The application exited with code $LASTEXITCODE." }
    }
}
finally {
    foreach ($name in $previousSettings.Keys) {
        [Environment]::SetEnvironmentVariable($name, $previousSettings[$name], 'Process')
    }
}
