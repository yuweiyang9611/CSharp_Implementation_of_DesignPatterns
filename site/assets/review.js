(() => {
  "use strict";

  const storageKey = "csharp-design-patterns-review-v1";
  const format = "csharp-design-patterns-review";
  const day = 24 * 60 * 60 * 1000;
  const intervals = [0, day, 3 * day, 7 * day, 14 * day, 30 * day];
  const listeners = new Set();
  let quizzes = [];
  let state = { version: 1, questions: {}, updatedAt: null };
  let initialized = false;

  function clone(value) {
    return JSON.parse(JSON.stringify(value));
  }

  function contentVersion(quiz) {
    return `${quiz.id}:${quiz.version}`;
  }

  function validRecord(value) {
    return value && typeof value === "object" &&
      Number.isInteger(value.box) && value.box >= 0 && value.box <= 5 &&
      Number.isInteger(value.streak) && value.streak >= 0 &&
      Number.isInteger(value.attempts) && value.attempts >= 0 &&
      Number.isInteger(value.lapses) && value.lapses >= 0 &&
      Number.isFinite(value.dueAt) && Number.isFinite(value.lastAnsweredAt);
  }

  function normalize(raw) {
    const next = { version: 1, questions: {}, updatedAt: raw?.updatedAt ?? null };
    if (!raw || raw.version !== 1 || !raw.questions || typeof raw.questions !== "object") return next;
    for (const quiz of quizzes) {
      const record = raw.questions[quiz.id];
      if (validRecord(record) && record.contentVersion === contentVersion(quiz)) {
        next.questions[quiz.id] = { ...record };
      }
    }
    return next;
  }

  function read() {
    try {
      return normalize(JSON.parse(localStorage.getItem(storageKey)));
    } catch {
      return normalize(null);
    }
  }

  function write() {
    try {
      localStorage.setItem(storageKey, JSON.stringify(state));
    } catch {
      // Review remains usable for this page when storage is unavailable.
    }
  }

  function notify() {
    const current = snapshot();
    for (const listener of listeners) listener(current);
  }

  function configure(items) {
    quizzes = Array.isArray(items) ? items.map((quiz) => ({ ...quiz })) : [];
    state = read();
    initialized = true;
    notify();
    return api;
  }

  function dueQuestions(at = Date.now()) {
    return quizzes
      .filter((quiz) => !state.questions[quiz.id] || state.questions[quiz.id].dueAt <= at)
      .map((quiz) => ({ ...quiz, review: state.questions[quiz.id] ? { ...state.questions[quiz.id] } : null }));
  }

  function record(questionId, correct, at = Date.now()) {
    const quiz = quizzes.find((candidate) => candidate.id === questionId);
    if (!quiz || !Number.isFinite(at)) return null;
    const previous = state.questions[questionId];
    const isCorrect = Boolean(correct);
    const box = isCorrect ? Math.min(5, (previous?.box ?? 0) + 1) : 0;
    const next = {
      contentVersion: contentVersion(quiz),
      box,
      streak: isCorrect ? (previous?.streak ?? 0) + 1 : 0,
      attempts: (previous?.attempts ?? 0) + 1,
      lapses: (previous?.lapses ?? 0) + (isCorrect ? 0 : 1),
      lastAnsweredAt: at,
      dueAt: isCorrect ? at + intervals[box] : at + 10 * 60 * 1000,
      lastCorrect: isCorrect,
    };
    state.questions[questionId] = next;
    state.updatedAt = at;
    write();
    notify();
    return { ...next };
  }

  function questionState(questionId) {
    const value = state.questions[questionId];
    return value ? { ...value } : null;
  }

  function stats(at = Date.now()) {
    const answered = quizzes.filter((quiz) => state.questions[quiz.id]).length;
    const mastered = quizzes.filter((quiz) => (state.questions[quiz.id]?.box ?? 0) >= 4).length;
    return { total: quizzes.length, due: dueQuestions(at).length, answered, mastered };
  }

  function snapshot() {
    return clone(state);
  }

  function restore(raw) {
    const payload = raw?.format === format ? raw.review : raw;
    if (!payload || payload.version !== 1) return false;
    state = normalize(payload);
    state.updatedAt = Date.now();
    write();
    notify();
    return true;
  }

  function exportData() {
    return { format, version: 1, exportedAt: Date.now(), review: snapshot() };
  }

  function reset() {
    const previous = snapshot();
    state = { version: 1, questions: {}, updatedAt: Date.now() };
    try { localStorage.removeItem(storageKey); } catch { /* Optional storage. */ }
    notify();
    return previous;
  }

  function subscribe(listener) {
    listeners.add(listener);
    if (initialized) listener(snapshot());
    return () => listeners.delete(listener);
  }

  window.addEventListener("storage", (event) => {
    if (!initialized || event.key !== storageKey) return;
    state = read();
    notify();
  });

  const api = Object.freeze({ configure, dueQuestions, record, questionState, stats, snapshot, restore, exportData, reset, subscribe });
  window.ReviewScheduler = api;
})();
