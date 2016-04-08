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
//  usage: mcgdiff [-b <arg>] [-d <arg>] [-o <arg>] [-t <arg>] [-r]
//                 [--] <assembly>...
//
//      -b, --base <arg>          The base compiler exe.
//      -d, --diff <arg>          The diff compiler exe.
//      -o, --output <arg>        The output path.
//      -p, --platform <arg>      Path to platform assemblies.
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
using System.Runtime.Loader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;

namespace ManagedCodeGen
{
    // Define options to be parsed 
    public class Config
    {
        private ArgumentSyntax syntaxResult;
        private string baseExe = null;
        private string diffExe = null;
        private string rootPath = null;
        private string tag = null;
        private string fileName = null;
        private IReadOnlyList<string> assemblyList = Array.Empty<string>();
        private bool wait = false;
        private bool recursive = false;
        private IReadOnlyList<string> methods = Array.Empty<string>();
        private string platformPath = null;
        
        public Config(string[] args) {

            syntaxResult = ArgumentSyntax.Parse(args, syntax =>
            {
                syntax.DefineOption("b|base", ref baseExe, "The base compiler exe.");
                syntax.DefineOption("d|diff", ref diffExe, "The diff compiler exe.");
                syntax.DefineOption("o|output", ref rootPath, "The output path.");
                syntax.DefineOption("t|tag", ref tag, "Name of root in output directory.  Allows for many sets of output.");
                syntax.DefineOption("f|file", ref fileName, "Name of file to take list of assemblies from. Both a file and assembly list can be used.");
                
                var waitArg = syntax.DefineOption("w|wait", ref wait, "Wait for debugger to attach.");
                waitArg.IsHidden = true;
                
                syntax.DefineOption("r|recursive", ref recursive, "Scan directories recursively.");
                syntax.DefineOption("p|platform", ref platformPath, "Path to platform assemblies");
                var methodsArg = syntax.DefineOptionList("m|methods", ref methods, 
                    "List of methods to disasm.");
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
        //       Pass two tools in and generate a set of disassembly with each.  Result directories
        //       will be tagged with "base" and "diff" in the output dir.
        //
        //    Scenario 2:  --base or --diff with --tag
        //       Pass single tool as either --base or --diff and tag the result directory with a user
        //       supplied tag.
        //
        private void validate() {
            
            if ((baseExe == null) && (diffExe == null)) {
                syntaxResult.ReportError("Specify --base and/or --diff.");
            }
            
            if ((tag != null) && (diffExe != null)  && (baseExe != null)) {
                syntaxResult.ReportError("Multiple compilers with the same tag: Specify --diff OR --base seperatly with --tag (one compiler for one tag).");
            }
            
            if ((fileName == null) && (assemblyList.Count == 0)) {
                syntaxResult.ReportError("No input: Specify --file <arg> or list input assemblies.");
            }
            
            // Check that we can find the baseExe.
            if (baseExe != null) {
                if (!File.Exists(baseExe)) {
                    syntaxResult.ReportError("Can't find --base tool.");   
                } else {
                    // Set to full path for the command resolution logic.
                    string fullBasePath = Path.GetFullPath(baseExe);
                    baseExe = fullBasePath;
                }
            }
            
            // Check that we can find the diffExe.
            if (diffExe != null) {
                if (!File.Exists(diffExe)) {
                    syntaxResult.ReportError("Can't find --diff tool.");
                } else {
                    // Set to full path for command resolution logic.
                    string fullDiffPath = Path.GetFullPath(diffExe);
                    diffExe = fullDiffPath;
                }
            }
            
            if (fileName != null) {
                if (!File.Exists(fileName)) {
                    var message = String.Format("Error reading input file {0}, file not found.", fileName);
                    syntaxResult.ReportError(message);
                }
            }
        }
        
        public bool HasUserAssemblies { get { return AssemblyList.Count > 0; }}
        public bool DoFileOutput { get {return (this.RootPath != null);}}
        public bool WaitForDebugger { get { return wait; }}
        public bool GenerateBaseline { get { return (baseExe != null); }}
        public bool GenerateDiff { get { return (diffExe != null); }}
        public bool HasTag { get { return (tag != null); }}
        public bool Recursive { get { return recursive; }}
        public bool UseFileName { get { return (fileName != null); }}
        public string BaseExecutable { get { return baseExe; }}
        public string DiffExecutable { get { return diffExe; }}
        public string RootPath { get { return rootPath; }}
        public string PlatformPath { get {return platformPath; }}
        public string Tag { get { return tag; }}
        public string FileName { get { return fileName; }}
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
        public static int Main(string[] args)
        {
            // Error count will be returned.  Start at 0 - this will be incremented
            // based on the error counts derived from the DisasmEngine executions.
            int errorCount = 0;
            
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
                
                DisasmEngine baseDisasm = new DisasmEngine(config.BaseExecutable, config.PlatformPath, taggedPath, assemblyWorkList);
                baseDisasm.GenerateAsm();

                if (baseDisasm.ErrorCount > 0) {
                    Console.WriteLine("{0} errors compiling base set.", baseDisasm.ErrorCount);
                    errorCount += baseDisasm.ErrorCount;
                }
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
                
                DisasmEngine diffDisasm = new DisasmEngine(config.DiffExecutable, config.PlatformPath, taggedPath, assemblyWorkList);
                diffDisasm.GenerateAsm();
                
                if (diffDisasm.ErrorCount > 0) {
                    Console.WriteLine("{0} errors compiling diff set.", diffDisasm.ErrorCount);
                    errorCount += diffDisasm.ErrorCount;
                }
            }

            return errorCount;
        }

        private static void WaitForDebugger() {
            Console.WriteLine("Wait for a debugger to attach. Press ENTER to continue");
            Console.WriteLine($"Process ID: {Process.GetCurrentProcess().Id}");
            Console.ReadLine();
        }
       
        public static List<AssemblyInfo> GenerateAssemblyWorklist(Config config)
        {
            List<string> assemblyList = new List<string>(); 
            List<AssemblyInfo> assemblyInfoList = new List<AssemblyInfo>();

            if (config.UseFileName)
            {
                assemblyList = new List<string>();
                string inputFile = config.FileName;
                
                // Open file, read assemblies one per line, and add them to the assembly list.
                using (var inputStream = System.IO.File.Open(inputFile, FileMode.Open)) {
                    using (var inputStreamReader = new StreamReader(inputStream)) {
                        string line;
                        while ((line = inputStreamReader.ReadLine()) != null) {
                            // Each line is a path to an assembly.
                            if (!File.Exists(line))
                            {
                                Console.WriteLine("Can't find {0} skipping...", line);
                                continue;
                            }
                            
                            assemblyList.Add(line);
                        }
                    }
                }
            }

            if (config.HasUserAssemblies) {
                // Append command line assemblies
                assemblyList.AddRange(config.AssemblyList);
            }

            // Process worklist and produce the info needed for the disasm engines.
            foreach (var path in assemblyList)
            {
                FileAttributes attr = File.GetAttributes(path);
            
                if ((attr & FileAttributes.Directory) == FileAttributes.Directory) {
                        
                    // For the directory case create a stack and recursively find any
                    // assemblies for compilation.
                    List<AssemblyInfo> directoryAssemblyInfoList = IdentifyAssemblies(path,
                        config.Recursive);
                        
                    // Add info generated at this directory
                    assemblyInfoList.AddRange(directoryAssemblyInfoList);
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
                            
            return assemblyInfoList;
        }
        
        // Check to see if the passed filePath is to an assembly.
        private static bool IsAssembly(string filePath) {
            try
            {
                System.Reflection.AssemblyName diffAssembly = 
                    System.Runtime.Loader.AssemblyLoadContext.GetAssemblyName(filePath);   
            }
            catch (System.IO.FileNotFoundException)
            {
                // File not found - not an assembly
                // TODO - should we log this case?
                return false;
            }
            catch (System.BadImageFormatException)
            {
                // Explictly not an assembly.
                return false;
            }
            catch (System.IO.FileLoadException)
            {
                // This is an assembly but it just happens to be loaded.
                // (leave true in so as not to rely on fallthrough)
                return true;
            }
            
            return true;
        }
        
        // Recursivly search for assemblies from a root path.
        private static List<AssemblyInfo> IdentifyAssemblies(string rootPath, bool recursive) {
            List<AssemblyInfo> assemblyInfoList = new List<AssemblyInfo>();
            Stack<string> workList = new Stack<string>();
            string fullRootPath = Path.GetFullPath(rootPath);
            
            // Enqueue the base case
            workList.Push(fullRootPath);
            
            while (workList.Count != 0)
            {
                string current = workList.Pop();
                string[] subFiles = Directory.GetFiles(current);
            
                foreach (var filePath in subFiles)
                {
                    // skip if not an assembly
                    if (!IsAssembly(filePath)) {
                        continue;
                    }

                    string fileName = Path.GetFileName(filePath);
                    string directoryName = Path.GetDirectoryName(filePath);
                    string outputPath = directoryName.Substring(fullRootPath.Length).TrimStart(Path.DirectorySeparatorChar);
                    //Console.WriteLine("Subpath is {0}", outputPath);

                    AssemblyInfo info = new AssemblyInfo {
                        Name = fileName,
                        Path = directoryName,
                        OutputPath = outputPath
                    };
                
                    assemblyInfoList.Add(info);
                }

                if (recursive) {
                    // Recurse through sub directories if requested.
                    string[] subDirectories = Directory.GetDirectories(current);
                    
                    foreach (var subDir in subDirectories) {
                        workList.Push(subDir);
                    }
                }
            }

            return assemblyInfoList;
        }       

        class DisasmEngine
        {   
            private string executablePath;
            private string rootPath = null;
            private string platformPath = null;
            private List<AssemblyInfo> assemblyInfoList;
            private bool doGCDump = false;
            private int errorCount = 0;
            
            public int ErrorCount { get { return errorCount; }}

            public DisasmEngine(string executable, string platformPath, string outputPath, 
                List<AssemblyInfo> assemblyInfoList)
            {
                this.executablePath = executable;
                this.rootPath = outputPath;
                this.platformPath = platformPath;
                this.assemblyInfoList = assemblyInfoList;
            }

            public void GenerateAsm()
            {
                // Build a command per assembly to generate the asm output.
                foreach (var assembly in this.assemblyInfoList)
                {
                    string fullPathAssembly = Path.Combine(assembly.Path, assembly.Name);
                    
                    if (!File.Exists(fullPathAssembly)) {
                        // Assembly not found.  Produce a warning and skip this input.
                        Console.WriteLine("Skipping. Assembly not found: {0}", fullPathAssembly);
                        continue;
                    }
                    
                    List<string> commandArgs = new List<string>() {fullPathAssembly};
                    
                    // Set platform assermbly path if it's defined.
                    if (platformPath != null) {
                        commandArgs.Insert(0, "/Platform_Assemblies_Paths");
                        commandArgs.Insert(1, platformPath);
                    }

                    Command generateCmd = Command.Create(
                        executablePath, 
                        commandArgs);

                    // Set up environment do disasm.
                    generateCmd.EnvironmentVariable("COMPlus_NgenDisasm", "*");
                    generateCmd.EnvironmentVariable("COMPlus_NgenUnwindDump", "*");
                    generateCmd.EnvironmentVariable("COMPlus_NgenEHDump", "*");
                    generateCmd.EnvironmentVariable("COMPlus_JitDiffableDasm", "1");
                    
                    if (doGCDump) {
                        generateCmd.EnvironmentVariable("COMPlus_NgenGCDump", "*");
                    }

                    CommandResult result;
                    
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
                                result = generateCmd.Execute();
                            }
                        }
                    }
                    else {

                        // By default forward to output to stdout/stderr.
                        generateCmd.ForwardStdOut();
                        generateCmd.ForwardStdErr();
                        result = generateCmd.Execute();
                    }
                    
                    if (result.ExitCode != 0) {
                        Console.WriteLine("Error running {0} on {1}", executablePath, fullPathAssembly);
                        errorCount++;
                    }
                }
            }   
        }
    }
}
