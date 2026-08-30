(() => {
  "use strict";

  document.documentElement.classList.add("js");

  const catalog = window.PatternCatalog;
  const progress = window.LearningProgress;
  const key = document.body.dataset.patternKey;
  if (!catalog || !progress || !key) return;

  const curriculum = [
    ...catalog.patterns.map((pattern) => ({
      id: `pattern:${pattern.key}`,
      type: "pattern",
      title: `${pattern.english} / ${pattern.chinese}`,
      url: `${pattern.key}.html`,
    })),
    ...catalog.learningItems.map((item) => ({ ...item, url: `../${item.url}` })),
  ];
  progress.configure(curriculum);

  const id = `pattern:${key}`;
  const select = document.querySelector("#lesson-progress");
  const announcement = document.querySelector("#lesson-announcement");
  const navToggle = document.querySelector("#lesson-nav-toggle");
  const nav = document.querySelector("#lesson-nav");

  function render() {
    const current = progress.getLevel(id);
    select.innerHTML = progress.stages.map((stage, index) =>
      `<option value="${index}"${index === current ? " selected" : ""}>${stage}</option>`,
    ).join("");
    const summary = progress.summary();
    document.querySelector("#lesson-progress-summary").textContent =
      `全课程 ${summary.percent}% · ${summary.verifiedItems} / ${summary.totalItems} 已验证`;
  }

  select.addEventListener("change", () => {
    progress.setLevel(id, Number(select.value));
    announcement.textContent = `${document.querySelector("h1").textContent.trim()}已更新为${progress.stages[Number(select.value)]}。`;
    render();
  });

  document.querySelectorAll("[data-copy-command]").forEach((button) => {
    button.addEventListener("click", async () => {
      const original = button.textContent;
      try {
        await navigator.clipboard.writeText(button.dataset.copyCommand);
        button.textContent = "已复制 ✓";
        announcement.textContent = "运行命令已复制。";
      } catch {
        button.textContent = "请手动复制";
        announcement.textContent = "无法访问剪贴板，请手动复制运行命令。";
      }
      window.setTimeout(() => { button.textContent = original; }, 1600);
    });
  });

  navToggle.addEventListener("click", () => {
    const open = nav.classList.toggle("open");
    navToggle.setAttribute("aria-expanded", String(open));
  });
  nav.addEventListener("click", (event) => {
    if (!event.target.closest("a")) return;
    nav.classList.remove("open");
    navToggle.setAttribute("aria-expanded", "false");
  });
  document.addEventListener("keydown", (event) => {
    if (event.key !== "Escape" || !nav.classList.contains("open")) return;
    nav.classList.remove("open");
    navToggle.setAttribute("aria-expanded", "false");
    navToggle.focus();
  });

  progress.subscribe(render);
  render();
})();
