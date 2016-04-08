## Install published utilites to a directory
##
## Runs the cross-platform C# installer in place to create layout
## in install directory (default <script dir>/bin).  To update what
## get's installed please look at C# installer which has direct knowledge
## of the projects in the files.

scriptDir="`dirname \"$0\"`"
buildType="Release"
platform="`dotnet --info | awk '/RID/ {print $2}'`"
# default install in 'bin' dir at script location
installDir="$scriptDir/bin"
force=""

function usage
{
    echo ""
    echo "install.sh [-f] [-h] [-b <BUILD TYPE>]"
    echo ""
    echo "    -b <BUILD TYPE> : Build type."
    echo "    -h              : Show this message"
    echo "    -f              : Force install (overwrite)."
    echo ""

}

# process for'-h', '-p', and '-b <arg>'
while getopts "hfb:" opt; do
    case "$opt" in
    h)
        usage
        exit 0
        ;;
    f)
        force="-f";
        ;;
    b)  
        buildType=$OPTARG
        ;;
    esac
done
## parameter parsing here

mkdir $installDir

dotnet run -p $scriptDir/src/install -- --build $buildType --platform $platform $force -i $installDir
