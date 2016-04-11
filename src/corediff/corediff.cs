using System;
using System.Diagnostics;
using System.CommandLine;
using System.IO;
using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;

namespace ManagedCodeGen
{
    public class corediff
    {
        private static string asmTool = "mcgdiff";
        
        public class Config
        {
            private ArgumentSyntax syntaxResult;
            private string baseExe = null;
            private string diffExe = null;
            private string outputPath = null;
            private string tag = null;
            private string platformPath = null;
            private string testPath = null;
            private bool mscorlibOnly = false;
            private bool frameworksOnly = false;
            
            public Config(string[] args) {

                syntaxResult = ArgumentSyntax.Parse(args, syntax =>
                {
                    syntax.DefineOption("b|base", ref baseExe, "The base compiler exe.");
                    syntax.DefineOption("d|diff", ref diffExe, "The diff compiler exe.");
                    syntax.DefineOption("o|output", ref outputPath, "The output path.");
                    syntax.DefineOption("t|tag", ref tag, "Name of root in output directory.  Allows for many sets of output.");
                    syntax.DefineOption("m|mscorlibonly", ref mscorlibOnly, "Disasm mscorlib only");
                    syntax.DefineOption("f|frameworksonly", ref frameworksOnly, "Disasm frameworks only");
                    syntax.DefineOption("core_root", ref platformPath, "Path to test CORE_ROOT.");
                    syntax.DefineOption("test_root", ref testPath, "Path to test tree");
                });
                
                // Run validation code on parsed input to ensure we have a sensible scenario.
                
                validate();
            }
            
            void validate() {
                if (platformPath == null) {
                    syntaxResult.ReportError("Specifiy --core_root <path>");
                }
                
                if ((mscorlibOnly == false) && 
                    (frameworksOnly == false) && (testPath == null)) {
                    syntaxResult.ReportError("Specify --test_root <path>");
                }
                
                if (outputPath == null) {
                    syntaxResult.ReportError("Specify --output <path>");
                }
                
                if ((baseExe == null) && (diffExe == null)) {
                    syntaxResult.ReportError("--base <path> or --diff <path> or both must be specified.");
                }
            }
            
            public string CoreRoot { get { return platformPath; } }
            public string TestRoot { get { return testPath; } }
            public string PlatformPath { get { return platformPath; } }
            public string BaseExecutable { get { return baseExe; } }
            public bool HasBaseExeutable { get { return (baseExe != null); } }
            public string DiffExecutable { get { return diffExe; } }
            public bool HasDiffExecutable { get { return (diffExe != null); } }
            public string OutputPath { get { return outputPath; } }
            public string Tag { get { return tag; } }
            public bool HasTag { get { return (tag != null); } }
            public bool MSCorelibOnly { get { return mscorlibOnly; } }
            public bool FrameworksOnly { get { return frameworksOnly; } }
            public bool DoMSCorelib { get { return true; } }
            public bool DoFrameworks { get { return !mscorlibOnly; } }
            public bool DoTestTree { get { return (!mscorlibOnly && !frameworksOnly); } }
        }
 
        private static string[] testDirectories = 
        {
            "Interop",
            "JIT"
        };
        
        private static string[] frameworkAssemblies = 
        {
            "mscorlib.dll",			
            "System.Runtime.dll",		
            "System.Runtime.Extensions.dll",		
            "System.Runtime.Handles.dll",		
            "System.Runtime.InteropServices.dll",		
            "System.Runtime.InteropServices.PInvoke.dll",		
            "System.Runtime.InteropServices.RuntimeInformation.dll",
            "System.Runtime.Numerics.dll",			
            "Microsoft.CodeAnalysis.dll",		
            "Microsoft.CodeAnalysis.CSharp.dll",		
            "System.Collections.dll",
            "System.Collections.Concurrent.dll",		
            "System.Collections.Immutable.dll",		
            "System.Collections.NonGeneric.dll",		
            "System.Collections.Specialized.dll",		
            "System.ComponentModel.dll",		
            "System.Console.dll",
            "System.Dynamic.Runtime.dll",
            "System.IO.dll",
            "System.IO.Compression.dll",
            "System.Linq.dll",
            "System.Linq.Expressions.dll",
            "System.Linq.Parallel.dll",
            "System.Net.Http.dll",
            "System.Net.NameResolution.dll",
            "System.Net.Primitives.dll",
            "System.Net.Requests.dll",
            "System.Net.Security.dll",
            "System.Net.Sockets.dll",		
            "System.Numerics.Vectors.dll",
            "System.Reflection.dll",
            "System.Reflection.DispatchProxy.dll",
            "System.Reflection.Emit.ILGeneration.dll",
            "System.Reflection.Emit.Lightweight.dll",
            "System.Reflection.Emit.dll",
            "System.Reflection.Extensions.dll",
            "System.Reflection.Metadata.dll",
            "System.Reflection.Primitives.dll",
            "System.Reflection.TypeExtensions.dll",
            "System.Text.Encoding.dll",		
            "System.Text.Encoding.Extensions.dll",		
            "System.Text.RegularExpressions.dll",		
            "System.Xml.ReaderWriter.dll",		
            "System.Xml.XDocument.dll",		
            "System.Xml.XmlDocument.dll"
        };
        
        public static int Main(string[] args)
        {
            Config config = new Config(args);
            string diffString = "mscorlib.dll";
            
            if (config.DoFrameworks) {
                diffString += ", framework assemblies";
            }
            
            if (config.DoTestTree) {
                diffString += ", " + config.TestRoot;
            }
            
            Console.WriteLine("Beginning diff of {0}!", diffString);
            
            // Add each framework assembly to commandArgs
            
            // Create subjob that runs mcgdiff, which should be in path, with the 
            // relevent coreclr assemblies/paths.
            
            string frameworkArgs = String.Join(" ", frameworkAssemblies);
            string testArgs = String.Join(" ", testDirectories);
            
            
            List<string> commandArgs = new List<string>();
            
            // Set up CoreRoot
            commandArgs.Add("--platform");
            commandArgs.Add(config.CoreRoot);
            
            commandArgs.Add("--output");
            commandArgs.Add(config.OutputPath);
            
            if (config.HasBaseExeutable) {
                commandArgs.Add("--base");  
                commandArgs.Add(config.BaseExecutable);
            }
            
            if (config.HasDiffExecutable) {
                commandArgs.Add("--diff");
                commandArgs.Add(config.DiffExecutable);
            }
            
            if (config.HasTag) {
                commandArgs.Add("--tag");
                commandArgs.Add(config.Tag);
            }

            if (config.MSCorelibOnly) {
                string coreRoot = config.CoreRoot;
                string fullPathAssembly = Path.Combine(coreRoot, "mscorlib.dll");
                commandArgs.Add(fullPathAssembly);
            }
            else {
                // Set up full framework paths
                foreach (var assembly in frameworkAssemblies) {
                    string coreRoot = config.CoreRoot;
                    string fullPathAssembly = Path.Combine(coreRoot, assembly);
                    
                    if (!File.Exists(fullPathAssembly)) {
                        Console.WriteLine("can't find {0}", fullPathAssembly);
                        continue;
                    }
                    
                    commandArgs.Add(fullPathAssembly);
                }

                if (config.TestRoot != null) {
                foreach (var dir in testDirectories) {
                    string testRoot = config.TestRoot;
                    string fullPathDir = Path.Combine(testRoot, dir);
                    
                    if (!Directory.Exists(fullPathDir)) {
                        Console.WriteLine("can't find {0}", fullPathDir);
                        continue;
                    }
                    
                    commandArgs.Add(fullPathDir);
                } 
                }
            }
            
            Console.WriteLine("Diff command: {0} {1}", asmTool, String.Join(" ", commandArgs));
            
            Command diffCmd = Command.Create(
                        asmTool, 
                        commandArgs);
                        
            // Wireup stdout/stderr so we can see outout.
            diffCmd.ForwardStdOut();
            diffCmd.ForwardStdErr();
            
            CommandResult result = diffCmd.Execute();
            
            if (result.ExitCode != 0) {
                Console.WriteLine("Returned with {0} failures", result.ExitCode);
            }
            
            return result.ExitCode;
        }
    }
}
