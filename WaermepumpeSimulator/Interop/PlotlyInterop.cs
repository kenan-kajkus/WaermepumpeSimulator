using System.Text.Json;
using Microsoft.JSInterop;

namespace WaermepumpeSimulator.Interop;

public class PlotlyInterop
{
    private readonly IJSRuntime _js;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public PlotlyInterop(IJSRuntime js) => _js = js;

    public async Task NewPlot(string elementId, object traces, object layout)
    {
        await _js.InvokeVoidAsync("PlotlyInterop.newPlot", elementId,
            JsonSerializer.Serialize(traces, JsonOpts),
            JsonSerializer.Serialize(layout, JsonOpts));
    }

    public async Task Purge(string elementId)
    {
        await _js.InvokeVoidAsync("PlotlyInterop.purge", elementId);
    }
}
