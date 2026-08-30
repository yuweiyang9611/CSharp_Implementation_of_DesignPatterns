(() => {
  "use strict";

  const storageKey = "csharp-design-patterns-progress-v2";
  const legacyStorageKey = "csharp-design-patterns-progress-v1";
  const stages = ["未开始", "已阅读", "已运行", "已改造", "已验证"];
  let catalog = [];
  let state = emptyState();
  let initialized = false;
  const listeners = new Set();

  function emptyState() {
    return { version: 2, updatedAt: null, items: {}, resume: null };
  }

  function now() {
    return new Date().toISOString();
  }

  function safeParse(value) {
    try {
      return JSON.parse(value);
    } catch {
      return null;
    }
  }

  function normalize(raw) {
    const validIds = new Set(catalog.map((item) => item.id));
    if (!raw || raw.version !== 2 || raw.items === null || typeof raw.items !== "object" || Array.isArray(raw.items)) {
      return null;
    }

    const normalized = emptyState();
    for (const [id, item] of Object.entries(raw.items)) {
      const level = Number(item?.level);
      if (!validIds.has(id) || !Number.isInteger(level) || level < 0 || level > 4) continue;
      if (level > 0) normalized.items[id] = { level, updatedAt: item.updatedAt ?? null };
    }

    const resumeId = raw.resume?.itemId;
    normalized.resume = validIds.has(resumeId)
      ? { itemId: resumeId, updatedAt: raw.resume.updatedAt ?? null }
      : null;
    normalized.updatedAt = raw.updatedAt ?? null;
    return normalized;
  }

  function readStorage() {
    try {
      const current = normalize(safeParse(localStorage.getItem(storageKey)));
      if (current) return current;

      const validPatternKeys = new Set(
        catalog.filter((item) => item.type === "pattern").map((item) => item.id.replace("pattern:", "")),
      );
      const legacy = safeParse(localStorage.getItem(legacyStorageKey));
      if (!Array.isArray(legacy)) return emptyState();

      const migrated = emptyState();
      const timestamp = now();
      for (const key of new Set(legacy)) {
        if (validPatternKeys.has(key)) {
          migrated.items[`pattern:${key}`] = { level: 1, updatedAt: timestamp };
        }
      }
      migrated.updatedAt = timestamp;
      writeStorage(migrated);
      return migrated;
    } catch {
      return emptyState();
    }
  }

  function writeStorage(nextState) {
    try {
      localStorage.setItem(storageKey, JSON.stringify(nextState));
    } catch {
      // The in-memory state still works when storage is blocked.
    }
  }

  function notify() {
    for (const listener of listeners) listener(snapshot());
  }

  function configure(items) {
    catalog = items.map((item) => ({ ...item }));
    state = readStorage();
    initialized = true;
    notify();
    return api;
  }

  function snapshot() {
    return JSON.parse(JSON.stringify(state));
  }

  function getLevel(id) {
    return state.items[id]?.level ?? 0;
  }

  function setLevel(id, requestedLevel) {
    const level = Number(requestedLevel);
    if (!catalog.some((item) => item.id === id) || !Number.isInteger(level) || level < 0 || level > 4) {
      return false;
    }

    const timestamp = now();
    if (level === 0) delete state.items[id];
    else state.items[id] = { level, updatedAt: timestamp };
    state.resume = { itemId: id, updatedAt: timestamp };
    state.updatedAt = timestamp;
    writeStorage(state);
    notify();
    return true;
  }

  function summary() {
    const totalItems = catalog.length;
    const earnedStages = catalog.reduce((sum, item) => sum + getLevel(item.id), 0);
    const totalStages = totalItems * 4;
    const verifiedItems = catalog.filter((item) => getLevel(item.id) === 4).length;
    return {
      totalItems,
      earnedStages,
      totalStages,
      verifiedItems,
      percent: totalStages === 0 ? 0 : Math.round((earnedStages / totalStages) * 100),
    };
  }

  function resumeItem() {
    const recentId = state.resume?.itemId;
    const recent = catalog.find((item) => item.id === recentId && getLevel(item.id) < 4);
    const item = recent ?? catalog.find((candidate) => getLevel(candidate.id) < 4) ?? catalog[0] ?? null;
    if (!item) return null;
    return { ...item, level: getLevel(item.id), nextStage: stages[Math.min(getLevel(item.id) + 1, 4)] };
  }

  function reset() {
    const previous = snapshot();
    state = emptyState();
    try {
      localStorage.removeItem(storageKey);
      localStorage.removeItem(legacyStorageKey);
    } catch {
      // Keep the in-memory reset when storage is blocked.
    }
    notify();
    return previous;
  }

  function restore(previous) {
    const normalized = normalize(previous);
    if (!normalized) return false;
    state = normalized;
    state.updatedAt = now();
    writeStorage(state);
    notify();
    return true;
  }

  function subscribe(listener) {
    listeners.add(listener);
    if (initialized) listener(snapshot());
    return () => listeners.delete(listener);
  }

  window.addEventListener("storage", (event) => {
    if (!initialized || (event.key !== storageKey && event.key !== legacyStorageKey)) return;
    state = readStorage();
    notify();
  });

  const api = Object.freeze({
    stages,
    configure,
    getLevel,
    setLevel,
    summary,
    resumeItem,
    reset,
    restore,
    subscribe,
    snapshot,
  });

  window.LearningProgress = api;
})();
