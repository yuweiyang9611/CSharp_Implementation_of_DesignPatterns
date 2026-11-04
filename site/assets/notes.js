// One note per lesson, kept in this browser. The index's JSON backup carries them with everything else.
(() => {
  "use strict";

  const key = "csharp-design-patterns-notes-v1";
  const format = "csharp-design-patterns-notes";
  const limit = 20 * 1024;
  const listeners = new Set();
  let entries = {};
  let persistent = true;

  function report(failed) {
    persistent = !failed;
    window.LearningStorageStatus?.report("notes", failed);
  }

  function normalize(payload) {
    if (payload?.format !== format || payload.version !== 1 || !payload.entries || typeof payload.entries !== "object" || Array.isArray(payload.entries)) return null;
    const result = {};
    for (const [id, entry] of Object.entries(payload.entries)) {
      if (!/^[a-z][a-z0-9-]{0,63}$/.test(id)) return null;
      if (!entry || typeof entry.text !== "string" || !Number.isFinite(entry.updatedAt)) return null;
      if (new TextEncoder().encode(entry.text).length > limit) return null;
      if (entry.text.trim()) result[id] = { text: entry.text, updatedAt: entry.updatedAt };
    }
    return result;
  }

  function exportData() {
    return { format, version: 1, entries: structuredClone(entries) };
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
      entries = next ?? {};
      report(false);
    } catch {
      entries = {};
      report(true);
    }
  }

  function get(id) { return entries[id]?.text ?? ""; }
  function updatedAt(id) { return entries[id]?.updatedAt ?? null; }

  function set(id, text) {
    if (new TextEncoder().encode(text).length > limit) return false;
    if (text.trim()) entries[id] = { text, updatedAt: Date.now() };
    else delete entries[id];
    save();
    return true;
  }

  function restore(payload) {
    const next = normalize(payload);
    if (!next) return false;
    entries = next;
    save();
    return true;
  }

  function snapshot() { return exportData(); }
  function validate(payload) { return normalize(payload) !== null; }
  function subscribe(listener) { listeners.add(listener); return () => listeners.delete(listener); }

  read();
  window.addEventListener("storage", (event) => {
    if (event.key === null || event.key === key) { read(); notify(); }
  });

  window.LessonNotes = Object.freeze({ get, set, updatedAt, exportData, snapshot, restore, validate, subscribe, limit, isPersistent: () => persistent });
})();
