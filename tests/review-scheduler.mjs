import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import vm from "node:vm";

const source = await readFile(new URL("../site/assets/review.js", import.meta.url), "utf8");
const values = new Map();
const windowObject = { addEventListener() {} };
const context = vm.createContext({
  window: windowObject,
  localStorage: {
    getItem(key) { return values.get(key) ?? null; },
    setItem(key, value) { values.set(key, value); },
    removeItem(key) { values.delete(key); },
  },
  Date,
  JSON,
  Number,
  Array,
  Set,
  Boolean,
});
vm.runInContext(source, context, { filename: "review.js" });

const quizzes = Array.from({ length: 6 }, (_, index) => ({ id: `q${index + 1}`, version: 1 }));
const scheduler = windowObject.ReviewScheduler.configure(quizzes);
const day = 24 * 60 * 60 * 1000;
const start = Date.UTC(2026, 8, 1, 0, 0, 0);
assert.equal(scheduler.dueQuestions(start).length, 6, "all new questions should be due");

let record = scheduler.record("q1", true, start);
assert.equal(record.box, 1);
assert.equal(record.dueAt, start + day);
for (const expectedDays of [3, 7, 14, 30, 30]) {
  const answeredAt = record.dueAt;
  record = scheduler.record("q1", true, answeredAt);
  assert.equal(record.dueAt, answeredAt + expectedDays * day);
}
assert.equal(record.box, 5, "Leitner box should cap at five");

const failedAt = record.dueAt;
record = scheduler.record("q1", false, failedAt);
assert.equal(record.box, 0);
assert.equal(record.lapses, 1);
assert.equal(record.dueAt, failedAt + 10 * 60 * 1000);
assert.ok(!scheduler.dueQuestions(failedAt).some((quiz) => quiz.id === "q1"));
assert.ok(scheduler.dueQuestions(record.dueAt).some((quiz) => quiz.id === "q1"));

const saved = scheduler.snapshot();
scheduler.configure(quizzes.map((quiz) => quiz.id === "q1" ? { ...quiz, version: 2 } : quiz));
assert.equal(scheduler.questionState("q1"), null, "content version changes should reset only that question");
assert.equal(scheduler.restore(saved), true);
scheduler.reset();
assert.equal(scheduler.dueQuestions(start).length, 6);

console.log("Review scheduler passed: due queue, 1/3/7/14/30-day intervals, lapse reset, version reset, and restore.");
