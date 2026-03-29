namespace FitnessApp;

public partial class MainPage : ContentPage
{
	

	public MainPage()
	{
		InitializeComponent();
	}

	private void OnSearchClicked(object sender, EventArgs e)
{
    var foodName = FoodEntry.Text;
    if (string.IsNullOrWhiteSpace(foodName))
    {
        ResultLabel.Text = "Παρακαλώ γράψτε κάτι!";
        return;
    }

    // Για τώρα κάνουμε ένα απλό μήνυμα. 
    // Εδώ ο συνεργάτης σου θα βάλει τη σύνδεση με τη βάση δεδομένων.
    ResultLabel.Text = $"Έφαγες {foodName}. Μπράβο!";
}
}
