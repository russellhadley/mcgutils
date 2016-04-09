:: Install published utilites to a directory
::
:: Runs the cross-platform C# installer in place to create layout
:: in install directory (default <script dir>/bin).  To update what
:: get's installed please look at C# installer which has direct knowledge
:: of the projects in the files.

set scriptDir=%~dp0
set buildType="Release"
set installDir="%scriptDir%\bin"
set force=""

for /f "usebackq tokens=1,2" %%a in (`dotnet --info`) do (
    if "%%a"=="RID:" set platform=%%b
)

:argLoop
if /i "%1"=="" goto :install

if /i "%1"=="-b" (
    set buildType=%2
    shift
)

if /i "%1"=="-f" (
    set force="-f"
)

if /i "%1" == "-h" (
    goto :usage
)

shift
goto :argLoop

:install

dotnet run -p %scriptDir%\src\install -- --build %buildType% --platform %platform% %force% -i %installDir%

exit 0

:usage
    echo.
    echo  install.cmd [-p] [-h] [-b ^<BUILD TYPE^>]
    echo.
    echo      -b ^<BUILD TYPE^> : Build type, can be Debug or Release.
    echo      -f              : Force install (overwrite)
    echo      -h              : Show this message.
    echo. 
    exit -1