using System.Threading.Tasks;
using Geo;
using MathUnit;
using Microsoft.JSInterop;
using TrackPlanner.Data;
using TrackPlanner.Shared;
using TrackPlanner.LinqExtensions;

namespace TrackPlanner.WebUI.Client
{
    public sealed class CommonDialog
    {
        private readonly IJSRuntime jsRuntime;

        public CommonDialog(IJSRuntime jsRuntime)
        {
            this.jsRuntime = jsRuntime;
        }
       
        public async ValueTask AlertAsync(string message)
        {
            await jsRuntime.InvokeAsync<object?>("alert", message );
        }

        public async ValueTask<bool> ConfirmAsync(string message)
        {
            bool confirmed = await jsRuntime.InvokeAsync<bool>("confirm", message); 
            return confirmed;
        }

        public async ValueTask<string?> PromptAsync(string message,string? initialValue = null)
        { 
            var input = await jsRuntime.InvokeAsync<string?>("prompt", message,initialValue); 
            return input;
        }
        
    }
}
