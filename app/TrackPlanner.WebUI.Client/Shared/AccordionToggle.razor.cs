using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

// https://stackoverflow.com/questions/58538154/how-to-collapse-expand-razor-components-using-blazor-syntax

namespace TrackPlanner.WebUI.Client.Shared
{
    public partial class AccordionToggle
    {
        [Parameter] public EventCallback<bool> CollapsedChanged { get; set; }

        private bool collapsed;

        [Parameter]
        public bool Collapsed
        {
            get { return this.collapsed; }
            set
            {
                setCollapsedAsync(value);
            }
        }

        [Parameter] public object? Tag { get; set; }
        [Parameter] public RenderFragment Header { get; set; } = default!;
        [Parameter] public EventCallback<AccordionToggle> OnToggled { get; set; }

        private Task setCollapsedAsync(bool value)
        {
            if (this.collapsed == value)
                return Task.CompletedTask;
            this.collapsed = value;
            return this.CollapsedChanged.InvokeAsync(value);
        }

        public async void ToggleAsync()
        {
            Console.WriteLine($"Toggling from collapsed {Collapsed}");
            await setCollapsedAsync(!Collapsed);
            await OnToggled.InvokeAsync(this);
        }
        
    }
}