import assert from "node:assert/strict";
import axe from "axe-core";
import { mkdir } from "node:fs/promises";
import { fileURLToPath } from "node:url";

export async function playgroundRegression(browser, baseUrl) {
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, acceptDownloads: true });
  const page = await context.newPage();
  const errors = [], requests = [];
  page.on("pageerror", error => errors.push(error.message));
  page.on("request", request => requests.push(request.url()));
  await page.goto(`${baseUrl}playground.html`);
  await page.locator("#code-editor").waitFor();
  assert.equal(requests.some(url => url.includes("/compiler/")), false, "runtime must load only on Run");
  const exercises = await page.evaluate(() => window.ExerciseCatalog.exercises);
  const editor = page.locator("#code-editor");
  async function run(code) {
    await editor.fill(code);
    await page.locator("#run-code").click();
    await page.waitForFunction(() => !document.querySelector("#run-code").disabled, null, { timeout: 95000 });
    return page.locator("#run-status").textContent();
  }
  async function download(selector) {
    const pending = page.waitForEvent("download");
    await page.locator(selector).click();
    const stream = await (await pending).createReadStream();
    const chunks = []; for await (const chunk of stream) chunks.push(chunk);
    return JSON.parse(Buffer.concat(chunks).toString());
  }
  try {
    for (const exercise of exercises) {
      await page.selectOption("#exercise-select", exercise.id);
      assert.match(await run(exercise.starter), /验收未通过/u, `${exercise.id}: starter must expose defect`);
      assert.ok(await page.locator("#check-results .failed").count() > 0);
      assert.match(await run(exercise.solution), /全部验收通过/u, `${exercise.id}: reference must pass`);
      console.log(`Browser C# exercise verified: ${exercise.id}`);
    }
    assert.equal(await page.evaluate(() => window.ExerciseProgress.stats().passed), exercises.length);
    const backup = await download("#export-exercises");
    await editor.fill("not valid C# <img src=x onerror=alert(1)>");
    assert.match(await page.locator("#current-result").textContent(), /尚未/u);
    assert.match(await run(await editor.inputValue()), /编译失败/u);
    assert.match(await page.locator("#diagnostics").textContent(), /Error/u);
    assert.equal(await page.locator("#diagnostics img").count(), 0);
    await page.locator("#import-exercises").setInputFiles({ name: "exercises.json", mimeType: "application/json", buffer: Buffer.from(JSON.stringify(backup)) });
    await page.waitForFunction(() => document.querySelector("#run-status").textContent.includes("备份已恢复"));
    assert.equal(await page.evaluate(() => window.ExerciseProgress.stats().passed), exercises.length);
    await page.reload();
    assert.match(await page.locator("#current-result").textContent(), /已通过/u);

    await page.selectOption("#exercise-select", exercises[0].id);
    const reference = exercises[0].solution;
    const infinite = reference.replace("public void Publish(int value) => Changed?.Invoke(value);", "public void Publish(int value) { while (true) { } }");
    assert.match(await run(infinite), /执行超时/u);
    await page.locator("#run-code").click();
    await page.waitForFunction(() => document.querySelector("#run-status").textContent.includes("执行中"), null, { timeout: 90000 });
    await page.locator("#stop-code").click();
    assert.match(await page.locator("#run-status").textContent(), /已停止/u);
    assert.match(await run(reference), /全部验收通过/u, "fresh Worker after stop must run normally");
    const noisy = reference.replace("public void Publish(int value) => Changed?.Invoke(value);", "public void Publish(int value) { Console.Write(new string('x', 70000)); Changed?.Invoke(value); }");
    assert.match(await run(noisy), /全部验收通过/u);
    assert.match(await page.locator("#output").textContent(), /输出已达到 64 KB/u);
    const exception = reference + '\npublic static class Failure { [System.Runtime.CompilerServices.ModuleInitializer] public static void Start() => throw new System.Exception("<img src=x>"); }';
    assert.match(await run(exception), /验收未通过/u);
    assert.match(await page.locator("#output").textContent(), /运行异常/u);
    assert.equal(await page.locator("#output img").count(), 0);
    assert.match(await run("/".repeat(102401)), /源码超过/u);

    // An actual C# 14 extension block, compiled and invoked in the shipping Worker.
    const csharp14 = await page.evaluate(() => new Promise((resolve, reject) => {
      const worker = new Worker(new URL("assets/compiler-worker.js", location.href), { type: "module" });
      const timeout = setTimeout(() => { worker.terminate(); reject(new Error("C# 14 probe timed out")); }, 90000);
      worker.onmessage = ({ data }) => { if (["done", "error"].includes(data.stage)) { clearTimeout(timeout); worker.terminate(); resolve(data); } };
      worker.onerror = event => { clearTimeout(timeout); worker.terminate(); reject(new Error(event.message)); };
      worker.postMessage({ source: "public static class Extensions { extension(int value) { public bool IsPositive() => value > 0; } }", checks: 'public static class ExerciseChecks { public static string[] Run() => [(3).IsPositive() ? "+C#14" : "-C#14"]; }' });
    }));
    assert.equal(csharp14.result?.checks[0]?.passed, true, JSON.stringify(csharp14));

    await page.locator("#reset-code").click();
    assert.equal(await editor.inputValue(), exercises[0].starter);
    // Saved old question IDs and versions retain review records while new questions become due.
    await page.goto(`${baseUrl}quiz.html`);
    const oldIds = await page.evaluate(() => {
      const old = window.PatternCatalog.quizzes.slice(0, 6);
      for (const question of old) window.ReviewScheduler.record(question.id, true);
      return old.map(question => question.id);
    });
    await page.reload();
    for (const id of oldIds) assert.equal(await page.evaluate(id => window.ReviewScheduler.questionState(id).attempts, id), 1);
    await page.locator("#practice-all").click();
    let simpleFound = false;
    for (let index = 0; index < (await page.evaluate(() => window.PatternCatalog.quizzes.length)); index++) {
      const simple = page.locator('#question-options input[value="simple"]');
      if (await simple.count()) {
        await simple.check(); await page.locator(".submit-answer").click();
        assert.match(await page.locator("#feedback-title").textContent(), /判断正确/u);
        assert.match(await page.locator("#feedback-lesson").getAttribute("href"), /^guides\//u);
        simpleFound = true; break;
      }
      await page.locator("#question-options input").first().check(); await page.locator(".submit-answer").click(); await page.locator("#next-question").click();
    }
    assert.ok(simpleFound, "must offer no-pattern choice");
    await page.goto(baseUrl);
    const fullBackup = await download("#export-progress");
    assert.ok(fullBackup.exercises);
    const oldBackup = structuredClone(fullBackup); delete oldBackup.exercises;
    await page.locator("#import-progress").setInputFiles({ name: "old.json", mimeType: "application/json", buffer: Buffer.from(JSON.stringify(oldBackup)) });
    await page.waitForFunction(() => document.querySelector("#progress-toast-message").textContent.includes("已从备份导入"));
    assert.deepEqual(await page.evaluate(() => window.ExerciseProgress.exportData()), fullBackup.exercises, "old backup must retain new exercise records");
    await page.goto(`${baseUrl}playground.html`);
    await page.evaluate(() => { Storage.prototype.setItem = () => { throw new DOMException("Quota", "QuotaExceededError"); }; });
    await editor.fill(reference + "\n// unsaved draft");
    assert.equal(await page.locator("#storage-warning").isVisible(), true);
    const emergency = await download("#storage-warning button");
    assert.ok(emergency.entries[exercises[0].id].code.endsWith("// unsaved draft"));
    await page.reload();
    await page.locator("#import-exercises").setInputFiles({ name: "rescue.json", mimeType: "application/json", buffer: Buffer.from(JSON.stringify(emergency)) });
    await page.waitForFunction(() => document.querySelector("#run-status").textContent.includes("备份已恢复"));
    assert.ok((await editor.inputValue()).endsWith("// unsaved draft"));
    for (const width of [1440, 390]) {
      await page.setViewportSize({ width, height: 900 });
      await page.addScriptTag({ content: axe.source });
      const issues = await page.evaluate(async () => (await window.axe.run()).violations.filter(v => ["critical", "serious"].includes(v.impact)).map(v => ({ id: v.id, nodes: v.nodes.map(n => n.target) })));
      assert.deepEqual(issues, [], `playground a11y at ${width}px`);
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth + 1), true, `playground overflow at ${width}px`);
      await mkdir(new URL("../output/site-regression/", import.meta.url), { recursive: true });
      await page.screenshot({ path: fileURLToPath(new URL(`../output/site-regression/playground-${width}.png`, import.meta.url)), fullPage: true });
    }
    // Advance page timers while a deliberately stalled Worker exercises each deadline.
    for (const stage of ["load", "compile"]) {
      const stalled = await context.newPage();
      await stalled.clock.install();
      await stalled.addInitScript(stage => {
        window.Worker = class {
          postMessage() { if (stage === "compile") queueMicrotask(() => this.onmessage({ data: { stage: "compile" } })); }
          terminate() { }
        };
      }, stage);
      await stalled.goto(`${baseUrl}playground.html`);
      await stalled.locator("#run-code").click();
      await stalled.clock.fastForward(stage === "load" ? 60001 : 30001);
      assert.match(await stalled.locator("#run-status").textContent(), stage === "load" ? /加载运行时超时/u : /编译超时/u);
      assert.equal(await stalled.locator("#run-code").isEnabled(), true);
      await stalled.close();
    }
    assert.deepEqual(errors, []);
    console.log("Playground regression passed: C# 14, all exercises, limits, stop/restart, backups, legacy review, mobile and accessibility.");
  } finally { await context.close(); }
}
