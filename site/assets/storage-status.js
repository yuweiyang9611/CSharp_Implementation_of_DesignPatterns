(() => {
  "use strict";

  const failures = new Set();
  const panel = document.createElement("aside");
  panel.id = "storage-warning";
  panel.className = "storage-warning";
  panel.hidden = true;
  panel.setAttribute("aria-label", "记录保存状态");
  const message = document.createElement("p");
  message.setAttribute("role", "status");
  const backup = document.createElement("button");
  backup.type = "button";
  backup.textContent = "下载当前记录备份";
  backup.addEventListener("click", () => {
    const progress = window.LearningProgress?.exportData();
    const review = window.ReviewScheduler?.exportData();
    const exercises = window.ExerciseProgress?.exportData();
    const payload = progress && review
      ? { format: "csharp-design-patterns-learning-backup", version: 1, exportedAt: Date.now(), progress, review, exercises }
      : exercises ?? progress ?? review;
    const url = URL.createObjectURL(new Blob([JSON.stringify(payload, null, 2)], { type: "application/json" }));
    const link = document.createElement("a");
    link.href = url;
    link.download = "csharp-design-patterns-unsaved-backup.json";
    link.click();
    window.setTimeout(() => URL.revokeObjectURL(url), 1000);
  });
  panel.append(message, backup);
  document.querySelector("main").prepend(panel);

  window.LearningStorageStatus = Object.freeze({
    report(source, failed) {
      failed ? failures.add(source) : failures.delete(source);
      panel.hidden = failures.size === 0;
      message.textContent = failures.size
        ? "浏览器无法保存或读取记录。当前页面的更改仅在本次页面有效，刷新或离开前请下载备份；可在首页导入恢复。"
        : "";
    },
  });
})();
