document.querySelectorAll('[data-submit-on-change]').forEach(select => select.addEventListener('change', () => select.form.requestSubmit()));
document.querySelectorAll('[data-print]').forEach(button => button.addEventListener('click', () => window.print()));
function renumberActionRows(body){
  [...body.rows].forEach((row,index)=>{
    row.querySelectorAll('input').forEach(input=>{
      input.name=input.name.replace(/\[\d+\]/,`[${index}]`);
      input.setAttribute('aria-label',(input.getAttribute('aria-label')||'Action').replace(/\d+/,String(index+1)));
    });
    row.querySelector('.delete-action-row')?.setAttribute('aria-label',`Delete action row ${index+1}`);
  });
}
document.querySelectorAll('.action-rows').forEach(group=>group.addEventListener('click',event=>{
  const add=event.target.closest('.add-row'),remove=event.target.closest('.delete-action-row');
  if(!add&&!remove)return;
  const body=group.querySelector('tbody');
  if(add){
    if(body.rows.length>=30){window.alert('Up to 30 action rows can be recorded.');return;}
    const row=body.rows[body.rows.length-1].cloneNode(true);
    row.querySelectorAll('input').forEach(input=>input.value='');
    body.appendChild(row);renumberActionRows(body);row.querySelector('input').focus();
  }else{
    const row=remove.closest('tr'),index=row.sectionRowIndex;
    if(body.rows.length===1)row.querySelectorAll('input').forEach(input=>input.value='');
    else row.remove();
    renumberActionRows(body);body.rows[Math.min(index,body.rows.length-1)].querySelector('input').focus();
  }
  group.dispatchEvent(new Event('input',{bubbles:true}));
}));
let dirty=false;
document.querySelectorAll('[data-dirty-form]').forEach(form=>{
  form.addEventListener('input',()=>{dirty=true;});
  form.addEventListener('submit',()=>{dirty=false;});
});
window.addEventListener('beforeunload',event=>{if(dirty){event.preventDefault();event.returnValue='';}});

// Present the existing sequential workflow as a conventional, one-based stepper.
(() => {
 const svgNS='http://www.w3.org/2000/svg';
 function icon(paths){const svg=document.createElementNS(svgNS,'svg');svg.setAttribute('viewBox','0 0 24 24');svg.setAttribute('fill','none');svg.setAttribute('stroke','currentColor');svg.setAttribute('stroke-width','1.8');svg.setAttribute('stroke-linecap','round');svg.setAttribute('stroke-linejoin','round');svg.setAttribute('aria-hidden','true');for(const d of paths){const path=document.createElementNS(svgNS,'path');path.setAttribute('d',d);svg.appendChild(path);}return svg;}
 const help=document.querySelector('[data-open-dialog="role-help"]');
 if(help){help.replaceChildren(icon(['M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18','M9.5 9a2.5 2.5 0 0 1 5 .2c0 1.8-2.5 1.8-2.5 3.8','M12 16.8v.1']));help.setAttribute('aria-label','Show help');help.title='Show help';help.classList.add('support-icon');}
 const bell=document.getElementById('notification-bell');
 if(bell){bell.querySelector('.notification-word')?.remove();bell.classList.add('support-icon');bell.title='Notifications';}
 document.querySelectorAll('.runtime-route:not(.numbered-stepper)').forEach(list=>{
  const steps=Array.from(list.children),finish=steps[steps.length-1];if(steps.length<2)return;
  const complete=finish.classList.contains('current'),total=steps.length-1;
  const current=complete?total:Math.max(0,steps.findIndex(step=>step.classList.contains('current')));
  const pageState=list.dataset.runtimeState||document.querySelector('.runtime-page .queue-page-heading p')?.textContent.trim().split(' · ')[0];
  const returned=pageState==='Returned';
  const labels=steps.map(step=>Array.from(step.childNodes).filter(node=>node.nodeType===Node.TEXT_NODE).map(node=>node.textContent).join(' ').trim());
  const wrapper=document.createElement('section');wrapper.className='workflow-stepper';wrapper.setAttribute('aria-label','Workflow progress');
  const summary=document.createElement('div');summary.className='stepper-summary';
  const heading=document.createElement('p');heading.className='stepper-position';heading.textContent=complete?'Workflow complete':`Step ${current+1} of ${total} · ${labels[current]}`;
  const status=document.createElement('span');status.className='stepper-state'+(returned?' returned':'');status.textContent=complete?'Completed':returned?'Returned for correction':pageState==='Preview'?'Preview':pageState==='Draft'?'Draft':'In progress';summary.append(heading,status);
  list.before(wrapper);wrapper.append(summary,list);list.classList.add('numbered-stepper');list.setAttribute('aria-label','Workflow steps');list.tabIndex=0;
  steps.forEach((step,index)=>{
   const terminal=index===total,done=complete||index<current,active=index===current;
   const person=step.querySelector('span')?.textContent.trim()||'';
   step.className='stepper-step '+(done?'done':active?'current':'upcoming')+(terminal?' terminal':'');
   if(active)step.setAttribute('aria-current','step');
   const state=terminal?(complete?'Completed':'Finish'):done?'Completed':active?(returned?'Needs correction':'Current step'):'Upcoming';
   step.setAttribute('aria-label',`${terminal?'Finish':'Step '+(index+1)}: ${labels[index]}. ${state}`);
   const number=document.createElement('b');number.className='step-index';number.setAttribute('aria-hidden','true');
   if(done||terminal)number.appendChild(icon(['M6 12.5l4 4L18 8']));else number.textContent=String(index+1);
   const title=document.createElement('strong');title.className='step-title';title.textContent=labels[index];
   const owner=document.createElement('span');owner.className='step-owner';owner.textContent=person;
   const stateLabel=document.createElement('small');stateLabel.className='step-status';stateLabel.textContent=state;
   step.replaceChildren(number,title);if(person)step.appendChild(owner);step.appendChild(stateLabel);
  });
  const note=document.createElement('p');note.className='stepper-scroll-note';note.textContent='Scroll to view all steps';wrapper.appendChild(note);
  const active=steps[current];
  const updateScrollNote=()=>{note.hidden=list.scrollWidth<=list.clientWidth+1;if(!note.hidden)list.scrollLeft=Math.max(0,active.offsetLeft-list.clientWidth/2+active.clientWidth/2);};updateScrollNote();if(typeof ResizeObserver!=='undefined')new ResizeObserver(updateScrollNote).observe(list);
  // Visible labels use the same 1-based numbering; stored workflow indexes remain unchanged.
  if(list.dataset.workflowNumbering!=='1'){
  document.querySelectorAll('.runtime-stage-label').forEach(label=>{label.textContent=label.textContent.replace(/^Step (\d+):/,(_match,n)=>'Step '+(Number(n)+1)+':');});
  document.querySelectorAll('.runtime-history article>span').forEach(label=>{label.textContent=label.textContent.replace(/step (\d+) → (\d+)/,(_match,from,to)=>'step '+(Number(from)+1)+' → '+(Number(to)+1));});
  document.querySelectorAll('#module-return-target option').forEach(option=>{option.textContent='Step '+(Number(option.value)+1)+': '+option.textContent.replace(/^Step \d+: /,'');});
  }
 });
})();
