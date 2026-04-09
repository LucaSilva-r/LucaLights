import tailwindcss from '@tailwindcss/vite';
import { sveltekit } from '@sveltejs/kit/vite';
import { defineConfig, loadEnv } from 'vite';

export default defineConfig(({ mode }) => {
	const env = loadEnv(mode, '.', '');
	const backendUrl = (env.LUCALIGHTS_BACKEND_URL || 'http://127.0.0.1:5050').replace(/\/+$/, '');

	return {
		plugins: [tailwindcss(), sveltekit()],
		server: {
			proxy: {
				'/api': {
					target: backendUrl,
					changeOrigin: true
				},
				'/ws': {
					target: backendUrl,
					changeOrigin: true,
					ws: true
				}
			}
		}
	};
});
