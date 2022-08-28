using System.Threading.Tasks;
using Blazored.Modal;
using Blazored.Modal.Services;
using TrackPlanner.WebUI.Client.Shared;

namespace TrackPlanner.WebUI.Client
{
    public static class ModalServiceDialogs
    {
        public static async ValueTask<string?> ShowFileDialogAsync(this IModalService modalService, string title, FileDialog.DialogKind kind)
        {
            var options = new ModalOptions()
            {
                DisableBackgroundCancel = true,
            };
            var parameters = new ModalParameters();
            parameters.Add(nameof(FileDialog.Kind), kind);
            var dialog = modalService.Show<FileDialog>(title, parameters, options);
            var modal_result = await dialog.Result;

            if (modal_result.Cancelled)
                return null;
            else
                return $"{modal_result.Data}";
        }

     

    }
}