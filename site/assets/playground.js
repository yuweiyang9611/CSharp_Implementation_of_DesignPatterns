(() => {
  "use strict";
  const exercises = window.ExerciseCatalog.exercises;
  const progress = window.ExerciseProgress;
  const find = (id) => document.getElementById(id);
  const select = find("exercise-select"), editor = find("code-editor"), run = find("run-code"), stop = find("stop-code");
  let current, worker = null, timer = 0, runningCode = "";
  for (const exercise of exercises) {
    const option = document.createElement("option");
    option.value = exercise.id;
    option.textContent = `${exercise.kind === "debug" ? "改错" : "扩展"} · ${exercise.title}`;
    select.append(option);
  }
  function finish(message) {
    clearTimeout(timer);
    worker?.terminate();
    worker = null;
    run.disabled = false;
    stop.disabled = true;
    find("run-status").textContent = message;
  }
  function stage(name) {
    clearTimeout(timer);
    const stages = { load: [60000, "加载运行时"], compile: [30000, "编译"], execute: [5000, "执行"] };
    const [timeout, label] = stages[name];
    find("run-status").textContent = `${label}中…`;
    timer = setTimeout(() => finish(`${label}超时（${timeout / 1000} 秒），已停止。可修改代码后重新运行。`), timeout);
  }
  function clearResults() {
    find("diagnostics").textContent = "";
    find("output").textContent = "";
    find("check-results").replaceChildren();
  }
  function choose() {
    finish("准备运行");
    clearResults();
    current = exercises.find((item) => item.id === select.value);
    editor.value = progress.get(current.id).code;
    find("exercise-task").textContent = current.task;
    find("reference-code").textContent = current.solution;
    find("reference-explanation").textContent = current.explanation;
    find("reference-answer").open = false;
    const url = new URL(location.href);
    url.searchParams.set("exercise", current.id);
    history.replaceState(null, "", url);
    refresh();
  }
  function refresh() {
    const stats = progress.stats();
    find("exercise-progress").textContent = `${stats.passed} / ${stats.total} 道编码练习已通过`;
    if (current) find("current-result").textContent = (progress.get(current.id).passed && progress.get(current.id).code === editor.value) ? "当前草稿已通过验收" : "当前草稿尚未通过验收";
  }
  function changed() {
    finish("代码已修改，请重新运行验收。");
    clearResults();
    if (!progress.update(current.id, editor.value)) {
      // Preserve the last valid draft but invalidate its old acceptance mark.
      progress.update(current.id, progress.get(current.id).code, false);
      find("run-status").textContent = "源码超过 100 KB，未保存；请缩短代码后运行。";
    }
  }
  editor.addEventListener("input", changed);
  select.addEventListener("change", choose);
  find("reset-code").addEventListener("click", () => { editor.value = current.starter; changed(); });
  stop.addEventListener("click", () => finish("已停止。可重新运行。"));
  run.addEventListener("click", () => {
    finish("准备运行");
    clearResults();
    if (new TextEncoder().encode(editor.value).length > 100 * 1024) { finish("源码超过 100 KB，已拒绝运行。"); return; }
    runningCode = editor.value;
    progress.update(current.id, runningCode, false);
    run.disabled = true;
    stop.disabled = false;
    stage("load");
    try {
      const active = new Worker(new URL("compiler-worker.js", document.querySelector('script[src$="playground.js"]').src), { type: "module" });
      worker = active;
      active.onerror = (event) => { event.preventDefault(); finish(`运行失败：${event.message}`); };
      active.onmessage = ({ data }) => {
        if (worker !== active) return;
        if (data.stage === "compile" || data.stage === "execute") { stage(data.stage); return; }
        if (data.stage === "error") { finish(`运行失败：${data.error}`); return; }
        if (data.stage !== "done") return;
        find("diagnostics").textContent = (data.compilation?.diagnostics ?? []).map((item) => `${item.severity} (${item.line},${item.column}): ${item.message}`).join("\n");
        const result = data.result;
        if (result) {
          find("output").textContent = result.output + (result.outputTruncated ? "\n[输出已达到 64 KB，后续输出已截断]" : "") + (result.error ? `\n运行异常：${result.error}` : "");
          for (const check of result.checks) {
            const item = document.createElement("li");
            item.textContent = `${check.passed ? "通过" : "未通过"} · ${check.name}`;
            item.className = check.passed ? "passed" : "failed";
            find("check-results").append(item);
          }
        }
        const passed = Boolean(result && !result.error && result.checks.length && result.checks.every((check) => check.passed));
        progress.update(current.id, runningCode, passed);
        finish(passed ? "全部验收通过。" : data.compilation?.success ? "运行完成，验收未通过。" : "编译失败，请根据诊断修改代码。");
      };
      active.postMessage({ source: runningCode, checks: current.checks });
    } catch (error) { finish(`无法启动运行环境：${error.message}`); }
  });
  find("export-exercises").addEventListener("click", () => {
    const url = URL.createObjectURL(new Blob([JSON.stringify(progress.exportData(), null, 2)], { type: "application/json" }));
    const link = document.createElement("a"); link.href = url; link.download = "csharp-exercises-backup.json"; link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  });
  find("import-exercises").addEventListener("change", async (event) => {
    const file = event.target.files?.[0]; event.target.value = "";
    if (!file) return;
    try {
      if (file.size > 2_000_000) throw new Error("备份超过 2 MB");
      const payload = JSON.parse(await file.text());
      if (!progress.restore(payload.exercises ?? payload)) throw new Error("无效的编码练习备份");
      choose();
      find("run-status").textContent = "编码练习备份已恢复。";
    } catch (error) { find("run-status").textContent = `无法导入：${error.message}`; }
  });
  const requested = new URL(location.href).searchParams.get("exercise");
  select.value = exercises.some((item) => item.id === requested) ? requested : exercises[0].id;
  choose();
  progress.subscribe(refresh);
  window.addEventListener("pagehide", () => finish("已停止"));
})();
