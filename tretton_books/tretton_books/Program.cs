using System.Collections.Concurrent;
using System.Threading.Channels;
using HtmlAgilityPack;

string baseUri = "https://books.toscrape.com/";
string outputDir = @"C:\Temp\";

Channel<Uri> parserChannel = Channel.CreateUnbounded<Uri>();
Channel<Uri> sourceChannel = Channel.CreateUnbounded<Uri>();
ConcurrentDictionary<Uri, int> seenUris = new();
HttpClient client = new();

async Task downloadSource()
{
    while (true)
    {
        try {
            Uri uri = await sourceChannel.Reader.ReadAsync();
            // Only parse new uris
            if (seenUris.TryAdd(uri, 0))
            {
                string info = uri.ToString();
                if (info.Length > 80)
                {
                    info = info[^80..];
                }
                Console.Write(String.Format("\rDownloading\t{0,-80}", info));
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
    while (true)
    {
        try {
            Uri uri = await parserChannel.Reader.ReadAsync();
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
                        await parserChannel.Writer.WriteAsync(new Uri(uri, tag.GetAttributeValue("href", "")));
                    }
                }

                // Check all source tags
                foreach (var tag in html.DocumentNode.Descendants("script"))
                {
                    if (tag.Attributes.Contains("src") && !tag.GetAttributeValue("src", "").StartsWith("http"))
                    {
                        await sourceChannel.Writer.WriteAsync(new Uri(uri, tag.GetAttributeValue("src", "")));
                    }
                }
                // Check all img tags
                foreach (var tag in html.DocumentNode.Descendants("img"))
                {
                    if (tag.Attributes.Contains("src") && !tag.GetAttributeValue("src", "").StartsWith("http"))
                    {
                        await sourceChannel.Writer.WriteAsync(new Uri(uri, tag.GetAttributeValue("src", "")));
                    }
                }
                // Check all link tags
                foreach (var tag in html.DocumentNode.Descendants("link"))
                {
                    if (tag.Attributes.Contains("href") && !tag.GetAttributeValue("href", "").StartsWith("http"))
                    {
                        await sourceChannel.Writer.WriteAsync(new Uri(uri, tag.GetAttributeValue("href", "")));
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
        catch (ChannelClosedException) {
            break;
        }

        if (parserChannel.Reader.Count == 0) {
            // No more pages to parse, mark channel as completed
            parserChannel.Writer.Complete();
        }
    }
}

await parserChannel.Writer.WriteAsync(new Uri(baseUri));
var parseTasks = new List<Task>() {parsePage(), parsePage(), parsePage()};
var downloadTasks = new List<Task>() {downloadSource(), downloadSource(), downloadSource()};
await Task.WhenAll(parseTasks);

// Parsing complete, no new sources will be added
sourceChannel.Writer.Complete();

await Task.WhenAll(downloadTasks);

Console.WriteLine();
Console.WriteLine("Download complete");