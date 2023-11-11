#nullable enable
using Microsoft.Extensions.DependencyInjection;
using MvvmCross.IoC;

namespace MvvmCross.Hosting;

public interface IMvxHostBuilder
{
    Func<IMvxIoCProvider, IServiceProvider>? ConfigureContainer { get; }
    IMvxIoCProvider Container { get; }

    IMvxHost Build();
}
