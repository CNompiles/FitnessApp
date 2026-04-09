using System.Net.Http.Json; // For future API calls

namespace FitnessApp;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent(); // Loading XAML
	}

    // Class For the food Data
    public class FoodItem
    {
        public string Name { get; set; } = string.Empty;
        public double Calories { get; set; }
        public double Protein { get; set; }
        public double Carbs { get; set; }
        public double Fat { get; set; }
    }

    // Search Food 
    private async void OnSearchClicked(object sender, EventArgs e)
    {
        var foodName = FoodEntry.Text;

        // check if he has written anything
        if (string.IsNullOrWhiteSpace(foodName))
        {
            ResultLabel.Text = "Παρακαλώ γράψτε κάτι";
            return;
        }

        ResultLabel.Text = "Αναζήτηση...";

        try 
        {
            await Task.Delay(1000); // search simulation (replaced by API)

            // mock result - fixed values ??to the present
            var mockResult = new FoodItem 
            { 
                Name = foodName, 
                Calories = 150, 
                Protein = 5 
            };

            ResultLabel.Text = $"{mockResult.Name}: {mockResult.Calories} �������� ��� {mockResult.Protein}g ���������.";
        }
        catch (Exception)
        {
            ResultLabel.Text = "Σφάλμα κατά την αναζήτηση.";
        }
    }
    // Method for helping close
    private async Task CloseMenu()
    {
        await Task.WhenAll(
            Overlay.FadeTo(0, 300),
            BottomSheet.TranslateTo(0, 400, 300, Easing.CubicIn)
        );
        Overlay.IsVisible = false;
        BottomSheet.IsVisible = false;
    }

    // MENU - OPEN
    private async void OnMenuClicked(object sender, EventArgs e)
    {
        BottomSheet.IsVisible = true;  // show menu
        Overlay.IsVisible = true;      // show dark background
        await Task.WhenAll(
            Overlay.FadeTo(0.5, 300),                           // The background gradually darkened.
            BottomSheet.TranslateTo(0, 0, 300, Easing.CubicOut) // upload the menu with animation
        );
    }

    // MENU - CLOSE (When you click outside the menu)
    private async void OnOverlayTapped(object sender, EventArgs e)
    {
        await Task.WhenAll(
            Overlay.FadeTo(0, 300),  // the background faded
            BottomSheet.TranslateTo(0, 400, 300, Easing.CubicIn) // download the menu with animation
        );
        Overlay.IsVisible = false;     // Hide Fonto
        BottomSheet.IsVisible = false; // Hide Menu
    }

    // MENU - SELECT PROFILE
    private async void OnProfileClicked(object sender, EventArgs e)
    {
        await CloseMenu(); // close the menu first
        // TODO: replace with: await Navigation.PushAsync(new ProfilePage());
        await DisplayAlert("Προφίλ", "Σελίδα Προφίλ - Σύντομα!", "OK");
    }

    // MENU - SELECT SETTINGS
    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        await CloseMenu(); // close the menu first
        // TODO: replace with: await Navigation.PushAsync(new SettingsPage());
        await DisplayAlert("Ρυθμίσεις", "Σελίδα Ρυθμίσεων - Σύντομα!", "OK");
    }

    // MENU - SELECT STATISTIC
    private async void OnStatsClicked(object sender, EventArgs e)
    {
        await CloseMenu(); // close the menu first
        // TODO: replace with: await Navigation.PushAsync(new StatsPage());
        await DisplayAlert("Στατιστικά", "Σελίδα Στατιστικών - Σύντομα!", "OK");
    }

    // MENU - EXIT
    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        await CloseMenu(); // close the menu first

        // Ask the user if they are sure
        bool confirm = await DisplayAlert("Έξοδος", "Θέλεις να αποσυνδεθείς;", "Ναι", "Όχι");
        
        if (confirm)
            Application.Current!.Windows[0].Page = new SplashPage(); // Return Main page
    }
}

