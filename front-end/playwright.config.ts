import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
	testDir: './e2e',
	fullyParallel: true,
	forbidOnly: !!process.env.CI,
	retries: process.env.CI ? 2 : 0,
	workers: process.env.CI ? 1 : undefined,
	reporter: process.env.CI ? 'github' : 'html',
	use: {
		// Dev server is faster than build+preview for local e2e; CI can set PLAYWRIGHT_BASE_URL to a preview URL if needed.
		baseURL: process.env.PLAYWRIGHT_BASE_URL ?? 'http://localhost:5173',
		trace: 'on-first-retry'
	},
	projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
	webServer: process.env.PLAYWRIGHT_BASE_URL
		? undefined
		: {
				command: 'npm run dev -- --port 5173 --strictPort',
				url: 'http://localhost:5173',
				reuseExistingServer: !process.env.CI,
				timeout: 120_000,
				stdout: 'pipe',
				stderr: 'pipe'
			}
});
