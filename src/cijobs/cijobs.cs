// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.CommandLine;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO.Compression;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ManagedCodeGen
{
    internal class cijobs
    {
        // Supported commands.  List to view information from the CI system, and Copy to download artifacts.
        public enum Command
        {
            List,
            Copy
        }
        
        // List options control what level and level of detail to put out.
        // Jobs lists jobs under a product.
        // Builds lists build instances under a job.
        // Number lists a particular builds info.
        public enum ListOption
        {
            Invalid,
            Jobs,
            Builds,
            Number
        }

        // Define options to be parsed 
        public class Config
        {
            private ArgumentSyntax _syntaxResult;
            private Command _command = Command.List;
            private ListOption _listOption = ListOption.Invalid;
            private string _jobName;
            private string _forkUrl;
            private int _number;
            private string _matchPattern = String.Empty;
            private string _coreclrBranchName = "master";
            private string _privateBranchName;
            private bool _lastSuccessful = true;
            private bool _install = false;
            private bool _unzip = false;
            private string _outputPath;
            private string _rid;
            private string _jitDasmRoot;

            public Config(string[] args)
            {
                _syntaxResult = ArgumentSyntax.Parse(args, syntax =>
                {
                    // NOTE!!! - Commands and their options are ordered.  Moving an option out of line
                    // could move it to another command.  Take a careful look at how they're organized
                    // before changing.
                    syntax.DefineCommand("list", ref _command, Command.List, "List jobs on the CI system.");
                    syntax.DefineOption("j|job", ref _jobName, "Name of the job.");
                    syntax.DefineOption("b|branch", ref _coreclrBranchName, "Name of the branch (dotnet/coreclr).");
                    syntax.DefineOption("m|match", ref _matchPattern, "Regex pattern used to select job output.");
                    syntax.DefineOption("n|number", ref _number, "Job number.");
                    syntax.DefineOption("l|last_successful", ref _lastSuccessful, "List last successfull build");

                    syntax.DefineCommand("copy", ref _command, Command.Copy, "Start a job on the CI system.");
                    syntax.DefineOption("j|job", ref _jobName, "Name of the job.");
                    syntax.DefineOption("n|number", ref _number, "Job number.");
                    syntax.DefineOption("b|branch", ref _coreclrBranchName, "Name of branch.");
                    syntax.DefineOption("o|output", ref _outputPath, "Output path.");
                    syntax.DefineOption("u|unzip", ref _unzip, "Unzip copied artifacts");
                    syntax.DefineOption("i|install", ref _install, "Install tool in asmdiff.json");
                });

                // Extract system RID from dotnet cli
                List<string> commandArgs = new List<string> { "--info" };
                Microsoft.DotNet.Cli.Utils.Command infoCmd = Microsoft.DotNet.Cli.Utils.Command.Create(
                    "dotnet", commandArgs);
                infoCmd.CaptureStdOut();
                infoCmd.CaptureStdErr();

                CommandResult result = infoCmd.Execute();

                if (result.ExitCode != 0)
                {
                    Console.WriteLine("dotnet --info returned non-zero");
                }

                var lines = result.StdOut.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

                foreach (var line in lines)
                {
                    Regex pattern = new Regex(@"RID:\s*([A-Za-z0-9\.-]*)$");
                    Match match = pattern.Match(line);
                    if (match.Success)
                    {
                        _rid = match.Groups[1].Value;
                    }
                }

                // Set up JIT_DASM_ROOT string in the context. (null or otherwise)
                _jitDasmRoot = Environment.GetEnvironmentVariable("JIT_DASM_ROOT");

                // Run validation code on parsed input to ensure we have a sensible scenario.
                validate();
            }

            private void validate()
            {
                switch (_command)
                {
                    case Command.List:
                        {
                            validateList();
                        }
                        break;
                    case Command.Copy:
                        {
                            validateCopy();
                        }
                        break;
                }
            }

            private void validateCopy()
            {
                ;
            }

            private void validateList()
            {
                if (_jobName != null)
                {
                    _listOption = ListOption.Builds;

                    if (_matchPattern != String.Empty)
                    {
                        _syntaxResult.ReportError("Match pattern not valid with --job");
                    }
                }
                else
                {
                    _listOption = ListOption.Jobs;
                }
            }

            public Command DoCommand { get { return _command; } }
            public ListOption DoListOption { get { return _listOption; } }
            public string JobName { get { return _jobName; } }
            public int Number { get { return _number; } }
            public string MatchPattern { get { return _matchPattern; } }
            public string CoreclrBranchName { get { return _coreclrBranchName; } }
            public bool LastSuccessful { get { return _lastSuccessful; } }
            public bool DoInstall { get { return _install; } }
            public bool DoUnzip { get { return _unzip; } }
            public string OutputPath { get { return _outputPath; } }
            public bool HasJitDasmRoot { get { return (_jitDasmRoot != null); } }
            public string JitDasmRoot { get { return _jitDasmRoot; } }
            public string RID { get { return _rid; } }
        }

        // The following block of simple structs maps to the data extracted from the CI system as json.
        // This allows to map it directly into C# and access it.

        private struct Artifact
        {
            public string fileName;
            public string relativePath;
        }

        private struct Revision
        {
            public string SHA1;
        }
        private class Action
        {
            public Revision lastBuiltRevision;
        }
        private class BuildInfo
        {
            public List<Action> actions;
            public List<Artifact> artifacts;
            public string result;
        }

        private struct Job
        {
            public string name;
            public string url;
        }

        private struct ProductJobs
        {
            public List<Job> jobs;
        }

        private struct Build
        {
            public int number;
            public string url;
            public BuildInfo info;
        }

        private struct JobBuilds
        {
            public List<Build> builds;
            public Build lastSuccessfulBuild;
        }

        // Main entry point.  Simply set up a httpClient to access the CI
        // and switch on the command to invoke underlying logic.
        private static int Main(string[] args)
        {
            Config config = new Config(args);
            int error = 0;

            CIClient cic = new CIClient(config);

            Command currentCommand = config.DoCommand;
            switch (currentCommand)
            {
                case Command.List:
                    {
                        ListCommand.List(cic, config).Wait();
                        break;
                    }
                case Command.Copy:
                    {
                        CopyCommand.Copy(cic, config).Wait();
                        break;
                    }
                default:
                    {
                        Console.WriteLine("super bad!  why no command!");
                        error = 1;
                        break;
                    }
            }

            return error;
        }

        // Wrap CI httpClient with focused APIs for product, job, and build.
        // This logic is seperate from listing/copying and just extracts data.
        private class CIClient
        {
            private HttpClient _client;

            public CIClient(Config config)
            {
                _client = new HttpClient();
                _client.BaseAddress = new Uri("http://dotnet-ci.cloudapp.net/");
                _client.DefaultRequestHeaders.Accept.Clear();
                _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            }

            public async Task<bool> DownloadProduct(Config config, string outputPath)
            {
                string messageString
                        = String.Format("job/dotnet_coreclr/job/{0}/job/{1}/{2}/artifact/bin/Product/*zip*/Product.zip",
                            config.CoreclrBranchName, config.JobName, config.Number);

                 Console.WriteLine("Downloading: {0}", messageString);

                 HttpResponseMessage response = await _client.GetAsync(messageString);

                 bool downloaded = false;

                 if (response.IsSuccessStatusCode)
                 {
                    var zipPath = Path.Combine(outputPath, "Product.zip");
                    using (var outputStream = System.IO.File.Create(zipPath))
                    {
                        Stream inputStream = await response.Content.ReadAsStreamAsync();
                        inputStream.CopyTo(outputStream);
                    }
                    downloaded = true;
                 }
                 else
                 {
                     Console.WriteLine("Zip not found!");
                 }
                 
                 return downloaded;
            }

            public async Task<IEnumerable<Job>> GetProductJobs(string productName, string branchName)
            {
                string productString
                    = String.Format("job/{0}/job/{1}/api/json?&tree=jobs[name,url]",
                        productName, branchName);
                HttpResponseMessage response = await _client.GetAsync(productString);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var productJobs = JsonConvert.DeserializeObject<ProductJobs>(json);
                    return productJobs.jobs;
                }
                else
                {
                    return Enumerable.Empty<Job>();
                }
            }

            public async Task<IEnumerable<Build>> GetJobBuilds(string productName, string branchName, 
                string jobName)
            {
                var jobString
                    = String.Format(@"job/dotnet_coreclr/job/master/job/{0}", jobName);
                var messageString
                    = String.Format("{0}/api/json?&tree=builds[number,url],lastSuccessfulBuild[number,url]",
                        jobString);
                HttpResponseMessage response = await _client.GetAsync(messageString);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var jobBuilds = JsonConvert.DeserializeObject<JobBuilds>(json);
                    var builds = jobBuilds.builds;

                    var count = builds.Count();
                    for (int i = 0; i < count; i++)
                    {
                        var build = builds[i];
                        // fill in build info
                        build.info = GetJobBuildInfo(jobName, build.number).Result;
                        builds[i] = build;
                    }

                    return jobBuilds.builds;
                }
                else
                {
                    return Enumerable.Empty<Build>();
                }
            }

            public async Task<BuildInfo> GetJobBuildInfo(string jobName, int number)
            {
                string buildString = String.Format("{0}/{1}/{2}", "job/dotnet_coreclr/job/master/job",
                   jobName, number);
                string buildMessage = String.Format("{0}/{1}", buildString,
                   "api/json?&tree=actions[lastBuiltRevision[SHA1]],artifacts[fileName,relativePath],result");
                HttpResponseMessage response = await _client.GetAsync(buildMessage);

                if (response.IsSuccessStatusCode)
                {
                    var buildInfoJson = await response.Content.ReadAsStringAsync();
                    var info = JsonConvert.DeserializeObject<BuildInfo>(buildInfoJson);
                    return info;
                }
                else
                {
                    return null;
                }
            }
        }

        // Implementation of the list command.
        private class ListCommand
        {
            // List jobs and their details from the dotnet_coreclr project on .NETCI Jenkins instance.
            // List functionality:
            //    if --job is not specified, ListOption.Jobs, list jobs under branch.
            //        (default is "master" set in Config).
            //    if --job is specified, ListOption.Builds, list job builds by id with details.
            //    if --job and --id is specified, ListOption.Number, list particular job instance, 
            //        status, and artifacts.
            // 
            public static async Task List(CIClient cic, Config config)
            {
                switch (config.DoListOption)
                {
                    case ListOption.Jobs:
                        {
                            var jobs = cic.GetProductJobs("dotnet_coreclr", "master").Result;

                            if (config.MatchPattern != null)
                            {
                                var pattern = new Regex(config.MatchPattern);
                                PrettyJobs(jobs.Where(x => pattern.IsMatch(x.name)));
                            }
                            else
                            {
                                PrettyJobs(jobs);
                            }
                        }
                        break;
                    case ListOption.Builds:
                        {
                            var builds = cic.GetJobBuilds("dotnet_coreclr",
                                "master", config.JobName);
                            PrettyBuilds(builds.Result);
                        }
                        break;
                    case ListOption.Number:
                        {
                            var info = cic.GetJobBuildInfo(config.JobName, config.Number);
                            // Pretty build info
                            PrettyBuildInfo(info.Result);
                        }
                        break;
                    default:
                        {
                            Console.WriteLine("Unknown list option!");
                        }
                        break;
                }
            }

            private static void PrettyJobs(IEnumerable<Job> jobs)
            {
                foreach (var job in jobs)
                {
                    Console.WriteLine("job {0}", job.name);
                }
            }

            private static void PrettyBuilds(IEnumerable<Build> buildList)
            {
                foreach (var build in buildList)
                {
                    var result = build.info.result;
                    if (result != null)
                    {
                        Console.Write("build {0} - {1} : ", build.number, result);
                        PrettyBuildInfo(build.info);
                    }
                }
            }

            private static void PrettyBuildInfo(BuildInfo info, bool artifacts = false)
            {
                var actions = info.actions.Where(x => x.lastBuiltRevision.SHA1 != null);

                if (actions.Any())
                {
                    var action = actions.First();
                    Console.WriteLine("commit {0}", action.lastBuiltRevision.SHA1);
                }
                else
                {
                    Console.WriteLine("");
                }

                if (artifacts)
                {
                    Console.WriteLine("    artifacts:");
                    foreach (var artifact in info.artifacts)
                    {
                        Console.WriteLine("       {0}", artifact.fileName);
                    }
                }
            }
        }

        // Implementation of the copy command.
        private class CopyCommand
        {
            // Based on the config, copy down the artifacts for the referenced job.
            // Today this is just the product bits.  This code also knows how to install
            // the bits into the asmdiff.json config file.
            public static async Task Copy(CIClient cic, Config config)
            {
                string tag = String.Format("{0}-{1}", config.JobName, config.Number);
                string outputPath = (config.OutputPath != null)
                    ? config.OutputPath : config.JitDasmRoot;
                string toolPath = Path.Combine(outputPath, "tools", tag);
                // Object filled out with asmdiff.json if install selected.
                JObject jObj = null;
                JArray tools = null;
                bool install = false;
                string asmDiffPath = String.Empty;

                if (config.DoInstall)
                {
                    if (config.HasJitDasmRoot)
                    {
                        asmDiffPath = Path.Combine(config.JitDasmRoot, "asmdiff.json");

                        if (File.Exists(asmDiffPath))
                        {
                            string configJson = File.ReadAllText(asmDiffPath);
                            jObj = JObject.Parse(configJson);
                            tools = (JArray)jObj["tools"];

                            if (tools.Where(x => (string)x["tag"] == tag).Any())
                            {
                                Console.WriteLine("{0} is already installed in the asmdiff.json. Remove before re-install.", tag);
                                return;
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Install specified but no asmdiff.json found - missing JIT_DASM_ROOT.");
                    }

                    // Flag install to happen now that we've vetted the input.
                    install = true;
                }

                // Pull down the zip file.
                Directory.CreateDirectory(toolPath);
                DownloadZip(cic, config, toolPath).Wait();

                if (install)
                {
                    JObject newTool = new JObject();
                    newTool.Add("tag", tag);
                    // Derive underlying tool directory based on current RID.
                    string[] platStrings = config.RID.Split('.');
                    string platformPath = Path.Combine(toolPath, "Product");
                    foreach (var dir in Directory.EnumerateDirectories(platformPath))
                    {
                        if (Path.GetFileName(dir).ToUpper().Contains(platStrings[0].ToUpper()))
                        {
                            newTool.Add("path", Path.GetFullPath(dir));
                            tools.Last.AddAfterSelf(newTool);
                            break;
                        }
                    }
                    // Overwrite current asmdiff.json with new data.
                    using (var file = File.CreateText(asmDiffPath))
                    {
                        using (JsonTextWriter writer = new JsonTextWriter(file))
                        {
                            writer.Formatting = Formatting.Indented;
                            jObj.WriteTo(writer);
                        }
                    }
                }
            }

            // Download zip file.  It's arguable that this should be in the 
            private static async Task DownloadZip(CIClient cic, Config config, string outputPath)
            {
                // Copy product tools to output location. 
                bool success = cic.DownloadProduct(config, outputPath).Result;

                if (config.DoUnzip)
                {
                    // unzip archive in place.
                    var zipPath = Path.Combine(outputPath, "Product.zip");
                    ZipFile.ExtractToDirectory(zipPath, outputPath);
                }
            }
        }
    }
}

