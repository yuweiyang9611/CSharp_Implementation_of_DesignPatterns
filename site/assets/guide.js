(() => {
  "use strict";

  document.documentElement.classList.add("js");

  const root = document.documentElement;
  const header = document.querySelector(".learning-site-header");
  const nav = document.querySelector(".learning-site-nav");
  const navToggle = document.querySelector(".learning-nav-toggle");
  const toc = document.querySelector(".guide-toc");
  const tocToggle = document.querySelector(".guide-toc-toggle");
  const tocList = document.querySelector("#guide-toc-list");
  const guideId = document.body.dataset.guideId;
  const storageKey = `csharp-design-patterns-guide-position-v1:${guideId}`;
  const headings = toc
    ? [...toc.querySelectorAll("a[data-heading-id]")]
      .map((link) => ({ link, heading: document.getElementById(link.dataset.headingId) }))
      .filter((item) => item.heading)
    : [];
  const chapters = headings.filter((item) => item.link.dataset.chapter === "true");
  let userScrolled = false;
  let scheduled = false;
  let pendingHeadingId = null;
  let saveTimer = 0;
  let lastSavedHeadingId = null;

  function headerOffset() {
    return Math.ceil(header?.getBoundingClientRect().height ?? 0) + 16;
  }

  function updateHeaderOffset() {
    root.style.setProperty("--guide-header-offset", `${headerOffset()}px`);
  }

  function toggleNav(force) {
    const open = force ?? !nav.classList.contains("open");
    nav.classList.toggle("open", open);
    navToggle.setAttribute("aria-expanded", String(open));
    updateHeaderOffset();
  }

  function toggleToc(force) {
    if (!toc) return;
    const open = force ?? !toc.classList.contains("open");
    toc.classList.toggle("open", open);
    tocToggle?.setAttribute("aria-expanded", String(open));
    if (tocToggle) tocToggle.textContent = open ? "收起目录" : "展开目录";
  }

  function readSavedPosition() {
    try {
      const saved = JSON.parse(localStorage.getItem(storageKey));
      return saved?.version === 1 && document.getElementById(saved.headingId) ? saved : null;
    } catch {
      return null;
    }
  }

  function persistPosition(headingId) {
    if (!headingId || headingId === lastSavedHeadingId) return;
    try {
      localStorage.setItem(storageKey, JSON.stringify({
        version: 1,
        headingId,
        updatedAt: Date.now(),
      }));
      lastSavedHeadingId = headingId;
    } catch {
      // Reading remains fully functional without browser storage.
    }
  }

  function savePosition(headingId) {
    if (!userScrolled || headingId === lastSavedHeadingId) return;
    pendingHeadingId = headingId;
    window.clearTimeout(saveTimer);
    saveTimer = window.setTimeout(() => {
      persistPosition(pendingHeadingId);
      pendingHeadingId = null;
    }, 350);
  }

  function updateReadingPosition() {
    scheduled = false;
    if (headings.length === 0) return;
    const offset = headerOffset() + 8;
    let active = headings[0];
    for (const item of headings) {
      if (item.heading.getBoundingClientRect().top <= offset) active = item;
      else break;
    }

    for (const item of headings) {
      if (item === active) item.link.setAttribute("aria-current", "location");
      else item.link.removeAttribute("aria-current");
    }

    const activeChapter = [...chapters].reverse().find((item) =>
      item.heading.offsetTop <= active.heading.offsetTop,
    ) ?? chapters[0];
    const chapterIndex = chapters.indexOf(activeChapter);
    const previous = document.querySelector("[data-guide-prev]");
    const next = document.querySelector("[data-guide-next]");
    const previousChapter = chapters[chapterIndex - 1];
    const nextChapter = chapters[chapterIndex + 1];

    if (previousChapter) {
      previous.hidden = false;
      previous.href = `#${previousChapter.heading.id}`;
      previous.textContent = `← ${previousChapter.heading.textContent.trim()}`;
    } else previous.hidden = true;

    if (nextChapter) {
      next.hidden = false;
      next.href = `#${nextChapter.heading.id}`;
      next.textContent = `${nextChapter.heading.textContent.trim()} →`;
    } else next.hidden = true;

    savePosition(active.heading.id);
  }

  function scheduleUpdate() {
    if (scheduled) return;
    scheduled = true;
    window.requestAnimationFrame(updateReadingPosition);
  }

  navToggle?.addEventListener("click", () => toggleNav());
  nav?.addEventListener("click", (event) => {
    if (event.target.closest("a")) toggleNav(false);
  });
  tocToggle?.addEventListener("click", () => toggleToc());
  tocList?.addEventListener("click", (event) => {
    if (event.target.closest("a") && window.matchMedia("(max-width: 900px)").matches) toggleToc(false);
  });
  document.addEventListener("keydown", (event) => {
    if (event.key !== "Escape") return;
    if (nav?.classList.contains("open")) {
      toggleNav(false);
      navToggle.focus();
    } else if (toc?.classList.contains("open") && window.matchMedia("(max-width: 900px)").matches) {
      toggleToc(false);
      tocToggle.focus();
    }
  });

  window.addEventListener("scroll", () => {
    userScrolled = true;
    scheduleUpdate();
  }, { passive: true });
  window.addEventListener("resize", () => {
    updateHeaderOffset();
    if (window.matchMedia("(min-width: 901px)").matches) toggleToc(true);
    else toggleToc(false);
    scheduleUpdate();
  });
  window.addEventListener("pagehide", () => persistPosition(pendingHeadingId));

  if (header && "ResizeObserver" in window) new ResizeObserver(updateHeaderOffset).observe(header);
  updateHeaderOffset();
  if (toc && window.matchMedia("(min-width: 901px)").matches) toggleToc(true);

  const saved = readSavedPosition();
  const continueLink = document.querySelector("[data-guide-continue]");
  if (saved && continueLink) {
    const savedHeading = document.getElementById(saved.headingId);
    lastSavedHeadingId = saved.headingId;
    continueLink.hidden = false;
    continueLink.href = `#${saved.headingId}`;
    continueLink.textContent = `继续阅读：${savedHeading.textContent.trim()} →`;
  }
  updateReadingPosition();
})();
