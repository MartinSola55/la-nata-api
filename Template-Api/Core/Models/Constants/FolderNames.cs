using System;

namespace Template_Api.Core.Models.Constants
{
    public static class FolderNames
    {
        public const string Files = "Files";
        public const string Images = "Images";
        public const string Documents = "Documents";

        public static readonly string[] All = [Files, Images, Documents];

        public static bool IsValid(string folder)
        {
            return Array.Exists(All, name => name.Equals(folder, StringComparison.Ordinal));
        }
    }
}
