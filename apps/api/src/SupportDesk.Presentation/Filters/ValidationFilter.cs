using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using ApplicationValidationException = SupportDesk.Application.Exceptions.ValidationException;

namespace SupportDesk.Presentation.Filters;

/// <summary>
/// Runs the FluentValidation validator registered for any action argument, before the action
/// itself runs. Failures become a 400 problem document via
/// <see cref="Middleware.ExceptionHandlingMiddleware"/>.
/// </summary>
public sealed class ValidationFilter(IServiceProvider services) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());

            if (services.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var result = await validator.ValidateAsync(
                new ValidationContext<object>(argument),
                context.HttpContext.RequestAborted);

            if (result.IsValid)
            {
                continue;
            }

            throw new ApplicationValidationException(
                result.Errors
                    .GroupBy(failure => failure.PropertyName)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(failure => failure.ErrorMessage).ToArray()));
        }

        await next();
    }
}
