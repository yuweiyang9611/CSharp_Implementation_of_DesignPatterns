// The lesson's self-check: the discrimination questions that name this pattern, answered in place.
// Due questions feed the review scheduler, as on the quiz page; the rest are practice.
(() => {
  "use strict";

  const catalog = window.PatternCatalog;
  const review = window.ReviewScheduler;
  const section = document.querySelector("#lesson-quiz");
  const key = document.body.dataset.patternKey;
  if (!catalog || !review || !section || !key) return;
  review.configure(catalog.quizzes);

  const questions = catalog.quizzes.filter((quiz) => quiz.patternKeys.includes(key));
  const list = section.querySelector("#lesson-quiz-list");
  const summary = section.querySelector("#lesson-quiz-summary");
  const step = document.querySelector('[data-step="quiz"]');

  function stateOf(quiz) {
    const record = review.questionState(quiz.id);
    const due = review.dueQuestions().some((item) => item.id === quiz.id);
    if (!record || !record.attempts) return { text: due ? "未作答" : "未作答", due, answered: false, correct: false };
    const correct = record.streak > 0;
    return { text: due ? "到期，再答一次" : correct ? "上次答对" : "上次答错", due, answered: true, correct };
  }

  function renderSummary() {
    if (!questions.length) {
      summary.textContent = "这个模式还没有辨析题。";
      step?.setAttribute("data-state", "none");
      return;
    }
    const states = questions.map(stateOf);
    const correct = states.filter((state) => state.correct).length;
    const answered = states.filter((state) => state.answered).length;
    summary.textContent = `${correct} / ${questions.length} 题上次答对`;
    step?.setAttribute("data-state", correct === questions.length ? "done" : answered ? "now" : "pending");
    step?.querySelector(".nb-steps-count")?.replaceChildren(`${correct} / ${questions.length}`);
  }

  function renderQuestion(quiz, index) {
    const article = document.createElement("article");
    article.className = "lesson-question nb-entry";
    article.dataset.quizId = quiz.id;
    const state = stateOf(quiz);
    article.innerHTML = `
      <p class="lesson-question-index"><span>第 ${index + 1} 题 / ${questions.length}</span><span class="lesson-question-state" data-due="${state.due}">${state.text}</span></p>
      <h3></h3>
      <p class="lesson-question-scenario"></p>
      <form><fieldset><legend></legend><div class="lesson-question-options"></div></fieldset><button class="nb-button nb-button--solid" type="submit" disabled>提交答案</button></form>
      <div class="lesson-question-feedback" role="status" aria-live="polite" hidden><strong></strong><p></p></div>`;
    article.querySelector("h3").textContent = quiz.title;
    article.querySelector(".lesson-question-scenario").textContent = quiz.scenario;
    article.querySelector("legend").textContent = quiz.prompt;
    const options = article.querySelector(".lesson-question-options");
    for (const option of quiz.options) {
      const label = document.createElement("label");
      const input = document.createElement("input");
      input.type = "radio";
      input.name = `answer-${quiz.id}`;
      input.value = option.id;
      input.required = true;
      const copy = document.createElement("span");
      copy.textContent = option.label;
      label.append(input, copy);
      options.append(label);
    }
    const form = article.querySelector("form");
    const submit = form.querySelector("button");
    options.addEventListener("change", () => { submit.disabled = false; });
    form.addEventListener("submit", (event) => {
      event.preventDefault();
      const selected = new FormData(form).get(`answer-${quiz.id}`);
      if (!selected) return;
      const correct = selected === quiz.correctKey;
      const wasDue = review.dueQuestions().some((item) => item.id === quiz.id);
      if (wasDue) review.record(quiz.id, correct);
      const correctOption = quiz.options.find((option) => option.id === quiz.correctKey);
      for (const input of options.querySelectorAll("input")) {
        input.disabled = true;
        input.closest("label").classList.toggle("correct", input.value === quiz.correctKey);
        input.closest("label").classList.toggle("wrong", input.checked && !correct);
      }
      submit.disabled = true;
      const feedback = article.querySelector(".lesson-question-feedback");
      feedback.hidden = false;
      feedback.classList.toggle("success", correct);
      feedback.querySelector("strong").textContent = correct ? "判断正确" : `更合适的是 ${correctOption.label}`;
      feedback.querySelector("p").textContent = wasDue
        ? `${quiz.decisionRule}${correct ? "" : "\n\n" + correctOption.explanation}`
        : `${quiz.decisionRule}\n\n这题今天不到期，这次作答不改变复习间隔。`;
      article.querySelector(".lesson-question-state").textContent = correct ? "刚答对" : "刚答错";
      renderSummary();
    });
    return article;
  }

  list.replaceChildren(...questions.map(renderQuestion));
  if (!questions.length) {
    const empty = document.createElement("p");
    empty.className = "lesson-quiz-empty";
    empty.innerHTML = '这个模式还没有专属的辨析题，可以去 <a href="../quiz.html">辨析训练</a> 练全部题。';
    list.append(empty);
  }
  renderSummary();
  review.subscribe(renderSummary);
})();
