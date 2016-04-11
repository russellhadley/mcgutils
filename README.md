# Managed Code Generation Utilities - mcgutil
This repo holds a collection of utilites used by the Managed CodeGen team 
to automate tasks when working on CoreCLR.  Initial utilies are around 
producing diffs of codegen changes.

TL;DR Run build.\{cmd|sh\} -f -p to create a bin directory in the root of the repo.  Add bin/mcgdiff and bin/corediff to your path and you can call them to generate *.dasm files.

For a more complete introduction look at the [getting started guide](doc/gettingstarted.md).

## mcgdiff
This is a general tool to produce diffs for compiled MSIL assemblies.  The 
tool relies on producing a base and diff crossgen.exe, either by using a
prebuilt base from the CI builds and a local experimental build, or 
building both a base and diff locally.

Sample help commandline:
```
    [D:\glue\mcgdiff]
    16:30:04 > mcgdiff --help

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
for other projects a new utility could be produced that works in a similar way
could be created.

Sample help commandline:
```
    [D:\glue\mcgdiff]
    16:32:10 > corediff --help

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

## packages
This is a skeleton project that exists to pull down a predicitable set of framework assemblies and publish them in the root in the subdirectory './fx'.  Today this is set to the RC2 version of the NetCoreApp1.0 frameworks.  When this package is installed via the build.\{cmd|sh\} script this  set can be used on any supported platform for diffing.  Note: The RC2 mscorlib.dll is removed, this assembly should be updated from the selected base runtime that is under test for consistency.
To add particular packages to the set you diff, add their dependencies to the project.json in this project and they will be pulled in and published in the standalone directory './fx'.