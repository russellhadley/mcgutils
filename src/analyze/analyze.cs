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
            private string basePath = null;
            private string diffPath = null;
            private bool recursive = false;
            
            public Config(string[] args) {

                syntaxResult = ArgumentSyntax.Parse(args, syntax =>
                {
                    syntax.DefineOption("b|base", ref basePath, "Base file or directory.");
                    syntax.DefineOption("d|diff", ref diffPath, "Diff file or directory.");
                    syntax.DefineOption("r|recursive", ref recursive, "Search directories recursivly.");
                });
                
                // Run validation code on parsed input to ensure we have a sensible scenario.
                
                validate();
            }
            
            void validate() {
                
            }
            
            public string BasePath { get { return basePath; } }
            public string DiffPath { get { return diffPath; } }
            public bool Recursive { get { return recursive; } }
        }
 
        public static void Main(string[] args)
        {
            Config config = new Config(args);
            
            Console.WriteLine("Hello World!");
        }
    }
}
