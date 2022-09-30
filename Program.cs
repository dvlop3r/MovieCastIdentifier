using MediaToolkit.Services;
using MovieCastIdentifier;
using MovieCastIdentifier.Services;
using MovieCastIdentifier.SignalRHubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
builder.Services.AddHostedService<QueuedHostedService>();
builder.Services.AddSingleton<ImdbSettings>(x => builder.Configuration.GetSection("ImdbSettings").Get<ImdbSettings>());
builder.Services.AddHttpClient<IImdbApi, ImdbApi>();
builder.Services.AddSingleton<ICastDetectorService, CastDetectorService>();

builder.Services.AddSignalR();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapHub<FileStreamHub>("/fileStreamHub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
