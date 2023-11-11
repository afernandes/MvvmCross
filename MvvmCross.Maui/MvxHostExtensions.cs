using MvvmCross.IoC;

namespace MvvmCross.Hosting
{
    public static class MvxHostExtensions
    {
        public static MauiAppBuilder UseMvvmCross(
            this MauiAppBuilder builder,
            IMvxIoCProvider serviceCollection,
            Action<MvxHostBuilder> configureMvvmCross)
        {
            var mvxBuilder = new MvxHostBuilder(builder, serviceCollection);
            configureMvvmCross(mvxBuilder);

            return builder;
        }
    }
}
