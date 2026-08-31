(() => {
  "use strict";

  const catalog = window.PatternCatalog;
  const progress = window.LearningProgress;
  const key = document.body.dataset.patternKey;
  if (!catalog || !progress || !key) return;
  document.documentElement.classList.add("js");

  const curriculum = [
    ...catalog.patterns.map((pattern) => ({
      id: `pattern:${pattern.key}`,
      type: "pattern",
      title: `${pattern.english} / ${pattern.chinese}`,
      url: `${pattern.key}.html`,
      tasks: pattern.evidenceCards,
    })),
    ...catalog.learningItems.map((item) => ({
      ...item,
      url: `../${item.url}`,
      tasks: item.milestones.map((milestone) => ({ ...milestone, href: milestone.anchor })),
    })),
  ];
  progress.configure(curriculum);

  const id = `pattern:${key}`;
  const announcement = document.querySelector("#lesson-announcement");
  const navToggle = document.querySelector("#lesson-nav-toggle");
  const nav = document.querySelector("#lesson-nav");

  function render() {
    const current = progress.itemProgress(id);
    if (!current) return;
    document.querySelector("#lesson-progress-count").textContent = `${current.completed} / ${current.total}`;
    const track = document.querySelector(".lesson-progress-track");
    track.setAttribute("aria-valuenow", String(current.completed));
    track.setAttribute("aria-valuemax", String(current.total));
    document.querySelector("#lesson-progress-bar").style.width = `${current.percent}%`;
    const summary = progress.summary();
    document.querySelector("#lesson-progress-summary").textContent =
      `全课程 ${summary.percent}% · ${summary.verifiedItems} / ${summary.totalItems} 已验证`;

    for (const task of current.tasks) {
      const input = document.querySelector(`[data-progress-task="${task.id}"]`);
      const card = input?.closest(".evidence-card");
      if (!input || !card) continue;
      input.checked = task.completed;
      input.disabled = !task.available;
      card.classList.toggle("completed", task.completed);
      card.classList.toggle("locked", !task.available);
      card.setAttribute("aria-disabled", String(!task.available));
    }
  }

  document.querySelector(".evidence-grid").addEventListener("change", (event) => {
    const input = event.target.closest("[data-progress-task]");
    if (!input) return;
    const card = input.closest(".evidence-card");
    if (!progress.setTaskComplete(id, input.dataset.progressTask, input.checked)) {
      input.checked = progress.isTaskComplete(id, input.dataset.progressTask);
      announcement.textContent = "请先完成前一项学习证据。";
      return;
    }
    announcement.textContent = input.checked
      ? `${card.querySelector("h3").textContent.trim()}已完成。`
      : `${card.querySelector("h3").textContent.trim()}及其后续证据已撤销。`;
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
