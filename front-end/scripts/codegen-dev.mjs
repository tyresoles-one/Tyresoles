import { execSync } from 'node:child_process';
import process from 'node:process';

// Bypass SSL certificate verification for development
process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';

const isWatchMode = process.argv.includes('--watch');
const command = isWatchMode 
  ? 'npm run codegen:watch' 
  : 'npm run codegen';

console.log(`🚀 Starting codegen in ${isWatchMode ? 'watch' : 'manual'} mode...`);
console.log('⚠️  SSL verification is disabled.');

try {
  execSync(command, { stdio: 'inherit' });
} catch (error) {
  process.exit(1);
}
