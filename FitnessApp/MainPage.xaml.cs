using FitnessApp.Models;
using System.Net.Http.Json;

namespace FitnessApp;

public partial class MainPage : ContentPage
{
	

	public MainPage()
	{
		InitializeComponent();
	}

    public class FoodItem
    {
        public string Name { get; set; } = string.Empty;
        public double Calories { get; set; }
        public double Protein { get; set; }
        public double Carbs { get; set; }
        public double Fat { get; set; }
    }
    private async void OnSearchClicked(object sender, EventArgs e)
    {
        var foodName = FoodEntry.Text;
        if (string.IsNullOrWhiteSpace(foodName))
        {
            ResultLabel.Text = "Παρακαλώ γράψτε κάτι!";
            return;
        }

        ResultLabel.Text = "Αναζήτηση...";

        try 
        {
            await Task.Delay(1000); 
        
            var mockResult = new FoodItem 
            { 
                Name = foodName, 
                Calories = 150, 
                Protein = 5 
            };

            ResultLabel.Text = $"{mockResult.Name}: {mockResult.Calories} θερμίδες και {mockResult.Protein}g πρωτεΐνης.";
        }
        catch (Exception ex)
        {
            ResultLabel.Text = "Σφάλμα κατά την αναζήτηση.";
        }
    }
}
