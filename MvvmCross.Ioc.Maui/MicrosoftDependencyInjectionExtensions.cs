using System.Collections;
using System.ComponentModel.Design;
using Microsoft.Maui.Controls;
using MvvmCross.IoC;

namespace MvvmCross.Ioc.Maui
{
    public static class MicrosoftDependencyInjectionExtensions
    {
        /// <summary>
        /// Creates an <see cref="IServiceProvider"/> based on the given <paramref name="serviceCollection"/>.
        /// </summary>
        /// <param name="container">The target <see cref="IMvxIoCProvider"/>.</param>
        /// <param name="serviceCollection">The <see cref="IServiceCollection"/> that contains information about the services to be registered.</param>
        /// <returns>A configured <see cref="IServiceProvider"/>.</returns>
        public static IServiceProvider CreateServiceProvider(this IMvxIoCProvider container, IServiceCollection serviceCollection)
        {
            //var rootScope = container.CreateChildContainer();
            //rootScope.Completed += (a, s) => container.Dispose();
            container.RegisterType(typeof(IServiceProvider), () => new MvxServiceProvider(container));
            //container.RegisterSingleton<IServiceScopeFactory>(f => new MvxServiceScopeFactory(container));
            //container.RegisterSingleton<IServiceProviderIsService>(factory => new MvxIsServiceProviderIsService(serviceType => container.CanGetInstance(serviceType, string.Empty)));
            RegisterServices(container, serviceCollection);
            return new MvxServiceProvider(container);
        }

        private static void RegisterServices(IMvxIoCProvider container, IServiceCollection serviceCollection)
        {
            for (int i = 0; i < serviceCollection.Count; i++)
            {
                var serviceDescriptor = serviceCollection[i];

                if (serviceDescriptor.Lifetime == ServiceLifetime.Transient)
                {
                    if (serviceDescriptor.ImplementationType != null)
                        container.RegisterType(serviceDescriptor.ServiceType, serviceDescriptor.ImplementationType);
                    else if (serviceDescriptor.ImplementationFactory != null)
                        container.RegisterType(serviceDescriptor.ServiceType, () => ServiceFactory(container, serviceDescriptor.ImplementationFactory));
                }
                else if (serviceDescriptor.Lifetime == ServiceLifetime.Singleton)
                {
                    if (serviceDescriptor.ImplementationType != null)
                        container.RegisterSingleton(serviceDescriptor.ServiceType,() => container.IoCConstruct(serviceDescriptor.ImplementationType));
                    else if (serviceDescriptor.ImplementationInstance != null)
                        container.RegisterSingleton(serviceDescriptor.ServiceType, serviceDescriptor.ImplementationInstance);
                    else if (serviceDescriptor.ImplementationFactory != null)
                        container.RegisterSingleton(serviceDescriptor.ServiceType, () => ServiceFactory(container, serviceDescriptor.ImplementationFactory));
                }
                else if (serviceDescriptor.Lifetime == ServiceLifetime.Scoped)
                {
                    //TODO: Scoped
                    if (serviceDescriptor.ImplementationType != null)
                        container.RegisterSingleton(serviceDescriptor.ServiceType, container.IoCConstruct(serviceDescriptor.ImplementationType));
                    else if (serviceDescriptor.ImplementationType is null)
                        container.RegisterSingleton(serviceDescriptor.ServiceType, () => ServiceFactory(container, serviceDescriptor.ImplementationFactory));
                    else if (serviceDescriptor.ServiceType.IsAbstract)
                        throw new NotSupportedException($"Cannot register the service {serviceDescriptor.ServiceType.FullName} as it is an abstract type");
                    else if (serviceDescriptor.ServiceType.IsInterface)
                        throw new NotSupportedException($"Cannot register the service {serviceDescriptor.ServiceType.FullName} as it is an interface. You must provide a concrete implementation");
                    else
                        container.RegisterSingleton(serviceDescriptor.ServiceType);
                }
            }
        }

        private static object ServiceFactory(IMvxIoCProvider container, Func<IServiceProvider, object> implementationFactory)
        {
            var sp = container.Resolve<IServiceProvider>();
            return implementationFactory(sp);
        }


        //public static void Populate(this IMvxIoCProvider container, IServiceCollection services)
        //{
        //    if (!(container is IServiceCollectionAware serviceCollectionAware))
        //        throw new InvalidOperationException("The instance of IContainerExtension does not implement IServiceCollectionAware");
        //    serviceCollectionAware.Populate(services);
        //}

        //public static IServiceProvider CreateServiceProvider(this IContainerExtension container) => container is IServiceCollectionAware serviceCollectionAware ? serviceCollectionAware.CreateServiceProvider() : throw new InvalidOperationException("The instance of IContainerExtension does not implement IServiceCollectionAware");
    }
}
