namespace FitnessApp;

public partial class SplashPage : ContentPage
{
    public SplashPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Task.Delay(3000); // 3 sec
        Application.Current!.Windows[0].Page = new AppShell();
    }
}