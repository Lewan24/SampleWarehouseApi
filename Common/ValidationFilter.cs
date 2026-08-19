using FluentValidation;

namespace WarehouseApi.Common;

/// <summary>
/// Minimal-API endpoint filter that validates the bound request DTO with FluentValidation
/// before the handler runs. Attach with `.AddEndpointFilter&lt;ValidationFilter&lt;TRequest&gt;&gt;()`.
///
/// This is the modern replacement for MVC's automatic [ApiController] model validation,
/// which Minimal APIs don't provide out of the box. Centralizing it here means every
/// write endpoint gets consistent 400 responses with field-level error details, and
/// handlers never need to remember to call the validator themselves (fail-safe by default).
/// </summary>
public class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var argument = context.Arguments.OfType<T>().FirstOrDefault();
        if (argument is null)
        {
            return Results.BadRequest(new { error = $"A valid {typeof(T).Name} request body is required." });
        }

        var validator = context.HttpContext.RequestServices.GetService<IValidator<T>>();
        if (validator is null)
        {
            // No validator registered for this type — nothing to enforce, let it through.
            return await next(context);
        }

        var validationResult = await validator.ValidateAsync(argument);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        return await next(context);
    }
}
