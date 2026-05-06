# Local Android Build

This project builds Android locally from a clean git copy inside:

`C:\Usman\Line-it-up\BuildAgents\Lint-It-Up`

## How it works

1. `BuildTools\Build-Android.ps1` reads the current repo's `origin` remote.
2. It clones or updates a clean copy in `BuildAgents\Lint-It-Up`.
3. It finds the Unity editor path from `ProjectSettings\ProjectVersion.txt`.
4. It runs Unity in batch mode with `BuildScript.BuildAndroid`.
5. The APK is saved to:
   `BuildAgents\Lint-It-Up\Builds\Android\Lint-It-Up.apk`

## One-click build

Double-click:

`BuildTools\Build-Android.bat`

## Optional Unity path override

If Unity is installed somewhere else, set:

`UNITY_EDITOR_PATH`

to the full path of `Unity.exe` before running the script.
