using Microsoft.AspNetCore.Mvc;
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
        var originalUri = httpRequestMessage.RequestUri;
        var configurationRoot = new ConfigurationBuilder()
                                        .SetBasePath
                                            (
                                                executionContext
                                                        .FunctionAppDirectory
                                            )
                                        .AddJsonFile
                                            ("custom.settings.json")
                                        .Build();

        var expectSecretPathSegment = configurationRoot["secretPathSegment"];
        var originalSecretPathSegment = originalUri.Segments[3].Trim('/');

        if
            (
                string
                    .Compare
                        (
                            expectSecretPathSegment
                            , originalSecretPathSegment
                            , StringComparison.OrdinalIgnoreCase
                        )
                !=
                0
            )
        {
            return

                new HttpResponseMessage
                                (
                                    HttpStatusCode.Unauthorized

                                )
                { 
                  Content = new StringContent("forbidden!")
                };
        }

        var forwardScheme = originalUri.Segments[4].Trim('/');
        var forwardBaseAddress = originalUri.Segments[5].Trim('/');
        var pathPrefix = $"/api/{expectSecretPathSegment}/{nameof(ReverseProxyFunction)}/{forwardScheme}/{forwardBaseAddress}/";
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
