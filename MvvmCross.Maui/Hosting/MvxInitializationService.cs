using MvvmCross.Hosting;

namespace MvvmCross.Maui.Hosting
{
    internal class MvxInitializationService : IMauiInitializeService
    {
        /// <summary>
        /// Initializes the modules.
        /// </summary>
        public void Initialize(IServiceProvider services)
        {
            var builder = services.GetRequiredService<MvxHostBuilder>();
            builder.OnInitialized();
        }
    }

}
