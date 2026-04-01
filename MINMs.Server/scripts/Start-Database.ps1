# Запускает MySQL через docker compose. SQL из Database/Migrations выполняются автоматически
# только при первом создании пустого тома mysql_data. Чтобы снова накатить скрипты: -Reset

param(
    [switch]$Reset
)

$ErrorActionPreference = 'Stop'
$composeDir = Split-Path -Parent $PSScriptRoot

Push-Location $composeDir
try {
    if ($Reset) {
        Write-Host 'Removing mysql volume and container...'
        docker compose down -v --remove-orphans
    }

    docker compose up -d mysql

    $deadline = (Get-Date).AddMinutes(2)
    do {
        $status = docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' messenger_mysql 2>$null
        if ($status -eq 'healthy') { break }
        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)

    if ($status -ne 'healthy') {
        docker compose logs mysql
        throw "MySQL health is '$status' (expected healthy)."
    }

    Write-Host 'MySQL is healthy. Init scripts in docker-entrypoint-initdb.d run only on first empty volume; use -Reset to re-apply.'
}
finally {
    Pop-Location
}
