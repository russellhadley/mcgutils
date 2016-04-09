:: Build and optionally publish sub projects
::
:: This script will by default build release versions of the tools.
:: If publish (-p) is requested it will create standalone versions of the
:: tools in <root>/src/<project>/<buildType>/netcoreapp1.0/<platform>/Publish/.
:: These tools can be installed via the install script (install.{sh|cmd}) in
:: this directory.
@echo off

set buildType="Release"
set publish="false"
set action="build"

:argLoop
if /i "%1"=="" goto :build

if /i "%1"=="-b" (
    set buildType=%2
    shift
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
    dotnet %action% -c %buildType% .\src\%%p
)

::Done
exit 0

:usage
    echo.
    echo  build.cmd [-p] [-h] [-b ^<BUILD TYPE^>]
    echo.
    echo      -b ^<BUILD TYPE^> : Build type, can be Debug or Release.
    echo      -h              : Show this message
    echo      -p              : Publish.
    echo. 
    exit -1
