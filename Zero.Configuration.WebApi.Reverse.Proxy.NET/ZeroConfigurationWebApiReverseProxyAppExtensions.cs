// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Transforms;

namespace Microshaoft;

public static class ZeroConfigurationWebApiReverseProxyAppExtensions
{
    public static void UseZeroConfigurationWebApiReverseProxy
                            (
                                this WebApplication @this
                                , Func<HttpRequest, ForwarderError> onAuthenticationProcessFunc = null!
                                , string pathBaseString = "api/proxy"
                            )
    {
        pathBaseString = pathBaseString.Trim().Trim('/');

#pragma warning disable CA2000 // Dispose objects before losing scope
        var socketsHttpHandler = new SocketsHttpHandler()
        {
            UseProxy = false
           , AllowAutoRedirect = false
           // , AutomaticDecompression    = DecompressionMethods.All
           // , UseCookies                = false
           , ConnectTimeout = TimeSpan.FromSeconds(15)
           , ActivityHeadersPropagator = new ReverseProxyPropagator
                                               (
                                                   DistributedContextPropagator
                                                                           .Current
                                               )
        };

        var httpMessageInvoker = new HttpMessageInvoker(socketsHttpHandler);
#pragma warning restore CA2000 // Dispose objects before losing scope

        var requestOptions = new ForwarderRequestConfig
        {
            ActivityTimeout = TimeSpan.FromSeconds(300)
        };

        var httpForwarder = @this.Services.GetService<IHttpForwarder>();

        var a = pathBaseString.Split('/');
        var p = a.Length;

        // When using IHttpForwarder for direct forwarding you are responsible for routing, destination discovery, load balancing, affinity, etc..
        // For an alternate example that includes those features see BasicYarpSample.
        @this
            .UseEndpoints
                (
                    (endpoints) =>
                    {
                        endpoints
                                .Map
                                    (
                                        $"{pathBaseString}/{{**catch-all}}"
                                        , requestDelegate: async (httpContext) =>
                                        {
                                            if (onAuthenticationProcessFunc != null)
                                            {
                                                if
                                                    (
                                                        ForwarderError.None
                                                        !=
                                                        onAuthenticationProcessFunc(httpContext.Request)
                                                    )
                                                {
                                                    httpContext.Response.StatusCode = 401;
                                                    return;
                                                }
                                            }

                                            var request = httpContext.Request;
                                            var requestPathSegments = request.Path.Value!.Split('/');

                                            var destinationScheme = requestPathSegments[p + 1];
                                            var destinationBaseAddress = requestPathSegments[p + 2];

                                            var pathPrefix = $"/{pathBaseString}/{destinationScheme}/{destinationBaseAddress}";

                                            var destinationPrefix = $"{destinationScheme}://{destinationBaseAddress}";
                                            var destinationPath = request.Path.Value![pathPrefix.Length..];

                                            var forwarderError =
                                                    await httpForwarder!
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
                                                var exception = forwarderErrorFeature!.Exception;
                                            }
                                        }
                                    );
                    }
                )
            ;
    }
}
