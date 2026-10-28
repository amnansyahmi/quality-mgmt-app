import fs from 'node:fs';

function copy(packageDir, files, destination) {
  const target = new URL(`../wwwroot/vendor/${destination}/`, import.meta.url);
  fs.mkdirSync(target, { recursive: true });
  for (const [source, name] of files) fs.copyFileSync(new URL(`../node_modules/${packageDir}/${source}`, import.meta.url), new URL(name, target));
}

copy('chart.js', [['dist/chart.umd.js', 'chart.umd.js'], ['LICENSE.md', 'LICENSE.md']], 'chart.js');
// Self-hosted Inter (SIL OFL 1.1): no font CDN, consistent type on phones and desktops.
copy('@fontsource-variable/inter', [
  ['files/inter-latin-wght-normal.woff2', 'inter-latin-wght-normal.woff2'],
  ['files/inter-latin-ext-wght-normal.woff2', 'inter-latin-ext-wght-normal.woff2'],
  ['LICENSE', 'LICENSE.txt'],
], 'inter');
console.log('Copied Chart.js (MIT) and Inter (OFL-1.1)');
