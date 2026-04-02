import tailwindcss from '@tailwindcss/vite';
import { sveltekit } from '@sveltejs/kit/vite';
import { defineConfig, loadEnv } from 'vite';

export default defineConfig(({ mode }) => {
	const env = loadEnv(mode, process.cwd(), '');
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
			}
		}
	};
});
