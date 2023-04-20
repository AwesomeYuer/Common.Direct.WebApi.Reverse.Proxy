using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Azure.Function.WebApi.Reverse.Proxy;
public static class ReverseProxyFunction
{
    [FunctionName(nameof(ReverseProxyFunction))]
    public static async Task<HttpResponseMessage> Run
        (
            [
                HttpTrigger
                    (
                          AuthorizationLevel.Anonymous
                        , "get"
                        , "post"
                        , "put"
                        , "patch"
                        , "options"
                        , Route = "{* }"
                    )
            ]
              HttpRequestMessage httpRequestMessage
            , ILogger logger
        )
    {
        var originalUri = httpRequestMessage.RequestUri;
        var forwardScheme = originalUri.Segments[3].Trim('/');
        var forwardBaseAddress = originalUri.Segments[4].Trim('/');
        var pathPrefix = $"/api/{nameof(ReverseProxyFunction)}/{forwardScheme}/{forwardBaseAddress}/";
        var forwardPathAndQuery = originalUri.PathAndQuery[pathPrefix.Length..];
        httpRequestMessage.RequestUri = new Uri($"{forwardScheme}://{forwardBaseAddress}/{forwardPathAndQuery}");
        httpRequestMessage.Headers.Host = null;
        using var httpClient = new HttpClient()
        { 
            Timeout = TimeSpan.FromSeconds(10 * 60)
        };
        return await httpClient.SendAsync(httpRequestMessage);
    }
}
