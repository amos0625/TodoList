param([ValidateSet('api','web','migrate','test','publish')][string]$Task = 'api')
$ErrorActionPreference = 'Stop'
$workspace = Split-Path $PSScriptRoot -Parent
Set-Location $workspace
if (Test-Path "$workspace/.tools/dotnet/dotnet.exe") {
    $env:DOTNET_ROOT = "$workspace/.tools/dotnet"
    $env:DOTNET_CLI_HOME = "$workspace/.tools/cli"
    $env:NUGET_PACKAGES = "$workspace/.tools/nuget"
    $env:PATH = "$env:DOTNET_ROOT;$env:PATH"
}
if (Test-Path "$workspace/.tools/node-v24.21.0-win-x64") { $env:PATH = "$workspace/.tools/node-v24.21.0-win-x64;$env:PATH" }
function Check-Exit { if ($LASTEXITCODE -ne 0) { throw "Command failed with exit code $LASTEXITCODE" } }
switch ($Task) {
    'migrate' { dotnet run --project apps/api -- --migrate; Check-Exit }
    'api' {
        dotnet run --project apps/api -- --migrate; Check-Exit
        $env:ASPNETCORE_URLS = 'http://127.0.0.1:5050'
        dotnet run --project apps/api --no-build; Check-Exit
    }
    'web' { npm.cmd --prefix apps/web run dev; Check-Exit }
    'test' {
        dotnet format --verify-no-changes --no-restore; Check-Exit
        dotnet build --configuration Release --no-restore; Check-Exit
        dotnet test --configuration Release --no-build; Check-Exit
        npm.cmd --prefix apps/web run lint; Check-Exit
        npm.cmd --prefix apps/web run build; Check-Exit
        npm.cmd --prefix apps/web test; Check-Exit
    }
    'publish' {
        npm.cmd --prefix apps/web run build; Check-Exit
        dotnet publish apps/api --configuration Release --output artifacts/publish; Check-Exit
        New-Item -ItemType Directory -Force artifacts/publish/wwwroot | Out-Null
        Copy-Item apps/web/dist/* artifacts/publish/wwwroot -Recurse -Force
    }
}
