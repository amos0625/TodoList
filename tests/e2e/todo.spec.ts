import { test, expect } from '../../apps/web/node_modules/@playwright/test';

test.beforeEach(async ({ request }) => {
  const response = await request.get('/api/v1/todos?pageSize=100');
  for (const todo of (await response.json()).data) await request.delete(`/api/v1/todos/${todo.id}`);
});

test('create, edit, persist, complete, filter and delete', async ({ page }, testInfo) => {
  await page.goto('/');
  await page.getByRole('button', { name: '＋ 新增任務' }).click();
  await page.getByLabel('任務標題').fill('完成首頁');
  await page.getByLabel('備註').fill('手機版也要好用');
  await page.getByLabel('優先級', { exact: true }).selectOption('high');
  await page.getByRole('button', { name: '儲存任務' }).click();
  await expect(page.getByRole('heading', { name: '完成首頁', exact: true })).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath('todo-desktop.png'), fullPage: true });
  await page.reload();
  await expect(page.getByRole('heading', { name: '完成首頁', exact: true })).toBeVisible();
  await page.getByRole('button', { name: '編輯：完成首頁' }).click();
  await page.getByLabel('任務標題').fill('完成響應式首頁');
  await page.getByRole('button', { name: '儲存任務' }).click();
  await page.getByRole('checkbox', { name: '完成任務：完成響應式首頁' }).click();
  await expect(page.getByRole('checkbox')).toBeChecked();
  await page.getByRole('button', { name: '未完成', exact: true }).click();
  await expect(page.getByText('沒有符合條件的任務')).toBeVisible();
  await page.getByRole('button', { name: '已完成', exact: true }).click();
  await page.getByRole('button', { name: '刪除：完成響應式首頁' }).click();
  await expect(page.getByRole('button', { name: '取消', exact: true })).toBeFocused();
  await page.getByRole('button', { name: '確認刪除' }).click();
  await expect(page.getByRole('heading', { name: '完成響應式首頁', exact: true })).toHaveCount(0);
});

test('plain text content, URL restoration and mobile layout', async ({ page, request }) => {
  await request.post('/api/v1/todos', { data: { title: '<script>alert(1)</script>', description: '長文字'.repeat(100), priority: 'high' } });
  await page.goto('/?status=pending&priority=high');
  await expect(page.getByRole('heading', { name: '<script>alert(1)</script>', exact: true })).toBeVisible();
  await page.reload();
  await expect(page.getByLabel('篩選優先級')).toHaveValue('high');
  for (const width of [320, 390, 768, 1280]) {
    await page.setViewportSize({ width, height: 900 });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    await expect(page.getByRole('button', { name: '＋ 新增任務' })).toBeVisible();
  }
});

test('dialog supports Escape and restores focus', async ({ page }) => {
  await page.goto('/');
  const add = page.getByRole('button', { name: '＋ 新增任務' });
  await add.click(); await expect(page.getByLabel('任務標題')).toBeFocused();
  await page.keyboard.press('Escape'); await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(add).toBeFocused();
});

test('failed save preserves form input and allows retry', async ({ page }) => {
  await page.goto('/');
  await page.route('**/api/v1/todos', async route => {
    if (route.request().method() === 'POST') await route.fulfill({ status: 503, json: { error: { message: '資料庫暫時無法使用', details: [] } } });
    else await route.continue();
  });
  await page.getByRole('button', { name: '＋ 新增任務' }).click();
  await page.getByLabel('任務標題').fill('失敗後保留');
  await page.getByRole('button', { name: '儲存任務' }).click();
  await expect(page.getByRole('alert')).toContainText('資料庫暫時無法使用');
  await expect(page.getByLabel('任務標題')).toHaveValue('失敗後保留');
  await page.unroute('**/api/v1/todos');
  await page.getByRole('button', { name: '儲存任務' }).click();
  await expect(page.getByRole('heading', { name: '失敗後保留', exact: true })).toBeVisible();
});

test('saved task survives refresh failure without duplicate submission', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByText('從一件小事開始')).toBeVisible();
  await page.getByRole('button', { name: '＋ 新增任務' }).click();
  await page.getByLabel('任務標題').fill('已儲存但更新失敗');
  await page.route('**/api/v1/todos?*', route => route.fulfill({ status: 503, json: { error: { message: '讀取失敗' } } }));
  await page.getByRole('button', { name: '儲存任務' }).click();
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(page.getByRole('alert')).toContainText('已儲存，列表更新失敗');
  await page.unroute('**/api/v1/todos?*');
  await page.getByRole('button', { name: '重新載入' }).click();
  await expect(page.getByRole('heading', { name: '已儲存但更新失敗', exact: true })).toHaveCount(1);
});

test('deleting the last task on a page returns to a valid page', async ({ page, request }) => {
  await request.post('/api/v1/todos', { data: { title: '第一件' } });
  await request.post('/api/v1/todos', { data: { title: '第二件' } });
  await page.goto('/?page=2&pageSize=1');
  await page.getByRole('button', { name: /^刪除：/ }).click();
  await page.getByRole('button', { name: '取消', exact: true }).click();
  await expect(page.getByRole('listitem')).toHaveCount(1);
  await page.getByRole('button', { name: /^刪除：/ }).click();
  await page.getByRole('button', { name: '確認刪除' }).click();
  await expect(page).toHaveURL(/page=1&/);
  await expect(page.getByRole('listitem')).toHaveCount(1);
});
