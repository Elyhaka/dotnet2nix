using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace dotnet2nix
{
    public class LockFile
    {
        [JsonPropertyName("dependencies")]
        public JsonElement RawDependencies { get; set; }

        private class RawLockFileDependency
        {
            [JsonPropertyName("resolved")]
            public string Resolved { get; set; }
        }

        private static LockFileDependency LockDepToObject(JsonProperty realDep)
        {
            var v = JsonSerializer.Deserialize<RawLockFileDependency>(realDep.Value.GetRawText());
            return new LockFileDependency(realDep.Name, v.Resolved);
        }

        public IEnumerable<LockFileDependency> Dependencies()
        {
            return RawDependencies.EnumerateObject()
                .SelectMany(eo => eo.Value.EnumerateObject())
                .Select(LockDepToObject)
                .Where(p => !string.IsNullOrWhiteSpace(p.Version));
        }
    }

    public class LockFileDependencyMetadata
    {
        [JsonPropertyName("resolvedUrl")]
        public string ResolvedUrl { get; set; }
        
        [JsonPropertyName("hash")]
        public string Hash { get; set; }
    }

    public class LockFileDependency
    {
        [JsonPropertyName("name")]
        public string Name { get; }

        [JsonPropertyName("version")]
        public string Version { get; }

        [JsonPropertyName("metadata")]
        public LockFileDependencyMetadata Metadata { get; set; }

        public LockFileDependency(string n, string v)
        {
            Name = n;
            Version = v;
        }

        public override string ToString() => $"{Name}, Version: {Version}";
    }
}
