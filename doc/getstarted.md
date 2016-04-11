# Quick start guide to running diffs in a CoreCLR tree across multiple platforms.

## Assumptions

This guide assumes that you have built a CoreCLR and have produced a crossgen executable and mscorlib assembly.  See the [CoreCLR](https://github.com/dotnet/coreclr) GitHub repo for directions on building.

## Build
Build mcgutils using the build script in the root of the repo - (build.\{cmd,sh\}). By default the script just builds the tools and does not publish them in a seperate directory.  To install the utilities add the '-p'flag which publishes each utility as a standalone app in a directory under ./bin in the root of the repo.  Additionally to publish the default set of frameworks that can be used for diff'ing cross-platform add '-f'.

Add usage here...

## Producing a baseline for CoreCLR

Today there are two scenarios within CoreCLR depending on platform.  This is largly a function of building the tests and windows is further ahead here.  Today you have to consume the results of a Windows test build on Linux and OSX to run tests and the set up can be involved.  (See CoreCLR repo unix test instructions [here](https://github.com/dotnet/coreclr/blob/master/Documentation/building/unix-test-instructions.md)) This leads to the following two scenarios.

### Scenario 1 - Running the mscorlib and frameworks diffs using just the assemblies made available by mcgutils.

Running the build script as mentioned above with '-f' produces a standalone './fx' directory in the root of the repo.  This can be used as inputs to the diff tool and gives the developer a simplified flow if 1) a platform builds CoreCLR/mscorlib and 2) the diff utilities build.

Steps:
1.  Ensure corediff and mcgdiff are on the path.
2.  Invoke command
``` 
> corediff --base <coreclr_repo>/bin/Product/<platform>/crossgen --output <output_directory> --core_root <mcgutils_repo>/fx
```
3. Check output directory
```
> ls <output_directory>/base/*
```
The output directory will contain a list of *.dasm files produced by the code generator.  These are ultimatly what are diff'ed.

### Scenario 2 - Running mscorlib, frameworks, and test assets diffs using the resources generated for a CoreCLR test run.

In this scenario follow the steps outlined in CoreCLR to set up for the tests a given platform.  This will create a "core_root" directory in the built test assets that has all the platform frameworks as well as test dependencies.  This should be used as the 'core_root' for the test run in addition to providing the test assemblies.

Steps:
1. Ensure corediff and mcgdiff are on the path.
2. Invoke command
```
> corediff --base <coreclr_repo>/bin/Product/<platform>/crossgen --output <output_directory> --core_root <test_root>/core_root --test_root <test_root>
```
3. Check putput directory
```
> ls <output_directory>/base/*
```
The base output directory should contane a tree that mirrors the test tree containing a *.dasm for each assembly it found.

This scenario will take a fair bit longer than the first since it traverses and identifies test assembles in addition to the mscorlib/frameworks *.asm.

## Producing diff output for CoreCLR

In simple terms you just run the base directions but instead of passing '--base' you pass '--diff' and use a path to a different CoreCLR crossgen.

This diff system is built on crossgen so producing a new crossgen with a new code generator - either through modifying your base CoreCLR repo, adding a second repo with changes, or pulling from build lab resource - and running it with '--diff' will produce a parallel 'diff' tree in the output with diffable *.dasm.

Below are the two scenarios listed above with modifications for producing a 'diff' tree.

### Scenario 1 - Running the mscorlib and frameworks diffs using just the assemblies made available by mcgutils.

Running the build script as mentioned above with '-f' produces a standalone './fx' directory in the root of the repo.  This can be used as inputs to the diff tool and gives the developer a simplified flow if 1) a platform builds CoreCLR/mscorlib and 2) the diff utilities build.

Steps:
1.  Ensure corediff and mcgdiff are on the path.
2.  Invoke command
``` 
> corediff --diff <diff_coreclr_repo>/bin/Product/<platform>/crossgen --output <output_directory> --core_root <mcgutils_repo>/fx
```
3. Check output directory
```
> ls <output_directory>/diff/*
```
The diff output directory will contain a list of *.dasm files produced by the code generator.  These are ultimatly what are diff'ed.

### Scenario 2 - Running mscorlib, frameworks, and test assets diffs using the resources generated for a CoreCLR test run.

In this scenario follow the steps outlined in CoreCLR to set up for the tests a given platform.  This will create a "core_root" directory in the built test assets that has all the platform frameworks as well as test dependencies.  This should be used as the 'core_root' for the test run in addition to providing the test assemblies.

Steps:
1. Ensure corediff and mcgdiff are on the path.
2. Invoke command
```
> corediff --diff <diff_coreclr_repo>/bin/Product/<platform>/crossgen --output <output_directory> --core_root <test_root>/core_root --test_root <test_root>
```
3. Check putput directory
```
ls <output_directory>/diff/*
```

## Putting it all together

In the above example we showed how to produce base and diff *.dasm in seperate steps but if a developer has two seperate sets of CoreCLR binaries - produced from two CoreCLR repos, or extracted from the lab - both '--base' and '--diff' arguments to corediff may be specified at the same time.  The tool will run the inputs through both tools (though not in parallel today) and produce the 'base' and 'diff' directories of output.

```
> corediff --base <coreclr_repo>/bin/Product/<platform>/crossgen --diff <diff_coreclr_repo> --output <output_directory> --core_root <core_root_directory> [ --test_root <test_root> ]
```

Note: that this may be used with either the built 'core_root' or with the mcgutils internal './fx' directory.

### Notes on tags

Corediff allows a user supplied '--tag' on the commandline.  This tag can be used to label different directories of *.dasm with in the output directory so multiple (more than two) runs can be done.  This supports a scenario like the following:

* Build base CoreCLR
* Produce baseline diffs by invoking the tool with '--base'
* Make changes to CoreCLR JIT subdirectory to fix a bug.
* Produce tagged output by invoking corediff --diff ...  --tag "bugfix1"
* Make changes to CoreCLR JIT subdirectory to address review feedback/throughput issue.
* Produce tagged output by invoking corediff --diff ... --tag "reviewed1"
* Address more review feedback in CoreCLR JIT.
* Produce tagged output by invoking corediff --diff ... --tag "reviewed_final"
* ...

The above scenario should show that there is some flexability in the work flow.
