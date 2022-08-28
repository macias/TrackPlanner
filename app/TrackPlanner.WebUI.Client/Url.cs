using System;

namespace TrackPlanner.WebUI.Client
{
    public static class Url
    {
        // Microsoft.AspNetCore.Mvc
        public static string Combine(Uri url1, string url2)
        {
            return Combine($"{url1}", url2);
        }

        public static string Combine(string url1, string url2)
        {
            // https://stackoverflow.com/a/2806717/6734314

            if (url1 == null || url1.Length == 0)
                return url2;

            if (url2 == null || url2.Length == 0)
                return url1;

            url1 = url1.TrimEnd('/', '\\');
            url2 = url2.TrimStart('/', '\\');

            return $"{url1}/{url2}";
        }

        public static string Combine(string url1, string url2, string url3)
        {
            return Combine(Combine(url1, url2), url3);
        }
    }
}
