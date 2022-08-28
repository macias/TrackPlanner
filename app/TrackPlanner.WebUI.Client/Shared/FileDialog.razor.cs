using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Blazored.Modal;
using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using TrackPlanner.Data;
using TrackPlanner.RestSymbols;

namespace TrackPlanner.WebUI.Client.Shared
{
    public partial class FileDialog
    {
        
        public enum DialogKind
        {
            Open,
            Save
        }

        [CascadingParameter] private BlazoredModalInstance ModalInstance { get; set; } = default!;
        [Inject] public RestClient Rest { get; set; } = default!;
        [Inject] public IJSRuntime jsRuntime { get; set; } = default!;
        [Parameter] public DialogKind Kind { get; set; }
        private string currentDirectory = "";
        private string? selectedFileName = null;
        private DirectoryData? directoryData;
        private CommonDialog commonDialog = default!;
        private readonly EditContext editContext;

        public FileDialog()
        {
            this.editContext = new EditContext(this);
        }
        
        protected override async Task OnInitializedAsync()
        {
            this.commonDialog = new CommonDialog(jsRuntime);
            await getDirectoryDataAsync();
           
            await base.OnInitializedAsync();
        }
        
        private async Task getDirectoryDataAsync()
        {
            this.selectedFileName = null;
            
            (string? failure,directoryData )=  await Rest.GetAsync<DirectoryData>(
                Url.Combine(Program.Configuration.PlannerServer, Routes.Planner, Methods.Get_GetDirectory),
                new RestQuery().Add(Parameters.Directory,this.currentDirectory),
                CancellationToken.None);
           
            if (failure!=null)
                Console.WriteLine(failure);
            
            StateHasChanged();
        }

        private async void backToParent()
        {
            this.currentDirectory = System.IO.Path.GetDirectoryName(this.currentDirectory)!;

            await getDirectoryDataAsync();
        }
        
        private async void changeDirectory(string dir)
        {
            this.currentDirectory = System.IO.Path.Combine(this.currentDirectory, dir);

            await getDirectoryDataAsync();
        }

        private void selectFile(string file)
        {
            this.selectedFileName = file;
        }
        
        private async void confirmDialog()
        {
            if (Kind == DialogKind.Open)
            {
                if (!this.directoryData!.Files.Contains(this.selectedFileName))
                {
                    await this.commonDialog.AlertAsync("File does not exist.");
                    return;
                }
            }
            else
            {
                if (this.directoryData!.Files.Contains(this.selectedFileName) && !await this.commonDialog.ConfirmAsync("Overwrite existing file?"))
                    return;
            }
            string path = System.IO.Path.Combine(this.currentDirectory, this.selectedFileName!);
            await ModalInstance.CloseAsync(ModalResult.Ok(path));
        }

        private async void cancelDialog()
        {
            await ModalInstance.CancelAsync();
        }
    }
}