(() => {
  "use strict";

  const catalog = window.PatternCatalog;
  const review = window.ReviewScheduler;
  if (!catalog || !review) return;
  document.documentElement.classList.add("js");
  review.configure(catalog.quizzes);
  const patterns = new Map(catalog.patterns.map((pattern) => [pattern.key, pattern]));
  const elements = {
    card: document.querySelector("#question-card"),
    empty: document.querySelector("#quiz-empty"),
    emptyTitle: document.querySelector("#quiz-empty-title"),
    emptyCopy: document.querySelector("#quiz-empty-copy"),
    form: document.querySelector("#quiz-form"),
    options: document.querySelector("#question-options"),
    index: document.querySelector("#question-index"),
    title: document.querySelector("#question-title"),
    scenario: document.querySelector("#question-scenario"),
    prompt: document.querySelector("#question-prompt"),
    feedback: document.querySelector("#answer-feedback"),
    feedbackTitle: document.querySelector("#feedback-title"),
    feedbackRule: document.querySelector("#feedback-rule"),
    feedbackLesson: document.querySelector("#feedback-lesson"),
    next: document.querySelector("#next-question"),
    due: document.querySelector("#quiz-due"),
    mastered: document.querySelector("#quiz-mastered"),
    answered: document.querySelector("#quiz-answered"),
    dueMode: document.querySelector("#practice-due"),
    allMode: document.querySelector("#practice-all"),
  };
  let mode = "due";
  let queue = [];
  let cursor = 0;
  let current = null;
  let answered = false;

  function shuffled(items) {
    return items.map((item) => ({ item, order: Math.random() })).sort((a, b) => a.order - b.order).map(({ item }) => item);
  }

  function refreshStats() {
    const stats = review.stats();
    elements.due.textContent = String(stats.due);
    elements.mastered.textContent = String(stats.mastered);
    elements.answered.textContent = String(stats.answered);
  }

  function buildQueue() {
    queue = shuffled(mode === "due" ? review.dueQuestions() : catalog.quizzes);
    cursor = 0;
  }

  function showQuestion() {
    current = queue[cursor] ?? null;
    answered = false;
    elements.card.hidden = !current;
    elements.empty.hidden = Boolean(current);
    if (!current) {
      elements.emptyTitle.textContent = mode === "due" ? "到期题已完成" : "本轮 6 题已完成";
      elements.emptyCopy.textContent = mode === "due"
        ? "今天的复习已完成；可以刷新到期题、练全部题，或回到模式课件继续积累证据。"
        : "自由练习不会推进复习间隔；可以再练一轮，或返回模式课件继续积累证据。";
      elements.empty.focus({ preventScroll: true });
      return;
    }
    elements.index.textContent = `${mode === "due" ? "到期复习" : "全部练习"} · ${cursor + 1} / ${queue.length}`;
    elements.title.textContent = current.title;
    elements.scenario.textContent = current.scenario;
    elements.prompt.textContent = current.prompt;
    elements.options.replaceChildren();
    for (const key of current.patternKeys) {
      const pattern = patterns.get(key);
      const label = document.createElement("label");
      const input = document.createElement("input");
      const copy = document.createElement("span");
      input.type = "radio";
      input.name = "answer";
      input.value = key;
      input.required = true;
      copy.textContent = pattern ? `${pattern.english} / ${pattern.chinese}` : key;
      label.append(input, copy);
      elements.options.append(label);
    }
    elements.form.hidden = false;
    elements.form.querySelector("button").disabled = true;
    elements.feedback.hidden = true;
    elements.next.hidden = true;
    elements.title.focus({ preventScroll: true });
  }

  elements.options.addEventListener("change", () => {
    if (!answered) elements.form.querySelector("button").disabled = false;
  });

  function setMode(nextMode) {
    mode = nextMode;
    elements.dueMode.setAttribute("aria-pressed", String(mode === "due"));
    elements.allMode.setAttribute("aria-pressed", String(mode === "all"));
    buildQueue();
    showQuestion();
  }

  elements.form.addEventListener("submit", (event) => {
    event.preventDefault();
    if (!current || answered) return;
    const selected = new FormData(elements.form).get("answer");
    if (!selected) return;
    answered = true;
    const correct = selected === current.correctKey;
    if (mode === "due") review.record(current.id, correct);
    const correctPattern = patterns.get(current.correctKey);
    for (const input of elements.options.querySelectorAll("input")) {
      input.disabled = true;
      input.closest("label").classList.toggle("correct", input.value === current.correctKey);
      input.closest("label").classList.toggle("wrong", input.checked && !correct);
    }
    elements.form.querySelector("button").disabled = true;
    elements.feedback.hidden = false;
    elements.feedback.classList.toggle("success", correct);
    elements.feedbackTitle.textContent = correct ? "判断正确" : `更合适的是 ${correctPattern?.english ?? current.correctKey}`;
    elements.feedbackRule.textContent = current.decisionRule;
    elements.feedbackLesson.href = `patterns/${current.correctKey}.html#evidence-read`;
    elements.next.hidden = false;
    elements.next.textContent = cursor + 1 < queue.length ? "下一题 →" : "完成本轮 →";
    elements.feedback.focus?.();
    refreshStats();
  });

  elements.next.addEventListener("click", () => {
    cursor += 1;
    showQuestion();
  });
  elements.dueMode.addEventListener("click", () => setMode("due"));
  elements.allMode.addEventListener("click", () => setMode("all"));
  document.querySelector("#empty-practice-all").addEventListener("click", () => setMode("all"));
  document.querySelector("#refresh-due").addEventListener("click", () => setMode("due"));
  document.querySelector("#reset-review").addEventListener("click", () => {
    if (!window.confirm("确定清空全部复习记录吗？此操作无法撤销。")) return;
    review.reset();
    setMode("due");
  });
  review.subscribe(refreshStats);
  setMode("due");
})();
