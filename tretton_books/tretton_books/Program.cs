using System.Collections.Concurrent;
using System.Reflection.Metadata;
using HtmlAgilityPack;

string baseUri = "https://books.toscrape.com/";
string outputDir = @"C:\Temp\";

BlockingCollection<Uri> parserQueue = [];
BlockingCollection<Uri> sourceQueue = [];
ConcurrentDictionary<Uri, int> seenUris = new();
HttpClient client = new();

async Task downloadSource()
{
    while (true)
    {
        try {
            Uri uri = sourceQueue.Take();
            // Only parse new uris
            if (seenUris.TryAdd(uri, 0))
            {
                string info = uri.ToString();
                if (info.Length > 80)
                {
                    info = info[^80..];
                }
                Console.Write(String.Format("\rDownloading\t\t{0,-80}", info));
                using HttpResponseMessage response = await client.GetAsync(uri);
                response.EnsureSuccessStatusCode();

                var filePath = Path.Combine(outputDir, uri.AbsolutePath[1..]);
                var dir = Path.GetDirectoryName(filePath);
                if (dir != null) {
                    Directory.CreateDirectory(dir);
                }
                await File.WriteAllBytesAsync(filePath, await response.Content.ReadAsByteArrayAsync());
            }
        }
        catch(InvalidOperationException) {
            break;
        }
    }
}

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

            var filePath = Path.Combine(outputDir, uri.AbsolutePath[1..]);
            if (Path.GetFileName(filePath)?.Length == 0) {
                filePath = Path.Combine(filePath, "index.html");
            }

            var dir = Path.GetDirectoryName(filePath);
            if (dir != null) {
                Directory.CreateDirectory(dir);
            }
            await File.WriteAllTextAsync(filePath, responseBody);
        }
    }
}

parserQueue.Add(new Uri(baseUri));
var parseTasks = new List<Task>() {parsePage(), parsePage(), parsePage()};
var downloadTasks = new List<Task>() {downloadSource(), downloadSource(), downloadSource()};
await Task.WhenAll(parseTasks);

// Parsing complete, no new sources will be added
sourceQueue.CompleteAdding();

await Task.WhenAll(downloadTasks);

Console.WriteLine();
Console.WriteLine("Download complete");