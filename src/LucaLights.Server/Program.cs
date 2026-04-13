using LucaLights.Server;

var app = LucaLightsServerHost.BuildApplication(args);
await app.RunAsync();
