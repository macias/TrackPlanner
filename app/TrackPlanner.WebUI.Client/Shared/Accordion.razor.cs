using Microsoft.AspNetCore.Components;

// https://stackoverflow.com/questions/58538154/how-to-collapse-expand-razor-components-using-blazor-syntax

namespace TrackPlanner.WebUI.Client.Shared
{
    public partial class Accordion 
    {
        [Parameter] 
        public RenderFragment Body { get; set; } = default!;
        [Parameter]
        public bool Collapsed { get; set; }
        [Parameter]
        public RenderFragment Header { get; set; } = default!;

        void Toggle()
        {
            Collapsed = !Collapsed;
        }

        public static MarkupString Foo(bool isCollapsed,MarkupString header)
        {
            var chevron = new MarkupString($"<i class='fas fa-chevron-{(isCollapsed?"down":"up")}' style='margin-right: 1rem; float:right' ></i>");
            return new MarkupString($@"
                <div style='  display: flex;align-items: center;'  @onclick='@Toggle'>
                    <div style='flex: 0 1 0;'>{header}</div>
                        <div style='flex: 1 1 0; '>
                        {chevron}
                    </div>
                </div>
            ");
        }
        
    }
}