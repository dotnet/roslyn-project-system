@echo off
powershell -ExecutionPolicy ByPass %~dp0Build.ps1 -restore -build -pack -ci /p:RootSuffix= %*
exit /b %ErrorLevel%
