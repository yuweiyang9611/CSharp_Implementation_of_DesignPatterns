import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import vm from "node:vm";
const catalog = JSON.parse(readFileSync(new URL("../site/data/coding-exercises.json", import.meta.url), "utf8"));
const quizzes = JSON.parse(readFileSync(new URL("../site/data/learning-catalog.json", import.meta.url), "utf8"));
assert.equal(catalog.exercises.length, 8);
assert.equal(new Set(catalog.exercises.map(e => e.id)).size, catalog.exercises.length);
assert.equal(quizzes.quizzes.length, 24);
assert.equal(quizzes.quizzes.filter(q => !q.options.find(o => o.id === q.correctKey).patternKey).length, 4);
assert.equal(new Set(quizzes.quizzes.flatMap(q => q.patternKeys)).size, 23);
const source = readFileSync(new URL("../site/assets/exercise-progress.js", import.meta.url), "utf8");
const key = "csharp-design-patterns-exercises-v1";
const values = new Map(), handlers = [];
let blocked = false, warning = false;
const window = {
  ExerciseCatalog: catalog,
  LearningStorageStatus: { report(name, failed) { warning = failed; } },
  addEventListener(type, handler) { handlers.push(handler); },
};
vm.runInNewContext(source, { window, structuredClone, TextEncoder, localStorage: {
  getItem: key => values.get(key) ?? null,
  setItem: (key, value) => { if (blocked) throw new Error("Quota"); values.set(key, value); },
} });
const state = window.ExerciseProgress, exercise = catalog.exercises[0];
state.update(exercise.id, exercise.solution, true);
assert.equal(state.stats().passed, 1);
state.update(exercise.id, exercise.solution + "\n// edited");
assert.equal(state.stats().passed, 0);
blocked = true;
state.update(exercise.id, "// unsaved draft");
assert.equal(warning, true);
values.delete(key); handlers.forEach(handler => handler({ key }));
assert.equal(state.get(exercise.id).code, "// unsaved draft", "storage events must not erase unsaved work");
const backup = state.exportData();
assert.equal(state.restore({ ...backup, entries: { [exercise.id]: { version: 1, code: 123, passed: true } } }), false);
assert.equal(state.get(exercise.id).code, "// unsaved draft");
blocked = false;
assert.equal(state.restore(backup), true);
assert.equal(warning, false);
backup.entries[exercise.id].version++;
assert.equal(state.restore(backup), true);
assert.equal(state.get(exercise.id).code, exercise.starter, "new exercise version invalidates old draft/result");
assert.equal(state.update(exercise.id, "x".repeat(102401)), false);
console.log("Exercise catalog/storage passed: 24 quizzes, 23 patterns, 4 simple answers, 8 exercises, draft invalidation, version reset and unsaved backup recovery.");
