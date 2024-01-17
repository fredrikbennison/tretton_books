import aiohttp
import asyncio
from bs4 import BeautifulSoup

site = "https://books.toscrape.com/"

async def pageparser(session, url, pgqueue, sources):
    async with session.get(url) as response:

        html = await response.text()
        soup = BeautifulSoup(html, "html.parser")

        # Find all links
        for alink in soup.find_all('a'):
            print("a: ", alink.attrs['href'])
            new_url = site + response.url.path + alink.attrs['href']
            print(new_url)
            await pgqueue.put(new_url)

    
async def main():

    async with aiohttp.ClientSession() as session:
        parserQueue = asyncio.Queue()
        sourceQueue = asyncio.Queue()
        await pageparser(session, site, parserQueue, sourceQueue)
asyncio.run(main())