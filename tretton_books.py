import aiohttp
import asyncio
import aiofiles.os
import os
import urllib.parse
from bs4 import BeautifulSoup

site = "https://books.toscrape.com"

outputPath = "/tmp/tretton/"
downloaded = []

async def sourcedownload(name, session, sources):
    while True:
        # Retrieve URL from queue
        url = await sources.get()

        # Don't download already downloaded url
        if not url in downloaded:
            print(f"{name} downloading: ", url)
            async with session.get(url) as response:
                if (response.url.parent.path.startswith('/')):
                    parentPath = response.url.parent.path[1:]
                else:
                    parentPath = response.url.parent.path

                filePath = os.path.join(outputPath, parentPath)
                
                await aiofiles.os.makedirs(filePath, exist_ok = True)
                
                filename = response.url.name
                if (len(filename) == 0):
                    filename = "index.html"

                async with aiofiles.open(os.path.join(filePath, filename), "wb") as outfile:
                    await outfile.write(await response.read())
                    
        downloaded.append(url)
        
        # Mark item as done in queue.
        sources.task_done()        


async def pageparser(name, session, pgqueue, sources):
    while True:
        # Retrieve URL from queue
        url = await pgqueue.get()

        # Don't parse already handled url
        if not url in downloaded:

            print(f"{name} parsing: ", url)
            async with session.get(url) as response:
    
                html = await response.text()
                soup = BeautifulSoup(html, "html.parser")

                # Find all links
                for alink in soup.find_all('a'):
                    new_url = urllib.parse.urljoin(site + response.url.parent.path + "/", alink.attrs['href'])
                    await pgqueue.put(new_url)
                    
                # Find script sources
                for source in soup.find_all('script'): 
                    if (source.has_attr('src') and not source.attrs['src'].startswith('http')):
                        new_url = urllib.parse.urljoin(site + response.url.parent.path + "/", source.attrs['src'])
                        await sources.put(new_url)

                # Find image sources
                for source in soup.find_all('img'): 
                    if (source.has_attr('src') and not source.attrs['src'].startswith('http')):
                        new_url = urllib.parse.urljoin(site + response.url.parent.path + "/", source.attrs['src'])
                        await sources.put(new_url)

                # Find link sources
                for source in soup.find_all('link'): 
                    if (source.has_attr('href') and not source.attrs['href'].startswith('http')):
                        new_url = urllib.parse.urljoin(site + response.url.parent.path + "/", source.attrs['href'])
                        await sources.put(new_url)
                    
                if (response.url.parent.path.startswith('/')):
                    parentPath = response.url.parent.path[1:]
                else:
                    parentPath = response.url.parent.path

                filePath = os.path.join(outputPath, parentPath)
                
                await aiofiles.os.makedirs(filePath, exist_ok = True)
                
                filename = response.url.name
                if (len(filename) == 0):
                    filename = "index.html"

                async with aiofiles.open(os.path.join(filePath, filename), "w") as outfile:
                    await outfile.write(html)
                
        downloaded.append(url)
        
        # Mark item as done in queue.
        pgqueue.task_done()        
    
async def main():

    await aiofiles.os.makedirs(outputPath, exist_ok = True)

    async with aiohttp.ClientSession() as session:
        # Create queues and add first url 
        parserQueue = asyncio.Queue()
        sourceQueue = asyncio.Queue()
        await parserQueue.put(site)
        
        # Create worker tasks for parsing and downloading
        tasks = []
        
        for i in range(3):
            task = asyncio.create_task(pageparser(f'parser-{i}', session, parserQueue, sourceQueue))
            tasks.append(task)

            task = asyncio.create_task(sourcedownload(f'retriever-{i}', session, sourceQueue))
            tasks.append(task)
 
        # Wait for the queues to be empty
        await parserQueue.join()
        await sourceQueue.join()
        
        # Cancel our worker tasks.
        for task in tasks:
            task.cancel()
        # Wait until all worker tasks are cancelled.
        await asyncio.gather(*tasks, return_exceptions=True)
                    
    print("\n\nDownload complete")
        
asyncio.run(main())