using LucaLights.Core.GameInput;
using LucaLights.Server.Services;

namespace LucaLights.Server.Endpoints;

public static class WebSocketEndpoints
{
    public static IEndpointRouteBuilder MapRuntimeWebSockets(this IEndpointRouteBuilder endpoints)
    {
        endpoints.Map(
            "/ws/events",
            async (
                HttpContext context,
                RuntimeEventBroadcaster eventBroadcaster,
                GameInputManager inputManager) =>
            {
                if (!context.WebSockets.IsWebSocketRequest)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsync("Expected a WebSocket request.");
                    return;
                }

                var socket = await context.WebSockets.AcceptWebSocketAsync();
                await eventBroadcaster.AcceptClientAsync(socket, inputManager, context.RequestAborted);
            });

        endpoints.Map(
            "/ws/preview",
            async (HttpContext context, PreviewBroadcaster previewBroadcaster) =>
            {
                if (!context.WebSockets.IsWebSocketRequest)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsync("Expected a WebSocket request.");
                    return;
                }

                var socket = await context.WebSockets.AcceptWebSocketAsync();
                await previewBroadcaster.AcceptClientAsync(socket, context.RequestAborted);
            });

        return endpoints;
    }
}
