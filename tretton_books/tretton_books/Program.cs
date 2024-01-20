using System.Collections.Concurrent;

string baseUri = "https://books.toscrape.com/catalogue/category/books/travel_2/index.html";
string outputDir = @"C:\Temp";

BlockingCollection<Uri> parserQueue = [];
BlockingCollection<Uri> sourceQueue = [];
ConcurrentDictionary<Uri, int> seenUris = new();
HttpClient client = new();

async Task parsePage()
{

    Uri uri = new(baseUri);

    while (sourceQueue.TryTake(out uri))
    {
        using HttpResponseMessage response = await client.GetAsync(uri);
        response.EnsureSuccessStatusCode();
        string responseBody = await response.Content.ReadAsStringAsync();

        Console.WriteLine(responseBody[..10]);
    }
}

sourceQueue.Add(new Uri(baseUri));
var tasks = new List<Task>() {parsePage()};

await Task.WhenAll(tasks);

Console.WriteLine("Download complete");