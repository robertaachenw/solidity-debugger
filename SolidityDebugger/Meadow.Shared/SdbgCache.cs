using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Meadow.Shared;

public static class SdbgCache
{
    static SdbgCache()
    {
        if (!Directory.Exists(SdbgHome.AppHomeEngineCache))
        {
            Directory.CreateDirectory(SdbgHome.AppHomeEngineCache);
        }
    }
    
    public static string KeyHash(string key)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        return BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes(key))).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static string CacheFilePath(string key)
    {
        return Path.Combine(SdbgHome.AppHomeEngineCache, KeyHash(key) + ".json");
    }
    
    public static bool Contains(string key)
    {
        return File.Exists(CacheFilePath(key));
    }
    
    public static bool Remove(string key)
    {
        var filePath = CacheFilePath(key);
        if (!File.Exists(filePath)) return false;
        File.Delete(filePath);
        return true;
    }

    public static TimeSpan GetAge(string key)
    {
        var filePath = CacheFilePath(key);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(filePath);
        }

        var lastModified = File.GetLastWriteTime(filePath);
        var result = DateTime.Now - lastModified;
        return result;
    }

    public static void Set(string key, JObject value)
    {
        File.WriteAllText(CacheFilePath(key), JsonConvert.SerializeObject(value));
    }

    public static JObject Get(string key)
    {
        try
        {
            return JObject.Parse(File.ReadAllText(CacheFilePath(key)));
        }
        catch (Exception ex)
        {
            throw new Exception($"SdbgCache.Get: {ex.Message}");
        }
    }
}
