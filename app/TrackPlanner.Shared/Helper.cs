namespace TrackPlanner.Shared
{
    public static class Helper
    {
        public static string GetUniqueFileName(string directory, string filename)
        {
            string ext = System.IO.Path.GetExtension(filename);
            string result = System.IO.Path.Combine(directory, filename);

            filename = System.IO.Path.GetFileNameWithoutExtension(filename);
            int count = 0;
            while (System.IO.File.Exists(result))
            {
                result = System.IO.Path.Combine(directory, $"{filename}-{count}{ext}");
                ++count;

            }

            return result;
        }
    }
}