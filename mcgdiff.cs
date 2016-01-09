using System;
using System.IO;
using System.Collections.Generic;

namespace ManagedCodeGen 
{
    // Define options to be parsed 
    class Options {
        public bool genFrameworkAssemblies { get {return true;} }
        public string assemblyPath { get {return "assemblyPath";} }
        
        //[Option('b', "base", Required = false, HelpText = "Base code generator")]
        public string baseExe { get {return "basePath";} }
        //[Option('d', "diff", Required = false, HelpText = "Diff code generator")]
        public string diffExe { get {return "diffPath";} }
        //[Option('o', "output", Required = true, HelpText = "Output Directory")]
        public string outputPath { get {return "outputPath";} }
        
        //[HelpOption]
        public string GetUsage() {
            return "mcgdiff [-f|--frameworks = <path>] | [-a|--assembly = <path>] -b|--base = <base crossgen> -d|--diff = <diff crossgen> -o|--out = <out path>";
        }
    }
    
    public class MCGDiff
    {
        private static string baseRuntimePath;
        private static string diffRuntimePath;        

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
        
        public static void Main(string[] args)
        {
            System.Console.WriteLine("mcgdiff");
            
            var options = new Options();
            
            if (options != null) {        
                // Validate base path
                
                // Validate diff path
                
                // Validate output path

                theList = MakeAssemblyWorklist();            
                
                ProduceDiff(theList);
            }
        }

       public void ProduceDiff(List<string> assemblies) {
           if (generateBase)
           {
                    GenerateAsm(theBase, theList);
                }
                
                if (generateDiff)
                {
                GenerateAsm(theDiff, theList);
                }
                
                if (ouputDiff)
                {
                    
                }
       }
        
        public List<string> MakeAssemblyWorklist() {
            return null;
        }
        
        public void GenerateAsm(string codegenExe, List<string> assemblies,       string outputPath) {
            
        }
    } 
}    
