# Local Unity Android Build Automation Plan

## Objective

Create a fully local Android build process for Unity projects so that:

- the build runs on your own computer
- no GitHub-hosted runners or GitHub build resources are used
- the APK is saved locally on the system
- the build can be started by pressing a button
- the build can optionally run after local git actions

Important note:
If the process is fully local, Unity still has to do the actual build work in batch mode. The goal is to avoid manual use of the Unity Editor UI, not to avoid Unity itself.

## What You Want

Your requirements are:

- local-only build
- Android only
- output saved locally
- reusable enough to apply to other Unity projects later

## Recommended Local Design

Use this 4-part local setup:

1. A Unity build script inside the project
2. A local PowerShell build script
3. A simple button launcher such as a `.bat` file
4. Optional git hook automation for local commits or pushes

## Why This Is The Best Fit

This approach gives you:

- no dependency on GitHub runners
- no cloud build cost
- APK files saved directly on your machine
- a one-click build flow
- a reusable structure for other Unity Android projects

## Local Build Flow

The recommended flow is:

1. You change code locally.
2. You either:
   - click a local build button
   - run a local script
   - or trigger the build from a local git hook
3. Unity starts in batch mode.
4. Unity builds the Android APK.
5. The APK is saved into a local folder like `Builds/Android/`.
6. A log file is saved locally for debugging.

## Recommended Folder Structure

```text
Assets/
  Editor/
    BuildScript.cs

BuildTools/
  Build-Android.ps1
  Build-Android.bat

Builds/
  Android/

Logs/
```

## Phase 1: Add the Unity Android build script

Each Unity project should include a reusable script at:

`Assets/Editor/BuildScript.cs`

That script should:

- collect enabled scenes from `EditorBuildSettings`
- build Android only
- save the APK to a local output path
- fail clearly if the build fails

## Unity Build Script Template

```csharp
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class BuildScript
{
    private static string[] GetEnabledScenes()
    {
        return EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
    }

    public static void BuildAndroid()
    {
        var options = new BuildPlayerOptions
        {
            scenes = GetEnabledScenes(),
            locationPathName = "Builds/Android/Game.apk",
            target = BuildTarget.Android
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new Exception("Android build failed");
        }
    }
}
```

## Phase 2: Add a local PowerShell build script

Create a local script such as:

`BuildTools/Build-Android.ps1`

This script should:

- know the path to the Unity Editor executable
- run Unity in batch mode
- call `BuildScript.BuildAndroid`
- write logs to a local `Logs/` folder

## PowerShell Build Script Template

```powershell
$unityPath = "C:\Program Files\Unity\Hub\Editor\6000.0.66f2\Editor\Unity.exe"
$projectPath = "C:\Path\To\Your\Project"
$logPath = "$projectPath\Logs\android-build.log"

New-Item -ItemType Directory -Force -Path "$projectPath\Builds\Android" | Out-Null
New-Item -ItemType Directory -Force -Path "$projectPath\Logs" | Out-Null

& $unityPath `
  -quit `
  -batchmode `
  -projectPath $projectPath `
  -executeMethod BuildScript.BuildAndroid `
  -logFile $logPath

if ($LASTEXITCODE -ne 0) {
    Write-Host "Android build failed. Check log: $logPath"
    exit $LASTEXITCODE
}

Write-Host "Android build completed successfully."
Write-Host "APK saved in $projectPath\Builds\Android"
```

## Phase 3: Add a one-click button launcher

Create a `.bat` file such as:

`BuildTools/Build-Android.bat`

Example:

```bat
@echo off
powershell -ExecutionPolicy Bypass -File "%~dp0Build-Android.ps1"
pause
```

This gives you a simple local button:

- double-click the `.bat`
- it launches the Android build
- the APK stays on your machine

## Phase 4: Optional local git automation

If you want the build to run after local git actions, use local hooks.

Examples:

- `.git/hooks/post-commit`
- `.git/hooks/pre-push`
- `.git/hooks/post-merge`

Important note:
Git hooks are local to your machine and are not shared automatically with other repos unless you set them up manually.

## Recommended Hook Strategy

For local Android builds, the safest hook is:

- `pre-push` if you want a build before pushing

or:

- no hook at all, and use the button only

Why:

- building after every commit can feel too heavy
- Android builds can take time
- button-based control is usually cleaner

## Example Local Pre-Push Hook

```sh
#!/bin/sh
powershell -ExecutionPolicy Bypass -File "./BuildTools/Build-Android.ps1"
if [ $? -ne 0 ]; then
  echo "Build failed. Push cancelled."
  exit 1
fi
```

## Best Practical Recommendation

For your case, I recommend this exact setup:

1. Build only Android
2. Save the APK locally in `Builds/Android/`
3. Use a PowerShell script for the real build
4. Use a `.bat` file as the button
5. Add a `pre-push` hook only if you really want automatic local enforcement

## Output Paths

Recommended local output:

- APK: `Builds/Android/Game.apk`
- Logs: `Logs/android-build.log`

If you want versioned builds later, you can move to:

- `Builds/Android/Game-2026-05-06.apk`
- `Builds/Android/Game-v1.2.0.apk`

## Generic Reusability Across Projects

This setup is still reusable across different Unity Android projects.

Usually only these values change:

- Unity Editor path
- project path
- APK name
- signing settings
- Unity version

The overall structure stays the same.

## Common Problems

### 1. Wrong Unity path

The local script must point to the correct Unity Editor installation.

### 2. Android SDK or module issues

Unity must have Android Build Support installed locally.

### 3. Keystore signing

If you want release-ready APKs, you may need signing setup in project settings or build code.

### 4. Build failures hidden in editor logs

That is why the local script should always write to `Logs/android-build.log`.

### 5. Hook inconvenience

If hooks run too often, they slow down your workflow. In that case, keep the one-click button and remove the hook.

## Validation Checklist

1. Double-click the local `.bat` file.
2. Confirm Unity starts in batch mode.
3. Confirm the APK is created in `Builds/Android/`.
4. Confirm the log file is created in `Logs/android-build.log`.
5. Install the APK and verify it runs.

## Exact Plan To Implement

1. Add `Assets/Editor/BuildScript.cs`.
2. Add `BuildTools/Build-Android.ps1`.
3. Add `BuildTools/Build-Android.bat`.
4. Set the correct local Unity path.
5. Run the button-based build once.
6. Check the APK in `Builds/Android/`.
7. Add an optional local git hook later if needed.

## Final Recommendation

For what you want right now, the best setup is:

- local-only
- Android-only
- button-triggered first
- APK saved locally
- optional git hook later

That is simpler, faster, and much better aligned with your goal than using GitHub Actions.
