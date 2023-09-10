using Microshaoft;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpForwarder();

var app = builder.Build();

var secretPathSegment = builder.Configuration["secretPathSegment"];
app.UseZeroConfigurationWebApiReverseProxy(pathBaseString: secretPathSegment);

app.UseRouting();

app.Run();
