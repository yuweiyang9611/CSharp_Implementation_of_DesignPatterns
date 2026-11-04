// 记一笔: one note per lesson, saved as you type.
(() => {
  "use strict";

  const notes = window.LessonNotes;
  const key = document.body.dataset.patternKey;
  const field = document.querySelector("#lesson-note");
  const status = document.querySelector("#lesson-note-status");
  if (!notes || !key || !field || !status) return;

  function describe() {
    const at = notes.updatedAt(key);
    if (!at) return "还没有记过。";
    const when = new Date(at);
    return `上次记于 ${when.getFullYear()}-${String(when.getMonth() + 1).padStart(2, "0")}-${String(when.getDate()).padStart(2, "0")}`;
  }

  field.value = notes.get(key);
  status.textContent = describe();
  let timer = 0;
  field.addEventListener("input", () => {
    window.clearTimeout(timer);
    timer = window.setTimeout(() => {
      if (!notes.set(key, field.value)) {
        status.textContent = `笔记超过 ${notes.limit / 1024} KB，没有保存。`;
        return;
      }
      status.textContent = notes.isPersistent() ? (field.value.trim() ? "已保存在这个浏览器里。" : "已清空。") : "这个浏览器无法保存，离开页面前请自己复制一份。";
    }, 400);
  });
  notes.subscribe(() => {
    if (document.activeElement !== field) { field.value = notes.get(key); status.textContent = describe(); }
  });
})();
