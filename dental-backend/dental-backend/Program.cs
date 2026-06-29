using System.Text.Json.Serialization;
using dental_backend.Dentally;
using dental_backend.Services;
using dental_backend.Storage;

var builder = WebApplication.CreateBuilder(args);

const string FrontendCors = "frontend";

builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
    options.AddPolicy(FrontendCors, policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:4200" })
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services.Configure<DentallyOptions>(builder.Configuration.GetSection(DentallyOptions.SectionName));
builder.Services.Configure<JsonStoreOptions>(builder.Configuration.GetSection(JsonStoreOptions.SectionName));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IAppointmentStore, JsonAppointmentStore>();
builder.Services.AddScoped<ISyncService, SyncService>();

var useMock = builder.Configuration.GetValue<bool>($"{DentallyOptions.SectionName}:UseMock");
if (useMock)
{
    builder.Services.AddSingleton<IDentallyClient, MockDentallyClient>();
}
else
{
    builder.Services
        .AddHttpClient<IDentallyClient, DentallyClient>((sp, http) =>
        {
            var section = builder.Configuration.GetSection(DentallyOptions.SectionName);
            var baseUrl = section["BaseUrl"] ?? "https://api.dentally.co/v1/";
            http.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
            // Dentally forbids requests without an acceptable User-Agent.
            http.DefaultRequestHeaders.UserAgent.ParseAdd(section["UserAgent"] ?? "DentPulse/1.0");
        })
        // Transient-fault retries, timeout and 429 Retry-After handling out of the box.
        .AddStandardResilienceHandler();
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(FrontendCors);
app.MapControllers();

app.Run();

// Exposed so a test project can reference the application entry point if needed.
public partial class Program;
