using Blazored.Modal;
using Microsoft.AspNetCore.Components;

namespace TrackPlanner.WebUI.Client.Dialogs
{
    public partial class ProgressDialog
    {
        [CascadingParameter] private BlazoredModalInstance ModalInstance { get; set; } = default!;
        [Parameter] public string? Message { get; set; }

        public ProgressDialog()
        {
        }
        
    }
}