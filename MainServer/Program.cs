using MainServer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection("Database"));
builder.Services.AddSingleton<ClientService>();
builder.Services.AddSingleton<ResultRepository>();
builder.Services.AddSingleton<LoginRepository>();
builder.Services.AddHostedService<ResultBatchWorker>();
builder.Services.AddHttpClient<MiddleWareClient>(httpClient =>
{
    var baseUrl = builder.Configuration["MiddleWare:BaseUrl"]
        ?? throw new InvalidOperationException("MiddleWare:BaseUrl is not configured.");

    httpClient.BaseAddress = new Uri(baseUrl);
    httpClient.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();


