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
  for (let attempt = 0; attempt < 50; attempt += 1) {
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
  const results = await page.evaluate(async () => window.axe.run(document, { resultTypes: ["violations"] }));
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

async function downloadJson(page, selector) {
  const downloadPromise = page.waitForEvent("download");
  await page.locator(selector).click();
  const download = await downloadPromise;
  const stream = await download.createReadStream();
  const chunks = [];
  for await (const chunk of stream) chunks.push(chunk);
  return JSON.parse(Buffer.concat(chunks).toString("utf8"));
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
  browser = await chromium.launch({ executablePath: browserExecutable(), headless: true });
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, acceptDownloads: true });
  const errors = [];
  const page = await context.newPage();
  activePage = page;
  watchPage(page, errors);

  await page.goto(baseUrl);
  await page.evaluate(() => localStorage.clear());
  await page.evaluate(() => {
    localStorage.setItem("csharp-design-patterns-progress-v2", JSON.stringify({ version: 2, items: null }));
    localStorage.setItem("csharp-design-patterns-progress-v1", JSON.stringify(["adapter"]));
  });
  await page.reload();
  await page.locator('.pattern-card[data-progress-id="pattern:adapter"]').waitFor();
  assert.match(await page.locator('.pattern-card[data-progress-id="pattern:adapter"] .pattern-task-link').textContent(), /1 \/ 4/u, "损坏 v2 应回退迁移合法 v1 进度");

  for (const [level, expected] of [[1, 1], [2, 2], [3, 7], [4, 8]]) {
    await page.evaluate(({ legacyLevel }) => {
      localStorage.clear();
      localStorage.setItem("csharp-design-patterns-progress-v2", JSON.stringify({
        version: 2,
        items: { "lab:checkout-refactoring": { level: legacyLevel } },
      }));
    }, { legacyLevel: level });
    await page.reload();
    assert.match(
      await page.locator('[data-learning-summary="lab:checkout-refactoring"] span').textContent(),
      new RegExp(`${expected} / 8`, "u"),
      `旧 level ${level} 应按 read/run/change/verify 语义迁移`,
    );
  }

  await page.evaluate(() => localStorage.clear());
  await page.reload();
  await page.locator(".pattern-card").first().waitFor();
  assert.equal(await page.locator(".pattern-card").count(), 23, "桌面首页应渲染 23 张模式卡片");
  assert.equal(await page.locator("#progress-track").getAttribute("aria-valuemax"), "116");
  assert.match(await page.locator("#progress-stage-summary").textContent(), /0 \/ 116.*0 \/ 28/u);
  assert.equal(await page.locator("#review-due-count").textContent(), "6");

  await page.goto(`${baseUrl}?category=constructor`);
  await page.locator(".pattern-card").first().waitFor();
  assert.equal(await page.locator(".pattern-card").count(), 23, "继承属性名不能被当成合法分类");
  assert.ok(!new URL(page.url()).searchParams.has("category"), "无效分类应从 URL 中移除");
  await page.goto(baseUrl);
  await page.locator('[data-category="Creational"]').click();
  assert.equal(await page.locator(".pattern-card").count(), 5, "创建型筛选应返回 5 种模式");
  await page.locator('[data-category="all"]').click();
  await page.locator("#pattern-search").fill("旧接口");
  assert.equal(await page.locator(".pattern-card").count(), 1, "问题别名搜索应只命中 Adapter");
  assert.match(await page.locator(".pattern-card h3").textContent(), /Adapter/u);
  await page.locator("#pattern-search").fill("");

  await page.locator('[data-open-pattern="adapter"]').click();
  await page.locator("#pattern-dialog").waitFor({ state: "visible" });
  assert.match(page.url(), /[?&]pattern=adapter/u);
  assert.equal(await page.locator("#dialog-progress-summary").textContent(), "0 / 4");
  await page.keyboard.press("Escape");
  await page.waitForFunction(() => !document.querySelector("#pattern-dialog").open);

  await page.locator("#site-search-input").fill("幂等");
  await page.locator("#site-search-form").evaluate((form) => form.scrollIntoView());
  await page.locator('#site-search-form button[type="submit"]').click();
  await page.waitForFunction(() => document.querySelector("#site-search-status").textContent.includes("找到"));
  assert.ok(await page.locator("#site-search-results li").count() > 0, "全文搜索应找到幂等相关章节");
  assert.ok(await page.locator('#site-search-results a[href*="reliable-checkout"]').count() > 0, "幂等搜索应命中可靠结账指南");
  await page.locator("#site-search-input").fill("<img src=x>");
  await page.locator('#site-search-form button[type="submit"]').click();
  assert.equal(await page.locator("#site-search-results img").count(), 0, "搜索词不得注入 HTML");
  await assertNoSeriousAxeViolations(page, "首页");

  await page.goto(`${baseUrl}patterns/adapter.html`);
  const evidence = page.locator("[data-progress-task]");
  assert.equal(await evidence.count(), 4);
  assert.equal(await evidence.nth(0).isEnabled(), true);
  assert.equal(await evidence.nth(1).isDisabled(), true, "后一项证据应锁定");
  for (let index = 0; index < 4; index += 1) {
    await evidence.nth(index).check();
    assert.equal(await evidence.nth(index).isChecked(), true);
  }
  assert.equal(await page.locator("#lesson-progress-count").textContent(), "4 / 4");
  assert.match(await page.locator("#lesson-progress-summary").textContent(), /1 \/ 28 已验证/u);
  await evidence.nth(1).uncheck();
  assert.equal(await evidence.nth(0).isChecked(), true);
  assert.equal(await evidence.nth(2).isChecked(), false, "撤销前项应级联撤销后续证据");
  await evidence.nth(1).check();
  await evidence.nth(2).check();
  await evidence.nth(3).check();
  await assertNoSeriousAxeViolations(page, "模式课件");

  await page.goto(baseUrl);
  await page.locator('.pattern-card[data-progress-id="pattern:adapter"]').waitFor();
  assert.ok(await page.locator('.pattern-card[data-progress-id="pattern:adapter"]').evaluate((card) => card.classList.contains("learned")));
  assert.match(await page.locator("#progress-stage-summary").textContent(), /4 \/ 116.*1 \/ 28/u);
  const backup = await downloadJson(page, "#export-progress");
  assert.equal(backup.format, "csharp-design-patterns-learning-backup");
  assert.equal(backup.progress.version, 3);
  assert.equal(backup.review.version, 1);
  await page.locator("#reset-progress").click();
  assert.match(await page.locator("#progress-stage-summary").textContent(), /0 \/ 116/u);
  await page.locator("#import-progress").setInputFiles({
    name: "learning-backup.json",
    mimeType: "application/json",
    buffer: Buffer.from(JSON.stringify(backup)),
  });
  await page.waitForFunction(() => document.querySelector("#progress-stage-summary").textContent.startsWith("4 / 116"));
  await page.locator("#reset-progress").click();
  await page.locator("#undo-reset").click();
  assert.match(await page.locator("#progress-stage-summary").textContent(), /4 \/ 116/u, "清空后应可撤销");

  const syncPage = await context.newPage();
  watchPage(syncPage, errors);
  await syncPage.goto(`${baseUrl}patterns/adapter.html`);
  await syncPage.locator('[data-progress-task="read"]').uncheck();
  await page.waitForFunction(() => !document.querySelector('.pattern-card[data-progress-id="pattern:adapter"]').classList.contains("learned"));
  await syncPage.close();

  await page.goto(`${baseUrl}guides/online-store.html`);
  await page.locator(".guide-milestones").waitFor();
  const milestones = page.locator(".guide-milestone [data-progress-task]");
  assert.equal(await milestones.count(), 4);
  assert.equal(await milestones.nth(1).isDisabled(), true);
  await milestones.nth(0).check();
  assert.equal(await milestones.nth(1).isEnabled(), true);
  assert.match(await page.locator("#guide-milestone-summary").textContent(), /1 \/ 4/u);
  assert.ok(await page.locator(".toc-chapters > li .toc-sections").count() > 0, "指南目录应按 H2/H3 分组");

  await page.goto(`${baseUrl}quiz.html`);
  await page.locator("#question-options input").first().waitFor();
  assert.equal(await page.locator("#quiz-due").textContent(), "6");
  assert.equal(await page.locator(".submit-answer").isDisabled(), true);
  const correctKey = await page.evaluate(() => {
    const title = document.querySelector("#question-title").textContent;
    return window.PatternCatalog.quizzes.find((quiz) => quiz.title === title).correctKey;
  });
  await page.locator(`#question-options input[value="${correctKey}"]`).check();
  assert.equal(await page.locator(".submit-answer").isEnabled(), true);
  await page.locator(".submit-answer").click();
  assert.match(await page.locator("#feedback-title").textContent(), /判断正确/u);
  assert.equal(await page.locator("#quiz-due").textContent(), "5");
  await assertNoSeriousAxeViolations(page, "辨析训练");

  await page.goto(baseUrl);
  await page.locator("#review-due-count").waitFor();
  assert.equal(await page.locator("#review-due-count").textContent(), "5", "首页应同步复习队列");
  assert.deepEqual(errors, [], `桌面页面发生脚本错误：\n${errors.join("\n")}`);

  const mobileErrors = [];
  const mobile = await context.newPage();
  activePage = mobile;
  watchPage(mobile, mobileErrors);
  await mobile.setViewportSize({ width: 390, height: 844 });
  await mobile.goto(baseUrl);
  await mobile.locator(".pattern-card").first().waitFor();
  assert.equal(await mobile.locator(".pattern-card").count(), 6, "移动端默认只显示前 6 种模式");
  assert.equal(await mobile.locator("#pattern-list-toggle").isVisible(), true);
  await mobile.locator("#pattern-list-toggle").click();
  assert.equal(await mobile.locator(".pattern-card").count(), 23);
  await mobile.locator("#pattern-search").fill("状态");
  assert.ok(await mobile.locator(".pattern-card").count() >= 1, "筛选结果不应被六项折叠截断");
  assert.equal(await mobile.locator("#terminal-toggle").getAttribute("aria-expanded"), "false");
  assert.equal(await mobile.locator("#terminal-body").isHidden(), true, "终端输出应与折叠状态一致");
  const touchSizes = await mobile.locator(".filter-button").evaluateAll((buttons) => buttons.map((button) => button.getBoundingClientRect().height));
  assert.ok(touchSizes.every((height) => height >= 44), `分类按钮触控高度不足：${touchSizes.join(",")}`);
  await mobile.locator("#nav-toggle").click();
  assert.equal(await mobile.locator("#nav-toggle").getAttribute("aria-expanded"), "true");
  await assertNoDocumentOverflow(mobile, "移动端首页");

  const guideUrl = `${baseUrl}guides/fundamentals.html#32-%E7%AC%AC-2-%E7%AB%A0-adapter%E9%80%82%E9%85%8D%E5%99%A8%E5%A1%AB%E5%B9%B3%E6%8E%A5%E5%8F%A3%E4%B8%8E%E5%8D%95%E4%BD%8D%E5%B7%AE%E5%BC%82`;
  await mobile.goto(guideUrl);
  await mobile.locator(".learning-layout").waitFor();
  await assertNoDocumentOverflow(mobile, "移动端基础教程");
  const tableDimensions = await mobile.locator(".table-wrap").first().evaluate((element) => ({
    clientWidth: element.clientWidth,
    scrollWidth: element.scrollWidth,
    overflowX: getComputedStyle(element).overflowX,
  }));
  assert.ok(tableDimensions.scrollWidth > tableDimensions.clientWidth, "宽表格应在自己的容器内滚动");
  assert.equal(tableDimensions.overflowX, "auto");
  const anchorPosition = await mobile.locator('[id="32-第-2-章-adapter适配器填平接口与单位差异"]').evaluate((heading) => {
    const headerBottom = document.querySelector(".learning-site-header").getBoundingClientRect().bottom;
    return { top: heading.getBoundingClientRect().top, headerBottom };
  });
  assert.ok(anchorPosition.top > anchorPosition.headerBottom, `深链接标题被固定导航遮挡：${JSON.stringify(anchorPosition)}`);
  await assertNoSeriousAxeViolations(mobile, "移动端基础教程");

  await mobile.goto(`${baseUrl}patterns/adapter.html`);
  await mobile.locator(".lesson-main").waitFor();
  await assertNoDocumentOverflow(mobile, "移动端模式课件");
  assert.equal(await mobile.locator(".related-list a").count(), 3);
  assert.deepEqual(mobileErrors, [], `移动端页面发生脚本错误：\n${mobileErrors.join("\n")}`);
  await context.close();

  const noScriptContext = await browser.newContext({ viewport: { width: 390, height: 844 }, javaScriptEnabled: false });
  const noScriptPage = await noScriptContext.newPage();
  activePage = noScriptPage;
  await noScriptPage.goto(baseUrl);
  assert.equal(await noScriptPage.locator(".pattern-card").count(), 23, "禁用 JavaScript 后仍应显示 23 个静态课件入口");
  assert.ok(await noScriptPage.locator("#site-nav").isVisible(), "禁用 JavaScript 后移动导航应保持可用");
  await noScriptPage.goto(`${baseUrl}guides/fundamentals.html`);
  assert.ok(await noScriptPage.locator(".learning-site-nav").isVisible());
  assert.ok(await noScriptPage.locator(".guide-toc-list").isVisible());
  assert.ok(await noScriptPage.locator(".toc-sections").count() > 0);
  await noScriptPage.goto(`${baseUrl}quiz.html`);
  assert.ok(await noScriptPage.locator("noscript").isVisible());
  await noScriptContext.close();

  console.log("Site regression passed: 116 tasks, backups, full-text search, milestones, quizzes, mobile/a11y, grouped guides, and no-JS navigation.");
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
