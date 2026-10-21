var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();

// Gör Program synlig för WebApplicationFactory i integrationstesterna.
public partial class Program { }
