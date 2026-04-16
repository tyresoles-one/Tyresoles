import tailwindcss from '@tailwindcss/vite';
import { sveltekit } from '@sveltejs/kit/vite';
import { defineConfig, loadEnv } from 'vite';

export default defineConfig(({ mode }) => {
	const env = loadEnv(mode, process.cwd(), '');
	const win = process.platform === 'win32';
	/** Vite 7 SSR can hit "transport invoke timed out after 60000ms" if file watchers are flaky (common on Windows). Set VITE_DEV_WATCH_POLLING=1 to use polling. */
	const watchPolling = env.VITE_DEV_WATCH_POLLING === '1';

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
			proxy: {
				'/graphql': {
					target: env.VITE_PUBLIC_API_URL || 'https://localhost:5002',
					changeOrigin: true,
					secure: false // Useful if using self-signed certs for local backend dev
				}
			},
			...(win && watchPolling
				? { watch: { usePolling: true, interval: 300 } }
				: {})
		}
	};
});
