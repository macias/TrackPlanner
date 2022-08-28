using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace TrackPlanner.WebUI.Client.Shared
{
    // this widget is a must-have for now, because classic InputBase requires EditForm, and EditForm
    // swallows any rendering from functions: https://github.com/dotnet/razor-compiler/issues/250
    // using explicit CascadeValue and using regular InputBase gives error
    // "Unhandled exception rendering component: i is undefined"
    public abstract class CustomInputBase<TValue> : ComponentBase
    {
        
        private ValidationMessageStore? _parsingValidationMessages;
        private Type? _nullableUnderlyingType;
        private bool _previousParsingAttemptFailed;

      [Parameter]  public EditContext EditContext { get; set; } = default!;

        [Parameter]
        public TValue? Value { get; set; }

        [Parameter] public string CssStyle { get; set; } = "";

        [Parameter] public EventCallback<TValue> ValueChanged { get; set; }
        
        private FieldIdentifier FieldIdentifier { get; set; }
        [Parameter] public Expression<Func<TValue>>? ValueExpression { get; set; }
        
        protected TValue? CurrentValue
        {
            get => Value;
            set
            {
                var hasChanged = !EqualityComparer<TValue>.Default.Equals(value, Value);
                if (hasChanged)
                {
                    Value = value;
                    _ = ValueChanged.InvokeAsync(Value);
                    EditContext.NotifyFieldChanged(FieldIdentifier);
                }
            }
        }

        protected string? CurrentValueAsString
        {
            get => FormatValueAsString(CurrentValue);
            set
            {
                _parsingValidationMessages?.Clear();

                bool parsingFailed;

                if (_nullableUnderlyingType != null && string.IsNullOrEmpty(value))
                {
                    // Assume if it's a nullable type, null/empty inputs should correspond to default(T)
                    // Then all subclasses get nullable support almost automatically (they just have to
                    // not reject Nullable<T> based on the type itself).
                    parsingFailed = false;
                    CurrentValue = default!;
                }
                else if (TryParseValueFromString(value, out var parsedValue, out var validationErrorMessage))
                {
                    parsingFailed = false;
                    CurrentValue = parsedValue!;
                }
                else
                {
                    parsingFailed = true;

                    if (_parsingValidationMessages == null)
                    {
                        _parsingValidationMessages = new ValidationMessageStore(EditContext);
                    }

                    _parsingValidationMessages.Add(FieldIdentifier, validationErrorMessage);

                    // Since we're not writing to CurrentValue, we'll need to notify about modification from here
                    EditContext.NotifyFieldChanged(FieldIdentifier);
                }

                // We can skip the validation notification if we were previously valid and still are
                if (parsingFailed || _previousParsingAttemptFailed)
                {
                    EditContext.NotifyValidationStateChanged();
                    _previousParsingAttemptFailed = parsingFailed;
                }
            }
        }
        
        public override Task SetParametersAsync(ParameterView parameters)
        {
            parameters.SetParameterProperties(this);

            if (EditContext == null)
                throw new ArgumentNullException(nameof(EditContext));
            if (ValueExpression == null)
                throw new ArgumentNullException(nameof(ValueExpression));

            FieldIdentifier = FieldIdentifier.Create(ValueExpression);
            _nullableUnderlyingType = Nullable.GetUnderlyingType(typeof(TValue));

            return base.SetParametersAsync(ParameterView.Empty);
        }

        protected abstract bool TryParseValueFromString(string? value, [MaybeNullWhen(false)] out TValue result, [NotNullWhen(false)] out string? validationErrorMessage);

        protected virtual string? FormatValueAsString(TValue? value)
            => value?.ToString();

    }
}