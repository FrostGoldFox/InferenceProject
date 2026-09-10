using MiddleWare.Auth;
using MiddleWare.Routing;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddHttpClient("Relay", httpClient =>
{
    httpClient.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.Configure<RelayOptions>(
    builder.Configuration.GetSection(RelayOptions.SectionName));
builder.Services.AddScoped<IMessageRouter, MessageRouter>();
builder.Services.AddSingleton<IClientSessionStore, ClientSessionStore>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.Run();




