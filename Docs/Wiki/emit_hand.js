// dumps the hand-authored entity ids/names so the importer never has to regex JS
const fs=require('fs'),vm=require('vm');
const ctx={};vm.createContext(ctx);
vm.runInContext(fs.readFileSync('data.js','utf8'),ctx);
vm.runInContext("globalThis.__D = DATA",ctx);
const D=ctx.__D;
fs.writeFileSync('hand.json',JSON.stringify({
  weapons:D.weapons.map(w=>({id:w.id,name:w.name})),
  abilities:D.abilities.map(a=>({id:a.id,name:a.name,frames:a.frames,slot:a.slot})),
  frames:D.frames.map(f=>({id:f.id,name:f.name,primary:f.primary,abilities:f.abilities}))
},null,1));
console.log('hand.json:',D.weapons.length,'weapons',D.abilities.length,'abilities',D.frames.length,'frames');
