using System.Text.Json;
using System.Text.Json.Serialization;

namespace LaNata_Api.Core.Helpers
{
    public static class JsonHelper
    {
        private const int MAX_DEPTH = 128;

        // The two option sets are not interchangeable. Storage emits no $id/$ref metadata so the
        // output stays readable by the web and by SQL consumers; cloning preserves references so a
        // graph with back-references round-trips intact
        private static readonly JsonSerializerOptions STORAGE_OPTIONS = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            MaxDepth = MAX_DEPTH
        };

        private static readonly JsonSerializerOptions CLONE_OPTIONS = new()
        {
            ReferenceHandler = ReferenceHandler.Preserve,
            MaxDepth = MAX_DEPTH
        };

        public static T Clone<T>(this T source)
        {
            if (source is null)
                return default;

            return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(source, CLONE_OPTIONS), CLONE_OPTIONS);
        }

        public static string Serialize<T>(this T source)
        {
            if (source is null)
                return null;

            return JsonSerializer.Serialize(source, STORAGE_OPTIONS);
        }

        public static T Deserialize<T>(this string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                return default;

            return JsonSerializer.Deserialize<T>(source, STORAGE_OPTIONS);
        }
    }
}
