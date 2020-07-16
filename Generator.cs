using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;

namespace dotnet2nix
{
    public class DepsNotFound : Exception
    {
        public DepsNotFound(string message) : base(message) {}
    }

    public class Generator
    {
        static readonly HttpClient _client = new HttpClient();

        private readonly Options _options;
        private string FullPath => Path.GetFullPath(_options.Folder);
        
        public Generator(Options options)
        {
            _options = options;
        }

        private async Task<IEnumerable<NugetHost>> ParseNugetHosts()
        {
            var nugetConfig = Path.Join(FullPath, "NuGet.config");

            if (!File.Exists(nugetConfig)) return new[] {
                new NugetHost { PackageUrl = "https://api.nuget.org/v3-flatcontainer/" }
            };

            var cfg = File.ReadAllText(nugetConfig);
            var parsed = XDocument.Parse(cfg);
            var elements = parsed.XPathSelectElements("configuration/packageSources/add");
            return await Task.WhenAll(elements
                .Select(e => NugetHost.FromUrl(e.Attribute("value")!.Value)));
        }

        private static IEnumerable<LockFileDependency> SearchPackages(string path, bool isSolution)
        {
            if (isSolution)
            {
                return new DirectoryInfo(path).GetDirectories().SelectMany(i => SearchPackages(i.FullName, false));
            }

            var packageLock = Path.Join(path, "packages.lock.json");

            if (!File.Exists(packageLock)) return new LockFileDependency[] { };

            var lockFile = File.ReadAllText(packageLock);
            var parsedFile = JsonSerializer.Deserialize<LockFile>(lockFile);
            return parsedFile.Dependencies();
        }

        public IEnumerable<LockFileDependency> GetRuntimes()
        {
            var p = new Process();
            p.StartInfo.FileName = "dotnet";
            p.StartInfo.Arguments = "--list-runtimes";
            p.StartInfo.RedirectStandardOutput = true;
            p.Start();
            p.WaitForExit();
            return p.StandardOutput.ReadToEnd()
                .Split("\n")
                .Where(str => !string.IsNullOrWhiteSpace(str))
                .Select(str => str.Split(" "))
                .Select(strs => new LockFileDependency(strs[0] + $".Runtime.{_options.Target}", strs[1]));
        }

        private string HashForData(Stream stream)
        {
            using SHA256 sha256 = SHA256.Create();
            var hashed = sha256.ComputeHash(stream);
            
            var sBuilder = new StringBuilder();
            foreach (var t in hashed)
            {
                sBuilder.Append(t.ToString("x2"));
            }
            return sBuilder.ToString();
        }

        private async Task<string> TryFetch(string url)
        {
            var resp = await _client.GetAsync(url);

            switch (resp.StatusCode)
            {
                case HttpStatusCode.Found:
                     return await TryFetch(resp.Headers.Location.ToString());
                case HttpStatusCode.OK:
                    var stream = await resp.Content.ReadAsStreamAsync();
                    return HashForData(stream);
                default:
                    throw new DepsNotFound($"Could not fetch {url}");
            }
        }

        private async Task GetMetadataForPackage(NugetHost[] hosts, LockFileDependency package)
        {
            foreach (var host in hosts)
            {
                try
                {
                    var url = $"{host.PackageUrl}{package.Name}/{package.Version}/{package.Name}.{package.Version}.nupkg";
                    var hash = await TryFetch(url);

                    Console.WriteLine($"Found package {package.Name} in repo {host.PackageUrl}. Hash: {hash}");

                    package.Metadata = new LockFileDependencyMetadata {
                        Hash = hash,
                        ResolvedUrl = url
                    };
                    return;
                }
                catch (DepsNotFound) { }
            }

            throw new DepsNotFound($"Could not fetch {package.Name}");
        }

        public async Task Run()
        {
            if (_options.SolutionMode)
            {
                Console.WriteLine("Running in solution mode");
            }
            
            var hosts = (await ParseNugetHosts()).ToArray();
            var packages = SearchPackages(_options.Folder, _options.SolutionMode).ToList();

            Console.WriteLine("Using NuGet hosts:");
            Console.WriteLine("==================");
            foreach (var host in hosts)
            {
                Console.WriteLine(host);
            }
            Console.WriteLine("");

            Console.WriteLine("Runtimes found:");
            Console.WriteLine("===================");
            var runtimes = GetRuntimes();
            foreach (var runtime in runtimes)
            {
                Console.WriteLine(runtime);
            }
            Console.WriteLine("");
            packages.AddRange(runtimes);

            Console.WriteLine("Dependencies:");
            Console.WriteLine("===================");
            foreach (var package in packages)
            {
                await GetMetadataForPackage(hosts, package);
            }
            Console.WriteLine("");

            var dotnet2nixFile = Path.Join(FullPath, $"dotnet2nix-pkgs.{_options.Target}.json");
            if (File.Exists(dotnet2nixFile))
            {
                Console.WriteLine($"Deleting previous {dotnet2nixFile} file");
                File.Delete(dotnet2nixFile);
            }
            Console.WriteLine($"Writing {dotnet2nixFile} file");

            var pkgsJson = JsonSerializer.Serialize(packages);
            await File.WriteAllTextAsync(dotnet2nixFile, pkgsJson);
        }
    }
}
