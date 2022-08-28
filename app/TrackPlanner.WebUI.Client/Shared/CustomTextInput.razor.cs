using System.Diagnostics.CodeAnalysis;

namespace TrackPlanner.WebUI.Client.Shared
{
    public partial class CustomTextInput
    {
        protected override bool TryParseValueFromString(string? value, [MaybeNullWhen(false)]  out string result, [NotNullWhen(false)] out string? validationErrorMessage)
        {
            result = $"{value}";
            validationErrorMessage = null;
            return true;
        }
    
    }
}