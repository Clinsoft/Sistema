import { Page } from '@playwright/test'

const USUARIO_TESTE = process.env.E2E_USUARIO ?? 'admin@clinsoft.com'
const SENHA_TESTE   = process.env.E2E_SENHA   ?? 'Admin@123'

export async function login(page: Page) {
  await page.goto('/#/login')
  await page.waitForSelector('input[type="email"], input[placeholder*="mail"], input[placeholder*="suário"]', { timeout: 10_000 })
  const emailField = page.locator('input').filter({ hasText: '' }).first()
  await page.fill('input[type="email"]', USUARIO_TESTE).catch(async () => {
    // fallback: preenche o primeiro input de texto
    await page.locator('input').first().fill(USUARIO_TESTE)
  })
  await page.fill('input[type="password"]', SENHA_TESTE)
  await page.locator('button[type="submit"], button:has-text("Entrar")').click()
  await page.waitForURL(/#\/$/, { timeout: 10_000 })
}

export async function irPara(page: Page, rota: string) {
  await page.goto(`/#${rota}`)
  await page.waitForLoadState('networkidle')
}
