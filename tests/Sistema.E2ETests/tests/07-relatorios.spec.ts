import { test, expect } from '@playwright/test'
import { login, irPara } from './helpers'

test.describe('Relatórios', () => {
  test.beforeEach(async ({ page }) => { await login(page) })

  test('tela de relatórios lista grupos', async ({ page }) => {
    await irPara(page, '/relatorios')
    await page.waitForTimeout(1000)
    await expect(page.locator('text=Vendas').first()).toBeVisible({ timeout: 5_000 })
    await expect(page.locator('text=Estoque').first()).toBeVisible({ timeout: 5_000 })
    await expect(page.locator('text=Financeiro').first()).toBeVisible({ timeout: 5_000 })
  })

  test('clique em relatório de vendas abre dialog de parâmetros', async ({ page }) => {
    await irPara(page, '/relatorios')
    await page.locator('text=Vendas realizadas').first().click()
    await expect(page.locator('.v-dialog')).toBeVisible({ timeout: 3_000 })
    await page.keyboard.press('Escape')
  })
})
