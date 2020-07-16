using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace dotnet2nix
{
    public class NugetHost
    {
        static readonly HttpClient _client = new HttpClient();
        public string PackageUrl { get; set; }

        public static async Task<NugetHost> FromUrl(string url)
        {
            var nh = new NugetHost();

            var jsonResp = await _client.GetStringAsync(url);
            var index = JsonSerializer.Deserialize<NugetIndex>(jsonResp);

            nh.PackageUrl = index.Resources.First(i => i.ItemType == "PackageBaseAddress/3.0.0").Id;
            if (!nh.PackageUrl.EndsWith("/")) nh.PackageUrl += "/";

            return nh;
        }

        public override string ToString()
        {
            return $"{PackageUrl}";
        }
    }

    public class NugetIndex
    {
        [JsonPropertyName("resources")]
        public IEnumerable<NugetDetails> Resources { get; set; }
    }

    public class NugetDetails
    {
        [JsonPropertyName("@id")]
        public string Id { get; set; }
        
        [JsonPropertyName("@type")]
        public string ItemType { get; set; }
    }
}
