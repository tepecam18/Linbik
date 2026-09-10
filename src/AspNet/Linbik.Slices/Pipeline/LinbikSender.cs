using System.Collections.Concurrent;
using Linbik.Core.Identity;
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

    /// <inheritdoc />
    public ValueTask<Result<TResponse>> Send<TResponse>(
        ILinbikRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var wrapper = RequestWrapper<TResponse>.Cache.GetOrAdd(
            request.GetType(),
            static requestType =>
            {
                var wrapperType = typeof(RequestWrapperImpl<,>).MakeGenericType(requestType, typeof(TResponse));
                return (RequestWrapper<TResponse>)Activator.CreateInstance(wrapperType)!;
            });

        return wrapper.Handle(request, serviceProvider, cancellationToken);
    }

    private abstract class RequestWrapper<TResponse>
    {
        internal static readonly ConcurrentDictionary<Type, RequestWrapper<TResponse>> Cache = new();

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
                var actor = serviceProvider.GetRequiredService<LActor>();
                var validation = await validator.ValidateAsync(typedRequest, actor, cancellationToken);
                if (!validation.IsValid)
                {
                    var message = string.Join(" ", validation.Errors.Select(e => e.Message));
                    return Result<TResponse>.Fail(LError.Validation(message, validation.Errors));
                }
            }

            var handler = serviceProvider.GetRequiredService<ILinbikHandler<TRequest, TResponse>>();
            return await handler.HandleAsync(typedRequest, cancellationToken);
        }
    }
}
