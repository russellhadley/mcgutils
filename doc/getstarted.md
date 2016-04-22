# Quick start guide to running diffs in a CoreCLR tree across multiple platforms.

## Assumptions

This guide assumes that you have built a CoreCLR and have produced a crossgen 
executable and mscorlib assembly. See the [CoreCLR](https://github.com/dotnet/coreclr) 
GitHub repo for directions on building.

## Dependencies

* dotnet cli - All the utilities in the repo rely on dotnet cli for packages and building.  
  The `dotnet` tool needs to be on the path. You can find information on installing dotnet cli
  on the official product page at http://dotnet.github.io/ or in the [dotnet cli GitHub repo](https://github.com/dotnet/cli)
  (where you can install more recent, "daily" builds). If installing from the GitHub repo packages,
  be sure to install the ".NET Core SDK" package, which includes the command-line tool.
  Note: mcgutils require a dotnet cli version after the pre-V1 RC1 build (version?)
  since that build does not include all the required features.
* git - The analyze tool uses `git diff` to check for textual differences since this is
  consistent across platforms, and fast.

## Build the tools

Build mcgutils using the build script in the root of the repo: build.\{cmd,sh\}. By 
default the script just builds the tools and does not publish them in a separate directory. 
To install the utilities add the '-p' flag which publishes each utility as a standalone app 
in a directory under ./bin in the root of the repo.  Additionally, to publish the default set 
of frameworks that can be used for diff'ing cross-platform, add '-f'.

```
 $ ./build.sh -h

build.sh [-p] [-h] [-b <BUILD TYPE>]

    -b <BUILD TYPE> : Build type.
    -h              : Show this message
    -p              : Publish apps.
    -f              : Install scratch framework directory in <script_root>/fx.
```

## 50,000 foot view

There are two different different diff tools in this repo and they both work together to make a 
diff run.  The first, mcgdiff, is the tool that knows how to generate assembly code into a \*.dasm file.  It's intended 
to be simple.  It takes a base and/or diff crossgen and drives it to produce a \*.dasm file on the 
specified output path.  Mcgdiff doesn't have any internal knowledge of frameworks, file names 
or directory names, rather it is a low level tool for generating disassembly output.  Corediff 
on the other hand knows about interesting frameworks to generate output for, understands 
the structure of the built test tree in CoreCLR, and generally holds the "how" or the policy part 
of a diff run.  With this context, corediff drives the mcgdiff tool to make an output a particular 
directory structure for coreclr.  With this in mind what follows is an outline of a few ways to 
generate diffs for CoreCLR using corediff and mcgdiff.  This is a tactical approach and it tries 
to avoid extraneous discussion of internals.

## Producing a baseline for CoreCLR

Today there are two scenarios within CoreCLR depending on platform.  This is largely a function 
of building the tests and Windows is further ahead here.  Today you have to consume the results 
of a Windows test build on Linux and OSX to run tests and the set up can be involved.  (See 
CoreCLR repo unix test instructions 
[here](https://github.com/dotnet/coreclr/blob/master/Documentation/building/unix-test-instructions.md)) 
This leads to the following two scenarios.

### Scenario 1 - Running the mscorlib and frameworks diffs using just the assemblies made available by mcgutils.

Running the build script as mentioned above with '-f' produces a standalone './fx' directory in 
the root of the repo.  This can be used as inputs to the diff tool and gives the developer a 
simplified flow if 1) a platform builds CoreCLR/mscorlib and 2) the diff utilities build.

Steps:
* Build a baseline CoreCLR by following build directions in coreclr repo 
  [build doc directory](https://github.com/dotnet/coreclr/tree/master/Documentation/building).
* Ensure corediff and mcgdiff are on the path.
* Create an empty output directory.
* Invoke command
``` 
> corediff --base <coreclr_repo>/bin/Product/<platform>/crossgen --output <output_directory> --core_root <mcgutils_repo>/fx
```
* Check output directory
```
> ls <output_directory>/base/*
```
The output directory will contain a list of *.dasm files produced by the code generator. These 
are ultimately what are diff'ed.

### Scenario 2 - Running mscorlib, frameworks, and test assets diffs using the resources generated for a CoreCLR test run.

In this scenario follow the steps outlined in CoreCLR to set up the tests for a given platform. This 
will create a "core_root" directory in the built test assets that has all the platform frameworks 
as well as test dependencies.  This should be used as the 'core_root' for the test run in addition 
to providing the test assemblies.

Steps:
* Build a baseline CoreCLR by following build directions in coreclr repo 
  [build doc directory](https://github.com/dotnet/coreclr/tree/master/Documentation/building).
* Ensure corediff and mcgdiff are on the path.
* Create an empty output directory.
* Invoke command
```
> corediff --base <coreclr_repo>/bin/Product/<platform>/crossgen --output <output_directory> --core_root <test_root>/core_root --test_root <test_root>
```
* Check output directory
```
> ls <output_directory>/base/*
```
The base output directory should contain a tree that mirrors the test tree containing a *.dasm for 
each assembly it found.

This scenario will take a fair bit longer than the first since it traverses and identifies test 
assembles in addition to the mscorlib/frameworks *.asm.

## Producing diff output for CoreCLR

In simple terms you just run the base directions but instead of passing '--base' you pass '--diff' 
and use a path to a different CoreCLR crossgen.

This diff system is built on crossgen so producing a new crossgen with a new code generator - either 
through modifying your base CoreCLR repo, adding a second repo with changes, or pulling from build 
lab resource - and running it with '--diff' will produce a parallel 'diff' tree in the output with 
diff'able *.dasm.

Below are the two scenarios listed above with modifications for producing a 'diff' tree.

### Scenario 1 - Running the mscorlib and frameworks diffs using just the assemblies made available by mcgutils.

Running the build script as mentioned above with '-f' produces a standalone './fx' directory in the root 
of the repo.  This can be used as inputs to the diff tool and gives the developer a simplified flow if 1) 
a platform builds CoreCLR/mscorlib and 2) the diff utilities build.

Steps:
* Build a new crossgen - either in a new repo or new branch in the current repo.
* Ensure corediff and mcgdiff are on the path.
* Reuse same output directory from above.
* Invoke command
``` 
> corediff --diff <diff_coreclr_repo>/bin/Product/<platform>/crossgen --output <output_directory> --core_root <mcgutils_repo>/fx
```
* Check output directory
```
> ls <output_directory>/diff/*
```
The diff output directory will contain a list of *.dasm files produced by the code generator. These are 
ultimatly what are diff'ed.

### Scenario 2 - Running mscorlib, frameworks, and test assets diffs using the resources generated for a CoreCLR test run.

In this scenario follow the steps outlined in CoreCLR to set up for the tests a given platform. This will 
create a "core_root" directory in the built test assets that has all the platform frameworks as well as 
test dependencies.  This should be used as the 'core_root' for the test run in addition to providing the 
test assemblies.

Steps:
* Build a new crossgen - either in a new repo or new branch in the current repo.
* Ensure corediff and mcgdiff are on the path.
* Reuse same output directory from above.
* Invoke command
```
> corediff --diff <diff_coreclr_repo>/bin/Product/<platform>/crossgen --output <output_directory> --core_root <test_root>/core_root --test_root <test_root>
```
* Check output directory
```
ls <output_directory>/diff/*
```

## Putting it all together

In the above example we showed how to produce base and diff *.dasm in separate steps but if a developer has 
two separate sets of CoreCLR binaries - produced from two CoreCLR repos, or extracted from the lab - both 
'--base' and '--diff' arguments to corediff may be specified at the same time.  The tool will run the inputs 
through both tools (though not in parallel today) and produce the 'base' and 'diff' directories of output.

```
> corediff --base <coreclr_repo>/bin/Product/<platform>/crossgen --diff <diff_coreclr_repo> --output <output_directory> --core_root <core_root_directory> [ --test_root <test_root> ]
```

Note: that this may be used with either the built 'core_root' or with the mcgutils internal './fx' directory.

### Notes on tags

Corediff allows a user supplied '--tag' on the command-line.  This tag can be used to label different 
directories of *.dasm in the output directory so multiple (more than two) runs can be done. 
This supports a scenario like the following:

* Build base CoreCLR
* Produce baseline diffs by invoking the tool with '--base'
* Make changes to CoreCLR JIT subdirectory to fix a bug.
* Produce tagged output by invoking corediff --diff ...  --tag "bugfix1"
* Make changes to CoreCLR JIT subdirectory to address review feedback/throughput issue.
* Produce tagged output by invoking corediff --diff ... --tag "reviewed1"
* Address more review feedback in CoreCLR JIT.
* Produce tagged output by invoking corediff --diff ... --tag "reviewed_final"
* ...

The above scenario should show that there is some flexibility in the work flow.

## Analyzing diffs

The mcgutils suite includes the analyze tool to speed up analyzing diffs produced by corediff/mcgdiffs utilities.
This tool cracks the *.dasm files produced in the earlier steps and extracts the bytes difference between 
the two.  This data is keyed by file and method name - for instance two files with different names will not 
diff even if passed as the base and diff since the tool is looking to identify files missing from the base 
dataset vs the diff dataset.

Here is the help output:
```
$ analyze --help
usage: analyze [-b <arg>] [-d <arg>] [-r] [-c <arg>] [-w] [--json <arg>]
               [--csv <arg>]

    -b, --base <arg>     Base file or directory.
    -d, --diff <arg>     Diff file or directory.
    -r, --recursive      Search directories recursively.
    -c, --count <arg>    Count of files and methods (at most) to output
                         in the summary. (count) improvements and
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

For the simplest case just point the tool at a base and diff dir produce by corediff and it 
will outline byte diff across the whole diff. On an significant set of diffs it will produce output 
like the following:

```
$ analyze --base ~/Work/glue/output/base --diff ~/Work/glue/output/diff
Found files with textual diffs.

Summary:
(Note: Lower is better)

Total bytes of diff: -4124
    diff is an improvement.

Top file regressions by size (bytes):
    193 : Microsoft.CodeAnalysis.dasm
    154 : System.Dynamic.Runtime.dasm
    60 : System.IO.Compression.dasm
    43 : System.Net.Security.dasm
    43 : System.Xml.ReaderWriter.dasm

Top file improvements by size (bytes):
    -1804 : mscorlib.dasm
    -1532 : Microsoft.CodeAnalysis.CSharp.dasm
    -726 : System.Xml.XmlDocument.dasm
    -284 : System.Linq.Expressions.dasm
    -239 : System.Net.Http.dasm

21 total files with size differences.

Top method regessions by size (bytes):
    328 : Microsoft.CodeAnalysis.CSharp.dasm - Microsoft.CodeAnalysis.CSharp.Syntax.InternalSyntax.DocumentationCommentXmlTokens:.cctor()
    266 : Microsoft.CodeAnalysis.CSharp.dasm - Microsoft.CodeAnalysis.CSharp.MethodTypeInferrer:Fix(int,byref):bool:this
    194 : mscorlib.dasm - System.DefaultBinder:BindToMethod(int,ref,byref,ref,ref,ref,byref):ref:this
    187 : Microsoft.CodeAnalysis.CSharp.dasm - Microsoft.CodeAnalysis.CSharp.Syntax.InternalSyntax.LanguageParser:ParseModifiers(ref):this
    163 : Microsoft.CodeAnalysis.CSharp.dasm - Microsoft.CodeAnalysis.CSharp.Symbols.SourceAssemblySymbol:DecodeWellKnownAttribute(byref,int,bool):this

Top method improvements by size (bytes):
    -160 : System.Xml.XmlDocument.dasm - System.Xml.XmlTextWriter:AutoComplete(int):this
    -124 : System.Xml.XmlDocument.dasm - System.Xml.XmlTextWriter:WriteEndStartTag(bool):this
    -110 : Microsoft.CodeAnalysis.CSharp.dasm - Microsoft.CodeAnalysis.CSharp.MemberSemanticModel:GetEnclosingBinder(ref,int):ref:this
    -95 : Microsoft.CodeAnalysis.CSharp.dasm - Microsoft.CodeAnalysis.CSharp.CSharpDataFlowAnalysis:AnalyzeReadWrite():this
    -85 : Microsoft.CodeAnalysis.CSharp.dasm - Microsoft.CodeAnalysis.CSharp.Syntax.InternalSyntax.LanguageParser:ParseForStatement():ref:this

3762 total methods with size differences.
```

If `--csv <file_name>` or `--json <file_name>` is passed, all the diff data extracted and analyzed 
will be written out for futher analysis.
