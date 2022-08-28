using Newtonsoft.Json;
using TrackPlanner.Data;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TrackPlanner.Data.Serialization;

namespace TrackPlanner.WebUI.Client
{
    public sealed class RestClient
    {
        private readonly HttpClient http;
        private readonly JsonSerializerSettings jsonOptions;

        public RestClient(HttpClient http)
        {
            this.http = http;
            http.DefaultRequestHeaders.Add("Accept", "application/json");
            jsonOptions = NewtonOptionsFactory.BuildJsonOptions(compact:false);
        }
        
        private string serialize<T>(T value)
        {
            return JsonConvert.SerializeObject(value, jsonOptions);
        }

        private T? deserialize<T>(string value)
        {
            return JsonConvert.DeserializeObject<T>(value, jsonOptions);
        }

        private Uri? createUri(string? uri) =>
            string.IsNullOrEmpty(uri) ? null : new Uri(uri, UriKind.RelativeOrAbsolute);

        public ValueTask<(string? failure, TResult? result)> PutAsync<TResult>(string? requestUri, object input, CancellationToken cancellationToken)
        {
            return sendAsync<TResult>(HttpMethod.Put, requestUri, input, cancellationToken);
        }

        public ValueTask<(string? failure, TResult? result)> PostAsync<TResult>(string? requestUri, object input, CancellationToken cancellationToken)
        {
            return sendAsync<TResult>(HttpMethod.Post, requestUri, input, cancellationToken);
        }

        public ValueTask<(string? failure, TResult? result)> GetAsync<TResult>(string? requestUri,RestQuery? query, CancellationToken cancellationToken)
        {
            if (query != null)
                requestUri = $"{requestUri}?{query}";
            return sendAsync<TResult>(HttpMethod.Get, requestUri, input:null, cancellationToken);
        }
        
        public ValueTask<(string? failure, TResult? result)> GetAsync<TResult>(string? requestUri, CancellationToken cancellationToken)
        {
            return GetAsync<TResult>(requestUri, query:null, cancellationToken);
        }

        private StringContent? createJsonContent(object? input)
        {
            if (input == null)
                return null;
            else
                return new StringContent(serialize(input), Encoding.UTF8, "application/json");
        }
        
        private async ValueTask<(string? failure, TResult? result)> sendAsync<TResult>(HttpMethod method, string? requestUri, object? input, CancellationToken cancellationToken)
        {
            using (var request_content = createJsonContent(input))
            {
                using (HttpRequestMessage request = new HttpRequestMessage(method, createUri(requestUri)) {Content = request_content})
                {
                    using (var response = await this.http.SendAsync(request, cancellationToken).ConfigureAwait(false))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            if (typeof(TResult) == typeof(ValueTuple))
                                return (null, default(TResult));
                            else
                            {
                                string content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                                var result = deserialize<TResult>(content);
                                return (null, result);
                            }
                        }
                        else
                        {
                            string content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                            return (content ?? "No error message", default);
                        }
                    }

                }
            }
        }
    }
}
