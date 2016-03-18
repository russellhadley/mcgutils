# mcgdiff - Managed CodeGen Difference

Repo contains a utility to drive the dotnet crossgen tool to produce
binary disassembly from the JIT compiler.  This can be used to create
diffs to check ongoing development.

To build/setup:

* Download dotnet cli.  Follow install instructions and get dotnet on your
  your path.
* Do 'dotnet restore --configfile ./NuGet.config' to create lock file and 
  pull down required packages.
* Issue a 'dotnet build' command.  This should download the appropraite
  packages and create a mcgdiff binary in the bin directory that you can
  use to drive creation of diffs.
* Run runtest.sh to see how tool can be driven.  Note that the runtest.sh
  script requires the path to the built mcgdiff as well as CoreCLR crossgen
  and an output directory.