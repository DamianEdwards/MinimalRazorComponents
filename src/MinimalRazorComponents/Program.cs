using MinimalRazorComponents.Components;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.UseStaticFiles();
app.UseBlazorFrameworkFiles();

app.MapGet("/", () => Results.Extensions.Component<HelloWorld>(new { Message = "Hello from Minimal APIs" }));
app.MapGet("/about", () => Results.Extensions.Component<About>());

//app.MapGet("/clientapp", () => Results.Extensions.Component<ClientApp>());

app.MapComponent<Form>();
app.MapComponent<ClientApp>();

app.Run();
