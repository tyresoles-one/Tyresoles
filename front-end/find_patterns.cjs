const fs = require('fs');
const path = require('path');

function walk(dir, fileList = []) {
  const files = fs.readdirSync(dir);
  for (const file of files) {
    const stat = fs.statSync(path.join(dir, file));
    if (stat.isDirectory() && file !== 'node_modules' && file !== '.svelte-kit') {
      walk(path.join(dir, file), fileList);
    } else if (file.endsWith('.svelte')) {
      fileList.push(path.join(dir, file));
    }
  }
  return fileList;
}

const svelteFiles = walk('src');
const blockCounts = {};

for (const file of svelteFiles) {
  const content = fs.readFileSync(file, 'utf8');
  const lines = content.split('\n');
  for (let i = 0; i < lines.length - 3; i++) {
     const block = lines.slice(i, i+3).map(l => l.trim()).join(' ').replace(/\s+/g, ' ');
     if (block.length > 30 && block.includes('<') && block.includes('>')) {
         const structure = block.replace(/>[^<]*</g, '><').replace(/\{([^}]+)\}/g, '{exp}');
         if (!blockCounts[structure]) blockCounts[structure] = [];
         blockCounts[structure].push(file);
     }
  }
}

const entries = Object.entries(blockCounts)
  .filter(([_, files]) => [...new Set(files)].length > 4)
  .sort((a, b) => [...new Set(b[1])].length - [...new Set(a[1])].length);

let out = `Found ${svelteFiles.length} svelte files.\n\nMost common 3-line blocks:\n`;
for (let i = 0; i < Math.min(20, entries.length); i++) {
  const uniqueFiles = [...new Set(entries[i][1])];
  out += `\n--- Block (Found in ${uniqueFiles.length} files) ---\n`;
  out += entries[i][0] + '\n';
  out += 'Example file: ' + uniqueFiles[0] + '\n';
}
fs.writeFileSync('patterns.txt', out);
