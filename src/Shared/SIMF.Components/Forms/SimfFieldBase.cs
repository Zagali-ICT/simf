using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace SIMF.Components.Forms;

/// <summary>
/// Shared base for the Simf* form-field components (text, password, code). It
/// binds the field to the cascaded <see cref="EditContext"/> so the validation
/// message the component renders comes from the same validation pass the form
/// runs — the component styles the message, it does not own the rule.
/// </summary>
public abstract class SimfFieldBase : ComponentBase, IDisposable
{
    private EditContext? _subscribedContext;
    private FieldIdentifier _fieldIdentifier;

    /// <summary>A stable DOM id, so the label and the message can reference the input.</summary>
    protected string FieldElementId { get; } = $"simf-field-{Guid.NewGuid():N}";

    protected string ErrorElementId => $"{FieldElementId}-error";
    protected string HelperElementId => $"{FieldElementId}-helper";

    [CascadingParameter] protected EditContext? EditContext { get; set; }

    /// <summary>The field label shown above the control.</summary>
    [Parameter, EditorRequired] public string Label { get; set; } = string.Empty;

    /// <summary>The bound value.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Raised when the bound value changes.</summary>
    [Parameter] public EventCallback<string> ValueChanged { get; set; }

    /// <summary>The expression identifying the bound value — supplied by <c>@bind-Value</c>.</summary>
    [Parameter] public Expression<Func<string>>? ValueExpression { get; set; }

    /// <summary>Disables the control — used while a form is submitting.</summary>
    [Parameter] public bool Disabled { get; set; }

    protected override void OnParametersSet()
    {
        if (ValueExpression is not null)
        {
            _fieldIdentifier = FieldIdentifier.Create(ValueExpression);
        }

        if (!ReferenceEquals(EditContext, _subscribedContext))
        {
            Unsubscribe();
            if (EditContext is not null)
            {
                EditContext.OnValidationStateChanged += HandleValidationStateChanged;
            }
            _subscribedContext = EditContext;
        }
    }

    /// <summary>The first validation message for this field, or <c>null</c> when valid.</summary>
    protected string? ErrorMessage =>
        EditContext is not null && ValueExpression is not null
            ? EditContext.GetValidationMessages(_fieldIdentifier).FirstOrDefault()
            : null;

    /// <summary>True when the field currently carries a validation message.</summary>
    protected bool HasError => ErrorMessage is not null;

    private void HandleValidationStateChanged(object? sender, ValidationStateChangedEventArgs e) =>
        StateHasChanged();

    private void Unsubscribe()
    {
        if (_subscribedContext is not null)
        {
            _subscribedContext.OnValidationStateChanged -= HandleValidationStateChanged;
        }
    }

    public void Dispose() => Unsubscribe();
}
