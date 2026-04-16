import { test, expect } from '@playwright/test';

test.describe('public routes', () => {
	test('login page shows welcome heading', async ({ page }) => {
		await page.goto('/login');
		await expect(page.getByRole('heading', { name: 'Welcome Back' })).toBeVisible({
			timeout: 60_000
		});
		await expect(
			page.getByText('Enter your credentials to access your account')
		).toBeVisible();
	});
});
