import tailwindcss from '@tailwindcss/vite';
import { sveltekit } from '@sveltejs/kit/vite';
import { defineConfig, loadEnv } from 'vite';

export default defineConfig(({ mode }) => {
	const env = loadEnv(mode, process.cwd(), '');
	const win = process.platform === 'win32';
	/** Vite 7 SSR can hit "transport invoke timed out after 60000ms" if file watchers are flaky (common on Windows). Set VITE_DEV_WATCH_POLLING=1 to use polling. */
	const watchPolling = env.VITE_DEV_WATCH_POLLING === '1';

	/**
	 * Ignore Capacitor / Gradle / Xcode / Tauri output so native builds do not trigger Vite reloads
	 * and starve SSR `fetchModule` (e.g. churn under `android/build/reports/**` or `src-tauri/target/**`).
	 */
	const capNativeWatchIgnores = [
		'**/android/build/**',
		'**/android/.gradle/**',
		'**/android/app/build/**',
		'**/android/**/intermediates/**',
		'**/android/app/src/main/assets/public/**',
		'**/ios/App/build/**',
		'**/ios/Pods/**',
		'**/ios/DerivedData/**',
		'**/src-tauri/**',
	];

	return {
		plugins: [tailwindcss(), sveltekit()],
		optimizeDeps: {
			include: ['pdfjs-dist'],
			// Worker is loaded via ?url in the component; do not pre-bundle it
			exclude: ['@graphql-typed-document-node/core', 'pdfjs-dist/build/pdf.worker.mjs', 'pdfjs-dist/build/pdf.worker.min.mjs']
		},
		resolve: {
			dedupe: ['pdfjs-dist']
		},
		server: {
			// Align HMR websocket host with the URL you open (avoids flaky full reloads where
			// the client requests stale ?t= route chunks → "Failed to fetch dynamically imported module").
			hmr: { host: 'localhost' },
			proxy: {
				'/graphql': {
					target: env.VITE_PUBLIC_API_URL || 'https://localhost:5002',
					changeOrigin: true,
					secure: false // Useful if using self-signed certs for local backend dev
				}
			},
			watch: {
				ignored: capNativeWatchIgnores,
				...(win && watchPolling ? { usePolling: true, interval: 300 } : {}),
			},
		}
	};
});
