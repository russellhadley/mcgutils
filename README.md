# Managed Code Generation Utilities - mcgutil
This repo holds a collection of utilites used by the Managed CodeGen team 
to automate tasks when working on CoreCLR.  Initial utilies are around 
producing diffs of codegen changes.

## mcgdiff
This is a general tool to produce diffs for compiled MSIL assemblies.  The 
tool relies on producing a base and diff crossgen.exe, either by using a
prebuilt base from the CI builds and a local experimental build, or 
building both a base and diff locally.

Sample help commandline:
```
    [D:\glue\mcgdiff]
    16:30:04 > dotnet run -p .\src\mcgdiff -- --help

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
    16:32:10 > dotnet run -p .\src\corediff -- --help

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

## install
A simple cross platform C# installer for other tools in the repo.  Assumes a 
complete build and publish of other tools. This installer creates a layout of
stand alone binaries in a target directory each sub directory of which can be
added to the path to allow it to be invoked independently.

Sample help commandline:
```
    D:\glue\mcgdiff]
    16:35:28 > dotnet run -p .\src\install -- --help

    usage: install [-b <arg>] [-p <arg>] [-i <arg>] [-f]
        -b, --build <arg>       Build type. May be Debug or Release.
        -p, --platform <arg>    The platform to install.
        -i, --install <arg>     The install path.
        -f, --force             Overwrite pre-existing files.
```

Sample install commandline:
```
    [D:\glue\mcgdiff]
    16:38:06 > dotnet run -p .\src\install -- --build Debug --platform win10-x64 -i D:\glue\install

    Installing corediff to D:\glue\install\corediff
        from D:\glue\mcgdiff\src\corediff\bin\Debug\netcoreapp1.0\win10-x64\publish
    Installing mcgdiff to D:\glue\install\mcgdiff
        from D:\glue\mcgdiff\src\mcgdiff\bin\Debug\netcoreapp1.0\win10-x64\publish
```