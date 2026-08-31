(() => {
  "use strict";

  const form = document.querySelector("#site-search-form");
  const input = document.querySelector("#site-search-input");
  const status = document.querySelector("#site-search-status");
  const results = document.querySelector("#site-search-results");
  if (!form || !input || !status || !results) return;

  let indexPromise;

  function normalize(value) {
    return String(value).normalize("NFKC").toLocaleLowerCase("zh-CN").replace(/\s+/gu, " ").trim();
  }

  function tokens(value) {
    return [...new Set(normalize(value).split(" ").filter((token) => token.length > 0))];
  }

  function loadIndex() {
    indexPromise ??= fetch("assets/search-index.json", { credentials: "same-origin" }).then((response) => {
      if (!response.ok) throw new Error(`search index: ${response.status}`);
      return response.json();
    });
    return indexPromise;
  }

  function score(entry, queryTokens) {
    const title = normalize(entry.title);
    const section = normalize(entry.section ?? "");
    const body = normalize(entry.body ?? "");
    if (!queryTokens.every((token) => `${title} ${section} ${body}`.includes(token))) return -1;
    return queryTokens.reduce((sum, token) => sum +
      (title.includes(token) ? 12 : 0) + (section.includes(token) ? 7 : 0) + (body.includes(token) ? 2 : 0), 0);
  }

  function appendHighlighted(parent, text, queryTokens) {
    const source = String(text);
    const normalized = normalize(source);
    const ranges = [];
    for (const token of queryTokens) {
      let start = normalized.indexOf(token);
      while (start >= 0) {
        ranges.push([start, start + token.length]);
        start = normalized.indexOf(token, start + token.length);
      }
    }
    ranges.sort((a, b) => a[0] - b[0]);
    const merged = [];
    for (const range of ranges) {
      const last = merged.at(-1);
      if (last && range[0] <= last[1]) last[1] = Math.max(last[1], range[1]);
      else merged.push([...range]);
    }
    let cursor = 0;
    for (const [start, end] of merged) {
      parent.append(document.createTextNode(source.slice(cursor, start)));
      const mark = document.createElement("mark");
      mark.textContent = source.slice(start, end);
      parent.append(mark);
      cursor = end;
    }
    parent.append(document.createTextNode(source.slice(cursor)));
  }

  function excerpt(entry, queryTokens) {
    const text = String(entry.body ?? "").replace(/\s+/gu, " ").trim();
    const normalized = normalize(text);
    const positions = queryTokens.map((token) => normalized.indexOf(token)).filter((position) => position >= 0);
    const center = positions.length ? Math.min(...positions) : 0;
    const start = Math.max(0, center - 55);
    const end = Math.min(text.length, start + 180);
    return `${start > 0 ? "…" : ""}${text.slice(start, end)}${end < text.length ? "…" : ""}`;
  }

  function render(entries, queryTokens) {
    results.replaceChildren();
    for (const entry of entries) {
      const item = document.createElement("li");
      const link = document.createElement("a");
      const heading = document.createElement("strong");
      const meta = document.createElement("span");
      const summary = document.createElement("p");
      link.href = entry.url;
      appendHighlighted(heading, entry.title, queryTokens);
      meta.textContent = entry.section ? `${entry.kind} · ${entry.section}` : entry.kind;
      appendHighlighted(summary, excerpt(entry, queryTokens), queryTokens);
      link.append(heading, meta, summary);
      item.append(link);
      results.append(item);
    }
    results.hidden = entries.length === 0;
  }

  async function search(rawQuery, { focusResults = false } = {}) {
    const queryTokens = tokens(rawQuery);
    const url = new URL(window.location.href);
    queryTokens.length ? url.searchParams.set("search", rawQuery.trim()) : url.searchParams.delete("search");
    history.replaceState(history.state, "", `${url.pathname}${url.search}${url.hash}`);
    if (!queryTokens.length) {
      results.replaceChildren();
      results.hidden = true;
      status.textContent = "输入关键词后搜索全部课程。";
      return;
    }
    status.textContent = "正在载入课程索引…";
    try {
      const index = await loadIndex();
      const ranked = index.entries
        .map((entry) => ({ entry, score: score(entry, queryTokens) }))
        .filter((item) => item.score >= 0)
        .sort((a, b) => b.score - a.score || a.entry.title.localeCompare(b.entry.title, "zh-CN"))
        .slice(0, 20)
        .map((item) => item.entry);
      render(ranked, queryTokens);
      status.textContent = ranked.length ? `找到 ${ranked.length} 条结果，按相关度排序。` : "没有找到结果；尝试更短的业务词。";
      if (focusResults && ranked.length) results.querySelector("a")?.focus();
    } catch {
      results.hidden = true;
      status.textContent = "课程索引暂时无法载入，请稍后重试。";
    }
  }

  form.addEventListener("submit", (event) => {
    event.preventDefault();
    search(input.value, { focusResults: true });
  });
  form.addEventListener("reset", () => window.setTimeout(() => search(""), 0));

  const initial = new URL(window.location.href).searchParams.get("search") ?? "";
  if (initial) {
    input.value = initial;
    search(initial);
  }
})();
