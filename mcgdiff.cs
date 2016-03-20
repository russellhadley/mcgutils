///////////////////////////////////////////////////////////////////////////////
//
//  mcgdiff - The managed code gen diff tool scripts the generation of
//  diffable assembly code output from the crossgen ahead of time compilation
//  tool.  This enables quickly generating A/B comparisons of .Net codegen
//  tools to validate ongoing development.
//
//  Scenario 1: Pass A and B compilers to mcgdiff.  Using the --base and --diff
//  arguments pass two seperate compilers and diff mscorlib (default location
//  in base CoreCLR directory) or passed set of assemblies.  This is the most
//  common scenario.
//
//  Scenario 2: Iterativly call mcgdiff with a series of compilers tagging
//  each run.  Allows for producing a broader set of results like 'base',
//  'experiment1', 'experiment2', and 'experiment3'.  This tagging is only
//  allowed in the case where a single compiler is passed to avoid any
//  confusion in the generated results.
//
//  usage: mcgdiff [-c] [-b <arg>] [-d <arg>] [-o <arg>] [-t <arg>] [-r]
//                 [--] <assembly>...
//
//      -c, --corelib             Generate asm for corelib assemblies.
//      -b, --base <arg>          The base compiler exe.
//      -d, --diff <arg>          The diff compiler exe.
//      -o, --output <arg>        The output path.
//      -t, --tag <arg>           Name of root in output directory.  Allows
//                                for many sets of output.
//      -r, --recursive           Scan directories recursivly.
//      <assembly>...             The list of assemblies or directories to
//                                scan for assemblies.
//


using System;
using System.Diagnostics;
using System.CommandLine;
using System.IO;
using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;

namespace ManagedCodeGen
{
    // Define options to be parsed 
    public class Config
    {
        private ArgumentSyntax syntaxResult;
        private bool genCorelib = false;
        private string baseExe = null;
        private string diffExe = null;
        private string rootPath = null;
        private string tag = null;
        private IReadOnlyList<string> assemblyList = Array.Empty<string>();
        private bool wait = false;
        private bool recursive = false;
        private IReadOnlyList<string> methods = Array.Empty<string>();
        
        public Config(string[] args) {

            syntaxResult = ArgumentSyntax.Parse(args, syntax =>
            {
                syntax.DefineOption("c|corelib", ref genCorelib, "Generate asm for corelib assemblies");
                syntax.DefineOption("b|base", ref baseExe, "The base compiler exe.");
                syntax.DefineOption("d|diff", ref diffExe, "The diff compiler exe.");
                syntax.DefineOption("o|output", ref rootPath, "The output path.");
                syntax.DefineOption("t|tag", ref tag, "Name of root in output directory.  Allows for many sets of output.");
                
                var waitArg = syntax.DefineOption("w|wait", ref wait, "Wait for debugger to attach.");
                waitArg.IsHidden = true;
                
                syntax.DefineOption("r|recursive", ref recursive, "Scan directories recursively.");
                var methodsArg = syntax.DefineOptionList("m|methods", ref methods, "List of methods to disasm.");
                methodsArg.IsHidden = true;
                
                // Warning!! - Parameters must occur after options to preserve parsing semantics.

                syntax.DefineParameterList("assembly", ref assemblyList, "The list of assemblies or directories to scan for assemblies.");
            });
            
            // Run validation code on parsed input to ensure we have a sensible scenario.
            
            validate();
        }
        
        // Validate supported scenarios
        // 
        //    Scenario 1:  --base and --diff
        //       Pass two tools in and generate a set of disassembly with each.  Result directories will be tagged with
        //       "base" and "diff" in the output dir.
        //
        //    Scenario 2:  --base or --diff with --tag
        //       Pass single tool as either --base or --diff and tag the result directory with a user supplied tag.
        //
        private void validate() {
            
            if ((baseExe == null) && (diffExe == null)) {
                syntaxResult.ReportError("Specify --base and/or --diff.");
            }
            
            if ((tag != null) && (diffExe != null)  && (baseExe != null)) {
                syntaxResult.ReportError("Multiple compilers with the same tag: Specify --diff OR --base seperatly with --tag (one compiler for one tag).");
            }
            
            if ((genCorelib == false) && (assemblyList.Count == 0)) {
                syntaxResult.ReportError("No input: Specify --frameworks or input assemblies.");
            }
            
            // Check that we can find the baseExe.
            if (!File.Exists(baseExe)) {
                syntaxResult.ReportError("Can't find --base tool.");   
            } else {
                // Set to full path for the command resolution logic.
                string fullBasePath = Path.GetFullPath(baseExe);
                baseExe = fullBasePath;
            }
            
            // Check that we can find the diffExe.
            if (!File.Exists(diffExe)) {
                syntaxResult.ReportError("Can't find --diff tool.");
            } else {
                // Set to full path for command resolution logic.
                string fullDiffPath = Path.GetFullPath(diffExe);
                diffExe = fullDiffPath;
            }
        }
        
        public bool GenCorelib { get { return genCorelib; }}
        public bool GenUserAssemblies { get { return AssemblyList.Count > 0; }}
        public bool DoFileOutput { get {return (this.RootPath != null);}}
        public bool WaitForDebugger { get { return wait; }}
        public bool GenerateBaseline { get { return (baseExe != null); }}
        public bool GenerateDiff { get { return (diffExe != null); }}
        public bool HasTag { get { return (tag != null); }}
        public bool Recursive { get { return recursive; }}
        public string BaseExecutable { get { return baseExe; }}
        public string DiffExecutable { get { return diffExe; }}
        public string RootPath { get { return rootPath; }}
        public string Tag { get { return tag; }}
        public IReadOnlyList<string> AssemblyList { get { return assemblyList; }}
    }

    public class AssemblyInfo {
        public string Name {get; set;}
        // Contains path to assembly.
        public string Path {get; set;}
        // Contains relative path within output directory for given assembly.
        // This allows for different output directories per tool.
        public string OutputPath {get; set;}
    }

    public class mcgdiff
    {
            // Define the set of assemblies we care about. NOTE: mscorlib.dll is treated specially.
            // It MUST be first in this array!
            // Also: #2 must be System, and #3 must be System.Core. This is because all assemblies hard bind to these.
            // TODO: Does crossgen also require these restrictions??
            private static string[] corelibAssemblies =
            {
            "mscorlib.dll"
            };

        
        public static void Main(string[] args)
        {
            // Parse and store comand line options.
            var config = new Config(args);

            // Stop to attach a debugger if desired.
            if (config.WaitForDebugger) {
                WaitForDebugger();
            }

            // Builds assemblyInfoList on mcgdiff
            List<AssemblyInfo> assemblyWorkList = GenerateAssemblyWorklist(config);
            
            // The disasm engine encapsulates a particular set of diffs.  An engine is
            // produced with a given code generator and assembly list, which then produces
            // a set of disasm outputs
            
            if (config.GenerateBaseline) {
                string taggedPath = null;
                if (config.DoFileOutput) {
                    string tag = "base";
                    if (config.HasTag) {
                        tag = config.Tag;
                    }
                    
                    taggedPath = Path.Combine(config.RootPath, tag);
                }
                
                DisasmEngine baseDisasm = new DisasmEngine(config.BaseExecutable, taggedPath, assemblyWorkList);
                baseDisasm.GenerateAsm();
            }
            
            if (config.GenerateDiff) {
                string taggedPath = null;
                if (config.DoFileOutput) {
                    string tag = "diff";
                    if (config.HasTag) {
                        tag = config.Tag;
                    }
                    
                    taggedPath = Path.Combine(config.RootPath, tag);
                }
                
                DisasmEngine diffDisasm = new DisasmEngine(config.DiffExecutable, taggedPath, assemblyWorkList);
                diffDisasm.GenerateAsm();
            }

        }

        private static void WaitForDebugger() {
            Console.WriteLine("Wait for a debugger to attach. Press ENTER to continue");
            Console.WriteLine($"Process ID: {Process.GetCurrentProcess().Id}");
            Console.ReadLine();
        }
       
        public static List<AssemblyInfo> GenerateAssemblyWorklist(Config config)
        {
            List<AssemblyInfo> assemblyInfoList = new List<AssemblyInfo>();
                
            if (config.GenCorelib) {
                // TODO get a path to a scratch project that pulls down the full list.  
                // For now we just will use mscorlib.
                var basePath = Path.GetDirectoryName(config.BaseExecutable);
                    
                // build list based on baked in list of assemblies                    
                foreach (var assembly in corelibAssemblies) {
                    // find assembly path, and compute output path.
                    AssemblyInfo info = new AssemblyInfo {
                        Name = assembly,
                        Path = basePath,
                        OutputPath =  ""
                    };
                        
                    assemblyInfoList.Add(info);
                }  
            }

            if (config.GenUserAssemblies) {
                var assemblyList = config.AssemblyList;
                Queue<string> workList = new Queue<string>(assemblyList); 
                    
                while (workList.Count != 0)
                {
                    var path = workList.Dequeue();
                    
                    FileAttributes attr = File.GetAttributes(path);
            
                    if ((attr & FileAttributes.Directory) == FileAttributes.Directory) {
                        // This is directory case.
                        System.Console.WriteLine("NYI - directory case");
                        Environment.Exit(-1);
                    }
                    else {
                        // This is the file case.

                        AssemblyInfo info = new AssemblyInfo {
                            Name = Path.GetFileName(path),
                            Path = Path.GetDirectoryName(path),
                            OutputPath = ""
                        };
                            
                        assemblyInfoList.Add(info);
                    }
                }
            }
                
            return assemblyInfoList;
        }       

        class DisasmEngine
        {   
            private string executablePath;
            private string rootPath = null;
            private List<AssemblyInfo> assemblyInfoList;

            public DisasmEngine(string executable, string outputPath, List<AssemblyInfo> assemblyInfoList)
            {
                this.executablePath = executable;
                this.rootPath = outputPath;
                this.assemblyInfoList = assemblyInfoList;
            }

            public void GenerateAsm()
            {
                // Build a command per assembly to generate the asm output.
                foreach (var assembly in this.assemblyInfoList)
                {
                    string fullpathAssembly = Path.Combine(assembly.Path, assembly.Name);
                    
                    if (!File.Exists(fullpathAssembly)) {
                        // Assembly not found.  Produce a warning and skip this input.
                        Console.WriteLine("Skipping. Assembly not found: {0}", fullpathAssembly);
                        continue;
                    }
                    
                    List<string> commandArgs = new List<string>() {fullpathAssembly};

                    Command generateCmd = Command.Create(
                        executablePath, 
                        commandArgs);

                    // Set up environment do disasm.
                    generateCmd.EnvironmentVariable("COMPlus_NgenDisasm", "*");

                    if (this.rootPath != null) {
                        // Generate path to the output file
                        var assemblyFileName = Path.ChangeExtension(assembly.Name, ".dasm");
                        var path = Path.Combine(rootPath, assembly.OutputPath, assemblyFileName);
                        
                        PathUtility.EnsureParentDirectory(path);

                        // Redirect stdout/stderr to disasm file and run command.
                        using (var outputStream = System.IO.File.Create(path)) {
                            using (var outputStreamWriter = new StreamWriter(outputStream)) {
                                
                                // Forward output and error to file.
                                generateCmd.ForwardStdOut(outputStreamWriter);
                                generateCmd.ForwardStdErr(outputStreamWriter);
                                generateCmd.Execute();
                            }
                        }
                    }
                    else {

                        // By default forward to output to stdout/stderr.
                        generateCmd.ForwardStdOut();
                        generateCmd.ForwardStdErr();
                        generateCmd.Execute();
                    }
                }
            }   
        }
    }
}
