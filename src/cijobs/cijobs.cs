using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ManagedCodeGen
{
    class cijobs
    {
        class Job {
            public string name;
            public string url;
        }
        
        class ProjectJobs {
            public List<Job> jobs;
        }
        
        static void Main()
        {
            RunAsync().Wait();
        }

        static async Task RunAsync()
        {
            using (var client = new HttpClient())
            {
                Console.WriteLine("firing!");
                client.BaseAddress = new Uri("http://dotnet-ci.cloudapp.net/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                
                HttpResponseMessage response = await client.GetAsync("job/dotnet_coreclr/job/master/api/json?&tree=jobs[name,url]");
                
                Console.WriteLine("message {0}", response.RequestMessage.ToString());
                
                if (response.IsSuccessStatusCode)
                {
                    Task<string> json = response.Content.ReadAsStringAsync();
                    
                    ProjectJobs coreclr = JsonConvert.DeserializeObject<ProjectJobs>(json.Result);
                    
                    foreach (var job in coreclr.jobs) {
                        Console.WriteLine("name {0}", job.name);
                    }
                    
                    //Console.WriteLine(JsonConvert.SerializeObject(parsedJson, Formatting.Indented));
                }
                else {
                    Console.WriteLine("hunh? {0}", response.ReasonPhrase);
                }
            }
        }
    }
}
