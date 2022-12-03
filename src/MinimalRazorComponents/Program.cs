using MinimalRazorComponents.Components;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.UseStaticFiles();
app.UseBlazorFrameworkFiles();

// Manually rendering components as "fragment" results
app.MapGet("/", () => Results.Extensions.Component<HelloWorld>(new { Message = "Hello from Minimal APIs" }));
app.MapGet("/about", () => Results.Extensions.Component<About>());

// Rendering components as first-class routable endpoints
app.MapComponent<Form>();
app.MapComponent<ClientApp>();

app.Run();
