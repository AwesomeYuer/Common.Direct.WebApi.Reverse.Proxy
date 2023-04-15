using System.Diagnostics;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpForwarder();

// Configure our own HttpMessageInvoker for outbound calls for proxy operations
var httpMessageInvoker = new HttpMessageInvoker
                            (
                                new SocketsHttpHandler()
                                {
                                      UseProxy                  = false
                                    , AllowAutoRedirect         = false
                                    // , AutomaticDecompression    = DecompressionMethods.All
                                    // , UseCookies                = false
                                    , ConnectTimeout            = TimeSpan.FromSeconds(15)
                                    , ActivityHeadersPropagator = new ReverseProxyPropagator
                                                                        (
                                                                            DistributedContextPropagator
                                                                                                    .Current
                                                                        )
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

                                    var destinationPrefix = $"{scheme}://{baseAddress}";
                                    var destinationPath = request.Path.Value![pathPrefix.Length..];

                                    var forwarderError =
                                            await httpForwarder
                                                    .SendAsync
                                                        (
                                                              httpContext
                                                            , destinationPrefix
                                                            , httpMessageInvoker
                                                            , requestOptions
                                                            , (context, forwardRequest) =>
                                                            {
                                                                // Customize the query string:
                                                                var queryTransformContext =
                                                                        new QueryTransformContext(context.Request);
                                                                // Assign the custom uri. Be careful about extra slashes when concatenating here. RequestUtilities.MakeDestinationAddress is a safe default.
                                                                forwardRequest
                                                                    .RequestUri =
                                                                        RequestUtilities
                                                                            .MakeDestinationAddress
                                                                                    (
                                                                                          destinationPrefix
                                                                                        , new PathString(destinationPath)
                                                                                        , queryTransformContext.QueryString
                                                                                    );
                                                                // Suppress the original request header, use the one from the destination Uri.
                                                                forwardRequest.Headers.Host = null;
                                                                return default;
                                                            }
                                                        );

                                    // Check if the proxy operation was successful
                                    if 
                                        (forwarderError != ForwarderError.None)
                                    {
                                        var forwarderErrorFeature =
                                                    httpContext
                                                            .Features
                                                            .Get<IForwarderErrorFeature>();
                                        var exception = forwarderErrorFeature.Exception;
                                    }
                                }
                            );
            }
        );

app.Run();
