(() => {
  "use strict";

  const catalog = window.PatternCatalog;
  const progress = window.LearningProgress;
  const review = window.ReviewScheduler;
  if (!catalog || !progress) {
    document.querySelector("#pattern-result").textContent = "学习目录加载失败，请刷新页面重试。";
    return;
  }
  document.documentElement.classList.add("js");

  const categoryMeta = {
    Creational: { label: "创建型", short: "C", color: "amber" },
    Structural: { label: "结构型", short: "S", color: "coral" },
    Behavioral: { label: "行为型", short: "B", color: "teal" },
  };
  const patterns = catalog.patterns;
  const problemTags = new Map(catalog.problemTags.map((tag) => [tag.id, tag]));
  const curriculum = [
    ...patterns.map((pattern) => ({
      id: `pattern:${pattern.key}`,
      type: "pattern",
      title: `${pattern.english} / ${pattern.chinese}`,
      url: `patterns/${pattern.key}.html`,
      tasks: pattern.evidenceCards,
    })),
    ...catalog.learningItems.map((item) => ({
      ...item,
      tasks: item.milestones.map((milestone) => ({ ...milestone, href: milestone.anchor })),
    })),
  ];
  progress.configure(curriculum);
  review?.configure(catalog.quizzes);

  const elements = {
    grid: document.querySelector("#pattern-grid"),
    search: document.querySelector("#pattern-search"),
    result: document.querySelector("#pattern-result"),
    noResults: document.querySelector("#no-results"),
    filters: [...document.querySelectorAll(".filter-button")],
    problemFilters: document.querySelector("#problem-filters"),
    patternListToggle: document.querySelector("#pattern-list-toggle"),
    progressCount: document.querySelector("#progress-count"),
    progressSummary: document.querySelector("#progress-stage-summary"),
    progressTrack: document.querySelector("#progress-track"),
    progressBar: document.querySelector("#progress-bar"),
    continueLink: document.querySelector("#continue-learning"),
    resetProgress: document.querySelector("#reset-progress"),
    exportProgress: document.querySelector("#export-progress"),
    exportMarkdown: document.querySelector("#export-markdown"),
    importProgress: document.querySelector("#import-progress"),
    progressToast: document.querySelector("#progress-toast"),
    progressToastMessage: document.querySelector("#progress-toast-message"),
    announcement: document.querySelector("#site-announcement"),
    dialog: document.querySelector("#pattern-dialog"),
    navToggle: document.querySelector("#nav-toggle"),
    nav: document.querySelector("#site-nav"),
    terminalToggle: document.querySelector("#terminal-toggle"),
    terminalCard: document.querySelector(".terminal-card"),
    terminalBody: document.querySelector("#terminal-body"),
    reviewDueCount: document.querySelector("#review-due-count"),
  };

  const params = new URL(window.location.href).searchParams;
  const categoryParam = params.get("category");
  const compactMedia = window.matchMedia("(max-width: 700px)");
  const compactLimit = 6;
  let activeCategory = Object.hasOwn(categoryMeta, categoryParam) ? categoryParam : "all";
  let activeProblems = new Set((params.get("problems") ?? "").split(",").filter((id) => problemTags.has(id)));
  let searchQuery = params.get("q") ?? "";
  let patternListExpanded = false;
  let resetSnapshot = null;
  let resetTimer = 0;

  function escapeHtml(value) {
    return String(value)
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");
  }

  function announce(message) {
    elements.announcement.textContent = "";
    window.requestAnimationFrame(() => { elements.announcement.textContent = message; });
  }

  function itemTaskUrl(item, task) {
    if (!task?.href) return item.url;
    return task.href.startsWith("#") ? `${item.url}${task.href}` : task.href;
  }

  function patternCard(pattern) {
    const category = categoryMeta[pattern.category];
    const id = `pattern:${pattern.key}`;
    const current = progress.itemProgress(id);
    const tags = pattern.problemTags.map((tagId) =>
      `<span>${escapeHtml(problemTags.get(tagId)?.label ?? tagId)}</span>`,
    ).join("");
    const taskUrl = itemTaskUrl({ url: `patterns/${pattern.key}.html` }, current.nextTask);
    const taskLabel = current.verified ? "4 / 4 · 已验证" : `${current.completed} / ${current.total} · ${escapeHtml(current.nextTask.title)}`;

    return `
      <article class="pattern-card ${category.color}${current.verified ? " learned" : ""}" data-progress-id="${id}">
        <div class="pattern-card-top">
          <span class="pattern-number">${String(pattern.number).padStart(2, "0")}</span>
          <span class="category-badge"><i>${category.short}</i>${category.label}</span>
        </div>
        <div class="pattern-body">
          <h3>${escapeHtml(pattern.english)}</h3><p class="pattern-chinese">${escapeHtml(pattern.chinese)}模式</p>
          <p class="pattern-intent">${escapeHtml(pattern.intent)}</p>
          <div class="pattern-problems" aria-label="适用问题">${tags}</div>
          <button class="pattern-open" type="button" data-open-pattern="${pattern.key}" aria-label="查看 ${escapeHtml(pattern.english)} ${escapeHtml(pattern.chinese)}详情">查看详情 <span aria-hidden="true">→</span></button>
        </div>
        <div class="pattern-card-footer">
          <a href="patterns/${pattern.key}.html">完整课件</a>
          <a class="pattern-task-link" href="${taskUrl}">${taskLabel}</a>
        </div>
      </article>`;
  }

  function searchableText(pattern) {
    const tagText = pattern.problemTags.flatMap((id) => {
      const tag = problemTags.get(id);
      return tag ? [tag.label, ...tag.aliases] : [];
    });
    return [
      pattern.english, pattern.chinese, pattern.key, pattern.intent, pattern.scenario,
      pattern.changeAxis, pattern.whenUse, pattern.avoidWhen, categoryMeta[pattern.category].label,
      ...tagText,
    ].join(" ").toLocaleLowerCase("zh-CN");
  }

  function renderProblemFilters() {
    const counts = new Map();
    for (const pattern of patterns) {
      for (const id of pattern.problemTags) counts.set(id, (counts.get(id) ?? 0) + 1);
    }
    elements.problemFilters.innerHTML = catalog.problemTags
      .filter((tag) => counts.has(tag.id))
      .map((tag) => `<button type="button" class="problem-filter${activeProblems.has(tag.id) ? " active" : ""}" data-problem="${tag.id}" aria-pressed="${activeProblems.has(tag.id)}">${escapeHtml(tag.label)} <span>${counts.get(tag.id)}</span></button>`)
      .join("");
  }

  function syncQuery() {
    const url = new URL(window.location.href);
    activeCategory === "all" ? url.searchParams.delete("category") : url.searchParams.set("category", activeCategory);
    activeProblems.size === 0
      ? url.searchParams.delete("problems")
      : url.searchParams.set("problems", [...activeProblems].sort().join(","));
    searchQuery.trim() ? url.searchParams.set("q", searchQuery.trim()) : url.searchParams.delete("q");
    history.replaceState(history.state, "", `${url.pathname}${url.search}${url.hash}`);
  }

  function renderPatterns() {
    const tokens = searchQuery.trim().toLocaleLowerCase("zh-CN").split(/\s+/u).filter(Boolean);
    const matched = patterns.filter((pattern) => {
      const categoryMatches = activeCategory === "all" || pattern.category === activeCategory;
      const problemMatches = activeProblems.size === 0 || pattern.problemTags.some((id) => activeProblems.has(id));
      const searchable = searchableText(pattern);
      return categoryMatches && problemMatches && tokens.every((token) => searchable.includes(token));
    });
    const hasFilters = activeCategory !== "all" || activeProblems.size > 0 || searchQuery.trim() !== "";
    const collapsed = compactMedia.matches && !hasFilters && !patternListExpanded && matched.length > compactLimit;
    const visible = collapsed ? matched.slice(0, compactLimit) : matched;

    elements.grid.innerHTML = visible.map(patternCard).join("");
    elements.noResults.hidden = matched.length > 0;
    const filters = [];
    if (activeCategory !== "all") filters.push(categoryMeta[activeCategory].label);
    if (activeProblems.size > 0) filters.push([...activeProblems].map((id) => problemTags.get(id).label).join("、"));
    if (searchQuery.trim()) filters.push(`“${searchQuery.trim()}”`);
    elements.result.textContent = filters.length
      ? `${filters.join(" · ")}：找到 ${matched.length} 种模式`
      : collapsed
        ? `共 ${matched.length} 种模式，按初学者顺序先显示 ${compactLimit} 种`
        : `按初学者顺序显示全部 ${matched.length} 种模式`;

    const canCollapse = compactMedia.matches && !hasFilters && matched.length > compactLimit;
    elements.patternListToggle.hidden = !canCollapse;
    elements.patternListToggle.setAttribute("aria-expanded", String(canCollapse && patternListExpanded));
    elements.patternListToggle.textContent = patternListExpanded
      ? `收起为前 ${compactLimit} 种`
      : `显示其余 ${matched.length - compactLimit} 种模式`;
    updateProgress();
    syncQuery();
  }

  function updateLearningSummaries() {
    for (const container of document.querySelectorAll("[data-learning-summary]")) {
      const item = curriculum.find((candidate) => candidate.id === container.dataset.learningSummary);
      const current = progress.itemProgress(item?.id);
      if (!item || !current) continue;
      const label = item.type === "pattern" ? "项证据" : "里程碑";
      container.querySelector("span").textContent = `${current.completed} / ${current.total} ${label}`;
      const link = container.querySelector("a");
      const nextTask = current.nextTask ?? item.tasks[0];
      link.href = itemTaskUrl(item, nextTask);
      link.textContent = current.verified ? "全部完成，重新复习 →" : `下一步：${nextTask.title} →`;
    }
  }

  function updateProgress() {
    const summary = progress.summary();
    elements.progressCount.textContent = `${summary.percent}%`;
    elements.progressSummary.textContent = `${summary.earnedTasks} / ${summary.totalTasks} 个任务 · ${summary.verifiedItems} / ${summary.totalItems} 已验证`;
    elements.progressTrack.setAttribute("aria-valuenow", String(summary.earnedTasks));
    elements.progressTrack.setAttribute("aria-valuemax", String(summary.totalTasks));
    elements.progressBar.style.width = `${summary.percent}%`;

    const resume = progress.resumeItem();
    if (resume) {
      elements.continueLink.href = resume.url;
      elements.continueLink.textContent = summary.verifiedItems === summary.totalItems
        ? "全部完成，选择一个主题复习 →"
        : summary.earnedTasks === 0
          ? `从 ${resume.title} 开始：${resume.nextAction} →`
          : `继续学习：${resume.title} · ${resume.nextAction} →`;
    }

    document.querySelectorAll(".pattern-card[data-progress-id]").forEach((card) => {
      const current = progress.itemProgress(card.dataset.progressId);
      const pattern = patterns.find((candidate) => `pattern:${candidate.key}` === card.dataset.progressId);
      if (!current || !pattern) return;
      card.classList.toggle("learned", current.verified);
      const link = card.querySelector(".pattern-task-link");
      const nextTask = current.nextTask ?? pattern.evidenceCards[0];
      link.href = itemTaskUrl({ url: `patterns/${pattern.key}.html` }, nextTask);
      link.textContent = current.verified ? "4 / 4 · 已验证" : `${current.completed} / ${current.total} · ${nextTask.title}`;
    });
    updateLearningSummaries();
    if (elements.dialog.open && elements.dialog.dataset.pattern) {
      const current = progress.itemProgress(`pattern:${elements.dialog.dataset.pattern}`);
      document.querySelector("#dialog-progress-summary").textContent = `${current.completed} / ${current.total}`;
    }
    if (review && elements.reviewDueCount) elements.reviewDueCount.textContent = String(review.dueQuestions().length);
  }

  function openPattern(key, { pushHistory = true } = {}) {
    const pattern = patterns.find((item) => item.key === key);
    if (!pattern) return;
    const category = categoryMeta[pattern.category];
    document.querySelector("#dialog-category").textContent = `${category.short} · ${category.label}`;
    document.querySelector("#dialog-number").textContent = String(pattern.number).padStart(2, "0");
    document.querySelector("#dialog-title").textContent = `${pattern.english} / ${pattern.chinese}`;
    document.querySelector("#dialog-intent").textContent = pattern.intent;
    document.querySelector("#dialog-scenario").textContent = pattern.scenario;
    document.querySelector("#dialog-command").textContent = `dotnet run --project src/DesignPatterns.Runner -- ${pattern.key}`;
    document.querySelector("#dialog-guide").href = `patterns/${pattern.key}.html`;
    document.querySelector("#dialog-source").href = pattern.sourceUrl;
    document.querySelector("#dialog-practice").href = pattern.practiceUrl;
    const current = progress.itemProgress(`pattern:${pattern.key}`);
    document.querySelector("#dialog-progress-summary").textContent = `${current.completed} / ${current.total}`;
    elements.dialog.dataset.pattern = pattern.key;

    const url = new URL(window.location.href);
    url.searchParams.set("pattern", pattern.key);
    if (pushHistory) history.pushState({ patternDialog: pattern.key }, "", url);
    else history.replaceState(history.state, "", url);
    if (!elements.dialog.open) elements.dialog.showModal();
  }

  function closePattern({ fromHistory = false } = {}) {
    if (elements.dialog.open) elements.dialog.close();
    if (fromHistory) return;
    if (history.state?.patternDialog) {
      history.back();
      return;
    }
    const url = new URL(window.location.href);
    url.searchParams.delete("pattern");
    history.replaceState(history.state, "", `${url.pathname}${url.search}${url.hash}`);
  }

  async function copyText(text, button) {
    const original = button.textContent;
    try {
      await navigator.clipboard.writeText(text);
      button.textContent = "已复制 ✓";
      announce("命令已复制到剪贴板。 ");
    } catch {
      button.textContent = "请手动复制";
      announce("无法访问剪贴板，请手动复制命令。 ");
    }
    window.setTimeout(() => { button.textContent = original; }, 1600);
  }

  function downloadFile(name, content, type) {
    const url = URL.createObjectURL(new Blob([content], { type }));
    const link = document.createElement("a");
    link.href = url;
    link.download = name;
    document.body.append(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
  }

  function backupSnapshot() {
    return {
      progress: progress.snapshot(),
      review: review?.snapshot() ?? null,
    };
  }

  function restoreSnapshot(previous) {
    if (!previous) return false;
    const restored = progress.restore(previous.progress ?? previous);
    if (restored && previous.review && review) review.restore(previous.review);
    return restored;
  }

  function exportBackup() {
    return {
      format: "csharp-design-patterns-learning-backup",
      version: 1,
      exportedAt: Date.now(),
      progress: progress.exportData(),
      review: review?.exportData() ?? null,
    };
  }

  function importBackup(payload) {
    if (payload?.format !== "csharp-design-patterns-learning-backup" || payload.version !== 1) {
      return progress.importData(payload);
    }
    if (!payload.progress || (payload.review && !review)) return false;
    const previous = backupSnapshot();
    if (!progress.importData(payload.progress)) return false;
    if (payload.review && !review.restore(payload.review)) {
      restoreSnapshot(previous);
      return false;
    }
    return true;
  }

  function showUndoToast(message, previous) {
    resetSnapshot = previous;
    window.clearTimeout(resetTimer);
    elements.progressToastMessage.textContent = message;
    document.querySelector("#undo-reset").hidden = !previous;
    elements.progressToast.hidden = false;
    const dismiss = () => {
      if (elements.progressToast.contains(document.activeElement)) {
        resetTimer = window.setTimeout(dismiss, 2000);
        return;
      }
      elements.progressToast.hidden = true;
      resetSnapshot = null;
    };
    resetTimer = window.setTimeout(dismiss, 8000);
  }

  function toggleNavigation(force) {
    const open = force ?? !elements.nav.classList.contains("open");
    elements.nav.classList.toggle("open", open);
    elements.navToggle.setAttribute("aria-expanded", String(open));
  }

  function setTerminalExpanded(expanded) {
    elements.terminalCard.classList.toggle("expanded", expanded);
    elements.terminalBody.hidden = !expanded;
    elements.terminalToggle.setAttribute("aria-expanded", String(expanded));
    elements.terminalToggle.textContent = expanded ? "收起输出" : "展开输出";
  }

  elements.search.value = searchQuery;
  elements.filters.forEach((button) => {
    const selected = button.dataset.category === activeCategory;
    button.classList.toggle("active", selected);
    button.setAttribute("aria-pressed", String(selected));
    button.addEventListener("click", () => {
      activeCategory = button.dataset.category;
      elements.filters.forEach((candidate) => {
        const isSelected = candidate === button;
        candidate.classList.toggle("active", isSelected);
        candidate.setAttribute("aria-pressed", String(isSelected));
      });
      renderPatterns();
    });
  });

  renderProblemFilters();
  elements.problemFilters.addEventListener("click", (event) => {
    const button = event.target.closest("[data-problem]");
    if (!button) return;
    const id = button.dataset.problem;
    activeProblems.has(id) ? activeProblems.delete(id) : activeProblems.add(id);
    renderProblemFilters();
    renderPatterns();
  });
  elements.search.addEventListener("input", (event) => {
    searchQuery = event.target.value;
    renderPatterns();
  });
  elements.patternListToggle.addEventListener("click", () => {
    patternListExpanded = !patternListExpanded;
    renderPatterns();
  });
  compactMedia.addEventListener("change", () => {
    patternListExpanded = false;
    setTerminalExpanded(!compactMedia.matches);
    renderPatterns();
  });
  elements.grid.addEventListener("click", (event) => {
    const openButton = event.target.closest("[data-open-pattern]");
    if (openButton) openPattern(openButton.dataset.openPattern);
  });

  elements.exportProgress.addEventListener("click", () => {
    downloadFile("csharp-design-patterns-learning-backup.json", JSON.stringify(exportBackup(), null, 2), "application/json");
    announce("课程进度与复习记录 JSON 备份已下载。 ");
  });
  elements.exportMarkdown.addEventListener("click", () => {
    downloadFile("csharp-design-patterns-learning-record.md", progress.toMarkdown(), "text/markdown;charset=utf-8");
    announce("Markdown 学习记录已下载。 ");
  });
  elements.importProgress.addEventListener("change", async () => {
    const file = elements.importProgress.files?.[0];
    elements.importProgress.value = "";
    if (!file) return;
    if (file.size > 250_000) {
      showUndoToast("备份文件超过 250 KB，已拒绝导入。", null);
      announce("备份文件超过 250 KB，已拒绝导入。 ");
      return;
    }
    const previous = backupSnapshot();
    try {
      const payload = JSON.parse(await file.text());
      if (!importBackup(payload)) throw new Error("invalid progress backup");
      showUndoToast("课程进度与复习记录已从备份导入。", previous);
      announce("学习备份导入成功，可以撤销。 ");
    } catch {
      showUndoToast("无法导入：请选择本站导出的有效 JSON 备份。", null);
      announce("无法导入：请选择本站导出的有效 JSON 备份。 ");
    }
  });
  elements.resetProgress.addEventListener("click", () => {
    if (progress.summary().earnedTasks === 0) return;
    const previous = progress.reset();
    showUndoToast("学习进度已清空。", previous);
    announce("学习进度已清空，可以撤销。 ");
  });
  document.querySelector("#undo-reset").addEventListener("click", () => {
    if (!resetSnapshot || !restoreSnapshot(resetSnapshot)) return;
    resetSnapshot = null;
    elements.progressToast.hidden = true;
    window.clearTimeout(resetTimer);
    announce("学习进度已恢复。 ");
  });

  document.querySelector("#dialog-close").addEventListener("click", () => closePattern());
  elements.dialog.addEventListener("click", (event) => {
    const bounds = elements.dialog.getBoundingClientRect();
    const outside = event.clientX < bounds.left || event.clientX > bounds.right ||
      event.clientY < bounds.top || event.clientY > bounds.bottom;
    if (outside) closePattern();
  });
  elements.dialog.addEventListener("cancel", (event) => {
    event.preventDefault();
    closePattern();
  });
  document.querySelector("#dialog-copy").addEventListener("click", (event) => {
    copyText(document.querySelector("#dialog-command").textContent, event.currentTarget);
  });
  document.addEventListener("click", (event) => {
    const button = event.target.closest("[data-copy-command]");
    if (button) copyText(button.dataset.copyCommand, button);
  });

  elements.terminalToggle.addEventListener("click", () => {
    setTerminalExpanded(elements.terminalToggle.getAttribute("aria-expanded") !== "true");
  });
  elements.navToggle.addEventListener("click", () => toggleNavigation());
  elements.nav.addEventListener("click", (event) => {
    if (event.target.closest("a")) toggleNavigation(false);
  });
  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape" && elements.nav.classList.contains("open")) {
      toggleNavigation(false);
      elements.navToggle.focus();
    }
  });
  window.addEventListener("popstate", () => {
    const key = new URL(window.location.href).searchParams.get("pattern");
    if (key) openPattern(key, { pushHistory: false });
    else closePattern({ fromHistory: true });
  });

  setTerminalExpanded(!compactMedia.matches);
  progress.subscribe(updateProgress);
  review?.subscribe(updateProgress);
  renderPatterns();
  const requestedPattern = new URL(window.location.href).searchParams.get("pattern");
  if (requestedPattern) openPattern(requestedPattern, { pushHistory: false });
})();
