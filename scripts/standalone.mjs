// Bundle dist/ into a single HTML file that runs when opened straight from disk.
import { readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const dist = join(process.cwd(), 'dist');
const assets = join(dist, 'assets');
let html = readFileSync(join(dist, 'index.html'), 'utf8');
const files = readdirSync(assets);
const js = files.find((f) => f.endsWith('.js'));
const css = files.find((f) => f.endsWith('.css'));
if (!js || !css) throw new Error('Run `npm run build` first.');

const script = readFileSync(join(assets, js), 'utf8')
  .replace(/\/\/# sourceMappingURL=.*$/m, '')
  .replace(/<\/script/g, '<\\/script');
const style = readFileSync(join(assets, css), 'utf8').replace(/<\/style/g, '<\\/style');

html = html
  .replace(/<script type="module"[^>]*src="[^"]*"[^>]*><\/script>/, () => `<script type="module">\n${script}\n</script>`)
  .replace(/<link rel="stylesheet"[^>]*href="\.\/assets\/[^"]*"[^>]*>/, () => `<style>\n${style}\n</style>`);

if (html.includes('./assets/')) throw new Error('An asset reference was not inlined.');
const out = join(process.cwd(), 'neon-line-rider.html');
writeFileSync(out, html);
console.log(`wrote ${out} (${(html.length / 1024).toFixed(0)} KB)`);
