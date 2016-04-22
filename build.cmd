@echo off
setlocal

:: Build and optionally publish sub projects
::
:: This script will by default build release versions of the tools.
:: If publish (-p) is requested it will create standalone versions of the
:: tools in <root>/src/<project>/<buildType>/netcoreapp1.0/<platform>/Publish/.
:: These tools can be installed via the install script (install.{sh|cmd}) in
:: this directory.

set scriptDir=%~dp0
set appInstallDir=%scriptDir%\bin
set fxInstallDir=%scriptDir%\fx
set buildType=Release
set publish=false
set fx=false

:: REVIEW: 'platform' is never used
for /f "usebackq tokens=1,2" %%a in (`dotnet --info`) do (
    if "%%a"=="RID:" set platform=%%b
)

:argLoop
if "%1"=="" goto :build

if /i "%1"=="-b" (
    set buildType=%2
    shift
)
if /i "%1"=="-f" (
    set fx=true
)
if /i "%1"=="-p" (
    set publish=true
)
if /i "%1" == "-h" (
    goto :usage
)

shift
goto :argLoop

:build

:: Declare the list of projects
set projects=corediff mcgdiff analyze

::Build each project
for %%p in (%projects%) do (
    if %publish%==true (
        dotnet publish -c %buildType% -o %appInstallDir%\%%p .\src\%%p
    ) else (
        dotnet build  -c %buildType% .\src\%%p
    )
)

if %fx%==true (
    dotnet publish -c %buildType% -o %fxInstallDir% .\src\packages
    
    :: remove package version of mscorlib* - refer to core root version for
    :: diff testing
    if exist %fxInstallDir%\mscorlib* del /q %fxInstallDir%\mscorlib*
)

::Done
exit /b 0

:usage
    echo.
    echo  build.cmd [-b ^<BUILD TYPE^>] [-f] [-h] [-p]
    echo.
    echo      -b ^<BUILD TYPE^> : Build type, can be Debug or Release.
    echo      -h                : Show this message.
    echo      -f                : Publish default framework directory in ^<script_root^>\fx.
    echo      -p                : Publish utilites.
    echo. 
    exit /b 1
