using System.Reflection;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CarRental.BuildingBlocks.Validation;

public sealed class ValidationEndpointFilter<TRequest> : IEndpointFilter where TRequest : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var validator = context.HttpContext.RequestServices.GetService<IValidator<TRequest>>();
        if (validator is null)
            return await next(context);

        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
        if (request is null)
        {
            return Microsoft.AspNetCore.Http.Results.Problem(
                title: "Validation Error",
                detail: "Request body is required.",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?> { ["errorCode"] = "validation.null_request" });
        }

        var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
        if (result.IsValid)
            return await next(context);

        var first = result.Errors[0];
        return Microsoft.AspNetCore.Http.Results.Problem(
            title: "Validation Error",
            detail: first.ErrorMessage,
            statusCode: StatusCodes.Status400BadRequest,
            extensions: new Dictionary<string, object?>
            {
                ["errorCode"] = string.IsNullOrWhiteSpace(first.ErrorCode) ? "validation.failed" : first.ErrorCode,
                ["errors"] = result.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            });
    }
}

public static class ValidationRouteHandlerBuilderExtensions
{
    public static RouteHandlerBuilder WithValidation<TRequest>(this RouteHandlerBuilder builder)
        where TRequest : class =>
        builder.AddEndpointFilter<ValidationEndpointFilter<TRequest>>();
}

public static class ValidationServiceCollectionExtensions
{
    public static IServiceCollection AddValidatorsFromAssembly(this IServiceCollection services, Assembly assembly)
    {
        foreach (var scanResult in AssemblyScanner.FindValidatorsInAssembly(assembly, includeInternalTypes: true))
        {
            services.AddScoped(scanResult.InterfaceType, scanResult.ValidatorType);
        }

        return services;
    }
}
