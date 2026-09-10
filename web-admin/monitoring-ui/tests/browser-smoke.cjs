const {
  chromium
} = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
async function run() {
  const {
    dashboard,
    jobs,
    session
  } = await import('./fixtures.mjs');
  const browser = await chromium.launch({
    channel: 'msedge',
    headless: true
  });
  const output = path.resolve(__dirname, '../../../TestResults/admin-web');
  await fs.mkdir(output, {
    recursive: true
  });
  try {
    for (const width of [1440, 390]) {
      const context = await browser.newContext({
        viewport: {
          width,
          height: 960
        }
      });
      const page = await context.newPage();
      const errors = [];
      page.on('pageerror', error => errors.push(error.message));
      let failure = 0;
      await page.route('**/gateway/**', async route => {
        const url = route.request().url();
        if (failure && url.includes('/admin/')) return route.fulfill({
          status: failure,
          body: '{"private":"must-not-display"}'
        });
        await route.fulfill({
          json: url.endsWith('/sign-in') ? session : url.endsWith('/jobs') ? jobs : dashboard
        });
      });
      await page.goto(process.env.MONITORING_UI_URL || 'http://127.0.0.1:5184');
      await page.getByLabel('Email', {
        exact: true
      }).fill('synthetic@example.invalid');
      await page.getByLabel('Password', {
        exact: true
      }).fill('synthetic-password-only');
      await page.getByRole('button', {
        name: 'Sign in',
        exact: true
      }).click();
      await page.getByRole('heading', {
        name: 'Overview',
        exact: true
      }).waitFor();
      await page.getByText('Healthy components', {
        exact: true
      }).waitFor();
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false);
      assert.equal(await page.locator('img').evaluateAll(images => images.every(image => image.complete && image.naturalWidth > 0)), true);
      await page.screenshot({
        path: path.join(output, 'overview-' + width + '.png'),
        fullPage: true
      });
      await page.getByRole('button', {
        name: 'Processing jobs',
        exact: true
      }).click();
      await page.getByRole('button', {
        name: 'Failed',
        exact: true
      }).click();
      assert.equal(await page.locator('tbody tr').count(), 1);
      await page.getByLabel('Search operation ID or service').fill('not-a-match');
      await page.getByText('No matching jobs.', {
        exact: true
      }).waitFor();
      await page.getByLabel('Search operation ID or service').fill('receipt');
      assert.equal(await page.locator('tbody tr').count(), 1);
      await page.screenshot({
        path: path.join(output, 'jobs-' + width + '.png'),
        fullPage: true
      });
      await page.getByRole('button', {
        name: 'AI & OCR',
        exact: true
      }).click();
      await page.getByRole('heading', {
        name: 'OCR & parsing quality'
      }).waitFor();
      await page.screenshot({
        path: path.join(output, 'usage-' + width + '.png'),
        fullPage: true
      });
      await page.getByRole('button', {
        name: 'Support',
        exact: true
      }).click();
      assert.equal(await page.getByRole('button', {
        name: 'Look up'
      }).isDisabled(), true);
      failure = 503;
      await page.getByRole('button', {
        name: 'Refresh',
        exact: true
      }).click();
      await page.getByRole('alert').filter({
        hasText: 'Monitoring is temporarily unavailable.'
      }).waitFor();
      assert.equal(await page.getByText('Healthy components', {
        exact: true
      }).count(), 0);
      assert.equal(await page.getByText('must-not-display', {
        exact: false
      }).count(), 0);
      failure = 403;
      await page.getByRole('button', {
        name: 'Retry',
        exact: true
      }).click();
      await page.getByRole('heading', {
        name: 'Administrator sign in'
      }).waitFor();
      assert.equal(await page.getByRole('button', {
        name: 'Overview',
        exact: true
      }).isDisabled(), true);
      failure = 0;
      await page.getByLabel('Email', {
        exact: true
      }).fill('synthetic@example.invalid');
      await page.getByLabel('Password', {
        exact: true
      }).fill('synthetic-password-only');
      await page.getByRole('button', {
        name: 'Sign in',
        exact: true
      }).click();
      await page.getByRole('button', {
        name: 'Sign out',
        exact: true
      }).click();
      await page.getByRole('heading', {
        name: 'Administrator sign in'
      }).waitFor();
      assert.deepEqual(await page.evaluate(() => [localStorage.length, sessionStorage.length]), [0, 0]);
      assert.deepEqual(errors, []);
      await page.screenshot({
        path: path.join(output, 'signed-out-' + width + '.png'),
        fullPage: true
      });
      await context.close();
    }
    console.log('Admin browser smoke passed at 1440px and 390px.');
  } finally {
    await browser.close();
  }
}
run().catch(error => {
  console.error(error);
  process.exitCode = 1;
});
