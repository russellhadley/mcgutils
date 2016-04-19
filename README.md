# Managed Code Generation Utilities - mcgutils
This repo holds a collection of utilites used by the Managed CodeGen team 
to automate tasks when working on CoreCLR.  Initial utilies are around 
producing diffs of codegen changes.

TL;DR Run build.\{cmd|sh\} -f -p to create a bin directory in the root of the repo. 
Add subdirectories for each tool to your path and you can call them to generate/analyze 
*.dasm files.

For a more complete introduction look at the [getting started guide](doc/gettingstarted.md).

## mcgdiff
This is a general tool to produce diffs for compiled MSIL assemblies.  The 
tool relies on a base and diff crossgen.exe as inputs, either by using a
prebuilt base from the CI builds and a local experimental build, or 
building both a base and diff locally.

Sample help commandline:
```
    $ mcgdiff --help

    usage: mcgdiff [-b <arg>] [-d <arg>] [-o <arg>] [-t <arg>] [-f <arg>]
                   [-r] [-p <arg>] [--] <assembly>...
        -b, --base <arg>        The base compiler exe.
        -d, --diff <arg>        The diff compiler exe.
        -o, --output <arg>      The output path.
        -t, --tag <arg>         Name of root in output directory.  Allows
                                for many sets of output.
        -f, --file <arg>        Name of file to take list of assemblies
                                from. Both a file and assembly list can be
                                used.
        -r, --recursive         Scan directories recursively.
        -p, --platform <arg>    Path to platform assemblies
        <assembly>...           The list of assemblies or directories to
                                scan for assemblies.
```

## corediff
Is a specific tool targeting CoreCLR.  It has a prebaked list of interesting
assemblies to dump and understands enough of the structure to make it more
streamlined.  Corediff uses mcgdiff under the covers to produce the diffs so 
for other projects a new utility could be produced that works in a similar way.

Sample help commandline:
```
    $ corediff --help

    usage: corediff [-b <arg>] [-d <arg>] [-o <arg>] [-t <arg>]
                    [--core_root <arg>] [--test_root <arg>]
        -b, --base <arg>      The base compiler exe.
        -d, --diff <arg>      The diff compiler exe.
        -o, --output <arg>    The output path.
        -t, --tag <arg>       Name of root in output directory.  Allows for
                              many sets of output.
        --core_root <arg>     Path to test CORE_ROOT.
        --test_root <arg>     Path to test tree
```

## analyze
The analyze tool understand the format of the *.dasm files in a diff and can extract
this data to produce a summary and/or dump the info to a unified data file (CSC, JSON)
The common usage of the tool is to extract interesting diffs, if any, from a diff run as
part of the development progresses.

Sample help commandline:
```
    $ analyze --help
    usage: analyze [-b <arg>] [-d <arg>] [-r] [-f] [-c <arg>] [-w]
                [--json <arg>] [--csv <arg>]

        -b, --base <arg>     Base file or directory.
        -d, --diff <arg>     Diff file or directory.
        -r, --recursive      Search directories recursively.
        -f, --full           Output full analysis to stdout (rather than a
                            summary).
        -c, --count <arg>    Count of files and methods (at most) to output
                            in the symmary. (count) improvements and
                            (count) regressions of each will be included.
                            (default 5)
        -w, --warn           Generate warning output for files/methods that
                            only exists in one dataset or the other (only
                            in base or only in diff).
        --json <arg>         Dump analysis data to specified file in JSON
                            format.
        --csv <arg>          Dump analysis data to specified file in CSV
                            format.
```


## packages
This is a skeleton project that exists to pull down a predicitable set of framework 
assemblies and publish them in the root in the subdirectory './fx'.  Today this is 
set to the RC2 version of the NetCoreApp1.0 frameworks.  When this package is installed 
via the build.\{cmd|sh\} script this  set can be used on any supported platform for 
diffing.  Note: The RC2 mscorlib.dll is removed, this assembly should be updated from 
the selected base runtime that is under test for consistency. To add particular packages 
to the set you diff, add their dependencies to the project.json in this project and 
they will be pulled in and published in the standalone directory './fx'.