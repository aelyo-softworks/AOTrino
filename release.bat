@echo off
setlocal

rem Publishes a release from GitHub Actions, without building anything locally.
rem The tag and the NuGet packages both take the Version in Directory.Build.props, so bump that first.
rem A version that is already released or already on nuget.org stops the run before anything is published.
rem GitHub builds what is pushed, not what is on this machine, so commit and push first.
rem
rem Needs the GitHub CLI, https://cli.github.com, signed in with "gh auth login",
rem and the NUGET_API_KEY repository secret for the push to nuget.org, see docs\MAINTENANCE.md.
rem The workflow also has to exist on the default branch, which is where GitHub looks for it.
rem
rem   release.bat           builds every sample for every architecture, pushes the packages and publishes the release.
rem   release.bat build     builds and packs only, the zips and the packages stay as artifacts of the run.
rem                         build.bat does the same.

set RESULT=0
set RELEASE=true
if /i "%~1"=="build" set RELEASE=false

where gh >nul 2>&1
if errorlevel 1 (
    echo the GitHub CLI is needed. see https://cli.github.com
    set RESULT=1
    goto done
)

set VERSION=
for /f "tokens=3 delims=<>" %%v in ('findstr /c:"<Version>" "%~dp0Directory.Build.props"') do if not defined VERSION set VERSION=%%v
if "%VERSION%"=="" (
    echo no ^<Version^> found in Directory.Build.props
    set RESULT=1
    goto done
)

set AHEAD=0
for /f %%n in ('git -C "%~dp0." rev-list --count "@{u}..HEAD" 2^>nul') do set AHEAD=%%n
if not "%AHEAD%"=="0" echo warning: %AHEAD% local commit^(s^) not pushed, GitHub builds what is pushed, not those.

if "%RELEASE%"=="true" (
    echo this publishes AOTrino and AOTrino.Templates %VERSION% to nuget.org, and the GitHub release v%VERSION%.
    choice /c yn /m "go ahead"
    if errorlevel 2 goto done
) else (
    echo building and packing %VERSION%, nothing is published.
)

echo starting the workflow with release=%RELEASE%
gh workflow run release.yml -f release=%RELEASE%
if errorlevel 1 (
    set RESULT=1
    goto done
)

rem the run takes a moment to be queued before it can be found.
timeout /t 6 /nobreak >nul

set RUNID=
for /f "delims=" %%i in ('gh run list --workflow=release.yml --limit 1 --json databaseId --jq ".[0].databaseId"') do set RUNID=%%i
if "%RUNID%"=="" (
    echo the run was started but could not be found. see it at the Actions tab.
    goto done
)

gh run watch %RUNID% --exit-status
if errorlevel 1 set RESULT=1

:done
rem a double click runs this through "cmd /c", which closes the window as soon as it ends, before anything can be read.
echo %cmdcmdline% | findstr /i /c:" /c " >nul && pause
exit /b %RESULT%
