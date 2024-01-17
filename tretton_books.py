import aiohttp
import asyncio
from bs4 import BeautifulSoup

site = "https://books.toscrape.com/"

async def pageparser(session, pgqueue, sources):
    while True:
        # Retrieve URL from queue
        url = await pgqueue.get()

        print("Parsing: ", url)
        async with session.get(url) as response:
    
            html = await response.text()
            soup = BeautifulSoup(html, "html.parser")
    
            # Find all links
            for alink in soup.find_all('a'):
                print("a: ", alink.attrs['href'])
                new_url = site + response.url.parent.path + alink.attrs['href']
                print(new_url)
                await pgqueue.put(new_url)
                
            for source in soup.find_all('script'): 
                if (source.has_attr('src')):
                    print("src: ", source.attrs['src'])
                

    
async def main():

    async with aiohttp.ClientSession() as session:
        parserQueue = asyncio.Queue()
        sourceQueue = asyncio.Queue()
        await parserQueue.put(site)
        await pageparser(session, parserQueue, sourceQueue)
        
asyncio.run(main())