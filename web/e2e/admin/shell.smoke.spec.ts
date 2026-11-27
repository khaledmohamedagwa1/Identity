import { expect, test } from '@playwright/test';
import { mockBff } from '../support/bff-mock';
import { expectNavOnStartSide } from '../support/layout';

test.describe('admin portal shell', () => {
  test.beforeEach(async ({ page }) => {
    await mockBff(page);
  });

  test('TC_F10_00_AdminShellEnglishLtr', async ({ page }) => {
    await page.goto('/?lang=en');
    await expect(page).toHaveURL(/\/t\/acme$/);
    await expect(page.locator('html')).toHaveAttribute('dir', 'ltr');
    await expect(page.locator('html')).toHaveAttribute('lang', 'en');
    await expect(page.getByTestId('app-title')).toHaveText('Admin portal');
    await expect(page.getByTestId('page-title')).toHaveText('Overview');
    await expectNavOnStartSide(page, 'ltr');
  });

  test('TC_F10_01_AdminShellArabicRtl', async ({ page }) => {
    await page.goto('/?lang=ar');
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
    await expect(page.locator('html')).toHaveAttribute('lang', 'ar');
    await expect(page.getByTestId('app-title')).toHaveText('بوابة الإدارة');
    await expect(page.getByTestId('page-title')).toHaveText('نظرة عامة');
    await expectNavOnStartSide(page, 'rtl');
  });

  test('switches language at runtime and remembers it', async ({ page }) => {
    await page.goto('/?lang=en');
    await page.getByTestId('language-toggle').click();
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
    await expectNavOnStartSide(page, 'rtl');

    await page.goto('/');
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
  });

  test('keeps tokens out of browser storage', async ({ page }) => {
    await page.goto('/?lang=en');
    await expect(page.getByTestId('page-title')).toBeVisible();
    const stored = await page.evaluate(() =>
      JSON.stringify({ ...localStorage, ...sessionStorage }),
    );
    expect(stored).not.toMatch(/token|eyJ/i);
  });
});

test('anonymous users land on the signed-out page', async ({ page }) => {
  await mockBff(page, { signedIn: false });
  await page.goto('/?lang=en');
  await expect(page).toHaveURL(/\/signed-out$/);
  await expect(page.getByTestId('status-title')).toHaveText('You are signed out');
});
