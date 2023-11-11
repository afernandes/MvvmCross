namespace MvvmCross.Maui;

internal sealed class MvxWindowManager : IWindowCreator //, IWindowManager
{
    private IApplication _application { get; }

    public MvxWindowManager(IApplication application)
    {
        _application = application;
    }

    private Window _initialWindow;

    public IReadOnlyList<Window> Windows => _application.Windows.OfType<Window>().ToList();

    public Window CreateWindow(Application app, IActivationState activationState)
    {
        if (_initialWindow is not null)
            return _initialWindow;
        //else if (app.Windows.OfType<IMvxWindowsView>().Any())
        //return _initialWindow = app.Windows.OfType<IMvxWindowsView>().First();

        //activationState.Context.Services.GetRequiredService<MvxHostBuilder>().OnAppStarted();

        return _initialWindow ?? throw new InvalidNavigationException("Expected Navigation Failed. No Root Window has been created.");
    }

    public void OpenWindow(Window window)
    {
        if (_initialWindow is null)
            _initialWindow = window;
        else
            _application.OpenWindow(window);
    }

    public void CloseWindow(Window window)
    {
        if (_initialWindow == window)
            _initialWindow = null;

        _application.CloseWindow(window);
    }
}
