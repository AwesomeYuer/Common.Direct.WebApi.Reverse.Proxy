using Microshaoft;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);


//builder.Services.Configure<KestrelServerOptions>(options =>
//{
//    options.AllowSynchronousIO = true;
//});

builder.Services.AddHttpForwarder();

var app = builder.Build();

app.UseFileServer();

string proxyPathBaseString = builder.Configuration.GetValue(nameof(proxyPathBaseString), "api/proxy");

app.UseRouting();

var logger = app.Logger;

app
    .Use
        (
            async (context, next) =>
            {
                var response = context.Response;
                var request = context.Request;
                var originalResponseBodyStream = response.Body;
                var requestBodyText = string.Empty;
                request.EnableBuffering();

                if (request.Body is not null)
                {
                    //using
                    var stream = request.Body;
                    //using
                    var streamReader = new StreamReader(request.Body);
                    requestBodyText = await streamReader.ReadToEndAsync();
                    //streamReader.Close();
                    streamReader = null;
                    if (request.ContentType == "application/json")
                    {
                        requestBodyText = requestBodyText.AsJsonEscapeUnsafeRelaxedJson();
                        request.Body.Position = 0;
                    }
                }
                using var responseBodyWorkingStream = new MemoryStream();
                response
                        .Body = responseBodyWorkingStream;
                await
                    next(context);
                responseBodyWorkingStream
                        .Position = 0;
            
                using var responseBodyStreamReader = new StreamReader(responseBodyWorkingStream);
            
                var responseBodyText = await responseBodyStreamReader.ReadToEndAsync();
                if (response.ContentType == "application/json")
                {
                    responseBodyText = requestBodyText.AsJsonEscapeUnsafeRelaxedJson();
                }
                responseBodyText = "*******";

                logger.LogError($@"

<<<<<<<<<<<<<<<<<<
{context.Request.GetDisplayUrl()}
{context.Request.GetEncodedUrl()}

Request.Body:
{requestBodyText}
===========================
Respose.Body:
{responseBodyText}
>>>>>>>>>>>>>>>>>>
@ {DateTime.Now: HH:mm:ss.fff}
__________________
");

                responseBodyWorkingStream
                                .Position = 0;
                await
                    responseBodyWorkingStream
                            .CopyToAsync
                                    (
                                        originalResponseBodyStream
                                    );
            
                response
                    .Body = originalResponseBodyStream;
            }
        );

app.UseZeroConfigurationWebApiReverseProxy(pathBaseString: proxyPathBaseString);

_ = Task.Run
        (
            () =>
            { 
                while (1 == 1) 
                {
                    var input = Console.ReadLine();
                    if (input.Equals("clear", StringComparison.OrdinalIgnoreCase))
                    { 
                        Console.Clear();
                    }
                }
            }

        );

app.Run();
