import { expect, type Locator, type Page } from '@playwright/test';

/** Asserts the side navigation sits on the start side for the document direction (left in LTR, right in RTL). */
export async function expectNavOnStartSide(page: Page, dir: 'ltr' | 'rtl'): Promise<void> {
  const nav = await box(page.getByTestId('side-nav'));
  const main = await box(page.getByTestId('main-content'));
  if (dir === 'ltr') {
    expect(nav.x + nav.width).toBeLessThanOrEqual(main.x + 1);
  } else {
    expect(nav.x).toBeGreaterThanOrEqual(main.x + main.width - 1);
  }
}

async function box(locator: Locator) {
  await expect(locator).toBeVisible();
  const value = await locator.boundingBox();
  expect(value).not.toBeNull();
  return value as NonNullable<typeof value>;
}
