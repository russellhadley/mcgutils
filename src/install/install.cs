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
        public class Config
        {
            private ArgumentSyntax syntaxResult;
            public string platform = null;
            public string buildType = null;
            public string outputPath = null;
            public bool overwrite = false;
            
            public Config(string[] args) {

                syntaxResult = ArgumentSyntax.Parse(args, syntax =>
                {
                    syntax.DefineOption("b|build", ref buildType, "Build type. May be Debug or Release.");
                    syntax.DefineOption("p|platform", ref platform, "The platform to install.");
                    syntax.DefineOption("i|install", ref outputPath, "The install path.");
                    syntax.DefineOption("f|force", ref overwrite, "Overwrite pre-existing files.");
                });
                
                // Run validation code on parsed input to ensure we have a sensible scenario.
                
                validate();
            }
            
            void validate() {
                if (outputPath == null) {
                    syntaxResult.ReportError("Specify --output <path>");
                }
                
                if (platform == null) {
                    syntaxResult.ReportError("Specify --platform <platform tag>");
                }
                
                if (buildType == null || ((buildType != "Debug") && (buildType != "Release"))) {
                    syntaxResult.ReportError("Specify --buildType <build_type> where build type is Debug or Release.");
                }
            }
        }
       
        // Managed CodeGen projects to install
        private static string[] mcgProjects  = {
            "corediff",
            "mcgdiff"
        };
        
        public static int Main(string[] args)
        {
            int err = 0;
            Config config = new Config(args);
            string rootDir = Directory.GetCurrentDirectory();
            
            // Foreach project, copy published results to the shared directory
            
            foreach(var project in mcgProjects) {
                string[] pathElements = {rootDir, "src", project, "bin", config.buildType, 
                    "netcoreapp1.0", config.platform, "publish"};
                string sourcePath = Path.Combine(pathElements);
                string destinationPath = Path.Combine(config.outputPath, project);
                
                Console.WriteLine("Installing {1} to {0}", destinationPath, project);
                Console.WriteLine("   from {0}", sourcePath);

                // Ensure we have a directory.
                if (!Directory.Exists(destinationPath)) {
                    Directory.CreateDirectory(destinationPath);
                }

                DirectoryInfo sourceDir = new DirectoryInfo(sourcePath);
                FileInfo[] sourceFiles = sourceDir.GetFiles();
                
                foreach (FileInfo file in sourceFiles) {
                    string destinationFile = Path.Combine(destinationPath, file.Name);
                    //Console.WriteLine("Installing {0} to {1}", file.Name, 
                    //    destinationFile);
                    
                    try {
                        // Copy file to install location.
                        file.CopyTo(destinationFile, config.overwrite);
                    } 
                    catch (System.IO.IOException e) {
                        Console.WriteLine("Error: {0} - skipping...", e.Message);
                        continue;
                    }
                }
            }
            return err;
        }
    }
}
