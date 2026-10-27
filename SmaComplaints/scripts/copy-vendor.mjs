import fs from 'node:fs';
const destination=new URL('../wwwroot/vendor/chart.js/',import.meta.url);
fs.mkdirSync(destination,{recursive:true});
for(const [source,name] of [['dist/chart.umd.js','chart.umd.js'],['LICENSE.md','LICENSE.md']])fs.copyFileSync(new URL('../node_modules/chart.js/'+source,import.meta.url),new URL(name,destination));
console.log('Copied Chart.js and MIT license');
