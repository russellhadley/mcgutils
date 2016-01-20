using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;

namespace ManagedCodeGen
{
    // Define options to be parsed 
    class Options
    {
        public bool genFrameworkAssemblies { get { return true; } }
        public string assemblyPath { get { return "assemblyPath"; } }

        //[Option('b', "base", Required = false, HelpText = "Base code generator")]
        public string baseExe { get { return "basePath"; } }
        //[Option('d', "diff", Required = false, HelpText = "Diff code generator")]
        public string diffExe { get { return "diffPath"; } }
        //[Option('o', "output", Required = true, HelpText = "Output Directory")]
        public string outputPath { get { return "outputPath"; } }

        //[HelpOption]
        public string GetUsage()
        {
            return "mcgdiff [-f|--frameworks = <path>] | [-a|--assembly = <path>] -b|--base = <base crossgen> -d|--diff = <diff crossgen> -o|--out = <out path>";
        }
    }

    public class MCGDiff
    {
        public static void Main(string[] args)
        {
            System.Console.WriteLine("mcgdiff");

            var options = new Options();

            if (options != null)
            {
                DifferenceEngine diff = new DifferenceEngine(options);
                diff.GenerateAssemblyWorklist();
                diff.Execute();
            }
        }

        class DifferenceEngine
        {
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

            public DifferenceEngine(Options options)
            {

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
            }

            public void GenerateAsm(string codegenExe, List<string> assemblies, string outputPath)
            {
                int numberOfAsseblies = assemblies.Count;
                string[] cmdArgs;
                
                foreach (var assembly in assemblies)
                {
                    
                }

                Command generateCmd = Command.Create("blah", "");
            }
        }
    }
}
