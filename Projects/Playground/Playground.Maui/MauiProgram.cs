//using Microsoft.Extensions.Logging;

using MvvmCross.Hosting;

namespace Playground.Maui
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMvvmCross(null, hostBuilder =>
                {
                    
                })
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

//#if DEBUG
//    		builder.Logging.AddDebug();
//#endif

            return builder.Build();
        }
    }
}
