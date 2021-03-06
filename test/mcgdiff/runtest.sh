# Runs a set of simple tests to validate that mcgdiff is working
#
# Required input is a path to a single built CoreCLR repo as well as
# the built mcgdiff executable.
#
# Tests will run through the simple scenarios to ensure that the flags work,
# as well as output structure being laid out as expected.
#

# Test 1: Run mcgdiff with the same crossgen to verify that --base, --diff
# work with --frameworks and that the output is generated with the correct 
# 'base' and 'diff' tags.

#set -x #echo on

# Process the incoming arguments and extract the location info needed.

while getopts :m:c:o: opt; do
    case $opt in
        m)
            MCGDIFF=$OPTARG
            ;;
        o)
            OUTPUT=$OPTARG
            ;;
        c)
            CROSSGEN=$OPTARG
            ;;
        :)
            echo "-$OPTARG requires an argument"
            exit -1
            ;;
    esac
done

# Test that we have the needed info to run the test.

if [ -z "$MCGDIFF" ]; then
    echo "Missing mcgdiff path."
    exit -1
fi

if [ -z "$CROSSGEN" ]; then
    echo "Missing crossgen path."
    exit -1
fi

if [ -z "$OUTPUT" ]; then
    echo "Missing output."
    exit -1
fi

# Create disasm of mscorlib in base/diff form.

echo Running: dotnet $MCGDIFF --base $CROSSGEN --diff $CROSSGEN --output $OUTPUT ${CROSSGEN%/*}/mscorlib.dll

if ! dotnet $MCGDIFF --base $CROSSGEN --diff $CROSSGEN --output $OUTPUT ${CROSSGEN%/*}/mscorlib.dll; then
    echo "Error! Managed code gen diff failed to generate disasm."
    exit -1
fi

# test that output has 'base' and 'diff' and
# that mscorlib.dasm appears.

if ! ls $OUTPUT/base/mscorlib.dasm; then 
    echo "missing base disasm!"
    exit -1
fi

if ! ls $OUTPUT/diff/mscorlib.dasm; then
    echo "missing diff disasm!"
    exit -1
fi

# verify that mscorlib.dasm is nodiff.

if ! diff $OUTPUT/diff/mscorlib.dasm $OUTPUT/base/mscorlib.dasm; then
    echo "Error! Found differences."
    exit -1
fi

echo mcgdiff passed validation.