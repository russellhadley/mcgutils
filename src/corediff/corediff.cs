// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.CommandLine;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ManagedCodeGen
{
    public class corediff
    {
        private static string s_asmTool = "mcgdiff";
        private static string s_analysisTool = "analyze";

        public class Config
        {
            private ArgumentSyntax _syntaxResult;
            private string _baseExe = null;
            private string _diffExe = null;
            private string _outputPath = null;
            private bool _list = false;
            private bool _analyze = false;
            private string _tag = null;
            private string _platformPath = null;
            private string _testPath = null;
            private bool _mscorlibOnly = false;
            private bool _frameworksOnly = false;
            private bool _verbose = false;

            private JObject _jObj;

            public Config(string[] args)
            {
                // Get configuration values from JIT_DASM_ROOT/asmdiff.json

                LoadFileConfig();

                _syntaxResult = ArgumentSyntax.Parse(args, syntax =>
                {
                    syntax.DefineOption("b|base", ref _baseExe, "The base compiler exe or tag.");
                    syntax.DefineOption("d|diff", ref _diffExe, "The diff compiler exe or tag.");
                    syntax.DefineOption("o|output", ref _outputPath, "The output path.");
                    syntax.DefineOption("l|list", ref _list, "List available tools (Set JIT_DASM_ROOT).");
                    syntax.DefineOption("a|analyze", ref _analyze, "Analyze resulting base, diff dasm directories.");
                    syntax.DefineOption("t|tag", ref _tag, "Name of root in output directory.  Allows for many sets of output.");
                    syntax.DefineOption("m|mscorlibonly", ref _mscorlibOnly, "Disasm mscorlib only");
                    syntax.DefineOption("f|frameworksonly", ref _frameworksOnly, "Disasm frameworks only");
                    syntax.DefineOption("v|verbose", ref _verbose, "Enable verbose output");
                    syntax.DefineOption("core_root", ref _platformPath, "Path to test CORE_ROOT.");
                    syntax.DefineOption("test_root", ref _testPath, "Path to test tree");
                });

                // Run validation code on parsed input to ensure we have a sensible scenario.

                Validate();

                ExpandToolTags();

                DeriveOutputTag();

                // Now that output path and tag are guaranteed to be set, update
                // the output path to included the tag.
                _outputPath = Path.Combine(_outputPath, _tag);
            }

            private void DeriveOutputTag()
            {
                if (_tag == null)
                {
                    int currentCount = 1;
                    foreach (var dir in Directory.EnumerateDirectories(_outputPath))
                    {
                        var name = Path.GetFileName(dir);
                        Regex pattern = new Regex(@"dasmset_([0-9]{1,})");
                        Match match = pattern.Match(name);
                        if (match.Success)
                        {
                            int count = Convert.ToInt32(match.Groups[1].Value);
                            if (count > currentCount)
                            {
                                currentCount = count;
                            }
                        }
                    }

                    currentCount++;
                    _tag = String.Format("dasmset_{0}", currentCount);
                    Console.WriteLine("tag {0}", _tag);
                }
            }

            private void ExpandToolTags()
            {
                var tools = _jObj["tools"];

                foreach (var tool in tools)
                {
                    var tag = (string)tool["tag"];
                    var path = (string)tool["path"];

                    if (_baseExe == tag)
                    {
                        // passed base tag matches installed tool, reset path.
                        _baseExe = Path.Combine(path, "crossgen");
                    }

                    if (_diffExe == tag)
                    {
                        // passed diff tag matches installed tool, reset path.
                        _diffExe = Path.Combine(path, "crossgen");
                    }
                }
            }

            private void Validate()
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

            public string GetToolPath(string tool, out bool found)
            {
                var token = _jObj["default"][tool];

                if (token != null)
                {
                    found = true;

                    string tag = _jObj["default"][tool].Value<string>();
                    var path = _jObj["tools"].Children()
                                        .Where(x => (string)x["tag"] == tag)
                                        .Select(x => (string)x["path"]);
                    if (!path.Any())
                    {
                        Console.WriteLine("Config error: can't find tool tag: \"{0}\" specified in default", tool);
                    }

                    return path.Any() ? path.First() : null;
                }

                found = false;
                return null;
            }

            public T ExtractDefault<T>(string name, out bool found)
            {
                var token = _jObj["default"][name];

                if (token != null)
                {
                    found = true;

                    try
                    {
                        return token.Value<T>();
                    }
                    catch (System.FormatException e)
                    {
                        Console.WriteLine("Bad format for default {0}.  See asmdiff.json", name, e);
                    }
                }

                found = false;
                return default(T);
            }

            private void LoadFileConfig()
            {
                string jitDasmRoot = Environment.GetEnvironmentVariable("JIT_DASM_ROOT");

                if (jitDasmRoot != null)
                {
                    string path = Path.Combine(jitDasmRoot, "asmdiff.json");

                    if (File.Exists(path))
                    {
                        string configJson = File.ReadAllText(path);

                        _jObj = JObject.Parse(configJson);

                        // Check if there is any default config specified.
                        if (_jObj["default"] != null)
                        {
                            bool found;

                            // Find baseline tool if any.
                            string basePath = GetToolPath("base", out found);
                            if (found)
                            {
                                _baseExe = Path.Combine(basePath, "crossgen");
                            }

                            // Find diff tool if any
                            string diffPath = GetToolPath("diff", out found);
                            if (found)
                            {
                                _diffExe = Path.Combine(diffPath, "crossgen");
                            }

                            // Set up output
                            var outputPath = ExtractDefault<string>("output", out found);
                            _outputPath = (found) ? outputPath : _outputPath;

                            // Setup platform path (core_root).
                            var platformPath = ExtractDefault<string>("core_root", out found);
                            _platformPath = (found) ? platformPath : _platformPath;

                            // Set up test path (test_root).
                            var testPath = ExtractDefault<string>("test_root", out found);
                            _testPath = (found) ? testPath : _testPath;

                            var analyze = ExtractDefault<bool>("analyze", out found);
                            _analyze = (found) ? analyze : _analyze;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Can't find asmdiff.json on {0}", jitDasmRoot);
                    }
                }
                else
                {
                    Console.WriteLine("No default asmdiff configuration found.");
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
            public bool DoAnalyze { get { return _analyze; } }
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
                Console.WriteLine("Dasm command returned with {0} failures", result.ExitCode);
            }

            // Analyze completed run.

            if (config.DoAnalyze == true)
            {
                List<string> analysisArgs = new List<string>();

                analysisArgs.Add("--base");
                analysisArgs.Add(Path.Combine(config.OutputPath, "base"));
                analysisArgs.Add("--diff");
                analysisArgs.Add(Path.Combine(config.OutputPath, "diff"));
                analysisArgs.Add("--recursive");

                Console.WriteLine("Analyze command: {0} {1}",
                    s_analysisTool, String.Join(" ", analysisArgs));

                Command analyzeCmd = Command.Create(s_analysisTool, analysisArgs);

                // Wireup stdout/stderr so we can see outout.
                analyzeCmd.ForwardStdOut();
                analyzeCmd.ForwardStdErr();

                CommandResult analyzeResult = analyzeCmd.Execute();
            }

            return result.ExitCode;
        }
    }
}
