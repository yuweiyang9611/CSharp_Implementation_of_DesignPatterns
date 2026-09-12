(() => {
  "use strict";
  const key = "csharp-design-patterns-exercises-v1";
  const catalog = window.ExerciseCatalog.exercises;
  const listeners = new Set();
  let entries = {};
  let persistent = true;
  function report(failed) {
    persistent = !failed;
    window.LearningStorageStatus?.report("exercises", failed);
  }
  function normalize(payload) {
    if (payload?.format !== "csharp-design-patterns-exercises" || payload.version !== 1 ||
        !payload.entries || typeof payload.entries !== "object" || Array.isArray(payload.entries)) return null;
    const result = {};
    for (const exercise of catalog) {
      if (!Object.hasOwn(payload.entries, exercise.id)) continue;
      const entry = payload.entries[exercise.id];
      if (!entry || !Number.isInteger(entry.version) || typeof entry.code !== "string" ||
          new TextEncoder().encode(entry.code).length > 100 * 1024 || typeof entry.passed !== "boolean") return null;
      if (entry.version === exercise.version) result[exercise.id] = { version: entry.version, code: entry.code, passed: entry.passed };
    }
    return result;
  }
  function exportData() {
    return { format: "csharp-design-patterns-exercises", version: 1, entries: structuredClone(entries) };
  }
  function notify() { for (const listener of listeners) listener(); }
  function save() {
    try { localStorage.setItem(key, JSON.stringify(exportData())); report(false); }
    catch { report(true); }
    notify();
  }
  function read() {
    try {
      const raw = localStorage.getItem(key);
      const next = raw === null ? {} : normalize(JSON.parse(raw));
      if (!next) throw new Error("Invalid exercise records");
      entries = next;
      report(false);
    } catch { report(true); }
  }
  read();
  window.ExerciseProgress = Object.freeze({
    get(id) {
      const exercise = catalog.find((item) => item.id === id);
      return structuredClone(entries[id] ?? { version: exercise.version, code: exercise.starter, passed: false });
    },
    update(id, code, passed = false) {
      const exercise = catalog.find((item) => item.id === id);
      if (!exercise || new TextEncoder().encode(code).length > 100 * 1024) return false;
      entries[id] = { version: exercise.version, code, passed };
      save();
      return true;
    },
    exportData,
    snapshot: exportData,
    validate: (payload) => normalize(payload) !== null,
    restore(payload) {
      const next = normalize(payload);
      if (!next) return false;
      entries = next;
      save();
      return true;
    },
    stats: () => ({ total: catalog.length, passed: Object.values(entries).filter((entry) => entry.passed).length }),
    isPersistent: () => persistent,
    subscribe(listener) { listeners.add(listener); listener(); return () => listeners.delete(listener); },
  });
  window.addEventListener("storage", (event) => {
    if ((event.key === key || event.key === null) && persistent) { read(); notify(); }
  });
})();
