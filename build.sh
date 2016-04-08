## Build and optionally publish sub projects
##
## This script will by default build release versions of the tools.
## If publish (-p) is requested it will create standalone versions of the
## tools in <root>/src/<project>/<buildType>/netcoreapp1.0/<platform>/Publish/.
## These tools can be installed via the install script (install.{sh|cmd}) in
## this directory.

function usage
{
    echo ""
    echo "build.sh [-p] [-h] [-b <BUILD TYPE>]"
    echo ""
    echo "    -b <BUILD TYPE> : Build type."
    echo "    -h              : Show this message"
    echo "    -p              : Publish."
    echo ""

}

# defaults
buildType="Release"
publish=false

# process for'-h', '-p', and '-b <arg>'
while getopts "hpb:" opt; do
    case "$opt" in
    h)
        usage
        exit 0
        ;;
    b)  buildType=$OPTARG
        ;;
    p)  publish=true
        ;;
    esac
done

#Select if we want to publish the standalone tools or not.
if [ "$publish" == true ]; then
    action="publish"
else
    action="build"
fi

# declare the array of projects   
declare -a projects=(mcgdiff corediff)

# for each project either build or publish
for proj in "${projects[@]}"
do
    dotnet $action -c $buildType ./src/$proj
done


