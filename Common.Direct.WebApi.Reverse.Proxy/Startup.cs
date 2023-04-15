// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Transforms;

namespace Yarp.Sample;

/// <summary>
/// ASP.NET Core pipeline initialization showing how to use IHttpForwarder to directly handle forwarding requests.
/// With this approach you are responsible for destination discovery, load balancing, and related concerns.
/// </summary>
public class Startup
{
    /// <summary>
    /// This method gets called by the runtime. Use this method to add services to the container.
    /// </summary>
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpForwarder();
    }

    /// <summary>
    /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    /// </summary>
    public void Configure(IApplicationBuilder app, IHttpForwarder forwarder)
    {
        // Configure our own HttpMessageInvoker for outbound calls for proxy operations
        var httpClient = new HttpMessageInvoker(new SocketsHttpHandler()
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false,
            ActivityHeadersPropagator = new ReverseProxyPropagator(DistributedContextPropagator.Current),
            ConnectTimeout = TimeSpan.FromSeconds(15),
        });

        var requestOptions = new ForwarderRequestConfig
                            { 
                                ActivityTimeout = TimeSpan.FromSeconds(300)
                            };

        app.UseRouting();

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

                                            var error =
                                                    await forwarder
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
                                            if (error != ForwarderError.None)
                                            {
                                                var errorFeature = httpContext.Features.Get<IForwarderErrorFeature>();
                                                var exception = errorFeature.Exception;
                                            }
                                        }
                                    );
                    }
                );
    }
}
