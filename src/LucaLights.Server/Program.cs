using LucaLights.Core;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet(
    "/",
    () => Results.Ok(
        new
        {
            app = "LucaLights.Server",
            status = "starting",
            colorTypeReady = Color.Black
        }));

app.Run();
