# Run analysis tool with indentical input and ensure that we get no diffs.

if analyze --base ./base/test.dasm --diff ./base/test.dasm; then
    echo "Passed null diff test"
else
    echo "Failed"
fi

analyze --base ./base/test.dasm --diff ./diff/test.dasm > test.out
RESULT=$?
#echo $RESULT
if [ $RESULT == 2 ]; then
    echo "Passed base diff case"
else
    echo "Failed"
fi

if diff ./test.out ./baseline.out; then
    echo "Passed baseline check"
else
    echo "Failed baseline check"
fi    