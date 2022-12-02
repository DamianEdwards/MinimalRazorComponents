using MinimalRazorComponents.Components;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => Results.Extensions.Component<HelloWorld>(new { Message = "Hello from Minimal APIs" }));
app.MapGet("/about", () => Results.Extensions.Component<About>());

app.Run();
