using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QuotesApi.Data;
using QuotesApi.Repositories;
using QuotesApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Register lifetimes
// Singleton: stateless cross-cutting (IClock)
builder.Services.AddSingleton<IClock, SystemClock>();

// Scoped: one instance per request (DbContext / repositories)
builder.Services.AddScoped<QuotesDbContext>();
builder.Services.AddScoped<IQuoteRepository, QuoteRepository>();

// Transient: new instance each resolve (lightweight helpers)
builder.Services.AddTransient<IQuoteFormatter, QuoteFormatter>();

builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();

app.Run();
