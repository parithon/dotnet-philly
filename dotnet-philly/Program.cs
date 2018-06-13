using System;
using System.IO;
using System.Net.Http;
using System.IO.Compression;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using DotNetSamples.Core;
using System.Net.Sockets;

namespace dotnet_philly
{
	[Command(Description = "Download and extract sample code from Microsoft's Sample Repository")]
	class Program
	{
		// TODO: Need to be able to configure registry in the future
		internal static readonly Uri Registry = new Uri("https://localhost:5001/Samples");

        private readonly ILogger _logger;

		public static int Main(string[] args) => CommandLineApplication.Execute<Program>(args);

        public Program()
        {
            _logger = new LoggerFactory()
                    .AddConsole(LogLevel.Information)
                    .AddDebug(LogLevel.Debug)
                    .CreateLogger<Program>();
        }

		[Argument(0, Description = "Name of the sample project to fetch.")]
		public string Name { get; }

		[Option("-f|--folder <FOLDER>", Description ="Folder to write the sample to.")]
		public string OutputFolder { get; }

		[Option(Description="Show a sample project's details.")]
		public bool Details {get;}

		private async Task<int> OnExecute(CommandLineApplication app)
		{
			using (var client = new HttpClient{ BaseAddress = Registry })
			{
				 
				if (Details)
				{
					_logger.LogInformation($"Retrieving sample '{Name}' from '{Registry}'");
					await OutputSampleDetails(Name, client);
					return 1;
				}
				else if (string.IsNullOrEmpty(Name))
				{
                    _logger.LogInformation($"Retrieving samples from '{Registry}'");
					await OutputAvailableSamples(client);
				} 
				else
				{
                    _logger.LogInformation($"Downloading sample '{Name}' from '{Registry}'");
					await DownloadProject(Name, client);
				}

				return 0;
			}
		}

		private async Task DownloadProject(string name, HttpClient client)
		{

            var sample = await GetSampleAsync(name, client);

			if (sample == null)
				return;
				
			var destinationFolder = OutputFolder ?? sample.Name;

			Stream zipFile;
			try
			{
				zipFile = await client.GetStreamAsync(sample.Url);
			}
			catch (HttpRequestException ex)
			{
				var errorMessage = $"Could not download sample '{name}'. {ex.Message}.";
				WriteError(errorMessage);
				_logger.LogError($"{errorMessage}\n{ex.StackTrace}");
				return;
			}

			using (var zipArchive = new ZipArchive(zipFile))
			{
				try
				{
					zipArchive.ExtractToDirectory(destinationFolder, true);
				}
				catch(Exception exception)
				{
					var errorMessage = $"An error occured while attempting to extract the {sample.Name} sample.\n{exception.Message}";
					WriteError(errorMessage);
					_logger.LogError($"{errorMessage}\n{exception.StackTrace}");
				}
			}
		}

		private async Task OutputAvailableSamples(HttpClient client)
		{
            var samplesList = string.Empty;
            try
            {
                samplesList = await client.GetStringAsync("");
            }
            catch (HttpRequestException ex)
            {
				var errorMessage = $"Could not retrieve samples from {Registry}. {ex.Message}";
                WriteError(errorMessage);
				_logger.LogError($"{errorMessage}\n\n{ex.StackTrace}");
				return;
            }

            Console.WriteLine($"The available samples from {Registry}");
			Console.WriteLine("---------------------------------------------\n");
            
			var samplesArray = JsonConvert.DeserializeObject<Sample[]>(samplesList);

			var lineFormat = "{0,-20}  {1,-10}  {2,-50}";
			Console.WriteLine(lineFormat, "Sample Name", "Command", "Description");
			Console.WriteLine(new String('-',84) + "\n");

			foreach (var s in samplesArray)
			{
				Console.WriteLine(lineFormat, s.Name, s.Command, s.Description.Substring(0,s.Description.Length > 50 ? 49 : s.Description.Length-1));
			}

			Console.WriteLine($"\nTotal samples found: {samplesArray.Length}");
		}

		private async Task OutputSampleDetails(string name, HttpClient client)
		{
			var sample = await GetSampleAsync(name, client);

			if (sample == null)
				return;

			var lineFormat = "{0,-10} {1}";
			Console.WriteLine(lineFormat, "Name:", sample.Name);
			Console.WriteLine(lineFormat, "Command:", sample.Command);
			Console.WriteLine(lineFormat, "Url:", sample.Url);
			Console.WriteLine(lineFormat, "Details:", sample.Description);
		}

		private async Task<Sample> GetSampleAsync(string name, HttpClient client)
		{
			var sampleData = string.Empty;
			try
			{
				sampleData = await client.GetStringAsync("Samples/" + name);
			}
			catch (HttpRequestException ex)
			{
				var errorMessage = $"Could not retrieve sample {name}. {ex.Message}";
				WriteError(errorMessage);
				_logger.LogError($"{errorMessage}\n{ex.StackTrace}");
				return null;
			}

			return JsonConvert.DeserializeObject<Sample>(sampleData);
		}

		private void WriteError(string message)
		{
			var foregroundColor = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine(message);
			Console.ForegroundColor = foregroundColor;
		}
	}
}