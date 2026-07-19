@echo off
rem publishes every sample, AOT, for every architecture, into publish\<rid>\
rem
rem   publish.bat                    all samples, all architectures
rem   publish.bat -upx               ...and compress the exes with UPX (upx.exe must be in the PATH)
rem   publish.bat -rid win-x64       one architecture only
rem   publish.bat -clean             delete the publish folder first
rem   publish.bat -zip               ...and one zip per architecture (exes only, no pdbs)
rem   publish.bat -release v1.0.0    ...and publish those zips as a GitHub release (gh.exe must be in the PATH)
rem
rem NOTE: do not name a variable here UPX. upx.exe reads an environment variable of that name for its default options,
rem so "set UPX=true" makes every upx call fail with "invalid string 'true' in environment variable 'UPX'",
rem and MSBuild reads environment variables as properties, so it would set $(Upx) behind your back too.
rem
rem AOT compilation ends in the MSVC linker, which ILCompiler finds by running vswhere.exe,
rem and vswhere is only on the PATH inside a Visual Studio developer prompt.
rem Putting the installer directory there is enough (ILCompiler takes it from there),
rem so this doesn't need vcvars and doesn't care which architecture vcvars would have selected, which matters here,
rem because one run publishes for three of them.

setlocal
if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" set "PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer;%PATH%"
set AOTRINO_UPX=false
set AOTRINO_ZIP=false
set TAG=
set RIDS=
set TARGET=PublishAll

:args
if "%~1" == "" goto run
if /i "%~1" == "-upx" set AOTRINO_UPX=true& shift & goto args
if /i "%~1" == "-clean" set TARGET=Clean;PublishAll& shift & goto args
if /i "%~1" == "-zip" set AOTRINO_ZIP=true& shift & goto args
if /i "%~1" == "-release" set TAG=%~2& shift & shift & goto args
if /i "%~1" == "-rid" set RIDS=%~2& shift & shift & goto args
if /i "%~1" == "-help" set TARGET=Help& shift & goto args
if /i "%~1" == "-?" set TARGET=Help& shift & goto args
echo unknown option: %~1
exit /b 1

:run
set ARGS=-t:%TARGET% -p:Upx=%AOTRINO_UPX% -p:Zip=%AOTRINO_ZIP%
if not "%RIDS%" == "" set ARGS=%ARGS% -p:Rids=%RIDS%
if not "%TAG%" == "" set ARGS=%ARGS% -p:ReleaseTag=%TAG%
dotnet build "%~dp0PublishSamples.proj" %ARGS% -v:minimal -nologo
exit /b %errorlevel%
