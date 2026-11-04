// The lesson's source panel: the demo, the interface and the practice file read in place, run in the browser.
(() => {
  "use strict";

  const config = JSON.parse(document.querySelector("#lesson-source")?.textContent ?? "null");
  const root = document.querySelector("#source-panel");
  if (!config || !root || !window.SourcePanel) return;

  const announcement = document.querySelector("#lesson-announcement");
  const panel = window.SourcePanel.create({
    root,
    base: config.base,
    files: config.files,
    active: config.demo.path,
    blobBase: config.blobBase,
  });

  // Run controls sit in the actions row; the output sits under it.
  const runButton = document.createElement("button");
  runButton.type = "button";
  runButton.className = "nb-button nb-button--solid";
  runButton.id = "run-demo";
  runButton.textContent = "运行";
  const editButton = document.createElement("button");
  editButton.type = "button";
  editButton.className = "nb-button";
  editButton.id = "edit-demo";
  editButton.textContent = "改一改再跑";
  const resetButton = document.createElement("button");
  resetButton.type = "button";
  resetButton.className = "nb-button";
  resetButton.id = "reset-demo";
  resetButton.textContent = "恢复原文";
  resetButton.hidden = true;
  const stopButton = document.createElement("button");
  stopButton.type = "button";
  stopButton.className = "nb-button";
  stopButton.id = "stop-demo";
  stopButton.textContent = "停止";
  stopButton.hidden = true;
  panel.actions.prepend(runButton, editButton, resetButton, stopButton);

  const output = document.createElement("div");
  output.className = "nb-code-output";
  output.id = "demo-output";
  output.hidden = true;
  const outputLabel = document.createElement("p");
  outputLabel.className = "nb-code-output-label";
  const outputText = document.createElement("pre");
  outputText.id = "demo-output-text";
  outputText.tabIndex = 0;
  output.append(outputLabel, outputText);
  root.append(output);

  const editor = document.createElement("textarea");
  editor.className = "nb-code-editor";
  editor.id = "demo-editor";
  editor.spellcheck = false;
  editor.setAttribute("aria-label", `${window.SourcePanel.fileName(config.demo.path)} 的可编辑副本`);
  editor.hidden = true;
  panel.view.after(editor);

  let editing = false;
  let original = null;
  let handle = null;

  async function demoSource() {
    original ??= await panel.load(config.demo.path);
    return editing ? editor.value : original;
  }

  function setEditing(next) {
    editing = next;
    resetButton.hidden = !next;
    editButton.hidden = next;
    panel.setOverride(next ? { path: config.demo.path, element: editor, value: () => editor.value } : null);
    if (next) editor.focus();
  }

  editButton.addEventListener("click", async () => {
    original ??= await panel.load(config.demo.path);
    if (!editing) editor.value = original;
    await panel.select(config.demo.path);
    setEditing(true);
    panel.status.textContent = "这是副本，改完点运行；原文件不会变。";
  });

  resetButton.addEventListener("click", async () => {
    editor.value = original ?? "";
    setEditing(false);
    await panel.select(config.demo.path);
    panel.status.textContent = "已恢复原文。";
  });

  function showOutput(state, label, text) {
    output.hidden = false;
    output.dataset.state = state;
    outputLabel.textContent = label;
    outputText.textContent = text;
  }

  async function runDemo() {
    if (handle) return;
    const source = await demoSource();
    runButton.disabled = true;
    stopButton.hidden = false;
    showOutput("running", "运行中", "");
    announcement.textContent = "开始在浏览器里运行这个 Demo。";
    handle = window.DemoRunner.run({
      source,
      typeName: config.demo.type,
      onStage: (name, label) => { panel.status.textContent = `${label}中…`; outputLabel.textContent = `${label}中`; },
    });
    const result = await handle.done;
    handle = null;
    runButton.disabled = false;
    stopButton.hidden = true;
    const diagnostics = result.diagnostics.length ? result.diagnostics.join("\n") : "";
    if (result.ok) {
      showOutput("done", `运行结果 · 浏览器内 .NET${editing ? " · 已改动的副本" : ""}`, result.output.trimEnd() || "（没有输出）");
      panel.status.textContent = "运行完成。对照左边的预期输出，然后勾选「运行并核对输出」。";
      announcement.textContent = "运行完成。";
    } else {
      const label = result.timedOut ? "运行超时" : result.stopped ? "已停止" : result.stage === "compile" ? "编译失败" : "运行失败";
      showOutput("wrong", label, [result.output.trimEnd(), diagnostics, result.error].filter(Boolean).join("\n"));
      panel.status.textContent = result.timedOut ? "超时了。可以改代码后重新运行。" : result.stage === "compile" ? "编译失败，看诊断里的行号。" : result.error ?? "";
      announcement.textContent = label;
    }
  }

  runButton.addEventListener("click", runDemo);
  stopButton.addEventListener("click", () => handle?.stop());
  window.addEventListener("pagehide", () => handle?.stop());

  // Links elsewhere on the page open a file in the panel instead of leaving the site.
  document.addEventListener("click", (event) => {
    const trigger = event.target.closest("[data-source-tab], [data-run-demo]");
    if (!trigger) return;
    event.preventDefault();
    if (trigger.hasAttribute("data-source-tab")) panel.select(trigger.dataset.sourceTab);
    root.scrollIntoView({ block: "start", behavior: "smooth" });
    if (trigger.hasAttribute("data-run-demo")) runDemo();
  });

  const range = window.SourcePanel.parseRange(location.hash);
  if (range) {
    panel.mark([range]);
    panel.load(config.demo.path).then(() => panel.scrollToLine(range[0]));
  }

  window.LessonSource = Object.freeze({ panel, runDemo, isEditing: () => editing });
})();
