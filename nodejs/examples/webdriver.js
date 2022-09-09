import eywa from '../src/eywa.js'
import puppeteer from 'puppeteer'



(async() => {
  eywa.open_pipe()
  eywa.info('Launching browser')
  const browser = await puppeteer.launch({headless: false});
  eywa.info('Navigation to https://news.ycombinator.com')
  const page = await browser.newPage();
  await page.goto('https://news.ycombinator.com', {
    waitUntil: 'networkidle2',
  });
  eywa.info('Page loaded. Now printing page to PDF!')
  await page.pdf({ path: 'hn.pdf', format: 'a4' });
  eywa.info('Closing web page')
  await browser.close();
  eywa.close_pipe()
})()
