# mcgdiff - Managed CodeGen Difference

Repo contains a utility to drive the dotnet crossgen tool to produce
binary disassembly from the JIT compiler.  This can be used to create
diffs to check ongoing development.

To build/setup:

* Download dotnet cli.  Follow install instructions and get dotnet on your
  your path.
* Do 'dotnet restore' to create lock file and 
  pull down required packages.
* Issue a 'dotnet build' command.  This will create a mcgdiff.dll in the bin
  directory that you can use to drive creation of diffs. Tool maybe invoked
  via 'dotnet <path_to_mcgdiffdll>mcgdiff.dll.
* Run runtest.sh to see how tool can be driven.  Note that the runtest.sh
  script requires the path to the built mcgdiff as well as CoreCLR crossgen
  and an output directory.