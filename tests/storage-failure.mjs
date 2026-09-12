import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import vm from "node:vm";

const progressSource = await readFile(new URL("../site/assets/progress.js", import.meta.url), "utf8");
const reviewSource = await readFile(new URL("../site/assets/review.js", import.meta.url), "utf8");

function createSession() {
  const values = new Map();
  const failures = new Set();
  const listeners = [];
  const blocked = { read: false, write: false, remove: false };
  const window = {
    addEventListener(name, listener) { if (name === "storage") listeners.push(listener); },
    LearningStorageStatus: { report(source, failed) { failed ? failures.add(source) : failures.delete(source); } },
  };
  const context = vm.createContext({
    window,
    localStorage: {
      getItem(key) { if (blocked.read) throw new Error("SecurityError"); return values.get(key) ?? null; },
      setItem(key, value) { if (blocked.write) throw new Error("QuotaExceededError"); values.set(key, value); },
      removeItem(key) { if (blocked.remove) throw new Error("SecurityError"); values.delete(key); },
    },
  });
  vm.runInContext(progressSource, context, { filename: "progress.js" });
  vm.runInContext(reviewSource, context, { filename: "review.js" });
  return { window, values, failures, blocked, storageEvent: (key) => listeners.forEach((listener) => listener({ key })) };
}

const catalog = [{ id: "pattern:observer", type: "pattern", title: "Observer", tasks: [{ id: "read", title: "Read", kind: "read" }] }];
const quizzes = [{ id: "q1", version: 1 }];
const progressKey = "csharp-design-patterns-progress-v3";
const reviewKey = "csharp-design-patterns-review-v1";

const session = createSession();
const progress = session.window.LearningProgress.configure(catalog);
const review = session.window.ReviewScheduler.configure(quizzes);
assert.equal(session.failures.size, 0);
session.blocked.write = true;
assert.equal(progress.setTaskComplete("pattern:observer", "read"), true);
review.record("q1", true, 1000);
assert.equal(progress.isPersistent(), false);
assert.equal(review.isPersistent(), false);
assert.deepEqual([...session.failures].sort(), ["progress", "review"]);
assert.equal(progress.summary().earnedTasks, 1, "failed storage must not prevent learning");
assert.equal(review.questionState("q1").attempts, 1);

session.storageEvent(null);
session.storageEvent(progressKey);
session.storageEvent(reviewKey);
assert.equal(progress.summary().earnedTasks, 1, "another tab must not discard unsaved progress");
assert.equal(review.questionState("q1").attempts, 1, "another tab must not discard unsaved answers");

const progressBackup = JSON.parse(JSON.stringify(progress.exportData()));
const reviewBackup = JSON.parse(JSON.stringify(review.exportData()));
const restoredSession = createSession();
const restoredProgress = restoredSession.window.LearningProgress.configure(catalog);
const restoredReview = restoredSession.window.ReviewScheduler.configure(quizzes);
assert.equal(restoredProgress.importData(progressBackup), true);
assert.equal(restoredReview.restore(reviewBackup), true);
assert.equal(restoredProgress.summary().earnedTasks, 1);
assert.equal(restoredReview.questionState("q1").attempts, 1);

session.blocked.write = false;
progress.restore(progressBackup);
assert.equal(progress.isPersistent(), true);
assert.deepEqual([...session.failures], ["review"], "one recovered store must not hide another failure");
review.restore(reviewBackup);
assert.equal(session.failures.size, 0);
assert.equal(JSON.parse(session.values.get(progressKey)).items["pattern:observer"].completed.read !== undefined, true);
assert.equal(JSON.parse(session.values.get(reviewKey)).questions.q1.attempts, 1);

session.blocked.remove = true;
progress.reset();
review.reset();
assert.equal(progress.summary().earnedTasks, 0);
assert.equal(review.stats().answered, 0);
assert.equal(session.failures.size, 2, "failed reset must warn that old saved records remain");
session.blocked.remove = false;
progress.reset();
review.reset();
assert.equal(session.failures.size, 0);
assert.equal(session.values.size, 0);

const blockedSession = createSession();
blockedSession.blocked.read = true;
const blockedProgress = blockedSession.window.LearningProgress.configure(catalog);
const blockedReview = blockedSession.window.ReviewScheduler.configure(quizzes);
assert.equal(blockedSession.failures.size, 2, "read failures should be visible on page load");
blockedSession.blocked.write = true;
blockedProgress.setTaskComplete("pattern:observer", "read");
blockedReview.record("q1", false, 1000);
assert.equal(blockedProgress.summary().earnedTasks, 1);
assert.equal(blockedReview.questionState("q1").lapses, 1);

restoredSession.blocked.read = true;
restoredSession.storageEvent(null);
assert.equal(restoredProgress.summary().earnedTasks, 1, "a failed cross-tab read must retain the current progress");
assert.equal(restoredReview.questionState("q1").attempts, 1, "a failed cross-tab read must retain current answers");
assert.equal(restoredSession.failures.size, 2);

console.log("Storage failure tests passed: blocked reads/writes/reset, in-memory records, backups, cross-tab protection and recovery.");
