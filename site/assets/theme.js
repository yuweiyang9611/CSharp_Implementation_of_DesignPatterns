// Load synchronously in <head>: sets the theme before the first paint.
(() => {
  "use strict";

  const root = document.documentElement;
  const storageKey = "site-theme";
  const paper = { light: "#f7f8f6", dark: "#141b2b" };

  function saved() {
    try {
      const value = localStorage.getItem(storageKey);
      return value === "light" || value === "dark" ? value : null;
    } catch {
      return null;
    }
  }

  function apply(theme) {
    root.dataset.theme = theme;
    document.querySelector('meta[name="theme-color"]')?.setAttribute("content", paper[theme]);
  }

  const system = window.matchMedia("(prefers-color-scheme: dark)");
  apply(saved() ?? (system.matches ? "dark" : "light"));
  system.addEventListener("change", (event) => {
    if (!saved()) apply(event.matches ? "dark" : "light");
  });

  // Fonts are shared across the family, so a course site's local test server has none to serve.
  if (location.hostname === "yuweiyang9611.github.io") {
    const fonts = document.createElement("link");
    fonts.rel = "stylesheet";
    fonts.href = "/kit/v1/fonts.css";
    document.head.append(fonts);
  }

  document.addEventListener("DOMContentLoaded", () => {
    const nav = document.querySelector(".nb-nav");
    if (!nav) return;
    const toggle = document.createElement("button");
    toggle.type = "button";
    toggle.className = "nb-theme";
    const label = () => { toggle.textContent = root.dataset.theme === "dark" ? "浅色" : "深色"; };
    label();
    toggle.addEventListener("click", () => {
      apply(root.dataset.theme === "dark" ? "light" : "dark");
      try {
        localStorage.setItem(storageKey, root.dataset.theme);
      } catch {
        // The theme still applies for this visit if storage is unavailable.
      }
      label();
    });
    nav.append(toggle);
  });
})();
