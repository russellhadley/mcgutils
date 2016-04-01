# corediff - Diff CoreCLR tree

Repo contains a utility to produce diffs from a CoreCLR test layout via
the mcgdiff tool.

To build/setup:

* Download dotnet cli.  Follow install instructions and get dotnet on your
  your path.
* Do 'dotnet restore' to create lock file and 
  pull down required packages.
* Issue a 'dotnet build' command.  This will create a mcgdiff.dll in the bin
  directory that you can use to drive creation of diffs. Tool maybe invoked
  via 'dotnet <path_to_corediffdll>corediff.dll.
* Ensure that mcgdiff is on your path.  (See mcgdiff README.md for details
  on how to build)
* Invoke dotnet corediff.dll --frameworks --base <base crossgen> --diff <diff crossgen> --coreroot <path to core_root> --testroot <path to test_root>
