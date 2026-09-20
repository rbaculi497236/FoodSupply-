param([string]$DumpExecutable = 'C:\xampp\mysql\bin\mysqldump.exe')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Data
$configuration = Get-Content (Join-Path $PSScriptRoot '../appsettings.json') -Raw | ConvertFrom-Json
$connection = $configuration.ConnectionStrings.DefaultConnection
$developmentFile = Join-Path $PSScriptRoot '../appsettings.Development.json'
if (Test-Path $developmentFile) {
    $development = Get-Content $developmentFile -Raw | ConvertFrom-Json
    if ($development.ConnectionStrings.DefaultConnection) { $connection = $development.ConnectionStrings.DefaultConnection }
}
if ($env:ConnectionStrings__DefaultConnection) { $connection = $env:ConnectionStrings__DefaultConnection }
$settings = New-Object System.Data.Common.DbConnectionStringBuilder
$settings.set_ConnectionString($connection)
function Get-Setting($keys, $fallback) {
    foreach ($key in $keys) { if ($settings.ContainsKey($key)) { return [string]$settings[$key] } }
    return $fallback
}
function Quote-Option([string]$value) {
    return '"' + $value.Replace('\', '\\').Replace('"', '\"').Replace("`r", '\r').Replace("`n", '\n') + '"'
}
$database = Get-Setting @('Database','Initial Catalog') ''
if (!$database -or !(Test-Path -LiteralPath $DumpExecutable)) { throw 'Database name or mysqldump executable unavailable.' }
$directory = Join-Path $PSScriptRoot '../artifacts/backups'
New-Item -ItemType Directory -Force -Path $directory | Out-Null
$backup = Join-Path (Resolve-Path $directory) ('foodsupply-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.sql')
$optionFile = Join-Path ([IO.Path]::GetTempPath()) ('foodsupply-dump-' + [guid]::NewGuid().ToString('N') + '.cnf')
try {
    $options = @('[client]',
        ('host=' + (Quote-Option (Get-Setting @('Server','Host','Data Source') 'localhost'))),
        ('port=' + (Get-Setting @('Port') '3306')),
        ('user=' + (Quote-Option (Get-Setting @('User','User ID','Uid','Username') 'root'))),
        ('password=' + (Quote-Option (Get-Setting @('Password','Pwd') ''))))
    [IO.File]::WriteAllLines($optionFile, $options, (New-Object System.Text.UTF8Encoding($false)))
    & $DumpExecutable "--defaults-extra-file=$optionFile" '--single-transaction' '--routines' '--triggers' "--result-file=$backup" $database
    if ($LASTEXITCODE -ne 0) { throw 'Database backup failed. Do not run the upgrade.' }
    $file = Get-Item -LiteralPath $backup
    if ($file.Length -eq 0) { throw 'Database backup is empty. Do not run the upgrade.' }
    Write-Output "Backup completed: $($file.FullName) ($($file.Length) bytes)"
}
finally {
    if (Test-Path -LiteralPath $optionFile) { Remove-Item -LiteralPath $optionFile }
}
