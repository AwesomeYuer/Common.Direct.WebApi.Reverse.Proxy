using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
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
            , ExecutionContext executionContext
            , ILogger logger
        )
    {
        var configurationRoot = new ConfigurationBuilder()
                                        .AddEnvironmentVariables()
                                        .Build();

        var expectProxyPathBaseString =
                        configurationRoot
                                    .GetValue
                                            (
                                                "PROXY_PATH_BASE_STRING"
                                                , "ReverseProxyFunction/temp"
                                            )
                                    .Trim('/');

        //expectProxyPathBaseString = "proxy/awesomeyuer@microshaoft";

        var originalUri = httpRequestMessage.RequestUri;

        var requestPath = originalUri.AbsolutePath;

        expectProxyPathBaseString= $"/api/{expectProxyPathBaseString}/";

        if (!requestPath.StartsWith(expectProxyPathBaseString, StringComparison.OrdinalIgnoreCase))
        {
            return
                new HttpResponseMessage
                                (
                                    HttpStatusCode
                                            .Unauthorized
                                )
                { 
                    Content = new StringContent("Forbidden!")
                };
        }

        requestPath = requestPath[expectProxyPathBaseString.Length..];

        var p = 0;

        p += originalUri.GetLeftPart(UriPartial.Authority).Length;
        p += expectProxyPathBaseString.Length;

        var i = requestPath.IndexOf('/');

        var forwardScheme = requestPath[..i];

        if
            (
                !forwardScheme.Equals("http", StringComparison.OrdinalIgnoreCase)
                &&
                !forwardScheme.Equals("https", StringComparison.OrdinalIgnoreCase)
            )
        {
            return
                new HttpResponseMessage
                                (
                                    HttpStatusCode
                                            .BadRequest
                                )
                {
                    Content = new StringContent("BadRequest!")
                };
        }
        
        p += forwardScheme.Length + 1;

        var forwardUrl = $"{forwardScheme}://{originalUri.ToString()[p..]}";
        
        httpRequestMessage.RequestUri = new Uri(forwardUrl);
        httpRequestMessage.Headers.Host = null;
        using var httpClient = new HttpClient()
        { 
            Timeout = TimeSpan.FromSeconds(10 * 60)
        };
        return await httpClient.SendAsync(httpRequestMessage);
    }
}
