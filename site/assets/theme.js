// Load synchronously in <head>: sets the colour scheme before the first paint.
(() => {
  "use strict";

  const root = document.documentElement;
  const storageKey = "site-theme";
  const paper = { light: "#f7f8f6", dark: "#141b2b" };
  const schemes = [["light", "白天"], ["dark", "晚上"]];

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
    document.querySelectorAll(".nb-theme button").forEach((button) => {
      button.setAttribute("aria-pressed", String(button.dataset.scheme === theme));
    });
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
    const group = document.createElement("div");
    group.className = "nb-theme";
    group.setAttribute("role", "group");
    group.setAttribute("aria-label", "配色方案");
    for (const [scheme, word] of schemes) {
      const button = document.createElement("button");
      button.type = "button";
      button.dataset.scheme = scheme;
      button.textContent = word;
      button.addEventListener("click", () => {
        apply(scheme);
        try {
          localStorage.setItem(storageKey, scheme);
        } catch {
          // The scheme still applies for this visit if storage is unavailable.
        }
      });
      group.append(button);
    }
    nav.after(group);
    apply(root.dataset.theme);
  });
})();
