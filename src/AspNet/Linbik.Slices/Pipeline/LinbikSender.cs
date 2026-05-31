using System.Collections.Concurrent;
using Linbik.Slices.Results;
using Microsoft.Extensions.DependencyInjection;

namespace Linbik.Slices.Pipeline;

/// <summary>
/// <see cref="ILinbikSender"/>'ın varsayılan uygulaması. İstek tipine göre
/// (validator → handler) pipeline'ını çözer ve çalıştırır. Tip başına oluşturulan
/// wrapper'lar önbelleğe alınır; böylece reflection maliyeti yalnızca ilk çağrıda oluşur.
/// </summary>
public sealed class LinbikSender(IServiceProvider serviceProvider) : ILinbikSender
{
    private static readonly ConcurrentDictionary<Type, object> Wrappers = new();

    /// <inheritdoc />
    public ValueTask<Result<TResponse>> Send<TResponse>(
        ILinbikRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var wrapper = (RequestWrapper<TResponse>)Wrappers.GetOrAdd(
            request.GetType(),
            static (requestType, responseType) =>
            {
                var wrapperType = typeof(RequestWrapperImpl<,>).MakeGenericType(requestType, responseType);
                return Activator.CreateInstance(wrapperType)!;
            },
            typeof(TResponse));

        return wrapper.Handle(request, serviceProvider, cancellationToken);
    }

    private abstract class RequestWrapper<TResponse>
    {
        public abstract ValueTask<Result<TResponse>> Handle(
            object request, IServiceProvider serviceProvider, CancellationToken cancellationToken);
    }

    private sealed class RequestWrapperImpl<TRequest, TResponse> : RequestWrapper<TResponse>
        where TRequest : ILinbikRequest<TResponse>
    {
        public override async ValueTask<Result<TResponse>> Handle(
            object request, IServiceProvider serviceProvider, CancellationToken cancellationToken)
        {
            var typedRequest = (TRequest)request;

            var validator = serviceProvider.GetService<ILinbikValidator<TRequest>>();
            if (validator is not null)
            {
                var validation = await validator.ValidateAsync(typedRequest, cancellationToken);
                if (!validation.IsValid)
                {
                    return Result<TResponse>.Fail(
                        LError.Validation("One or more validation errors occurred.", validation.Errors));
                }
            }

            var handler = serviceProvider.GetRequiredService<ILinbikHandler<TRequest, TResponse>>();
            return await handler.HandleAsync(typedRequest, cancellationToken);
        }
    }
}
