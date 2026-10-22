using System.Text.Json.Serialization;
using Backend.Api;
using Backend.Application;
using Backend.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
// Enums skickas som text ("Assignment") i stället för tal, både in och ut.
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.Services.EnsureDatabaseCreated();

app.UseSwagger();
app.UseSwaggerUI();

// Klienten byggs in i wwwroot vid dotnet publish. Lokalt körs den via Vite i stället.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapApiEndpoints();
app.MapClient();

app.Run();

// Gör Program synlig för WebApplicationFactory i integrationstesterna.
public partial class Program { }
