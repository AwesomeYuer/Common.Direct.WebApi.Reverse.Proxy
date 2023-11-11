// Copyright (c) Microsoft. All rights reserved.

using Microshaoft;
using Microsoft.AspNetCore.Http.Extensions;
using System.Text.Encodings.Web;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

//builder.Services.Configure<KestrelServerOptions>(options =>
//{
//    options.AllowSynchronousIO = true;
//});

var configuration = builder.Configuration;

builder.Services.AddHttpForwarder();

var app = builder.Build();

app.UseFileServer();

string proxyPathBaseString = configuration.GetValue(nameof(proxyPathBaseString), "api/proxy");

app.UseRouting();

var logger = app.Logger;

var jsonSerializerOptions = new JsonSerializerOptions()
{
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    , WriteIndented = true
};

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

                var requestHeadersJson = JsonSerializer.Serialize(request.Headers, jsonSerializerOptions);
                var responseHeadersJson = JsonSerializer.Serialize(response.Headers, jsonSerializerOptions);

                if (args.Length > 0)
                {
                    if (args.Any(x => x.Equals("-o", StringComparison.OrdinalIgnoreCase)))
                    {
                        responseBodyText = "*******";
                    }
                    if (args.Any(x => x.Equals("-h", StringComparison.OrdinalIgnoreCase)))
                    {
                        requestHeadersJson = responseHeadersJson = "*******";
                    }
                }

                logger.LogError($@"

<<<<<<<<<<<<<<<<<<
{context.Request.GetDisplayUrl()}
{context.Request.GetEncodedUrl()}
===========================
Request.Headers:
{requestHeadersJson}
===========================
Request.Body:
{requestBodyText}
===========================
Response.Headers:
{responseHeadersJson}
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

// add by Awesome Yuer
bool enableClearConsole = configuration.GetValue(nameof(enableClearConsole), true);
if (enableClearConsole)
{
    _ = Task.Run
    (
        () =>
        {
            while (1 == 1)
            {
                var input = Console.ReadLine();
                if (input!.Equals("clear", StringComparison.OrdinalIgnoreCase))
                {
                    Console.SetCursorPosition(0, 0);
                    for (int i = 0; i < 8; i++)
                    {
                        // Invalid when run in Windows Terminal
                        // You can set keybindings such as "Ctrl + k" for clear buffer in Windows Terminal by yourself
                        Console.Clear();
                    }
                    Console.WriteLine($"<<<<<<<<<<<<<<<控制台已清屏 @ {DateTime.Now: yyyy-MM-dd HH:mm:ss.fffff}>>>>>>>>>>>>>>>>");
                }
                else
                {
                    Console.WriteLine(@"输入""clear""可以清屏控制台");
                }
            }
        }
    );
}

app.Run();
