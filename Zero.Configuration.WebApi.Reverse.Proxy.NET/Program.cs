using System.Diagnostics;
using System.Net;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpForwarder();

// Configure our own HttpMessageInvoker for outbound calls for proxy operations
var httpClient = new HttpMessageInvoker
                    (
                        new SocketsHttpHandler()
                        {
                              UseProxy = false
                            , AllowAutoRedirect = false
                            , AutomaticDecompression = DecompressionMethods.None
                            , UseCookies = false
                            , ActivityHeadersPropagator =
                                        new ReverseProxyPropagator
                                                    (DistributedContextPropagator.Current)
                            , ConnectTimeout = TimeSpan.FromSeconds(15)
                            ,
                        }
                    );

var requestOptions = new ForwarderRequestConfig
{
    ActivityTimeout = TimeSpan.FromSeconds(300)
};
var app = builder.Build();

app.UseRouting();

var httpForwarder = app.Services.GetService<IHttpForwarder>();

// When using IHttpForwarder for direct forwarding you are responsible for routing, destination discovery, load balancing, affinity, etc..
// For an alternate example that includes those features see BasicYarpSample.
app
    .UseEndpoints
        (
            (endpoints) =>
            {
                endpoints
                        .Map
                            (
                                "/{**catch-all}"
                                , async (httpContext) =>
                                {
                                    var request = httpContext.Request;
                                    var pathSegments = request.Path.Value.Split('/');

                                    var scheme = pathSegments[1];
                                    var baseAddress = pathSegments[2];

                                    var pathPrefix = $"/{scheme}/{baseAddress}";

                                    var path = request.Path.Value![pathPrefix.Length..];

                                    var query = request.QueryString.Value;

                                    if
                                        (
                                            !string.IsNullOrEmpty(query)
                                            &&
                                            !string.IsNullOrWhiteSpace(query)
                                        )
                                    {
                                        query = $"?{query}";
                                    }

                                    var destinationPrefix = $"{scheme}://{baseAddress}";

                                    var forwarderError =
                                            await httpForwarder
                                                    .SendAsync
                                                        (
                                                              httpContext
                                                            , destinationPrefix
                                                            , httpClient
                                                            , requestOptions
                                                            , (context, proxyRequest) =>
                                                            {
                                                                // Customize the query string:
                                                                var queryContext = new QueryTransformContext(context.Request);
                                                                // Assign the custom uri. Be careful about extra slashes when concatenating here. RequestUtilities.MakeDestinationAddress is a safe default.
                                                                proxyRequest
                                                                    .RequestUri =
                                                                        RequestUtilities
                                                                                .MakeDestinationAddress
                                                                                        (
                                                                                                destinationPrefix
                                                                                            , new PathString(path)
                                                                                            , queryContext.QueryString
                                                                                        );
                                                                // Suppress the original request header, use the one from the destination Uri.
                                                                proxyRequest.Headers.Host = null;
                                                                return default;
                                                            }
                                                        );

                                    // Check if the proxy operation was successful
                                    if (forwarderError != ForwarderError.None)
                                    {
                                        var errorFeature = httpContext.Features.Get<IForwarderErrorFeature>();
                                        var exception = errorFeature.Exception;
                                    }
                                }
                            );
            }
        );

app.Run();
