// Chart.js is bundled locally under wwwroot/vendor/chart.js (MIT).
const boards = new WeakMap();
function palette(root) {
  const css = getComputedStyle(root), read = name => css.getPropertyValue('--wf-' + name).trim();
  return {ink:read('ink'), muted:read('muted'), grid:read('border'), surface:read('surface'), accent:read('accent'), red:read('danger'), amber:read('warning'), green:read('accent'), font:read('font')};
}
function options(p, horizontal=false) {
  return {responsive:true, maintainAspectRatio:false, animation:false, resizeDelay:80,
    interaction:{mode:'nearest',intersect:true},
    plugins:{legend:{display:false},tooltip:{backgroundColor:p.ink,titleColor:p.surface,bodyColor:p.surface,padding:10,cornerRadius:4,displayColors:false,callbacks:{label:context=>{const n=typeof context.raw==='number'?context.raw:0;return n+' complaint'+(n===1?'':'s');}}}},
    indexAxis:horizontal?'y':'x',
    scales:{x:{beginAtZero:true,border:{display:false},grid:{display:horizontal,drawTicks:false,color:p.grid},ticks:{color:p.muted,font:{family:p.font,size:11},precision:0,maxRotation:0,autoSkip:true,padding:8}},
    y:{beginAtZero:true,border:{display:false},grid:{display:!horizontal,drawTicks:false,color:p.grid},ticks:{color:p.muted,font:{family:p.font,size:11},precision:0,padding:8}}}};
}
export function mount(root, data, callback) {
  destroy(root);
  const p=palette(root), charts=[];
  for(const canvas of root.querySelectorAll('canvas[data-chart]')) {
    const name=canvas.dataset.chart, series=data[name];
    if(!series) continue;
    const donut=name==='deadline', horizontal=name==='aging'||name==='department';
    const colors=donut?[p.red,p.amber,p.green]:name==='stage'?series.labels.map((_,i)=>i===2?p.amber:i===5?'#9BAAA7':p.accent):p.accent;
    const config={type:donut?'doughnut':'bar',data:{labels:series.labels,datasets:[{label:'Complaints',data:series.values,backgroundColor:colors,borderWidth:0,borderRadius:donut?0:2,maxBarThickness:horizontal?13:28,barPercentage:.64,categoryPercentage:.72,hoverBackgroundColor:colors}]},options:options(p,horizontal)};
    if(donut){delete config.options.scales;config.options.cutout='78%';config.options.spacing=3;config.options.plugins.tooltip.callbacks.label=context=>context.label+': '+context.raw;}
    else {const countAxis=horizontal?'x':'y', max=Math.max(0,...series.values);config.options.scales[countAxis].suggestedMax=Math.max(2,max);config.options.scales[countAxis].ticks.stepSize=Math.max(1,Math.ceil(max/4));}
    if(name==='stage'||donut){config.options.onClick=(_event,elements)=>{if(elements.length)callback.invokeMethodAsync('FilterChart',name,elements[0].index);};config.options.onHover=(event,elements)=>{event.native.target.style.cursor=elements.length?'pointer':'default';};}
    charts.push(new Chart(canvas,config));
  }
  root.dataset.chartsReady='true';
  boards.set(root,{charts,callback});
}
export function refresh(root,data){const board=boards.get(root);if(board)mount(root,data,board.callback);}
export function destroy(root){const board=boards.get(root);if(board){board.charts.forEach(chart=>chart.destroy());boards.delete(root);}}
