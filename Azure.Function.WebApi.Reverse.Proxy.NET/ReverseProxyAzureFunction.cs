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
              HttpRequestMessage request
            , ILogger log
        )
    {
        var originalUri = request.RequestUri;
        var scheme = originalUri.Segments[3].Trim('/');
        var baseAddress = originalUri.Segments[4].Trim('/');
        var pathPrefix = $"/api/{nameof(ReverseProxyFunction)}/{scheme}/{baseAddress}/";
        var pathAndQuery = originalUri.PathAndQuery[pathPrefix.Length..];
        request.RequestUri = new Uri($"{scheme}://{baseAddress}/{pathAndQuery}");
        request.Headers.Host = null;
        using var httpClient = new HttpClient();
        return await httpClient.SendAsync(request);
    }
}
