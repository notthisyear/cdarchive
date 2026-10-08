using System;

namespace CdArchiveBackend.Common
{
    internal static class Utilities
    {
        public static bool CompareNullableStrings(string? a, string? b)
        {
            if (string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b))
                return false;
            else if (!string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b))
                return false;
            else if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b))
                return true;
            else
                return a!.Equals(b, StringComparison.Ordinal);
        }
    }
}
