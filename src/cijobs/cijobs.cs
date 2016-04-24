using System;
using System.IO;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.CommandLine;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;
using Newtonsoft.Json;

namespace ManagedCodeGen
{
    class cijobs {
        public enum Command {
            List,
            Start
        }
        
        // Define options to be parsed 
        public class Config
        {
            private ArgumentSyntax syntaxResult;
            private Command command = Command.List;
            private string jobName;
            private string forkUrl;
            private string number;
            private string matchPattern = String.Empty;
            private string coreclrBranchName = "master";
            private string privateBranchName;
  
            public Config(string[] args) {

                syntaxResult = ArgumentSyntax.Parse(args, syntax =>
                {
                    // NOTE!!! - Commands and their options are ordered.  Moving an option out of line
                    // could move it to another command.  Take a careful look at how they're organized
                    // before changing.
                    syntax.DefineCommand("list", ref command, Command.List, "List jobs on the CI system.");
                    syntax.DefineOption("j|job", ref jobName, "Name of the job.");
                    syntax.DefineOption("b|branch", ref coreclrBranchName, "Name of the branch (dotnet/coreclr).");
                    syntax.DefineOption("m|match", ref matchPattern, "Regex pattern used to select job output.");
                    syntax.DefineOption("n|number", ref number, "Job number.");
                    
                    syntax.DefineCommand("start", ref command, Command.Start, "Start a job on the CI system.");
                    syntax.DefineOption("j|job", ref jobName, "Name of the job.");
                    syntax.DefineOption("f|fork", ref forkUrl, "URL of user fork.");
                    syntax.DefineOption("p|private", ref privateBranchName, "Name of private branch. Used with --fork");
                });
                
                // Run validation code on parsed input to ensure we have a sensible scenario.
                
                validate();
            }
            
            private void validate() {
                ;
            }
            
            public Command DoCommand { get { return command; } }
            public string JobName { get { return jobName; } }
            public string Number { get { return number; } }
            public string MatchPattern { get { return matchPattern; } }
            public string CoreclrBranchName { get { return coreclrBranchName; } }
            public string PrivateBranchName { get { return privateBranchName; } }
            public string ForkUrl { get { return forkUrl; } }
            
        }
        
        struct Artifact {
            public string fileName;
            public string relativePath;
        }
        
        struct Revision {
            public string SHA1;
        }
        struct Action {
            public Revision lastBuiltRevision;
        }
        class BuildInfo {
            public List<Action> actions;
            public List<Artifact> artifacts;
        }
        
        struct Job {
            public string name;
            public string url;
        }
        
        struct ProductJobs {
            public List<Job> jobs;
        }
        
        struct Build {
            public int number;
            public string url;
        }
        
        struct JobBuilds {
            public List<Build> builds;
            public Build lastSuccessfulBuild;
        }

        static int Main(string[] args)
        {
            Config config = new Config(args);
            int error = 0;
            
            Command currentCommand = config.DoCommand;
            switch (currentCommand) {
                case Command.List: {
                    ListCommand.List(config).Wait();
                    break;
                }
                case Command.Start: {
                    StartCommand.Start(config).Wait();
                    break;
                }
                default: {
                    Console.WriteLine("super bad!  why no command!");
                    error = 1;
                    break;
                }
            }
            
            return error;
        }

        class ListCommand {
            // List jobs and their details from the dotnet_coreclr project on .NETCI Jenkins instance.
            // List functionality:
            //    if --job is not specified, list jobs under branch. (default is "master" set in Config).
            //    if --job is specified, list job instances by id with details.
            //    if --job and --id is specified list particular job instance, status, and artifacts.
            // 
            public static async Task<string> List(Config config) {
                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri("http://dotnet-ci.cloudapp.net/");
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    string productString = String.Format("job/dotnet_coreclr/job/{0}/api/json?&tree=jobs[name,url]", config.CoreclrBranchName);

                    HttpResponseMessage response = await client.GetAsync(productString);
                    
                    if (response.IsSuccessStatusCode)
                    {

                        var json = await response.Content.ReadAsStringAsync();

                        ProductJobs product = JsonConvert.DeserializeObject<ProductJobs>(json);
                        
                        if (config.JobName != null) {
                            Job job = GetJob(product, config.JobName);
                            var jobString = String.Format(@"job/dotnet_coreclr/job/master/job/{0}", job.name);
                            var messageString = String.Format("{0}/api/json?&tree=builds[number,url],lastSuccessfulBuild[number,url]",
                                              jobString);
                                              
                            HttpResponseMessage jobResponse = await client.GetAsync(messageString);
                            
                            var buildJson = await jobResponse.Content.ReadAsStringAsync();
                            
                            JobBuilds jobBuilds = JsonConvert.DeserializeObject<JobBuilds>(buildJson);
                            
                            if (config.Number != null) {
                                try {
                                    Build build = GetBuild(jobBuilds, Convert.ToInt32(config.Number));
                                }
                                catch (FormatException e)
                                {
                                    Console.WriteLine("Input string is not a sequence of digits.", e);
                                }
                                catch (OverflowException e)
                                {
                                    Console.WriteLine("The number cannot fit in an Int32.", e);
                                }
                                
                                string buildString = String.Format("{0}/{1}/{2}", "job/dotnet_coreclr/job/master/job",
                                    job.name , config.Number);
                                string buildMessage = String.Format("{0}/{1}", buildString,
                                    "api/json?&tree=actions[lastBuiltRevision[SHA1]],artifacts[fileName,relativePath]");
                                Console.WriteLine(buildMessage);
                                HttpResponseMessage buildResponse = await client.GetAsync(buildMessage);
                                var buildNumberJson = await buildResponse.Content.ReadAsStringAsync();
                                BuildInfo info = JsonConvert.DeserializeObject<BuildInfo>(buildNumberJson);
                                
                                if (config.DoCommand == Command.List) {
                                    PrettyBuilds(Enumerable.Repeat(info, 1));
                                }
                                
                                return String.Format("{0}/{1}", client.BaseAddress.ToString(), buildString);
                            }
                            else {
                                foreach (var build in jobBuilds.builds) {
                                    Console.WriteLine("{0} : {1}", job.name, build.number);
                                }
                                Console.WriteLine("lastSuccessfulBuild : {0}", jobBuilds.lastSuccessfulBuild.number);
                                
                                string buildString = String.Format("{0}/{1}/{2}", "job/dotnet_coreclr/job/master/job",
                                    job.name , jobBuilds.lastSuccessfulBuild.number);
                                 
                                 return String.Format(@"{0}/{1}",client.BaseAddress.ToString(), buildString);
                            }
                        }
                        else {
                            List<Job> jobs = ListJobs(product, new Regex(config.MatchPattern)).ToList();
                            
                            if (config.DoCommand == Command.List) {
                                PrettyJobs(jobs);
                            }
                            
                            // Return verified job string.
                            return String.Format("{0}/{1}", client.BaseAddress.ToString(), productString);
                        }   
                    }
                    else {
                        // Error status code, dump the response.
                        Console.WriteLine(response.ToString());
                        
                        return "Error!";
                    }
                }
            }

            static void PrettyJobs (IEnumerable<Job> jobs) {
                foreach(var job in jobs) {
                    Console.WriteLine("job {0}", job.name);
                }
            }
            
            static void PrettyBuilds (IEnumerable<BuildInfo> buildInfoList) {
                foreach (var buildInfo in buildInfoList) {
                    foreach (var artifact in buildInfo.artifacts) {
                        Console.WriteLine("artifact {0}", artifact.fileName);
                    }
                }
            }

            static Build GetBuild(JobBuilds job, int number) {
                return job.builds.FirstOrDefault(x => x.number == number);
            }
            
            static Job GetJob(ProductJobs product, string jobName) {
                return product.jobs.FirstOrDefault(x => x.name == jobName);
            }
            
            static IEnumerable<Job> ListJobs(ProductJobs product, Regex matchPattern) {
                return product.jobs.Where(x => matchPattern.IsMatch(x.name));
            }
        }
        
        class CopyCommand {
            
        }
        
        class StartCommand {
            public static async Task Start(Config config) {
                // create parameter string and post to Jenkins
                ;
            }
        }
    }
}

