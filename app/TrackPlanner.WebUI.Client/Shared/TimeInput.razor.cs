using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using TrackPlanner.Data;

namespace TrackPlanner.WebUI.Client.Shared
{
    public partial class TimeInput : CustomInputBase<TimeSpan>
    {
        public static string DefaultCssStyle => "padding-left: 0.5em;padding-right: 0.5em;";
        
        public TimeInput()
        {
            CssStyle = DefaultCssStyle;
        }
        protected override bool TryParseValueFromString(string? value,[NotNullWhen(false)] out TimeSpan result,[NotNullWhen(false)] out string? validationErrorMessage)
        {
            if (TimeSpan.TryParse(value, out result))
            {
                validationErrorMessage = "";
                return true;
            }
            else
            {
                validationErrorMessage = "Invalid time";
                return false;
            }
        }

        protected override string? FormatValueAsString(TimeSpan value)
        {
            return TrackPlanner.Data.DataFormat.Format(value);
        }

    }
}