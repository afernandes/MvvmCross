namespace MvvmCross.Hosting
{
    public static class MvxHostExtensions
    {
        public static MauiAppBuilder UseMvvmCross(
            this MauiAppBuilder builder,
            IServiceCollection serviceCollection,
            Action<MvxHostBuilder> configureMvvmCross)
        {
            var mvxBuilder = new MvxHostBuilder(serviceCollection);
            configureMvvmCross(mvxBuilder);

            return builder;
        }
    }
}
