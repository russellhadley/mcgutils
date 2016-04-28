// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
        private static string s_asmTool = "mcgdiff";

        public class Config
        {
            private ArgumentSyntax _syntaxResult;
            private string _baseExe = null;
            private string _diffExe = null;
            private string _outputPath = null;
            private string _list = false;
            private string _tag = null;
            private string _platformPath = null;
            private string _testPath = null;
            private bool _mscorlibOnly = false;
            private bool _frameworksOnly = false;
            private bool _verbose = false;

            public Config(string[] args)
            {
                _syntaxResult = ArgumentSyntax.Parse(args, syntax =>
                {
                    syntax.DefineOption("b|base", ref _baseExe, "The base compiler exe.");
                    syntax.DefineOption("d|diff", ref _diffExe, "The diff compiler exe.");
                    syntax.DefineOption("o|output", ref _outputPath, "The output path.");
                    syntax.DefineOptions("l|list", ref _list, "List available tools (Set JIT_DASM_ROOT).");
                    syntax.DefineOption("t|tag", ref _tag, "Name of root in output directory.  Allows for many sets of output.");
                    syntax.DefineOption("m|mscorlibonly", ref _mscorlibOnly, "Disasm mscorlib only");
                    syntax.DefineOption("f|frameworksonly", ref _frameworksOnly, "Disasm frameworks only");
                    syntax.DefineOption("v|verbose", ref _verbose, "Enable verbose output");
                    syntax.DefineOption("core_root", ref _platformPath, "Path to test CORE_ROOT.");
                    syntax.DefineOption("test_root", ref _testPath, "Path to test tree");
                });

                // Run validation code on parsed input to ensure we have a sensible scenario.

                validate();
            }

            private void validate()
            {
                if (_platformPath == null)
                {
                    _syntaxResult.ReportError("Specifiy --core_root <path>");

                }

                if ((_mscorlibOnly == false) &&
                    (_frameworksOnly == false) && (_testPath == null))
                {
                    _syntaxResult.ReportError("Specify --test_root <path>");
                }

                if (_outputPath == null)
                {
                    _syntaxResult.ReportError("Specify --output <path>");
                }

                if ((_baseExe == null) && (_diffExe == null))
                {
                    _syntaxResult.ReportError("--base <path> or --diff <path> or both must be specified.");
                }
            }
            
            void Configure ()
            {
                string jitDasmRoot = Environment.GetEnvironmentVariable("JIT_DASM_ROOT");
                
                if (jitDasmRoot != null) {
                    if (list) {
                        List();
                    }
                    else {
                        string baseToolPath = FindTool(baseExe);
                        string diffToolPath = FindTool(diffExe);
                        
                        if (baseToolPath != null) {
                            baseExe = baseToolPath;
                        }
                        
                        if (diffToolPath != null) {
                            diffExe = diffToolPath;
                        }
                    }
                }
                else {
                    if (list) {
                        Console.WriteLine("Can't list, missing JIT_DASM_ROOT in the environment.");
                    }
                }
            }
            
            string FindTool(string tool) {
                
                if (tool == null) {
                    return null;
                }
                
                // Found JIT_DASM_ROOT, list the available tool sets. 
                IEnumerate<string> files = Directory.EnumerateFiles(jitDasmRoot, "*Tools");

                foreach (file in files) {
                    string name = Path.GetFileName(file);
                    if (Regex.IsMatch(name, tool)) {
                        // Set path to file.
                        return file;
                    }
                }
                return null;
            }
            
            // List available tools
            void List() {                 
                // Found JIT_DASM_ROOT, list the available tool sets.
                IEnumerate<string> files = Directory.EnumerateFiles(jitDasmRoot, "*Tools");
                    
                Console.WriteLine("Available tools:");
                foreach (file in files) {
                    Console.WriteLine("    {0}", file);
                }
            }
            
            public string CoreRoot { get { return _platformPath; } }
            public string TestRoot { get { return _testPath; } }
            public string PlatformPath { get { return _platformPath; } }
            public string BaseExecutable { get { return _baseExe; } }
            public bool HasBaseExeutable { get { return (_baseExe != null); } }
            public string DiffExecutable { get { return _diffExe; } }
            public bool HasDiffExecutable { get { return (_diffExe != null); } }
            public string OutputPath { get { return _outputPath; } }
            public string Tag { get { return _tag; } }
            public bool HasTag { get { return (_tag != null); } }
            public bool MSCorelibOnly { get { return _mscorlibOnly; } }
            public bool FrameworksOnly { get { return _frameworksOnly; } }
            public bool DoMSCorelib { get { return true; } }
            public bool DoFrameworks { get { return !_mscorlibOnly; } }
            public bool DoTestTree { get { return (!_mscorlibOnly && !_frameworksOnly); } }
            public bool Verbose { get { return _verbose; } }
        }

        private static string[] s_testDirectories =
        {
            "Interop",
            "JIT"
        };

        private static string[] s_frameworkAssemblies =
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

            if (config.DoFrameworks)
            {
                diffString += ", framework assemblies";
            }

            if (config.DoTestTree)
            {
                diffString += ", " + config.TestRoot;
            }

            Console.WriteLine("Beginning diff of {0}!", diffString);

            // Add each framework assembly to commandArgs

            // Create subjob that runs mcgdiff, which should be in path, with the 
            // relevent coreclr assemblies/paths.

            string frameworkArgs = String.Join(" ", s_frameworkAssemblies);
            string testArgs = String.Join(" ", s_testDirectories);


            List<string> commandArgs = new List<string>();

            // Set up CoreRoot
            commandArgs.Add("--platform");
            commandArgs.Add(config.CoreRoot);

            commandArgs.Add("--output");
            commandArgs.Add(config.OutputPath);

            if (config.HasBaseExeutable)
            {
                commandArgs.Add("--base");
                commandArgs.Add(config.BaseExecutable);
            }

            if (config.HasDiffExecutable)
            {
                commandArgs.Add("--diff");
                commandArgs.Add(config.DiffExecutable);
            }

            if (config.HasTag)
            {
                commandArgs.Add("--tag");
                commandArgs.Add(config.Tag);
            }

            if (config.DoTestTree)
            {
                commandArgs.Add("--recursive");
            }

            if (config.Verbose)
            {
                commandArgs.Add("--verbose");
            }

            if (config.MSCorelibOnly)
            {
                string coreRoot = config.CoreRoot;
                string fullPathAssembly = Path.Combine(coreRoot, "mscorlib.dll");
                commandArgs.Add(fullPathAssembly);
            }
            else
            {
                // Set up full framework paths
                foreach (var assembly in s_frameworkAssemblies)
                {
                    string coreRoot = config.CoreRoot;
                    string fullPathAssembly = Path.Combine(coreRoot, assembly);

                    if (!File.Exists(fullPathAssembly))
                    {
                        Console.WriteLine("can't find framework assembly {0}", fullPathAssembly);
                        continue;
                    }

                    commandArgs.Add(fullPathAssembly);
                }

                if (config.TestRoot != null)
                {
                    foreach (var dir in s_testDirectories)
                    {
                        string testRoot = config.TestRoot;
                        string fullPathDir = Path.Combine(testRoot, dir);

                        if (!Directory.Exists(fullPathDir))
                        {
                            Console.WriteLine("can't find test directory {0}", fullPathDir);
                            continue;
                        }

                        commandArgs.Add(fullPathDir);
                    }
                }
            }

            Console.WriteLine("Diff command: {0} {1}", s_asmTool, String.Join(" ", commandArgs));

            Command diffCmd = Command.Create(
                        s_asmTool,
                        commandArgs);

            // Wireup stdout/stderr so we can see outout.
            diffCmd.ForwardStdOut();
            diffCmd.ForwardStdErr();

            CommandResult result = diffCmd.Execute();

            if (result.ExitCode != 0)
            {
                Console.WriteLine("Returned with {0} failures", result.ExitCode);
            }

            return result.ExitCode;
        }
    }
}
