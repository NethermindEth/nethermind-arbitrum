// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Autofac;
using Autofac.Core;

namespace Nethermind.Arbitrum;

public static class ContainerBuilderExtensions
{
    /// <summary>
    /// Create a registration for T with access to a factory function for its exact previous registration. Useful as
    /// decorator, but need to optionally instantiate previous configuration or instantiate multiple of previous configuration.
    /// </summary>
    public static ContainerBuilder AddWithAccessToPreviousRegistration<T>(this ContainerBuilder builder, Func<IComponentContext, Func<IComponentContext, T>, T> decoratorFunc) where T : class
    {
        Guid regId = Guid.NewGuid();
        Service thisService = new TypedService(typeof(T));
        const string metadataName = "registrationId";

        builder
            .Register(ctx =>
            {
                IComponentRegistration? registrationBeforeThis = null;
                bool wasFound = false;
                foreach (IComponentRegistration componentRegistration in ctx.ComponentRegistry.RegistrationsFor(thisService))
                {
                    if (wasFound)
                    {
                        registrationBeforeThis = componentRegistration;
                        break;
                    }

                    if (componentRegistration.Metadata.TryGetValue(metadataName, out var value) &&
                        value is Guid guidValue && guidValue == regId)
                    {
                        wasFound = true;
                    }
                }

                if (!wasFound)
                    throw new InvalidOperationException("Missing current registration");
                if (registrationBeforeThis is null)
                    throw new InvalidOperationException("Missing previous registration");

                Func<IComponentContext, T> prevFactory = innerCtx => (T)innerCtx.ResolveComponent(
                    new ResolveRequest(
                        thisService,
                        new ServiceRegistration(registrationBeforeThis.ResolvePipeline, registrationBeforeThis),
                        []));

                return decoratorFunc(ctx, prevFactory);
            })
            .As(thisService)
            .WithMetadata(metadataName, regId);

        return builder;
    }
}
