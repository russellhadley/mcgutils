:: Build and optionally publish sub projects
::
:: This script will by default build release versions of the tools.
:: If publish (-p) is requested it will create standalone versions of the
:: tools in <root>/src/<project>/<buildType>/netcoreapp1.0/<platform>/Publish/.
:: These tools can be installed via the install script (install.{sh|cmd}) in
:: this directory.
@echo on

set scriptDir=%~dp0
set appInstallDir=%scriptDir%\bin
set fxInstallDir=%scriptDir%\fx
set buildType="Release"
set publish="false"
set fx="false"
set action="build"

for /f "usebackq tokens=1,2" %%a in (`dotnet --info`) do (
    if "%%a"=="RID:" set platform=%%b
)


:argLoop
if /i "%1"=="" goto :build

if /i "%1"=="-b" (
    set buildType=%2
    shift
)
if /i "%1"=="-f" (
    set fx="true"
)
if /i "%1"=="-p" (
    set publish="true"
)
if /i "%1" == "-h" (
    goto :usage
)

shift
goto :argLoop

:build

::Change to publish if requested by the user.
if %publish% == "true" (
    set action="publish"
)

::Build each project - list any new projects here - this is a kludge but
::cmd has it's challenges.
for %%p in (corediff mcgdiff) do (
    if /i %publish%=="true" (
        dotnet publish -c %buildType% -o %appInstallDir%\%%p .\src\%%p
    ) else (
        dotnet build  -c %buildType% .\src\%%p
    )
)

if /i %fx%=="true" (
    dotnet publish -c %buildType% -o %fxInstallDir% .\src\packages
    
    :: remove package version of mscorlib* - refer to core root version for
    :: diff testing
    del %fxInstallDir%\mscorlib*
)

::Done
exit /b 0

:usage
    echo.
    echo  build.cmd [-b ^<BUILD TYPE^>] [-f] [-h] [-p]
    echo.
    echo      -b ^<BUILD TYPE^> : Build type, can be Debug or Release.
    echo      -f                : Publish default framework directory in ^<script_root^>\fx.
    echo      -h                : Show this message
    echo      -p                : Publish utilites.
    echo. 
    exit /b -1
