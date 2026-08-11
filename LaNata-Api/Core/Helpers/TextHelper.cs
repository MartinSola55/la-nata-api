using System.Globalization;
using System.Text.RegularExpressions;

namespace LaNata_Api.Core.Helpers
{
    // The legacy app normalized every free text field on the way in, so the stored values stay
    // comparable (product descriptions are unique by text) and print the same way on every report.
    public static partial class TextHelper
    {
        // Invariant on purpose: the stored text is compared and printed, so casing must not depend
        // on the culture the host happens to run with
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        // Names: letters, spaces and apostrophes only, title cased
        public static string ToTitleCase(string value)
        {
            var clean = LettersOnly().Replace(value ?? string.Empty, string.Empty).Trim();

            return clean.Length == 0 ? null : Culture.TextInfo.ToTitleCase(clean.ToLower(Culture));
        }

        // Descriptions and addresses: digits allowed, only the first letter upper cased
        public static string ToUpperFirst(string value)
        {
            var clean = LettersAndDigitsOnly().Replace(value ?? string.Empty, string.Empty).Trim();

            if (clean.Length == 0)
                return null;

            return char.ToUpper(clean[0], Culture) + clean[1..].ToLower(Culture);
        }

        public static string ToNumber(string value)
        {
            var clean = DigitsOnly().Replace(value ?? string.Empty, string.Empty);

            return clean.Length == 0 ? null : clean;
        }

        [GeneratedRegex(@"[^a-zA-ZÀ-ſ\s']")]
        private static partial Regex LettersOnly();

        [GeneratedRegex(@"[^0-9a-zA-ZÀ-ſ\s']")]
        private static partial Regex LettersAndDigitsOnly();

        [GeneratedRegex(@"[^0-9]")]
        private static partial Regex DigitsOnly();
    }
}
