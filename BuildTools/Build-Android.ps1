param(
    [string]$AgentFolderName = "Lint-It-Up",
    [string]$Branch = "main",
    [string]$FinalOutputDirectory = "C:\Usman\APKs\APKs"
)

$ErrorActionPreference = "Stop"

function Get-ProjectRoot {
    return (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

function Get-UnityVersion {
    param([string]$ProjectPath)

    $versionFile = Join-Path $ProjectPath "ProjectSettings\ProjectVersion.txt"
    if (-not (Test-Path $versionFile)) {
        throw "Unity version file not found: $versionFile"
    }

    $line = Get-Content $versionFile | Where-Object { $_ -like "m_EditorVersion:*" } | Select-Object -First 1
    if (-not $line) {
        throw "Could not read Unity editor version from $versionFile"
    }

    return ($line -split ":", 2)[1].Trim()
}

function Get-UnityEditorPath {
    param([string]$ProjectPath)

    if ($env:UNITY_EDITOR_PATH -and (Test-Path $env:UNITY_EDITOR_PATH)) {
        return $env:UNITY_EDITOR_PATH
    }

    $unityVersion = Get-UnityVersion -ProjectPath $ProjectPath
    $defaultPath = "C:\Program Files\Unity\Hub\Editor\$unityVersion\Editor\Unity.exe"
    if (Test-Path $defaultPath) {
        return $defaultPath
    }

    throw @"
Unity.exe not found.

Expected path:
$defaultPath

Either install that Unity version through Unity Hub or set UNITY_EDITOR_PATH to the full Unity.exe path.
"@
}

function Sync-BuildAgentRepo {
    param(
        [string]$SourceRepoPath,
        [string]$AgentRepoPath,
        [string]$BranchName
    )

    $originUrl = (git -C $SourceRepoPath remote get-url origin).Trim()
    if (-not $originUrl) {
        throw "No git remote named 'origin' was found in $SourceRepoPath"
    }

    if (-not (Test-Path $AgentRepoPath)) {
        Write-Host "Cloning clean build copy into $AgentRepoPath"
        git clone --branch $BranchName $originUrl $AgentRepoPath
        if ($LASTEXITCODE -ne 0) {
            throw "git clone failed"
        }
        return
    }

    Write-Host "Updating existing build copy in $AgentRepoPath"
    git -C $AgentRepoPath fetch origin
    if ($LASTEXITCODE -ne 0) {
        throw "git fetch failed"
    }

    git -C $AgentRepoPath checkout $BranchName
    if ($LASTEXITCODE -ne 0) {
        throw "git checkout failed"
    }

    git -C $AgentRepoPath reset --hard "origin/$BranchName"
    if ($LASTEXITCODE -ne 0) {
        throw "git reset failed"
    }

    git -C $AgentRepoPath clean -fd
    if ($LASTEXITCODE -ne 0) {
        throw "git clean failed"
    }
}

function Invoke-UnityAndroidBuild {
    param(
        [string]$UnityExePath,
        [string]$ProjectPath,
        [string]$LogPath
    )

    & $UnityExePath `
      -quit `
      -batchmode `
      -nographics `
      -accept-apiupdate `
      -buildTarget Android `
      -projectPath $ProjectPath `
      -executeMethod BuildScript.BuildAndroid `
      -logFile $LogPath

    return $LASTEXITCODE
}

$projectRoot = Get-ProjectRoot
$buildAgentsRoot = Join-Path $projectRoot "BuildAgents"
$agentRepoPath = Join-Path $buildAgentsRoot $AgentFolderName
$logsPath = Join-Path $buildAgentsRoot "Logs"
$logFile = Join-Path $logsPath "android-build.log"

New-Item -ItemType Directory -Force -Path $buildAgentsRoot | Out-Null
New-Item -ItemType Directory -Force -Path $logsPath | Out-Null
New-Item -ItemType Directory -Force -Path $FinalOutputDirectory | Out-Null

Sync-BuildAgentRepo -SourceRepoPath $projectRoot -AgentRepoPath $agentRepoPath -BranchName $Branch

$unityPath = Get-UnityEditorPath -ProjectPath $agentRepoPath

Write-Host "Unity path: $unityPath"
Write-Host "Build repo:  $agentRepoPath"
Write-Host "Log file:    $logFile"

$buildExitCode = Invoke-UnityAndroidBuild -UnityExePath $unityPath -ProjectPath $agentRepoPath -LogPath $logFile
if ($buildExitCode -ne 0) {
    Write-Host ""
    Write-Host "Android build failed."
    Write-Host "Check log: $logFile"
    exit $buildExitCode
}

$apkPath = Join-Path $agentRepoPath "Builds\Android\Lint-It-Up.apk"
$finalApkPath = Join-Path $FinalOutputDirectory "Lint-It-Up.apk"

if (-not (Test-Path $apkPath)) {
    Write-Host ""
    Write-Host "First Unity run finished without producing the APK."
    Write-Host "Retrying once now that the clean clone has finished importing and compiling..."

    $buildExitCode = Invoke-UnityAndroidBuild -UnityExePath $unityPath -ProjectPath $agentRepoPath -LogPath $logFile
    if ($buildExitCode -ne 0) {
        Write-Host ""
        Write-Host "Android build failed on retry."
        Write-Host "Check log: $logFile"
        exit $buildExitCode
    }

    if (-not (Test-Path $apkPath)) {
        throw "Build finished but APK was not found at $apkPath after retry"
    }
}

Copy-Item -Path $apkPath -Destination $finalApkPath -Force

Write-Host ""
Write-Host "Android build completed successfully."
Write-Host "Build copy APK path: $apkPath"
Write-Host "Final APK path: $finalApkPath"
Write-Host "Log path: $logFile"
