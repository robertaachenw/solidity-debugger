using System;
using System.Text.RegularExpressions;

namespace Meadow.Shared
{
    public static class SolcVersion
    {
        private static readonly Regex VerRegex = new Regex("(\\d+)\\.(\\d+)\\.(\\d+)"); 
        public static Version FromString(string s)
        {
            try
            {
                var result = VerRegex.Match(s);
                return new Version(
                    int.Parse(result.Groups[1].ToString()),
                    int.Parse(result.Groups[2].ToString()),
                    int.Parse(result.Groups[3].ToString()));
            }
            catch
            {
                return null;
            }
        }

        public static string ToString(Version v)
        {
            return v.ToString();
        }
    }
}