using System.Collections.Specialized;
using System.Web;

namespace TrackPlanner.WebUI.Client
{
    public sealed class RestQuery
    {
        private readonly NameValueCollection query;

        public RestQuery()
        {
            this.query = HttpUtility.ParseQueryString(string.Empty);
        }

        public RestQuery Add(string key, string? value)
        {
            query[key] = value;
            return this;
        }

        public override string? ToString()
        {
            return this.query.ToString();
        }
    }
}