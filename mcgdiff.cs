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
        private bool genFrameworkAssemblies = true;
        private string baseExe = "crossgen.exe";
        private string diffExe = null;
        private string compExe = null;
        private string rootPath = null;
        private string tag = null;
        private IReadOnlyList<string> assemblyList = Array.Empty<string>();
        private bool wait = false;
        private bool recursive = false;
        private IReadOnlyList<string> methods = Array.Empty<string>();
        
        public Config(string[] args) {

            // App name is currently not derived correctly due to some API issues - this
            // will be fixed in the future.
            var result = ArgumentSyntax.Parse(args, syntax =>
            {
                syntax.DefineOption("f|frameworks", ref genFrameworkAssemblies, "Generate asm for default framework assemblies");
                syntax.DefineOption("b|base", ref baseExe, "The base compiler exe.");
                syntax.DefineOption("d|diff", ref diffExe, "The diff compiler exe.");
                syntax.DefineOption("c|compiler", ref compExe, "The compiler exe to generate with");
                syntax.DefineOption("o|output", ref rootPath, "The output path.");
                syntax.DefineOption("t|tag", ref tag, "Name of root in output directory.  Allows for many sets of output.");
                syntax.DefineOption("w|wait", ref wait, "Wait for debugger to attach.");
                syntax.DefineOption("r|recursive", ref recursive, "Scan directories recursivly.");
                syntax.DefineOptionList("m|methods", ref methods, "List of methods to disasm.");

                syntax.DefineParameterList("assembly", ref assemblyList, "The list of assemblies or directories to scan for assemblies.");
            });

            System.Console.WriteLine("Parsing commandline:");

            System.Console.WriteLine("base: {0}", baseExe);
            System.Console.WriteLine("diff: {0}", diffExe);
            System.Console.WriteLine("rootPath: {0}", rootPath);
            
            // Weird, ToArray not working on IReadOnlyList in core.
            foreach(var assembly in assemblyList) {
                System.Console.WriteLine("assembly: {0}", assembly);
            }
        }
        
        public bool GenFrameworkAssemblies { get { return genFrameworkAssemblies; }}
        public bool GenUserAssemblies { get { return AssemblyList.Count > 0; }}
        public bool WaitForDebugger { get { return wait; }}
        public bool GenerateBaseline { get { return (baseExe != null); }}
        public bool GenerateDiff { get { return (diffExe != null); }}
        public bool Recursive { get { return recursive; }}
        public string BaseExecutable { get { return baseExe; }}
        public string DiffExecutable { get { return diffExe; }}
        public string RootPath { get { return rootPath; }}
        public IReadOnlyList<string> AssemblyList { get { return assemblyList; }}
    }

    class AssemblyInfo {
        public string Name {get; set;}
        // Contains path to assembly.
        public string Path {get; set;}
        // Contains relative path within output directory for given assembly.
        // This allows for different output directories per tool.
        public string OutputPath {get; set;}
    }

    public class mcgdiff
    {
        public static void Main(string[] args)
        {
            WaitForDebugger();
            
            // Parse and store comand line options.
            var config = new Config(args);

            // Stop to attach a debugger if desired.
            if (config.WaitForDebugger) {
                WaitForDebugger();
            }

            // The difference engine encapsulates a particular set of diffs.  An engine is
            // produced with a given config, which then lets it generate a particular worklist
            // after which it executes the diff.
            DifferenceEngine diff = new DifferenceEngine(config);
            diff.GenerateAssemblyWorklist();
            diff.Execute();
        }

        private static void WaitForDebugger() {
            Console.WriteLine("Wait for a debugger to attach. Press ENTER to continue");
            Console.WriteLine($"Process ID: {Process.GetCurrentProcess().Id}");
            Console.ReadLine();
        }

        class DifferenceEngine
        {
            private Config config;
            
            private static string baseExecutablePath = "D:\\dotnet\\coreclr\bin\\Product\\Windows_NT.x64.Debug";
            private static string diffExecutablePath;

            private bool generateBase = true;
            private bool generateDiff = false;
            private bool outputDiff = false;

            // Define the set of assemblies we care about. NOTE: mscorlib.dll is treated specially.
            // It MUST be first in this array!
            // Also: #2 must be System, and #3 must be System.Core. This is because all assemblies hard bind to these.
            // TODO: Does crossgen also require these restrictions??
            private static string[] frameworkAssemblies =
            {
            "mscorlib.dll"
#if false
            ,
            "System",
            "System.Core",
            "System.Configuration",
            "System.Security",
            "System.Xml",
            "System.Data.SqlXml",
            "System.Numerics",
            "System.Drawing",
            "System.Runtime.Serialization.Formatters.Soap",
            "Accessibility",
            "System.Deployment",
            "System.Windows.Forms",
            "System.Data",
            "System.EnterpriseServices",
            "System.Runtime.Remoting",
            "System.DirectoryServices",
            "System.Transactions",
            "System.Web",
            "System.Xaml",
            "Microsoft.VisualC",
            "Microsoft.Build.Framework",
            "System.Runtime.Caching",
            "System.Web.ApplicationServices",
            "System.Web.Services",
            "System.Design",
            "System.Drawing.Design",
            "System.Web.RegularExpressions",
            "System.DirectoryServices.Protocols",
            "System.ServiceProcess",
            "System.Configuration.Install",
            "System.Runtime.Serialization",
            "System.ServiceModel.Internals",
            "SMDiagnostics",
            "System.Data.OracleClient",
            "System.Runtime.DurableInstancing",
            "System.IdentityModel.Selectors",
            "System.Xml.Linq",
            "System.ServiceModel",
            "System.Messaging",
            "System.IdentityModel",
            "Microsoft.Transactions.Bridge",
            "System.ServiceModel.Activation",
            "System.ServiceModel.Activities",
            "System.Activities",
            "Microsoft.VisualBasic",
            "System.Management",
            "Microsoft.JScript",
            "System.Net.Http",
            "System.Activities.DurableInstancing",
            "System.Xaml.Hosting",
            "System.Data.Linq",
            "System.ComponentModel.DataAnnotations"
#endif
            };

            List<AssemblyInfo> assemblyInfoList = null;

            public DifferenceEngine(Config config)
            {
                this.config = config;
            }

            public void Execute()
            {
                if (config.GenerateBaseline) {
                    var baselinePath = "base";
                    
                    if (config.RootPath != null) {
                        baselinePath = Path.Combine(config.RootPath, baselinePath);
                    }

                    // Traverse the assembly list and generate asm into the root directory
                    // taged to make it unique.
                    GenerateAsm(config.BaseExecutable, baselinePath, assemblyInfoList);
                }

                if (config.GenerateDiff) {
                    var diffPath = "diff";
                    
                    if (config.RootPath != null) {
                        diffPath = Path.Combine(config.RootPath, diffPath);
                    }

                    GenerateAsm(config.DiffExecutable, diffPath, assemblyInfoList);
                }
            }

            public void GenerateAssemblyWorklist()
            {      
                if (config.GenFrameworkAssemblies) {
                    // TODO get a path to a scratch project that pulls down the full list.  
                    // For now we just will use mscorlib.
                    var basePath = Path.GetDirectoryName(config.BaseExecutable);
                    // build list based on baked in list of assemblies
                    assemblyInfoList = new List<AssemblyInfo>();
                    
                    foreach (var assembly in frameworkAssemblies) {
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
                    
                    foreach (var path in assemblyList)
                    {
                        FileAttributes attr = File.GetAttributes(path);
                        
                        if ((attr & FileAttributes.Directory) == FileAttributes.Directory) {
                            // This is directory case.
                            System.Console.WriteLine("NYI - directory case");
                            Environment.Exit(-1);
                        }
                        else {
                            // This is the file case.
                            
                            System.Console.WriteLine("File case.");

                            AssemblyInfo info = new AssemblyInfo {
                            Name = Path.GetFileName(path),
                            Path = Path.GetDirectoryName(path),
                            OutputPath = ""
                            };
                        }
                    }
                }
                
                // case for single assembly
            }

            public void GenerateAsm(string codegenExe, string rootPath, List<AssemblyInfo> infoList)
            {   
                // Build a command per assembly to generate the asm output.
                foreach (var assembly in infoList)
                {
                    string fullpathAssembly = Path.Combine(assembly.Path, assembly.Name);
                    List<string> commandArgs = new List<string>() {fullpathAssembly};
                    
                    Console.WriteLine("args: {0}", string.Join(" ", commandArgs.ToArray()));
                    
                    Command generateCmd = Command.Create(
                        codegenExe, 
                        commandArgs);

                    // Set up environment do disasm.
                    generateCmd.EnvironmentVariable("COMPlus_NgenDisasm", "*");

                    if (config.RootPath != null) {
                        // Generate path to the output file
                        var assemblyFileName = Path.ChangeExtension(assembly.Name, ".dasm");
                        var path = Path.Combine(rootPath, assembly.OutputPath, assemblyFileName);
                        
                        PathUtility.EnsureParentDirectory(path);
                        
                        Console.WriteLine("Generating to {0}", path);
                        // Redirect stdout/stderr to disasm file and run command.
                        using (var outputStream = System.IO.File.Create(path)) {
                            using (var outputStreamWriter = new StreamWriter(outputStream)) {
                                
                                // Forward output and error to file.
                                generateCmd.ForwardStdOut(outputStreamWriter);
                                generateCmd.ForwardStdErr(outputStreamWriter);

                                Console.WriteLine("Starting processing {0}.", assembly.Name);

                                generateCmd.Execute();
                        
                                Console.WriteLine("Finished processing {0}.", assembly.Name);
                            }
                        }
                    }
                    else {

                        // By default forward to output to stdout/stderr.
                        generateCmd.ForwardStdOut();
                        generateCmd.ForwardStdErr();
                        
                        Console.WriteLine("Starting processing {0}.", assembly.Name);

                        generateCmd.Execute();
                        
                        Console.WriteLine("Finished processing {0}.", assembly.Name);
                    }
                }
            }   
        }
    }
}
