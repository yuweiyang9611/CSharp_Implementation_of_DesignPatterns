import assert from "node:assert/strict";
import { existsSync } from "node:fs";
import { mkdir } from "node:fs/promises";
import { createServer } from "node:net";
import { join, resolve } from "node:path";
import { spawn } from "node:child_process";
import axe from "axe-core";
import { chromium } from "playwright-core";

const repositoryRoot = resolve(import.meta.dirname, "..");
const siteRoot = resolve(process.argv[2] ?? join(repositoryRoot, "output", "pages-site"));
const artifactRoot = join(repositoryRoot, "output", "site-regression");

function browserExecutable() {
  const candidates = [
    process.env.CHROME_PATH,
    process.env.CHROME_BIN,
    process.platform === "win32" && join(process.env.PROGRAMFILES ?? "", "Google", "Chrome", "Application", "chrome.exe"),
    process.platform === "win32" && join(process.env["PROGRAMFILES(X86)"] ?? "", "Microsoft", "Edge", "Application", "msedge.exe"),
    process.platform === "win32" && join(process.env.LOCALAPPDATA ?? "", "Google", "Chrome", "Application", "chrome.exe"),
    process.platform === "darwin" && "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
    process.platform === "darwin" && "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge",
    process.platform === "linux" && "/usr/bin/google-chrome",
    process.platform === "linux" && "/usr/bin/google-chrome-stable",
    process.platform === "linux" && "/usr/bin/chromium",
    process.platform === "linux" && "/usr/bin/chromium-browser",
  ].filter(Boolean);
  const executable = candidates.find(existsSync);
  assert.ok(executable, `没有找到 Chrome/Edge。已检查：${candidates.join(", ")}`);
  return executable;
}

async function reservePort() {
  const probe = createServer();
  await new Promise((resolveListen, reject) => {
    probe.once("error", reject);
    probe.listen(0, "127.0.0.1", resolveListen);
  });
  const { port } = probe.address();
  await new Promise((resolveClose, reject) => probe.close((error) => error ? reject(error) : resolveClose()));
  return port;
}

async function waitForServer(url, processHandle) {
  for (let attempt = 0; attempt < 40; attempt += 1) {
    if (processHandle.exitCode !== null) throw new Error(`静态服务器提前退出：${processHandle.exitCode}`);
    try {
      const response = await fetch(url);
      if (response.ok) return;
    } catch {
      // The server may still be binding its port.
    }
    await new Promise((resolveDelay) => setTimeout(resolveDelay, 100));
  }
  throw new Error(`静态服务器未能启动：${url}`);
}

function watchPage(page, errors) {
  page.on("pageerror", (error) => errors.push(`pageerror: ${error.message}`));
  page.on("console", (message) => {
    if (message.type() === "error") errors.push(`console: ${message.text()}`);
  });
}

async function assertNoSeriousAxeViolations(page, label) {
  await page.addScriptTag({ content: axe.source });
  const results = await page.evaluate(async () => window.axe.run(document, {
    resultTypes: ["violations"],
  }));
  const violations = results.violations.filter((item) => ["serious", "critical"].includes(item.impact));
  assert.deepEqual(
    violations.map((item) => ({ id: item.id, impact: item.impact, targets: item.nodes.map((node) => node.target) })),
    [],
    `${label} 存在严重可访问性问题`,
  );
}

async function assertNoDocumentOverflow(page, label) {
  const dimensions = await page.evaluate(() => ({
    clientWidth: document.documentElement.clientWidth,
    scrollWidth: document.documentElement.scrollWidth,
  }));
  assert.ok(dimensions.scrollWidth <= dimensions.clientWidth + 1, `${label} 横向溢出：${JSON.stringify(dimensions)}`);
}

const port = await reservePort();
const baseUrl = `http://127.0.0.1:${port}/`;
const server = spawn(process.execPath, [join(repositoryRoot, "tests", "site-server.mjs"), siteRoot, String(port)], {
  cwd: repositoryRoot,
  stdio: ["ignore", "pipe", "pipe"],
});
let serverDiagnostics = "";
server.stdout.on("data", (chunk) => { serverDiagnostics += chunk; });
server.stderr.on("data", (chunk) => { serverDiagnostics += chunk; });

let browser;
let activePage;
try {
  await waitForServer(baseUrl, server);
  browser = await chromium.launch({
    executablePath: browserExecutable(),
    headless: true,
  });
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
  const errors = [];
  activePage = await context.newPage();
  watchPage(activePage, errors);

  await activePage.goto(baseUrl);
  await activePage.evaluate(() => localStorage.clear());
  await activePage.reload();
  await activePage.locator(".pattern-card").first().waitFor();
  await activePage.evaluate(() => {
    localStorage.setItem("csharp-design-patterns-progress-v2", JSON.stringify({ version: 2, items: null }));
    localStorage.setItem("csharp-design-patterns-progress-v1", JSON.stringify(["adapter"]));
  });
  await activePage.reload();
  await activePage.locator(".pattern-card").first().waitFor();
  assert.equal(await activePage.locator('[data-progress-id="pattern:adapter"]').inputValue(), "1", "损坏的 v2 应回退迁移合法 v1 进度");
  await activePage.evaluate(() => localStorage.clear());
  await activePage.reload();
  await activePage.locator(".pattern-card").first().waitFor();
  assert.equal(await activePage.locator(".pattern-card").count(), 23, "首页应渲染 23 张模式卡片");
  assert.equal(await activePage.locator("#progress-track").getAttribute("aria-valuemax"), "112");
  assert.match(await activePage.locator("#progress-stage-summary").textContent(), /0 \/ 112.*0 \/ 28/u);
  await activePage.goto(`${baseUrl}?category=constructor`);
  await activePage.locator(".pattern-card").first().waitFor();
  assert.equal(await activePage.locator(".pattern-card").count(), 23, "继承属性名不能被当成合法分类");
  assert.ok(!new URL(activePage.url()).searchParams.has("category"), "无效分类应从 URL 中移除");
  await activePage.goto(baseUrl);
  await activePage.locator(".pattern-card").first().waitFor();

  await activePage.locator('[data-category="Creational"]').click();
  assert.equal(await activePage.locator(".pattern-card").count(), 5, "创建型筛选应返回 5 种模式");
  await activePage.locator('[data-category="all"]').click();
  await activePage.locator("#pattern-search").fill("旧接口");
  assert.equal(await activePage.locator(".pattern-card").count(), 1, "问题别名搜索应只命中 Adapter");
  assert.match(activePage.url(), /[?&]q=%E6%97%A7%E6%8E%A5%E5%8F%A3/u);
  assert.match(await activePage.locator(".pattern-card h3").textContent(), /Adapter/u);
  await activePage.locator("#pattern-search").fill("");

  await activePage.locator('[data-open-pattern="adapter"]').click();
  await activePage.locator("#pattern-dialog").waitFor({ state: "visible" });
  assert.match(activePage.url(), /[?&]pattern=adapter/u);
  assert.match(await activePage.locator("#dialog-title").textContent(), /Adapter/u);
  await activePage.locator("#dialog-progress").selectOption("2");
  assert.match(await activePage.locator("#progress-stage-summary").textContent(), /2 \/ 112/u);
  await activePage.keyboard.press("Escape");
  await activePage.waitForFunction(() => !document.querySelector("#pattern-dialog").open);
  assert.ok(!new URL(activePage.url()).searchParams.has("pattern"), "关闭对话框后应移除 pattern 查询参数");

  await activePage.reload();
  await activePage.locator(".pattern-card").first().waitFor();
  assert.equal(await activePage.locator('[data-progress-id="pattern:adapter"]').inputValue(), "2", "进度应在刷新后保留");
  await activePage.locator("#reset-progress").click();
  assert.match(await activePage.locator("#progress-stage-summary").textContent(), /0 \/ 112/u);
  await activePage.locator("#undo-reset").click();
  assert.match(await activePage.locator("#progress-stage-summary").textContent(), /2 \/ 112/u);

  const syncPage = await context.newPage();
  watchPage(syncPage, errors);
  await syncPage.goto(baseUrl);
  await syncPage.locator('[data-progress-id="pattern:adapter"]').selectOption("4");
  await activePage.waitForFunction(() => document.querySelector('[data-progress-id="pattern:adapter"]').closest(".pattern-card").classList.contains("learned"));
  await syncPage.locator('[data-progress-id="pattern:adapter"]').selectOption("2");
  await activePage.waitForFunction(() => !document.querySelector('[data-progress-id="pattern:adapter"]').closest(".pattern-card").classList.contains("learned"));
  await syncPage.close();

  await assertNoSeriousAxeViolations(activePage, "首页");
  assert.deepEqual(errors, [], `首页发生脚本错误：\n${errors.join("\n")}`);

  const mobile = await context.newPage();
  activePage = mobile;
  const mobileErrors = [];
  watchPage(mobile, mobileErrors);
  await mobile.setViewportSize({ width: 390, height: 844 });
  await mobile.goto(baseUrl);
  await mobile.locator(".pattern-card").first().waitFor();
  await assertNoDocumentOverflow(mobile, "移动端首页");
  assert.equal(await mobile.locator("#nav-toggle").getAttribute("aria-expanded"), "false");
  await mobile.locator("#nav-toggle").click();
  assert.equal(await mobile.locator("#nav-toggle").getAttribute("aria-expanded"), "true");
  assert.ok(await mobile.locator("#site-nav").isVisible(), "移动端菜单应可见");

  const guideUrl = `${baseUrl}guides/fundamentals.html#32-%E7%AC%AC-2-%E7%AB%A0-adapter%E9%80%82%E9%85%8D%E5%99%A8%E5%A1%AB%E5%B9%B3%E6%8E%A5%E5%8F%A3%E4%B8%8E%E5%8D%95%E4%BD%8D%E5%B7%AE%E5%BC%82`;
  await mobile.goto(guideUrl);
  await mobile.locator(".learning-layout").waitFor();
  await assertNoDocumentOverflow(mobile, "移动端基础教程");
  const guideHeaderStyle = await mobile.locator(".learning-site-header").evaluate((element) => ({
    display: getComputedStyle(element).display,
    backgroundColor: getComputedStyle(element).backgroundColor,
  }));
  assert.equal(guideHeaderStyle.display, "flex");
  assert.notEqual(guideHeaderStyle.backgroundColor, "rgba(0, 0, 0, 0)");
  const tableDimensions = await mobile.locator(".table-wrap").first().evaluate((element) => ({
    clientWidth: element.clientWidth,
    scrollWidth: element.scrollWidth,
    overflowX: getComputedStyle(element).overflowX,
  }));
  assert.ok(tableDimensions.scrollWidth > tableDimensions.clientWidth, "宽表格应在自己的容器内滚动");
  assert.equal(tableDimensions.overflowX, "auto");
  const anchorPosition = await mobile.locator('[id="32-第-2-章-adapter适配器填平接口与单位差异"]').evaluate((heading) => {
    const headerBottom = document.querySelector(".learning-site-header").getBoundingClientRect().bottom;
    const toc = document.querySelector(".guide-toc");
    const tocStyle = toc ? getComputedStyle(toc) : null;
    const tocBottom = toc && ["fixed", "sticky"].includes(tocStyle.position)
      ? toc.getBoundingClientRect().bottom
      : 0;
    return { top: heading.getBoundingClientRect().top, obstructionBottom: Math.max(headerBottom, tocBottom) };
  });
  assert.ok(anchorPosition.top > anchorPosition.obstructionBottom, `深链接标题被固定导航遮挡：${JSON.stringify(anchorPosition)}`);
  await assertNoSeriousAxeViolations(mobile, "基础教程");

  await mobile.goto(`${baseUrl}guides/pattern-index.html`);
  await mobile.setViewportSize({ width: 920, height: 844 });
  await mobile.setViewportSize({ width: 880, height: 844 });
  assert.equal(await mobile.locator(".guide-toc").count(), 0, "无章节索引页不应生成空目录");

  await mobile.setViewportSize({ width: 390, height: 844 });
  await mobile.goto(`${baseUrl}patterns/adapter.html`);
  await mobile.locator(".lesson-main").waitFor();
  await assertNoDocumentOverflow(mobile, "移动端模式课件");
  assert.equal(await mobile.locator('link[rel="canonical"]').count(), 1);
  assert.equal(await mobile.locator('script[type="application/ld+json"]').count(), 1);
  assert.equal(await mobile.locator(".related-list a").count(), 3);
  await mobile.locator("#lesson-progress").selectOption("4");
  assert.match(await mobile.locator("#lesson-progress-summary").textContent(), /1 \/ 28 已验证/u);
  await assertNoSeriousAxeViolations(mobile, "模式课件");
  assert.deepEqual(mobileErrors, [], `移动端页面发生脚本错误：\n${mobileErrors.join("\n")}`);

  await context.close();

  const noScriptContext = await browser.newContext({
    viewport: { width: 390, height: 844 },
    javaScriptEnabled: false,
  });
  const noScriptPage = await noScriptContext.newPage();
  activePage = noScriptPage;
  await noScriptPage.goto(baseUrl);
  assert.equal(await noScriptPage.locator(".pattern-card").count(), 23, "禁用 JavaScript 后仍应显示 23 个静态课件入口");
  assert.ok(await noScriptPage.locator("#site-nav").isVisible(), "禁用 JavaScript 后移动导航应保持可用");
  await noScriptPage.goto(`${baseUrl}guides/fundamentals.html`);
  assert.ok(await noScriptPage.locator(".learning-site-nav").isVisible(), "禁用 JavaScript 后教程导航应保持可用");
  assert.ok(await noScriptPage.locator(".guide-toc-list").isVisible(), "禁用 JavaScript 后教程目录应保持可用");
  await noScriptContext.close();

  console.log("Site regression passed: catalog, filters, progress, mobile layout, guide anchors, lessons, no-JS navigation, and axe checks.");
} catch (error) {
  await mkdir(artifactRoot, { recursive: true });
  if (activePage && !activePage.isClosed()) {
    await activePage.screenshot({ path: join(artifactRoot, "failure.png"), fullPage: true }).catch(() => {});
  }
  if (serverDiagnostics.trim()) console.error(serverDiagnostics.trim());
  throw error;
} finally {
  if (browser) await browser.close().catch(() => {});
  if (server.exitCode === null) server.kill();
}
