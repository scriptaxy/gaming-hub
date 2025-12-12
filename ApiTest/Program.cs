using System.Net.Http.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// See https://aka.ms/new-console-template for more information
Console.WriteLine("🎮 Gaming Hub API Test\n");
Console.WriteLine("=".PadRight(50, '='));

var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.Add("User-Agent", "GamingHub-Test/1.0");

// Test 1: CheapShark Deals API (No API key needed!)
Console.WriteLine("\n📦 Testing CheapShark Deals API...\n");
try
{
    var dealsUrl = "https://www.cheapshark.com/api/1.0/deals?pageNumber=0&pageSize=10&onSale=1";
    var dealsResponse = await httpClient.GetStringAsync(dealsUrl);
    var deals = JArray.Parse(dealsResponse);
    
    Console.WriteLine($"✅ Found {deals.Count} deals!\n");
    
    foreach (var deal in deals.Take(5))
    {
        var title = deal["title"]?.ToString();
        var originalPrice = deal["normalPrice"]?.Value<decimal>() ?? 0;
        var salePrice = deal["salePrice"]?.Value<decimal>() ?? 0;
        var savings = deal["savings"]?.Value<double>() ?? 0;
        var store = GetStoreName(deal["storeID"]?.ToString());
        
        Console.WriteLine($"  🎯 {title}");
        Console.WriteLine($" ${originalPrice:F2} → ${salePrice:F2} ({savings:F0}% off) @ {store}");
        Console.WriteLine();
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Deals API Error: {ex.Message}");
}

// Test 2: CheapShark Stores API
Console.WriteLine("\n🏪 Testing Stores API...\n");
try
{
    var storesUrl = "https://www.cheapshark.com/api/1.0/stores";
    var storesResponse = await httpClient.GetStringAsync(storesUrl);
    var stores = JArray.Parse(storesResponse);
    
    Console.WriteLine($"✅ Found {stores.Count} stores!\n");
    
    foreach (var store in stores.Take(10))
    {
    var name = store["storeName"]?.ToString();
        var isActive = store["isActive"]?.Value<int>() == 1 ? "✓" : "✗";
        Console.WriteLine($"  {isActive} {name}");
}
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Stores API Error: {ex.Message}");
}

// Test 3: Search for a specific game deal
Console.WriteLine("\n\n🔍 Searching for 'Elden Ring' deals...\n");
try
{
    var searchUrl = "https://www.cheapshark.com/api/1.0/deals?title=elden%20ring";
    var searchResponse = await httpClient.GetStringAsync(searchUrl);
    var searchResults = JArray.Parse(searchResponse);
    
    if (searchResults.Count > 0)
  {
        Console.WriteLine($"✅ Found {searchResults.Count} deals for Elden Ring!\n");
        
        foreach (var deal in searchResults.Take(3))
        {
    var title = deal["title"]?.ToString();
          var salePrice = deal["salePrice"]?.Value<decimal>() ?? 0;
         var store = GetStoreName(deal["storeID"]?.ToString());
            Console.WriteLine($"  💰 {title} - ${salePrice:F2} @ {store}");
        }
    }
    else
    {
        Console.WriteLine("  No deals found for Elden Ring");
  }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Search Error: {ex.Message}");
}

Console.WriteLine("\n" + "=".PadRight(50, '='));
Console.WriteLine("\n✅ API Tests Complete!");
Console.WriteLine("\n📱 IPA/iPhone Options (without Mac):");
Console.WriteLine("─".PadRight(40, '─'));
Console.WriteLine("  See details printed below...\n");

static string GetStoreName(string? storeId) => storeId switch
{
    "1" => "Steam",
    "2" => "GamersGate",
    "3" => "GreenManGaming",
 "7" => "GOG",
    "8" => "Origin",
    "11" => "Humble Store",
    "13" => "Uplay",
    "15" => "Fanatical",
    "25" => "Epic Games",
    "31" => "Blizzard",
    _ => "Store #" + storeId
};
