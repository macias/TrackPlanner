namespace TrackPlanner.RestService
{
    public sealed class RestServiceConfig
    {
        public static string SectionName { get; } = "Rest";
        public static string CorsPolicyName { get; } = "CorsPolicy";

        public string[] CorsOrigins { get; set; } = default!;
        
        public bool DummyRouting { get; set; }
        public string Maps { get; set; } = default!;
        public bool UseSummaryLightTheme { get; set; }
        public SummaryTheme SummaryLightTheme { get; set; }
        public SummaryTheme SummaryDarkTheme { get; set; }

        public SummaryTheme SummaryActiveTheme => UseSummaryLightTheme ? SummaryLightTheme : SummaryDarkTheme;

        public RestServiceConfig()
        {
            SummaryLightTheme = new();
            SummaryDarkTheme = new();
        }
    }
}
