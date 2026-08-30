(() => {
  "use strict";

  document.documentElement.classList.add("js");

  const catalog = window.PatternCatalog;
  const progress = window.LearningProgress;
  if (!catalog || !progress) {
    document.querySelector("#pattern-result").textContent = "学习目录加载失败，请刷新页面重试。";
    return;
  }

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
    })),
    ...catalog.learningItems,
  ];
  progress.configure(curriculum);

  const elements = {
    grid: document.querySelector("#pattern-grid"),
    search: document.querySelector("#pattern-search"),
    result: document.querySelector("#pattern-result"),
    noResults: document.querySelector("#no-results"),
    filters: [...document.querySelectorAll(".filter-button")],
    problemFilters: document.querySelector("#problem-filters"),
    progressCount: document.querySelector("#progress-count"),
    progressSummary: document.querySelector("#progress-stage-summary"),
    progressTrack: document.querySelector("#progress-track"),
    progressBar: document.querySelector("#progress-bar"),
    continueLink: document.querySelector("#continue-learning"),
    resetProgress: document.querySelector("#reset-progress"),
    progressToast: document.querySelector("#progress-toast"),
    announcement: document.querySelector("#site-announcement"),
    dialog: document.querySelector("#pattern-dialog"),
    navToggle: document.querySelector("#nav-toggle"),
    nav: document.querySelector("#site-nav"),
  };

  const params = new URL(window.location.href).searchParams;
  const categoryParam = params.get("category");
  let activeCategory = Object.hasOwn(categoryMeta, categoryParam) ? categoryParam : "all";
  let activeProblems = new Set((params.get("problems") ?? "").split(",").filter((id) => problemTags.has(id)));
  let searchQuery = params.get("q") ?? "";
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

  function stageOptions(level) {
    return progress.stages.map((stage, index) =>
      `<option value="${index}"${index === level ? " selected" : ""}>${stage}</option>`,
    ).join("");
  }

  function announce(message) {
    elements.announcement.textContent = "";
    window.requestAnimationFrame(() => { elements.announcement.textContent = message; });
  }

  function patternCard(pattern) {
    const category = categoryMeta[pattern.category];
    const id = `pattern:${pattern.key}`;
    const level = progress.getLevel(id);
    const tags = pattern.problemTags.map((tagId) =>
      `<span>${escapeHtml(problemTags.get(tagId)?.label ?? tagId)}</span>`,
    ).join("");

    return `
      <article class="pattern-card ${category.color}${level === 4 ? " learned" : ""}">
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
          <label class="stage-control"><span class="sr-only">${escapeHtml(pattern.english)} 学习阶段</span><select data-progress-id="${id}" aria-label="${escapeHtml(pattern.english)} 学习阶段">${stageOptions(level)}</select></label>
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

  function renderPatterns({ keepFocusId = null } = {}) {
    const tokens = searchQuery.trim().toLocaleLowerCase("zh-CN").split(/\s+/u).filter(Boolean);
    const visible = patterns.filter((pattern) => {
      const categoryMatches = activeCategory === "all" || pattern.category === activeCategory;
      const problemMatches = activeProblems.size === 0 || pattern.problemTags.some((id) => activeProblems.has(id));
      const searchable = searchableText(pattern);
      return categoryMatches && problemMatches && tokens.every((token) => searchable.includes(token));
    });

    elements.grid.innerHTML = visible.map(patternCard).join("");
    elements.noResults.hidden = visible.length > 0;
    const filters = [];
    if (activeCategory !== "all") filters.push(categoryMeta[activeCategory].label);
    if (activeProblems.size > 0) filters.push([...activeProblems].map((id) => problemTags.get(id).label).join("、"));
    if (searchQuery.trim()) filters.push(`“${searchQuery.trim()}”`);
    elements.result.textContent = filters.length
      ? `${filters.join(" · ")}：找到 ${visible.length} 种模式`
      : `按初学者顺序显示全部 ${visible.length} 种模式`;
    updateProgress();
    syncQuery();
    if (keepFocusId) {
      window.requestAnimationFrame(() => elements.grid.querySelector(`[data-progress-id="${keepFocusId}"]`)?.focus());
    }
  }

  function updateProgress() {
    const summary = progress.summary();
    elements.progressCount.textContent = `${summary.percent}%`;
    elements.progressSummary.textContent = `${summary.earnedStages} / ${summary.totalStages} 个阶段 · ${summary.verifiedItems} / ${summary.totalItems} 已验证`;
    elements.progressTrack.setAttribute("aria-valuenow", String(summary.earnedStages));
    elements.progressTrack.setAttribute("aria-valuemax", String(summary.totalStages));
    elements.progressBar.style.width = `${summary.percent}%`;

    const resume = progress.resumeItem();
    if (resume) {
      elements.continueLink.href = resume.url;
      elements.continueLink.textContent = summary.verifiedItems === summary.totalItems
        ? "全部完成，选择一个主题复习 →"
        : `继续学习：${resume.title} · ${resume.nextStage} →`;
    }

    document.querySelectorAll("[data-progress-id]").forEach((select) => {
      const level = progress.getLevel(select.dataset.progressId);
      if (select.value !== String(level)) select.value = String(level);
    });
    document.querySelectorAll(".pattern-card").forEach((card) => {
      const select = card.querySelector("[data-progress-id]");
      card.classList.toggle("learned", Boolean(select) && progress.getLevel(select.dataset.progressId) === 4);
    });
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
    const stage = document.querySelector("#dialog-progress");
    stage.dataset.progressId = `pattern:${pattern.key}`;
    stage.setAttribute("aria-label", `${pattern.english} 学习阶段`);
    stage.innerHTML = stageOptions(progress.getLevel(stage.dataset.progressId));
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

  function toggleNavigation(force) {
    const open = force ?? !elements.nav.classList.contains("open");
    elements.nav.classList.toggle("open", open);
    elements.navToggle.setAttribute("aria-expanded", String(open));
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

  elements.grid.addEventListener("click", (event) => {
    const openButton = event.target.closest("[data-open-pattern]");
    if (openButton) openPattern(openButton.dataset.openPattern);
  });

  document.addEventListener("change", (event) => {
    const select = event.target.closest("select[data-progress-id]");
    if (!select) return;
    const item = curriculum.find((candidate) => candidate.id === select.dataset.progressId);
    if (!progress.setLevel(select.dataset.progressId, Number(select.value))) return;
    announce(`${item?.title ?? "学习项目"}已更新为${progress.stages[Number(select.value)]}。`);
    renderPatterns({ keepFocusId: select.dataset.progressId });
  });

  elements.resetProgress.addEventListener("click", () => {
    if (progress.summary().earnedStages === 0) return;
    resetSnapshot = progress.reset();
    window.clearTimeout(resetTimer);
    elements.progressToast.hidden = false;
    resetTimer = window.setTimeout(() => {
      elements.progressToast.hidden = true;
      resetSnapshot = null;
    }, 6000);
    renderPatterns();
    announce("学习进度已清空，可以撤销。 ");
  });

  document.querySelector("#undo-reset").addEventListener("click", () => {
    if (!resetSnapshot || !progress.restore(resetSnapshot)) return;
    resetSnapshot = null;
    elements.progressToast.hidden = true;
    window.clearTimeout(resetTimer);
    renderPatterns();
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

  progress.subscribe(updateProgress);
  renderPatterns();
  const requestedPattern = new URL(window.location.href).searchParams.get("pattern");
  if (requestedPattern) openPattern(requestedPattern, { pushHistory: false });
})();
