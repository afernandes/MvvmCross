using MvvmCross.Plugin;
using MvvmCross.ViewModels;

namespace Playground.Maui
{
    public partial class App : Application, IMvxApplication
    {
        public App()
        {
            InitializeComponent();

            MainPage = new AppShell();
        }

        public IMvxViewModelLocator FindViewModelLocator(MvxViewModelRequest request)
        {
            throw new NotImplementedException();
        }

        public void Initialize()
        {
            throw new NotImplementedException();
        }

        public void LoadPlugins(IMvxPluginManager pluginManager)
        {
            throw new NotImplementedException();
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        public Task Startup()
        {
            throw new NotImplementedException();
        }
    }
}
