using System;
using System.Threading.Tasks;
using Blazored.Modal;
using Blazored.Modal.Services;
using TrackPlanner.LinqExtensions;
using TrackPlanner.WebUI.Client.Dialogs;
using TrackPlanner.WebUI.Client.Shared;

namespace TrackPlanner.WebUI.Client
{
    public static class ModalServiceDialogs
    {
        public static IDisposable ShowGuardDialog(this IModalService modalService, string message)
        {
            var options = new ModalOptions()
            {
                DisableBackgroundCancel = true,
                HideHeader = true,
                HideCloseButton = true,

            };
            var parameters = new ModalParameters();
            parameters.Add(nameof(ProgressDialog.Message), message);
            var dialog = modalService.Show<ProgressDialog>(title: null, parameters, options);
            return new CompositeDisposable(() => dialog.Close());
        }

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