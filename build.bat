@echo off
rem Builds every sample for every architecture and packs the NuGet packages on GitHub Actions, without publishing anything.
rem The zips and the packages stay as artifacts of the run, see release.bat.
call "%~dp0release.bat" build
