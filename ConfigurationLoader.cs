using Newtonsoft.Json;
using revit_aec_dm_extensibility_sample.Models;
using System.IO;
using System.Reflection;

namespace revit_aec_dm_extensibility_sample
{
    public static class ConfigurationLoader
    {
        public static async Task<ApsConfiguration> LoadConfigurationAsync()
        {
            const string resourceName = "revit_aec_dm_extensibility_sample.appsettings.json";
            var assembly = Assembly.GetExecutingAssembly();

            await using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException(
                    $"Configuration file '{resourceName}' not found. " +
                    $"Available resources: {string.Join(", ", assembly.GetManifestResourceNames())}");

            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();

            // Using Newtonsoft.Json instead of System.Text.Json
            return JsonConvert.DeserializeObject<ApsConfiguration>(json) ?? throw new InvalidOperationException("Failed to deserialize configuration.");
        }
    }
}