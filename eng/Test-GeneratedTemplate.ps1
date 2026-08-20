[CmdletBinding()]
param(
    [string[]] $Frameworks = @('net8.0', 'net10.0'),
    [string] $ArtifactsPath = (Join-Path (Get-Location) 'artifacts'),
    [string] $PackagePath,
    [switch] $SkipPack,
    [switch] $KeepArtifacts
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Invoke-Dotnet {
    param([string] $WorkingDirectory, [Parameter(Mandatory)][string[]] $Arguments)
    Write-Host ("dotnet " + ($Arguments -join ' '))
    Push-Location -LiteralPath $WorkingDirectory
    try {
        & dotnet @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet command failed with exit code ${LASTEXITCODE}: $($Arguments -join ' ')"
        }
    }
    finally { Pop-Location }
}

function Invoke-DotnetCapture {
    param([string] $WorkingDirectory, [Parameter(Mandatory)][string[]] $Arguments)
    Write-Host ("dotnet " + ($Arguments -join ' '))
    Push-Location -LiteralPath $WorkingDirectory
    try {
        $output = & dotnet @Arguments 2>&1 | Out-String
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet command failed with exit code ${LASTEXITCODE}: $($Arguments -join ' ')`n$output"
        }
        return $output
    }
    finally { Pop-Location }
}

function Invoke-DotnetExpectExit {
    param(
        [string] $WorkingDirectory,
        [Parameter(Mandatory)][string[]] $Arguments,
        [int] $ExpectedExitCode,
        [hashtable] $Environment = @{}
    )
    $saved = @{}
    foreach ($name in $Environment.Keys) {
        $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        [Environment]::SetEnvironmentVariable($name, [string]$Environment[$name], 'Process')
    }
    Push-Location -LiteralPath $WorkingDirectory
    try {
        Write-Host ("dotnet " + ($Arguments -join ' '))
        & dotnet @Arguments
        if ($LASTEXITCODE -ne $ExpectedExitCode) {
            throw "Expected exit code $ExpectedExitCode but received ${LASTEXITCODE}: dotnet $($Arguments -join ' ')"
        }
    }
    finally {
        Pop-Location
        foreach ($name in $Environment.Keys) {
            [Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process')
        }
    }
}

function Assert-PathInside {
    param([string] $Path, [string] $Root, [string] $Description)
    $resolvedPath = ([IO.Path]::GetFullPath($Path)).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $resolvedRoot = ([IO.Path]::GetFullPath($Root)).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $inside = $resolvedPath.Equals($resolvedRoot, [StringComparison]::OrdinalIgnoreCase) -or
        $resolvedPath.StartsWith($resolvedRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
    if (-not $inside) {
        throw "$Description '$resolvedPath' is outside allowed root '$resolvedRoot'."
    }
    return $resolvedPath
}

function Remove-VerifiedDirectory {
    param([string] $Path, [string] $Root)
    if (-not (Test-Path -LiteralPath $Path)) { return }
    $resolved = Assert-PathInside $Path $Root 'cleanup target'
    $resolvedRoot = ([IO.Path]::GetFullPath($Root)).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    if ($resolved.Equals($resolvedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean the artifacts root '$resolved'."
    }
    $item = Get-Item -LiteralPath $resolved -Force
    if (-not $item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Refusing to clean non-directory or linked target '$resolved'."
    }
    $linkedItems = @(Get-ChildItem -LiteralPath $resolved -Force -Recurse | Where-Object {
        $_.Attributes -band [IO.FileAttributes]::ReparsePoint
    })
    if ($linkedItems.Count -gt 0) {
        throw "Refusing to clean '$resolved' because it contains reparse points: $($linkedItems.FullName -join ', ')"
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}

function Assert-FileContains {
    param([string] $Path, [string] $Text)
    if (-not (Select-String -LiteralPath $Path -Pattern $Text -SimpleMatch -Quiet)) {
        throw "Expected '$Text' in '$Path'."
    }
}

function Assert-NoVulnerablePackages {
    param([string] $WorkingDirectory, [string] $ProjectOrSolution)
    $jsonText = Invoke-DotnetCapture $WorkingDirectory @(
        'list', $ProjectOrSolution, 'package', '--vulnerable', '--include-transitive', '--format', 'json'
    )
    try { $report = $jsonText | ConvertFrom-Json -Depth 100 }
    catch { throw "Unable to parse vulnerability report as JSON.`n$jsonText" }
    $json = $report | ConvertTo-Json -Depth 100 -Compress
    if ($json -match '"vulnerabilities":\[(?!\])') {
        throw "Vulnerable packages were reported for '$ProjectOrSolution'.`n$jsonText"
    }
}

function Get-TemplateNames {
    param([string] $Hive)
    return Invoke-DotnetCapture $repo @('new', 'list', '--debug:custom-hive', $Hive)
}

function Assert-PackageBoundary {
    param([string] $Path)
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $entries = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
        foreach ($required in @(
            'README.md',
            'webapi/.template.config/template.json',
            'webapi/Directory.Build.props',
            'webapi/Directory.Packages.props',
            'webapi/global.json',
            'webapi/src/Modules/Guihua.Persistence.FreeSql/Guihua.Persistence.FreeSql.csproj',
            'module/.template.config/template.json',
            'module/ModuleName/ModuleName.csproj',
            'module/ModuleName.Tests/ModuleName.Tests.csproj',
            'tool/.template.config/template.json',
            'tool/content/src/YOU.Tool/YOU.Tool.csproj'
        )) {
            if ($entries -notcontains $required) { throw "Package is missing '$required'." }
        }
        foreach ($entry in $entries) {
            $allowed = $entry.StartsWith('webapi/', [StringComparison]::OrdinalIgnoreCase) -or
                $entry.StartsWith('module/', [StringComparison]::OrdinalIgnoreCase) -or
                $entry.StartsWith('tool/', [StringComparison]::OrdinalIgnoreCase) -or
                $entry.StartsWith('_rels/', [StringComparison]::OrdinalIgnoreCase) -or
                $entry.StartsWith('package/', [StringComparison]::OrdinalIgnoreCase) -or
                $entry.Equals('README.md', [StringComparison]::OrdinalIgnoreCase) -or
                $entry.Equals('[Content_Types].xml', [StringComparison]::OrdinalIgnoreCase) -or
                $entry.Equals('Guihua.Templates.nuspec', [StringComparison]::OrdinalIgnoreCase) -or
                $entry.Equals('.signature.p7s', [StringComparison]::OrdinalIgnoreCase)
            if (-not $allowed) { throw "Package contains unexpected root entry '$entry'." }
        }
        if ($entries | Where-Object { $_.EndsWith('.nupkg', [StringComparison]::OrdinalIgnoreCase) }) {
            throw 'Package contains a nested nupkg.'
        }
        if ($entries | Where-Object {
            $_.EndsWith('.nuspec', [StringComparison]::OrdinalIgnoreCase) -and
            -not $_.Equals('Guihua.Templates.nuspec', [StringComparison]::OrdinalIgnoreCase)
        }) {
            throw 'Package contains nested template nuspec metadata.'
        }
    }
    finally { $archive.Dispose() }
}

function Get-LoopbackPort {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    try { $listener.Start(); return ([Net.IPEndPoint]$listener.LocalEndpoint).Port }
    finally { $listener.Stop() }
}

function Wait-Api {
    param([Diagnostics.Process] $Process, [string] $Address)
    $client = [Net.Http.HttpClient]::new()
    $client.Timeout = [TimeSpan]::FromSeconds(2)
    try {
        $deadline = [DateTime]::UtcNow.AddSeconds(30)
        while ([DateTime]::UtcNow -lt $deadline) {
            if ($Process.HasExited) { throw "Generated API exited before readiness (code $($Process.ExitCode))." }
            try {
                $response = $client.GetAsync("$Address/api/demo/greeting").GetAwaiter().GetResult()
                $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                if ([int]$response.StatusCode -ne 200 -or $body -notmatch '"code"\s*:\s*200') {
                    throw "Unexpected API response: $body"
                }
                $openApi = $client.GetAsync("$Address/swagger/v1/swagger.json").GetAwaiter().GetResult()
                if ([int]$openApi.StatusCode -ne 200) { throw 'OpenAPI endpoint did not return HTTP 200 in Development.' }
                $unknown = $client.GetAsync("$Address/api/not-found").GetAwaiter().GetResult()
                if ([int]$unknown.StatusCode -ne 404) { throw 'Unknown route did not preserve HTTP 404.' }
                return
            }
            catch [Net.Http.HttpRequestException] { Start-Sleep -Milliseconds 250 }
            catch [Threading.Tasks.TaskCanceledException] { Start-Sleep -Milliseconds 250 }
        }
        throw "Generated API did not become ready at $Address."
    }
    finally { $client.Dispose() }
}

$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$allowedArtifactsRoot = Join-Path $repo 'artifacts'
[IO.Directory]::CreateDirectory($allowedArtifactsRoot) | Out-Null
$workspaceArtifacts = Assert-PathInside $ArtifactsPath $allowedArtifactsRoot 'artifacts path'
[IO.Directory]::CreateDirectory($workspaceArtifacts) | Out-Null
$run = Join-Path $workspaceArtifacts ("ci-" + [DateTime]::UtcNow.ToString('yyyyMMddHHmmss') + '-' + [Guid]::NewGuid().ToString('N'))
$packageOutput = Join-Path $run 'packages'
$hive = Join-Path $run 'custom-hive'
$dotnetHome = Join-Path $run 'dotnet-home'
$generated = Join-Path $run 'generated'
foreach ($path in @($packageOutput, $hive, $dotnetHome, $generated)) { [IO.Directory]::CreateDirectory($path) | Out-Null }

$oldDotnetHome = $env:DOTNET_CLI_HOME
$oldSkip = $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE
$oldTelemetry = $env:DOTNET_CLI_TELEMETRY_OPTOUT
try {
    $env:DOTNET_CLI_HOME = $dotnetHome
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

    if (-not $SkipPack) {
        Invoke-Dotnet $repo @('pack', (Join-Path $repo 'packaging/Guihua.Templates.csproj'), '-c', 'Release', '-o', $packageOutput, '--nologo')
        $PackagePath = Get-ChildItem -LiteralPath $packageOutput -Filter 'Guihua.Templates.*.nupkg' -File |
            Select-Object -ExpandProperty FullName -First 1
    }
    if (-not $PackagePath) { throw 'PackagePath is required when -SkipPack is used.' }
    $PackagePath = (Resolve-Path -LiteralPath $PackagePath).Path
    Assert-PackageBoundary $PackagePath

    Invoke-Dotnet $repo @('new', 'install', $PackagePath, '--force', '--debug:custom-hive', $hive, '-v', 'q')
    $templateList = Get-TemplateNames $hive
    foreach ($shortName in @('guihua', 'guihua-module', 'guihua-tool')) {
        if ($templateList -notmatch "(?m)\b$([regex]::Escape($shortName))\b") {
            throw "Installed package does not expose '$shortName'."
        }
    }

    foreach ($framework in $Frameworks) {
        if ($framework -notin @('net8.0', 'net10.0')) { throw "Unsupported framework '$framework'." }
        $sampleName = "Generated$($framework.Replace('.', ''))"
        $mainOutput = Join-Path $generated $framework
        Invoke-Dotnet $repo @(
            'new', 'guihua', '-n', $sampleName, '-o', $mainOutput, '--framework', $framework,
            '--no-update-check', '--debug:custom-hive', $hive, '-v', 'q'
        )
        $solution = Get-ChildItem -LiteralPath $mainOutput -Filter '*.sln' -File |
            Select-Object -ExpandProperty FullName -First 1
        if (-not $solution) { throw "Main template did not generate a solution for $framework." }

        Invoke-Dotnet $mainOutput @('restore', $solution, '--nologo')
        Assert-NoVulnerablePackages $mainOutput $solution
        Invoke-Dotnet $mainOutput @('build', $solution, '-c', 'Release', '--no-restore', '--nologo', '-warnaserror')
        Invoke-Dotnet $mainOutput @('test', $solution, '-c', 'Release', '--no-build', '--no-restore', '--nologo')
        $assets = Get-ChildItem -LiteralPath $mainOutput -Filter project.assets.json -Recurse -File | Get-Content -Raw
        foreach ($package in @('freesql/', 'stackexchange.redis/', 'automapper/', 'nlog/', 'restsharp/', 'refit/')) {
            if ($assets -match [regex]::Escape($package)) { throw "Default generated project unexpectedly contains '$package'." }
        }

        $apiProject = Get-ChildItem -LiteralPath (Join-Path $mainOutput 'src') -Filter '*.Api.csproj' -Recurse -File |
            Select-Object -ExpandProperty FullName -First 1
        if (-not $apiProject) { throw "Generated API project was not found for $framework." }
        $port = Get-LoopbackPort
        $address = "http://127.0.0.1:$port"
        $oldEnvironment = $env:ASPNETCORE_ENVIRONMENT
        $env:ASPNETCORE_ENVIRONMENT = 'Development'
        try {
            $startParameters = @{
                FilePath = 'dotnet'
                WorkingDirectory = $mainOutput
                ArgumentList = @('run', '--project', $apiProject, '-c', 'Release', '--no-build', '--no-restore', '--urls', $address)
                PassThru = $true
            }
            if ($IsWindows) { $startParameters['WindowStyle'] = 'Hidden' }
            $process = Start-Process @startParameters
        }
        finally { $env:ASPNETCORE_ENVIRONMENT = $oldEnvironment }
        try { Wait-Api $process $address }
        finally {
            if (-not $process.HasExited) { $process.Kill($true) }
            $process.WaitForExit()
        }

        $freeSqlOutput = Join-Path $generated "$framework-freesql"
        $freeSqlName = "Generated$($framework.Replace('.', ''))FreeSql"
        Invoke-Dotnet $repo @(
            'new', 'guihua', '-n', $freeSqlName, '-o', $freeSqlOutput,
            '--framework', $framework, '--persistence', 'freesql',
            '--no-update-check', '--debug:custom-hive', $hive, '-v', 'q'
        )
        $freeSqlSolution = Get-ChildItem -LiteralPath $freeSqlOutput -Filter '*.sln' -File |
            Select-Object -ExpandProperty FullName -First 1
        if (-not $freeSqlSolution) { throw "FreeSql template did not generate a solution for $framework." }
        $freeSqlModule = Join-Path $freeSqlOutput 'src/Modules/Guihua.Persistence.FreeSql/Guihua.Persistence.FreeSql.csproj'
        $freeSqlEntity = Join-Path $freeSqlOutput "src/${freeSqlName}.Core/Entities/PersistenceSample.cs"
        $freeSqlProgram = Join-Path $freeSqlOutput "src/${freeSqlName}.Api/Program.cs"
        $freeSqlDevelopmentSettings = Join-Path $freeSqlOutput "src/${freeSqlName}.Api/appsettings.Development.json"
        foreach ($requiredPath in @($freeSqlModule, $freeSqlEntity, $freeSqlDevelopmentSettings)) {
            if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) { throw "FreeSql template did not generate '$requiredPath'." }
        }
        Assert-FileContains $freeSqlProgram 'AddFreeSqlPersistence'
        Assert-FileContains $freeSqlDevelopmentSettings '"AutoSyncStructure": true'
        Invoke-Dotnet $freeSqlOutput @('restore', $freeSqlSolution, '--nologo')
        Assert-NoVulnerablePackages $freeSqlOutput $freeSqlSolution
        $freeSqlAssets = Get-ChildItem -LiteralPath $freeSqlOutput -Filter project.assets.json -Recurse -File | Get-Content -Raw
        foreach ($package in @('freesql/', 'freesql.extensions.baseentity/')) {
            if ($freeSqlAssets -notmatch [regex]::Escape($package)) { throw "FreeSql generated project is missing '$package'." }
        }
        Invoke-Dotnet $freeSqlOutput @('build', $freeSqlSolution, '-c', 'Release', '--no-restore', '--nologo', '-warnaserror')
        Invoke-Dotnet $freeSqlOutput @('test', $freeSqlSolution, '-c', 'Release', '--no-build', '--no-restore', '--nologo')

        $freeSqlApiProject = Join-Path $freeSqlOutput "src/${freeSqlName}.Api/${freeSqlName}.Api.csproj"
        $freeSqlPort = Get-LoopbackPort
        $freeSqlAddress = "http://127.0.0.1:$freeSqlPort"
        $oldEnvironment = $env:ASPNETCORE_ENVIRONMENT
        $env:ASPNETCORE_ENVIRONMENT = 'Development'
        try {
            $freeSqlStartParameters = @{
                FilePath = 'dotnet'
                WorkingDirectory = $freeSqlOutput
                ArgumentList = @('run', '--project', $freeSqlApiProject, '-c', 'Release', '--no-build', '--no-restore', '--urls', $freeSqlAddress)
                PassThru = $true
            }
            if ($IsWindows) { $freeSqlStartParameters['WindowStyle'] = 'Hidden' }
            $freeSqlProcess = Start-Process @freeSqlStartParameters
        }
        finally { $env:ASPNETCORE_ENVIRONMENT = $oldEnvironment }
        try {
            Wait-Api $freeSqlProcess $freeSqlAddress
            $freeSqlDatabase = Join-Path (Split-Path -Parent $freeSqlApiProject) 'app.db'
            if (-not (Test-Path -LiteralPath $freeSqlDatabase -PathType Leaf)) {
                throw 'FreeSql Development startup did not create app.db.'
            }
        }
        finally {
            if (-not $freeSqlProcess.HasExited) { $freeSqlProcess.Kill($true) }
            $freeSqlProcess.WaitForExit()
        }

        $moduleOutput = Join-Path $mainOutput 'src/Modules/DeviceCommunication'
        Invoke-Dotnet $mainOutput @(
            'new', 'guihua-module', '-n', 'DeviceCommunication', '-o', $moduleOutput,
            '--no-update-check', '--debug:custom-hive', $hive, '-v', 'q'
        )
        $moduleProject = Join-Path $moduleOutput 'DeviceCommunication/DeviceCommunication.csproj'
        $moduleTests = Join-Path $moduleOutput 'DeviceCommunication.Tests/DeviceCommunication.Tests.csproj'
        foreach ($project in @($moduleProject, $moduleTests)) {
            if (-not (Test-Path -LiteralPath $project -PathType Leaf)) { throw "Module template did not generate '$project'." }
        }
        Assert-FileContains (Join-Path $moduleOutput 'README.md') 'AddDeviceCommunication(builder.Configuration)'
        if (Select-String -LiteralPath $moduleProject -Pattern '<TargetFramework>|Version=' -Quiet) {
            throw 'Generated module must inherit TargetFramework and central package versions from its host.'
        }
        Invoke-Dotnet $mainOutput @('sln', $solution, 'add', $moduleProject, $moduleTests)
        Invoke-Dotnet $mainOutput @('add', $apiProject, 'reference', $moduleProject)
        Invoke-Dotnet $mainOutput @('restore', $solution, '--nologo')
        Assert-NoVulnerablePackages $mainOutput $solution
        Invoke-Dotnet $mainOutput @('build', $solution, '-c', 'Release', '--no-restore', '--nologo', '-warnaserror')
        Invoke-Dotnet $mainOutput @('test', $solution, '-c', 'Release', '--no-build', '--no-restore', '--nologo')

        $toolOutput = Join-Path $generated "tool-$framework"
        Invoke-Dotnet $repo @(
            'new', 'guihua-tool', '-n', "Tool$($framework.Replace('.', ''))", '-o', $toolOutput,
            '--framework', $framework, '--no-update-check', '--debug:custom-hive', $hive, '-v', 'q'
        )
        $toolSolution = Get-ChildItem -LiteralPath $toolOutput -Filter '*.sln' -File |
            Select-Object -ExpandProperty FullName -First 1
        $toolProject = Get-ChildItem -LiteralPath (Join-Path $toolOutput 'src') -Filter '*.csproj' -Recurse -File |
            Select-Object -ExpandProperty FullName -First 1
        if (-not $toolSolution -or -not $toolProject) { throw "Tool template output is incomplete for $framework." }
        Invoke-Dotnet $toolOutput @('restore', $toolSolution, '--nologo')
        Assert-NoVulnerablePackages $toolOutput $toolSolution
        Invoke-Dotnet $toolOutput @('build', $toolSolution, '-c', 'Release', '--no-restore', '--nologo', '-warnaserror')
        Invoke-DotnetExpectExit $toolOutput @(
            'run', '--project', $toolProject, '-c', 'Release', '--no-build', '--no-restore'
        ) 0 @{
            'GuihuaTool__Iterations' = '1'
            'GuihuaTool__IntervalMilliseconds' = '50'
        }
        Invoke-DotnetExpectExit $toolOutput @(
            'run', '--project', $toolProject, '-c', 'Release', '--no-build', '--no-restore'
        ) 2 @{
            'GuihuaTool__Iterations' = '-1'
            'GuihuaTool__IntervalMilliseconds' = '50'
        }
    }

    Assert-NoVulnerablePackages $repo (Join-Path $repo 'Guihua.Template.sln')
    Write-Host "T-32 pre-release validation passed for: $($Frameworks -join ', ')"
    Write-Host "Candidate package: $PackagePath"
    Write-Host "Run artifacts: $run"
}
finally {
    $env:DOTNET_CLI_HOME = $oldDotnetHome
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = $oldSkip
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = $oldTelemetry
    if (-not $KeepArtifacts) { Remove-VerifiedDirectory $run $workspaceArtifacts }
}
