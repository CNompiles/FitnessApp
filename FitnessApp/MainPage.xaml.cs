using System.Net.Http.Headers; //For setting HTTP headers
using System.Text;  //For encoding text to bytes (UTF-8)
using System.Text.Json; //For converting C# objects to/from JSON

namespace FitnessApp;

public partial class MainPage : ContentPage
{
    //Constants — values

    //API key from openrouter hosting
    private const string ApiKey = "MY_API_KEY_HERE";
    //The AI model we want to use for nutrition lookups
    private const string Model = "openai/gpt-oss-120b:free";
    //HttpClient is designed to be reused — creating a new one each time
    private readonly HttpClient _http = new();

    public MainPage()
    {
        InitializeComponent();
    }

    //Data Model — represents one food item

    public class FoodItem
    {
        //A simple data class (POCO) that holds nutrition values for one food
        //The AI will return JSON that maps directly onto this class

        public string Name { get; set; } = string.Empty;
        public double Calories { get; set; }
        public double Protein { get; set; }
        public double Carbs { get; set; }
        public double Fat { get; set; }
        public double Fiber { get; set; }
        public double Sugar { get; set; }
    }

    //Event Handler — triggered when the user taps "Search"

    private async void OnSearchClicked(object sender, EventArgs e)
    {
        var foodName = FoodEntry.Text?.Trim();

        if (string.IsNullOrWhiteSpace(foodName))
        {
            ResultLabel.Text = "Please Write Something";
            return;
        }

        //Show a loading indicator while we wait for the API
       
        ResultLabel.Text = "⏳ Search...";

        try
        {

            //Call the API and wait for a FoodItem object back and Format and display all nutrition values on screen

            var food = await GetFoodNutrition(foodName);

            ResultLabel.Text =
                $"🍽️ {food.Name} (100g)\n\n" +
                $"🔥 Calories: {food.Calories} kcal\n" +
                $"💪 Protein: {food.Protein}g\n" +
                $"🍞 Carbohydrates: {food.Carbs}g\n" +
                $"🧈 Fat: {food.Fat}g\n" +
                $"🌿 Vegetable fibers: {food.Fiber}g\n" +
                $"🍬 Sugar: {food.Sugar}g";
        }
        catch (Exception ex)
        {
            ResultLabel.Text = $"❌ Error: {ex.Message}";
        }
    }
    //Core API Method — talks to OpenRouter

    //Sends a request to the OpenRouter AI API and returns a FoodItem with nutrition data for the given food name

    private async Task<FoodItem> GetFoodNutrition(string foodName)
    {
        var prompt = $@"You are a nutrition database. 
Return ONLY a JSON object (no extra text) with nutrition per 100g for: {foodName}
Format:
{{
  ""name"": ""{foodName}"",
  ""calories"": 0,
  ""protein"": 0,
  ""carbs"": 0,
  ""fat"": 0,
  ""fiber"": 0,
  ""sugar"": 0
}}";

        //Build the request body

        //OpenRouter uses the same format as OpenAI's Chat API

        var requestBody = new
        {
            model = Model,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        //Serialize to JSON
        
        //Build the HTTP request

        var json = JsonSerializer.Serialize(requestBody);
        var request = new HttpRequestMessage(HttpMethod.Post,
            "https://openrouter.ai/api/v1/chat/completions");

        //Attach our API key in the Authorization header

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(request);
        var responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"API Error {response.StatusCode}: {responseJson}");
        }

        //Parse the outer API response

        //The API wraps the AI's reply inside a JSON structure

        using var doc = JsonDocument.Parse(responseJson);

        if (doc.RootElement.TryGetProperty("error", out var errorProp))
        {
            throw new Exception($"API: {errorProp.GetProperty("message").GetString()}");
        }

        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";

        //Clean up markdown fences if present, Some models wrap their JSON in ```json ... ```
        content = content.Trim();
        if (content.StartsWith("```"))
        {
            content = content.Split('\n', 2)[1];
            content = content[..content.LastIndexOf("```")];
        }

        //Deserialize into a FoodItem object Convert the JSON string the AI returned into a real C# FoodItem

        var food = JsonSerializer.Deserialize<FoodItem>(content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return food ?? throw new Exception("No data found");
    }

    //Menu Logic

    //Shared helper that animates the slide-in menu closed

    private async Task CloseMenu()
    {
        //Run both animations simultaneously using Task.WhenAll await Task.WhenAll

        await Task.WhenAll(
            Overlay.FadeTo(0, 300),
            BottomSheet.TranslateTo(200, 0, 300, Easing.CubicIn));
        Overlay.IsVisible = false;
        BottomSheet.IsVisible = false;
    }

    //Opens the slide-in menu when the ☰ button is tapped

    private async void OnMenuClicked(object sender, EventArgs e)
    {
        BottomSheet.IsVisible = true;
        Overlay.IsVisible = true;
        await Task.WhenAll(
            Overlay.FadeTo(0.5, 300),
            BottomSheet.TranslateTo(0, 0, 300, Easing.CubicOut));
    }

    //Closes the menu when the user taps the dark overlay behind it

    private async void OnOverlayTapped(object sender, EventArgs e)
    {
        await Task.WhenAll(
            Overlay.FadeTo(0, 300),
            BottomSheet.TranslateTo(0, 400, 300, Easing.CubicIn));
        Overlay.IsVisible = false;
        BottomSheet.IsVisible = false;
    }

    //Handles the Profile menu button tap

    private async void OnProfileClicked(object sender, EventArgs e)
    {
        await CloseMenu();
        await DisplayAlert("Profile", "Profile Page - Coming Soon!", "OK");
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        await CloseMenu();
        await DisplayAlert("Settings", "Settings Page - Coming Soon!", "OK");
    }

    private async void OnStatsClicked(object sender, EventArgs e)
    {
        await CloseMenu();
        await DisplayAlert("Statistics", "Statistics Page - Coming Soon!", "OK");
    }

    //Handles the Logout button. Asks for confirmation, then returns to SplashPage

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        //Effectively "resetting" the navigation stack

        await CloseMenu();
        bool confirm = await DisplayAlert("Exit", "Do you want to log out?", "Yes", "No");
        if (confirm)
            Application.Current!.Windows[0].Page = new SplashPage();
    }
}