using System.Net.Http.Headers; //For setting HTTP headers
using System.Text;  //For encoding text to bytes (UTF-8)
using System.Text.Json; //For converting C# objects to/from JSON
using System.Text.RegularExpressions;

namespace FitnessApp;

public partial class MainPage : ContentPage
{
    //Constants — values

    //API key loaded at runtime — never hardcoded.

    private static readonly string ApiKey = LoadApiKey();
    private static string LoadApiKey()
    {
        //Windows reads from environment variable

        var envKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
        if (!string.IsNullOrEmpty(envKey)) return envKey;
        
        //Android reads from RuntimeHostConfigurationOption 
        
        var configKey = AppContext.GetData("OPENROUTER_API_KEY") as string;
        if (!string.IsNullOrEmpty(configKey)) return configKey;

        return string.Empty;
    }

    //The AI model we want to use for nutrition lookups

    private const string Model = "openai/gpt-oss-120b:free";

    //HttpClient is designed to be reused — creating a new one each time

    private readonly HttpClient _http = new();

    //Data Model
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
        public string Source { get; set; } = string.Empty;
    }

    public MainPage()
    {
        InitializeComponent();
    }

    //Extracts the food name and quantity from a single free-text input.
    private static (string foodName, double grams) ParseInput(string input)
    {
        var match = Regex.Match(input.Trim(),
            @"(\d+(?:[.,]\d+)?)\s*(g|gr|γρ|ml|kg)?",
            RegexOptions.IgnoreCase);

        if (!match.Success)
            return (input.Trim(), 100);

        var raw = match.Value;
        var numberStr = match.Groups[1].Value.Replace(',', '.');
        var unit = match.Groups[2].Value.ToLower();

        if (!double.TryParse(numberStr,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out var amount))
            return (input.Trim(), 100);

        var grams = unit switch
        {
            "kg" => amount * 1000,
            _ => amount
        };

        var foodName = input.Replace(raw, "").Trim().Trim(',', '-', '·').Trim();
        if (string.IsNullOrWhiteSpace(foodName))
            foodName = input.Trim();

        return (foodName, grams);
    }

    //Method multiplies all nutrition values by (grams / 100) the result matches the portion the user actually asked about.

    private static FoodItem ScaleTo(FoodItem per100g, double grams)
    {
        if (Math.Abs(grams - 100) < 0.01) return per100g;

        var factor = grams / 100.0;
        return new FoodItem
        {
            Name = per100g.Name,
            Calories = Math.Round(per100g.Calories * factor, 1),
            Protein = Math.Round(per100g.Protein * factor, 1),
            Carbs = Math.Round(per100g.Carbs * factor, 1),
            Fat = Math.Round(per100g.Fat * factor, 1),
            Fiber = Math.Round(per100g.Fiber * factor, 1),
            Sugar = Math.Round(per100g.Sugar * factor, 1),
            Source = per100g.Source
        };
    }

    //Search Handler

    private async void OnSearchClicked(object sender, EventArgs e)
    {
        var rawInput = FoodEntry.Text?.Trim();

        if (string.IsNullOrWhiteSpace(rawInput))
        {
            ResultLabel.Text = "Γράψε κάτι για να ψάξεις!";
            return;
        }

        var (foodName, grams) = ParseInput(rawInput);
        var displayGrams = grams % 1 == 0 ? $"{(int)grams}" : $"{grams}";

        ResultLabel.Text = "⏳ Αναζήτηση...";

        try
        {
            var per100g = await GetFoodNutrition(foodName);
            var scaled = ScaleTo(per100g, grams);

            //Call the API and wait for a FoodItem object back and Format and display all nutrition values on screen

            ResultLabel.Text =
                $"🍽️ {scaled.Name} ({displayGrams}g)" +
                $"🔥 Calories: {scaled.Calories} kcal" +
                $"💪 Protein: {scaled.Protein}" +
                $"🍞 Carbohydrates: {scaled.Carbs}" +
                $"🧈 Fat: {scaled.Fat}" +
                $"🌿 Fiber: {scaled.Fiber}" +
                $"🍬 Sugar: {scaled.Sugar}g";
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
        var offResult = await TryOpenFoodFacts(foodName);
        if (offResult is not null) return offResult;

        ResultLabel.Text = "🤖 Δεν βρέθηκε στο OFF — εκτίμηση από AI...";
        return await GetFoodNutritionFromAI(foodName);
    }

    //Open Food Facts lookup

    private async Task<FoodItem?> TryOpenFoodFacts(string foodName)
    {
        try
        {
            var fields = "product_name,nutriments,brands";
            var url =
                $"https://world.openfoodfacts.org/cgi/search.pl" +
                $"?search_terms={Uri.EscapeDataString(foodName)}" +
                $"&search_simple=1&action=process&json=1&page_size=5" +
                $"&fields={fields}";

            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            //Off wraps all results inside a "products" array
            
            if (!doc.RootElement.TryGetProperty("products", out var products))
                return null;

            foreach (var product in products.EnumerateArray())
            {
                var item = TryParseOffProduct(product, foodName);
                if (item is not null) return item;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    //Off product parser

    private static FoodItem? TryParseOffProduct(JsonElement product, string searchQuery)
    {
        var name = product.TryGetProperty("product_name", out var nameProp)
            ? nameProp.GetString()?.Trim()
            : null;

        if (string.IsNullOrWhiteSpace(name)) name = searchQuery;

        if (product.TryGetProperty("brands", out var brandsProp))
        {
            var brand = brandsProp.GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(brand))
                name = $"{name} ({brand.Split(',')[0].Trim()})";
        }

        if (!product.TryGetProperty("nutriments", out var n)) return null;

        static double Get(JsonElement n, string key)
        {
            if (n.TryGetProperty(key, out var v))
            {
                if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d))
                    return Math.Round(d, 1);
                if (v.ValueKind == JsonValueKind.String &&
                    double.TryParse(v.GetString(), System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var ds))
                    return Math.Round(ds, 1);
            }
            return -1;
        }

        var calories = Get(n, "energy-kcal_100g");
        var protein = Get(n, "proteins_100g");
        var carbs = Get(n, "carbohydrates_100g");
        var fat = Get(n, "fat_100g");

        if (calories < 0 || protein < 0 || carbs < 0 || fat < 0) return null;

        return new FoodItem
        {
            Name = name!,
            Calories = calories,
            Protein = protein,
            Carbs = carbs,
            Fat = fat,
            Fiber = Math.Max(0, Get(n, "fiber_100g")),
            Sugar = Math.Max(0, Get(n, "sugars_100g")),
            Source = "Open Food Facts (πραγματικά δεδομένα)"
        };
    }

    //AI Fallback

    private async Task<FoodItem> GetFoodNutritionFromAI(string foodName)
    {
        var prompt = $@"You are a nutrition database.
Return ONLY a JSON object (no extra text, no markdown) with nutrition per 100g for: {foodName}
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
            messages = new[] { new { role = "user", content = prompt } }
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
            throw new Exception($"AI API Error {response.StatusCode}: {responseJson}");
        
        //Parse the outer API response

        //The API wraps the AI's reply inside a JSON structure
        
        using var doc = JsonDocument.Parse(responseJson);

        if (doc.RootElement.TryGetProperty("error", out var errorProp))
            throw new Exception($"AI API: {errorProp.GetProperty("message").GetString()}");

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

        if (food is null) throw new Exception("No data found");

        food.Source = "AI εκτίμηση (OpenRouter)";
        return food;
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