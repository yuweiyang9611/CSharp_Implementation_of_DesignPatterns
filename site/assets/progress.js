(() => {
  "use strict";

  const storageKey = "csharp-design-patterns-progress-v3";
  const legacyV2Key = "csharp-design-patterns-progress-v2";
  const legacyV1Key = "csharp-design-patterns-progress-v1";
  const exportFormat = "csharp-design-patterns-progress";
  const stages = ["未开始", "已阅读", "已运行", "已改造", "已验证"];
  const legacyStageKinds = [null, "read", "run", "change", "verify"];
  let catalog = [];
  let state = emptyState();
  let initialized = false;
  const listeners = new Set();

  function emptyState() {
    return { version: 3, updatedAt: null, items: {}, resume: null };
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

  function clone(value) {
    return JSON.parse(JSON.stringify(value));
  }

  function catalogItem(id) {
    return catalog.find((item) => item.id === id) ?? null;
  }

  function normalizeCatalog(items) {
    const seenItems = new Set();
    return items.map((item) => {
      const tasks = Array.isArray(item.tasks) ? item.tasks : [];
      const seenTasks = new Set();
      const normalizedTasks = tasks.map((task) => ({
        ...task,
        id: String(task.id),
        title: String(task.title),
      })).filter((task) => task.id && !seenTasks.has(task.id) && seenTasks.add(task.id));
      return { ...item, id: String(item.id), tasks: normalizedTasks };
    }).filter((item) => item.id && item.tasks.length > 0 && !seenItems.has(item.id) && seenItems.add(item.id));
  }

  function normalizeV3(raw) {
    if (!raw || raw.version !== 3 || raw.items === null || typeof raw.items !== "object" || Array.isArray(raw.items)) {
      return null;
    }

    const normalized = emptyState();
    for (const item of catalog) {
      const rawItem = raw.items[item.id];
      if (!rawItem || rawItem.completed === null || typeof rawItem.completed !== "object" || Array.isArray(rawItem.completed)) continue;
      const completed = {};
      for (const task of item.tasks) {
        if (!Object.hasOwn(rawItem.completed, task.id)) break;
        const timestamp = rawItem.completed[task.id];
        completed[task.id] = typeof timestamp === "string" ? timestamp : null;
      }
      if (Object.keys(completed).length > 0) {
        normalized.items[item.id] = { completed, updatedAt: typeof rawItem.updatedAt === "string" ? rawItem.updatedAt : null };
      }
    }

    const resumeId = raw.resume?.itemId;
    const resumeTaskId = raw.resume?.taskId;
    const resumeItem = catalogItem(resumeId);
    normalized.resume = resumeItem
      ? {
          itemId: resumeId,
          taskId: resumeItem.tasks.some((task) => task.id === resumeTaskId) ? resumeTaskId : null,
          updatedAt: typeof raw.resume.updatedAt === "string" ? raw.resume.updatedAt : null,
        }
      : null;
    normalized.updatedAt = typeof raw.updatedAt === "string" ? raw.updatedAt : null;
    return normalized;
  }

  function migrateV2(raw) {
    if (!raw || raw.version !== 2 || raw.items === null || typeof raw.items !== "object" || Array.isArray(raw.items)) return null;
    const migrated = emptyState();
    const timestamp = now();
    for (const item of catalog) {
      const level = Number(raw.items[item.id]?.level);
      if (!Number.isInteger(level) || level <= 0 || level > 4) continue;
      const completedCount = taskCountForLegacyLevel(item, level);
      migrated.items[item.id] = {
        completed: Object.fromEntries(item.tasks.slice(0, completedCount).map((task) => [task.id, timestamp])),
        updatedAt: timestamp,
      };
    }
    if (catalogItem(raw.resume?.itemId)) {
      migrated.resume = { itemId: raw.resume.itemId, taskId: null, updatedAt: timestamp };
    }
    migrated.updatedAt = timestamp;
    return migrated;
  }

  function taskCountForLegacyLevel(item, level) {
    if (level === 4) return item.tasks.length;
    const kind = legacyStageKinds[level];
    const lastMatchingIndex = item.tasks.findLastIndex((task) => task.kind === kind);
    return lastMatchingIndex >= 0 ? lastMatchingIndex + 1 : Math.min(level, item.tasks.length);
  }

  function migrateV1(raw) {
    if (!Array.isArray(raw)) return null;
    const migrated = emptyState();
    const timestamp = now();
    const keys = new Set(raw.map(String));
    for (const item of catalog.filter((candidate) => candidate.type === "pattern")) {
      const key = item.id.replace(/^pattern:/u, "");
      if (!keys.has(key)) continue;
      migrated.items[item.id] = {
        completed: { [item.tasks[0].id]: timestamp },
        updatedAt: timestamp,
      };
    }
    migrated.updatedAt = timestamp;
    return migrated;
  }

  function readStorage() {
    try {
      const current = normalizeV3(safeParse(localStorage.getItem(storageKey)));
      if (current) return current;
      const migrated = migrateV2(safeParse(localStorage.getItem(legacyV2Key))) ??
        migrateV1(safeParse(localStorage.getItem(legacyV1Key)));
      if (migrated) {
        writeStorage(migrated);
        return migrated;
      }
    } catch {
      // Device-local progress is optional; the in-memory state remains usable.
    }
    return emptyState();
  }

  function writeStorage(nextState) {
    try {
      localStorage.setItem(storageKey, JSON.stringify(nextState));
    } catch {
      // Device-local progress is optional; the in-memory state remains usable.
    }
  }

  function notify() {
    const current = snapshot();
    for (const listener of listeners) listener(current);
  }

  function configure(items) {
    catalog = normalizeCatalog(items);
    state = readStorage();
    initialized = true;
    notify();
    return api;
  }

  function snapshot() {
    return clone(state);
  }

  function completedTaskIds(id) {
    const item = catalogItem(id);
    if (!item) return [];
    const completed = state.items[id]?.completed ?? {};
    return item.tasks.filter((task) => Object.hasOwn(completed, task.id)).map((task) => task.id);
  }

  function itemProgress(id) {
    const item = catalogItem(id);
    if (!item) return null;
    const completedIds = completedTaskIds(id);
    const nextTask = item.tasks[completedIds.length] ?? null;
    return {
      id,
      completed: completedIds.length,
      total: item.tasks.length,
      percent: Math.round((completedIds.length / item.tasks.length) * 100),
      verified: completedIds.length === item.tasks.length,
      nextTask: nextTask ? { ...nextTask } : null,
      tasks: item.tasks.map((task, index) => ({
        ...task,
        completed: index < completedIds.length,
        available: index <= completedIds.length,
      })),
    };
  }

  function getLevel(id) {
    const current = itemProgress(id);
    if (!current) return 0;
    if (current.verified) return 4;
    return Math.min(3, Math.floor((current.completed / current.total) * 4));
  }

  function isTaskComplete(id, taskId) {
    return completedTaskIds(id).includes(taskId);
  }

  function setTaskComplete(id, taskId, requestedComplete = true) {
    const item = catalogItem(id);
    const taskIndex = item?.tasks.findIndex((task) => task.id === taskId) ?? -1;
    if (!item || taskIndex < 0) return false;

    const current = itemProgress(id);
    const complete = Boolean(requestedComplete);
    if (complete && taskIndex > current.completed) return false;
    const timestamp = now();
    const completedCount = complete ? Math.max(current.completed, taskIndex + 1) : Math.min(current.completed, taskIndex);
    if (completedCount === 0) delete state.items[id];
    else {
      state.items[id] = {
        completed: Object.fromEntries(item.tasks.slice(0, completedCount).map((task) => [task.id, timestamp])),
        updatedAt: timestamp,
      };
    }
    const nextTask = item.tasks[completedCount] ?? null;
    state.resume = { itemId: id, taskId: nextTask?.id ?? item.tasks.at(-1).id, updatedAt: timestamp };
    state.updatedAt = timestamp;
    writeStorage(state);
    notify();
    return true;
  }

  function setLevel(id, requestedLevel) {
    const item = catalogItem(id);
    const level = Number(requestedLevel);
    if (!item || !Number.isInteger(level) || level < 0 || level > 4) return false;
    const targetCount = level === 0 ? 0 : taskCountForLegacyLevel(item, level);
    const timestamp = now();
    if (targetCount === 0) delete state.items[id];
    else {
      state.items[id] = {
        completed: Object.fromEntries(item.tasks.slice(0, targetCount).map((task) => [task.id, timestamp])),
        updatedAt: timestamp,
      };
    }
    state.resume = { itemId: id, taskId: item.tasks[targetCount]?.id ?? item.tasks.at(-1).id, updatedAt: timestamp };
    state.updatedAt = timestamp;
    writeStorage(state);
    notify();
    return true;
  }

  function summary() {
    const totalItems = catalog.length;
    const earnedTasks = catalog.reduce((sum, item) => sum + itemProgress(item.id).completed, 0);
    const totalTasks = catalog.reduce((sum, item) => sum + item.tasks.length, 0);
    const verifiedItems = catalog.filter((item) => itemProgress(item.id).verified).length;
    return {
      totalItems,
      earnedTasks,
      totalTasks,
      earnedStages: earnedTasks,
      totalStages: totalTasks,
      verifiedItems,
      percent: totalTasks === 0 ? 0 : Math.round((earnedTasks / totalTasks) * 100),
    };
  }

  function taskUrl(item, task) {
    if (!task?.href) return item.url;
    if (/^https?:/u.test(task.href)) return task.href;
    if (task.href.startsWith("#")) return `${item.url}${task.href}`;
    return task.href;
  }

  function resumeItem() {
    const recent = catalogItem(state.resume?.itemId);
    const item = recent && !itemProgress(recent.id).verified
      ? recent
      : catalog.find((candidate) => !itemProgress(candidate.id).verified) ?? catalog[0] ?? null;
    if (!item) return null;
    const current = itemProgress(item.id);
    const nextTask = current.nextTask ?? item.tasks[0];
    return {
      ...item,
      progress: current,
      nextTask,
      nextAction: current.verified ? "复习" : nextTask.title,
      url: taskUrl(item, nextTask),
    };
  }

  function reset() {
    const previous = snapshot();
    state = emptyState();
    try {
      localStorage.removeItem(storageKey);
      localStorage.removeItem(legacyV2Key);
      localStorage.removeItem(legacyV1Key);
    } catch {
      // Keep the in-memory reset when storage is blocked.
    }
    notify();
    return previous;
  }

  function restore(previous) {
    const normalized = normalizeV3(previous?.progress ?? previous);
    if (!normalized) return false;
    state = normalized;
    state.updatedAt = now();
    writeStorage(state);
    notify();
    return true;
  }

  function exportData() {
    return {
      format: exportFormat,
      version: 3,
      exportedAt: now(),
      progress: snapshot(),
    };
  }

  function importData(payload) {
    if (!payload || payload.format !== exportFormat || payload.version !== 3) return false;
    return restore(payload.progress);
  }

  function toMarkdown() {
    const lines = [
      "# C# 设计模式学习记录",
      "",
      `导出时间：${now()}`,
      "",
    ];
    for (const item of catalog) {
      const current = itemProgress(item.id);
      lines.push(`## ${item.title}`, "");
      for (const task of current.tasks) lines.push(`- [${task.completed ? "x" : " "}] ${task.title}`);
      lines.push("");
    }
    return lines.join("\n");
  }

  function subscribe(listener) {
    listeners.add(listener);
    if (initialized) listener(snapshot());
    return () => listeners.delete(listener);
  }

  window.addEventListener("storage", (event) => {
    if (!initialized || ![storageKey, legacyV2Key, legacyV1Key].includes(event.key)) return;
    state = readStorage();
    notify();
  });

  const api = Object.freeze({
    stages,
    configure,
    getLevel,
    setLevel,
    isTaskComplete,
    setTaskComplete,
    itemProgress,
    summary,
    resumeItem,
    reset,
    restore,
    exportData,
    importData,
    toMarkdown,
    subscribe,
    snapshot,
  });

  window.LearningProgress = api;
})();
