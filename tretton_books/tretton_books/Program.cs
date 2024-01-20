using System.Collections.Concurrent;
using System.Reflection.Metadata;
using HtmlAgilityPack;

string baseUri = "https://books.toscrape.com/catalogue/category/books/travel_2/index.html";
string outputDir = @"C:\Temp";

BlockingCollection<Uri> parserQueue = [];
BlockingCollection<Uri> sourceQueue = [];
ConcurrentDictionary<Uri, int> seenUris = new();
HttpClient client = new();

async Task parsePage()
{

    while (parserQueue.TryTake(out Uri? uri))
    {
        // Only parse new uris
        if (seenUris.TryAdd(uri, 0))
        {
            string info = uri.ToString();
            if (info.Length > 80)
            {
                info = info[^80..];
            }
            Console.Write(String.Format("\rParsing\t\t{0,-80}", info));
            using HttpResponseMessage response = await client.GetAsync(uri);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();

            HtmlDocument html = new();
            html.LoadHtml(responseBody);

            // Check all a link tags
            foreach (var tag in html.DocumentNode.Descendants("a"))
            {
                if (tag.Attributes.Contains("href"))
                {
                    parserQueue.Add(new Uri(uri, tag.GetAttributeValue("href", "")));
                }
            }

            // Check all source tags
            foreach (var tag in html.DocumentNode.Descendants("script"))
            {
                if (tag.Attributes.Contains("src") && !tag.GetAttributeValue("src", "").StartsWith("http"))
                {
                    sourceQueue.Add(new Uri(uri, tag.GetAttributeValue("src", "")));
                }
            }
            // Check all img tags
            foreach (var tag in html.DocumentNode.Descendants("img"))
            {
                if (tag.Attributes.Contains("src") && !tag.GetAttributeValue("src", "").StartsWith("http"))
                {
                    sourceQueue.Add(new Uri(uri, tag.GetAttributeValue("src", "")));
                }
            }
            // Check all link tags
            foreach (var tag in html.DocumentNode.Descendants("link"))
            {
                if (tag.Attributes.Contains("href") && !tag.GetAttributeValue("href", "").StartsWith("http"))
                {
                    sourceQueue.Add(new Uri(uri, tag.GetAttributeValue("href", "")));
                }
            }

        }

        // If the queue is empty, signal all tasks that the queue is complete
        if (parserQueue.Count == 0) {
            parserQueue.CompleteAdding();
        }
    }
}

parserQueue.Add(new Uri(baseUri));
var tasks = new List<Task>() {parsePage()};

await Task.WhenAll(tasks);

Console.WriteLine("Download complete");