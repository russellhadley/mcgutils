using System;
using System.Diagnostics;
using System.CommandLine;
using System.IO;
using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;

namespace ManagedCodeGen
{
    // Define options to be parsed 
    class Config
    {
        public bool genFrameworkAssemblies { get { return true; } }
        public string assemblyPath { get { return "assemblyPath"; } }

        //[Option('b', "base", Required = false, HelpText = "Base code generator")]
        public string baseExe { get { return "basePath"; } }
        //[Option('d', "diff", Required = false, HelpText = "Diff code generator")]
        public string diffExe { get { return "diffPath"; } }
        //[Option('o', "output", Required = true, HelpText = "Output Directory")]
        public string outputPath { get { return "outputPath"; } }
    }

    public class MCGDiff
    {
        public static void Main(string[] args)
        {
            var baseExe = "";
            var diffExe = "";
            var outputPath = "d:\\\\output\\";
            var assemblyName = "";
            
            // App name is currently not derived correctly due to some API issues - this
            // will be fixed in the future.
            ArgumentSyntax.Parse(args, syntax =>
            {
                syntax.DefineOption("b|base", ref baseExe, "The base crossgen exe");
                syntax.DefineOption("d|diff", ref diffExe,  "The diff crossgen exe");
                syntax.DefineOption("o|output", ref outputPath, "The output path");
                
                syntax.DefineParameter("assembly", ref assemblyName, "The assembly to asm diff"); 
            });
            
            System.Console.WriteLine("mcgdiff");

            // Stop to attach a debugger if desired.
            //WaitForDebugger();

            var config = new Config();

            // set config values based on arguments.

            if (config != null)
            {
                DifferenceEngine diff = new DifferenceEngine(config);
                diff.GenerateAssemblyWorklist();
                diff.Execute();
            }
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
            private static string outputPath = "D:\\output";

            private bool generateBase = true;
            private bool generateDiff = false;
            private bool outputDiff = false;

            // Define the set of assemblies we care about. NOTE: mscorlib.dll is treated specially.
            // It MUST be first in this array!
            // Also: #2 must be System, and #3 must be System.Core. This is because all assemblies hard bind to these.
            // TODO: Does crossgen also require these restrictions??
            private static string[] frameworkAssemblies =
            {
            "mscorlib",
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
            };

            List<string> assemblyList = null;

            public DifferenceEngine(Config config)
            {
                this.config = config;
            }

            public void Execute()
            {
                if (generateBase)
                {
                    GenerateAsm(baseExecutablePath, assemblyList, outputPath);
                }

                if (generateDiff)
                {
                    GenerateAsm(diffExecutablePath, assemblyList, outputPath);
                }
            }

            public void GenerateAssemblyWorklist()
            {
                if (config.genFrameworkAssemblies) {
                    // build list based on baked in list of assemblies
                    assemblyList = new List<string>(frameworkAssemblies);  
                }
            }

            public void GenerateAsm(string codegenExe, List<string> assemblies, string outputPath)
            {   
                // Build a command per assembly to generate the asm output
              
                foreach(var assembly in assemblies) {
                    string fullpathAssembly = "D:\\dotnet\\coreclr\\bin\\Product\\Windows_NT.x64.Debug\\" + assembly + ".dll";
                    List<string> commandArgs = new List<string>() {fullpathAssembly};

                    Command generateCmd = Command.Create(
                        "D:\\dotnet\\coreclr\\bin\\Product\\Windows_NT.x64.Debug\\crossgen.exe", 
                        commandArgs);

                    // Generate stream writer for the output file
                    
                    StreamWriter outputFile = new StreamWriter(outputPath + assembly + ".asm");
                    
                    generateCmd.ForwardStdOut(outputFile);
                    generateCmd.ForwardStdErr(outputFile);

                    generateCmd.EnvironmentVariable("COMPlus_NgenDisasm", "*");

                    generateCmd.Execute();
                
                    Console.WriteLine("Finished processing {0}.", assembly);
                }
            }
        }
    }
}
