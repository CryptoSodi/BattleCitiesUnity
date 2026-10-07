import { build } from 'esbuild';
import { fileURLToPath } from 'node:url';
const root=new URL('../../',import.meta.url);
await build({
  entryPoints:[fileURLToPath(new URL('Assets/StreamingAssets/BattleCitiesCheckout/wallet-entry.js',root))],
  outfile:fileURLToPath(new URL('Assets/StreamingAssets/BattleCitiesCheckout/wallet.js',root)),
  nodePaths:[fileURLToPath(new URL('node_modules',import.meta.url))],
  bundle:true,minify:true,platform:'browser',format:'iife',target:'es2020',legalComments:'eof',
});
