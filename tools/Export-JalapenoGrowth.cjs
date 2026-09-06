const fs = require('node:fs/promises');
const path = require('node:path');
const { pathToFileURL } = require('node:url');
const { chromium } = require('playwright');
const sharp = require('sharp');

async function main() {
  const variety = process.argv[2] || 'jalapeno';
  if (!['jalapeno', 'habanero'].includes(variety)) throw new Error('Expected jalapeno or habanero');
  const root = path.resolve(__dirname, '..');
  const output = path.join(root, 'art', 'previews', `${variety}-growing.gif`);
  const review = path.join(root, 'tmp', `${variety}-growth-gif`);
  await fs.mkdir(review, { recursive: true });
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  const frames = [];
  try {
    const page = await browser.newPage({ viewport: { width: 800, height: 800 }, deviceScaleFactor: 1 });
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.goto(pathToFileURL(path.join(root, 'art', 'previews', `${variety}-stages.html`)).href);
    await page.waitForFunction(() => window.ready === true);
    await page.addStyleTag({ content: `
      header, nav, #layers, #file { display:none !important }
      #gif-title { position:absolute;top:30px;left:34px;font-size:24px;font-weight:600;color:#26372e }
      #gif-label { position:absolute;left:34px;bottom:50px;font-size:17px;color:#334c3d }
      #gif-progress { position:absolute;left:34px;right:34px;bottom:30px;display:flex;gap:6px }
      #gif-progress span { flex:1;height:3px;background:#d3ddd6 }
      #gif-progress span.active { background:#426c4b }
    ` });
    await page.evaluate(variety => {
      document.querySelector('#scale').checked = true;
      document.querySelector('#spin').checked = false;
      document.body.insertAdjacentHTML('beforeend', '<div id="gif-title"></div><div id="gif-label"></div><div id="gif-progress">' + '<span></span>'.repeat(8) + '</div>');
      document.querySelector('#gif-title').textContent = variety[0].toUpperCase() + variety.slice(1);
    }, variety);
    for (let stage = 1; stage <= 8; stage++) {
      await page.evaluate(async stage => {
        window.setStage(stage);
        window.setView('perspective');
        document.querySelector('#gif-label').textContent = `${stage} / 8   ${window.stageInfo[stage].label}`;
        document.querySelectorAll('#gif-progress span').forEach((bar, index) => bar.classList.toggle('active', index < stage));
        // Let OrbitControls settle before capturing a fixed, consistent camera.
        for (let i = 0; i < 12; i++) await new Promise(requestAnimationFrame);
      }, stage);
      const screenshot = await page.screenshot();
      await fs.writeFile(path.join(review, `stage${stage}.png`), screenshot);
      const { data, info } = await sharp(screenshot).removeAlpha().raw().toBuffer({ resolveWithObject: true });
      if (info.width !== 800 || info.height !== 800) throw new Error('Unexpected frame dimensions');
      let plantPixels = 0;
      for (let y = 100; y < 700; y++) for (let x = 100; x < 700; x++) {
        const offset = (y * 800 + x) * 3;
        if (data[offset + 1] > data[offset + 2] * 1.3 && data[offset + 2] < 150) plantPixels++;
      }
      if (plantPixels < 200) throw new Error(`Stage ${stage} rendered blank`);
      frames.push(data);
      console.log(`Rendered stage ${stage}: ${plantPixels} plant pixels`);
    }
    if (errors.length) throw new Error(errors.join('\n'));
  } finally {
    await browser.close();
  }
  await sharp(Buffer.concat(frames), { raw: { width: 800, height: 800 * frames.length, channels: 3, pageHeight: 800 } })
    .gif({ loop: 0, delay: [1400, 1200, 1200, 1400, 1600, 1400, 1400, 2800], colours: 256, effort: 7, dither: 0.4 })
    .toFile(output);
  const metadata = await sharp(output, { animated: true }).metadata();
  if (metadata.pages !== 8 || metadata.pageHeight !== 800 || metadata.loop !== 0) throw new Error('Invalid GIF animation');
  const thumbnails = await Promise.all(frames.map(data => sharp(data, { raw: { width: 800, height: 800, channels: 3 } }).resize(240, 240).png().toBuffer()));
  await sharp({ create: { width: 960, height: 480, channels: 3, background: '#edf0ef' } })
    .composite(thumbnails.map((input, index) => ({ input, left: index % 4 * 240, top: Math.floor(index / 4) * 240 })))
    .png().toFile(path.join(review, 'contact-sheet.png'));
  console.log(JSON.stringify({ output, bytes: (await fs.stat(output)).size, frames: metadata.pages, durationMs: metadata.delay.reduce((a, b) => a + b, 0) }));
}

main().catch(error => { console.error(error); process.exitCode = 1; });
