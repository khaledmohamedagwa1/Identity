import { expect, test } from '@playwright/test';
import { mockBff } from '../support/bff-mock';
import { expectNavOnStartSide } from '../support/layout';

test.describe('account portal shell', () => {
  test.beforeEach(async ({ page }) => {
    await mockBff(page);
  });

  test('TC_F10_00_AccountShellEnglishLtr', async ({ page }) => {
    await page.goto('/?lang=en');
    await expect(page.locator('html')).toHaveAttribute('dir', 'ltr');
    await expect(page.getByTestId('app-title')).toHaveText('My account');
    await expect(page.getByTestId('page-title')).toHaveText('Profile');
    await expect(page.getByText('mona@acme.test')).toBeVisible();
    await expectNavOnStartSide(page, 'ltr');
  });

  test('TC_F10_01_AccountShellArabicRtl', async ({ page }) => {
    await page.goto('/?lang=ar');
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
    await expect(page.getByTestId('app-title')).toHaveText('حسابي');
    await expect(page.getByTestId('page-title')).toHaveText('الملف الشخصي');
    await expectNavOnStartSide(page, 'rtl');
  });
});
