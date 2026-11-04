// Colours code after load, so the exported guide, its search index and its print output stay plain text.
// window.CodeColors colours text on demand for the source panel, which loads files after the page.
(() => {
  "use strict";

  const csharpKeywords = "abstract and as async await base bool break byte case catch char checked class const continue decimal default delegate do double else enum event explicit extern false file finally fixed float for foreach get goto if implicit in init int interface internal is lock long namespace new not null object operator or out override params partial private protected public readonly record ref required return sbyte sealed set short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked unsafe ushort using var virtual void volatile when where while with yield".replaceAll(" ", "|");

  const csharp = {
    comment: String.raw`//[^\n]*|/\*[\s\S]*?\*/`,
    string: String.raw`\$?@?"""[\s\S]*?"""|\$?@?"(?:[^"\\\n]|\\.|"")*"|'(?:[^'\\\n]|\\.)+'`,
    meta: String.raw`^[ \t]*#[a-z]+[^\n]*|(?<=^[ \t]*)\[[A-Z]\w*(?:\([^)\n]*\))?\]`,
    number: String.raw`\b\d[\d_]*(?:\.\d+)?[fFdDmMlLuU]*\b`,
    keyword: String.raw`\b(?:${csharpKeywords})\b`,
    call: String.raw`\b[A-Za-z_]\w*(?=(?:<[\w<>, ?]*>)?\()`,
    type: String.raw`\b[A-Z]\w*`,
  };

  // In a shell the program is the type and its switches are the keywords.
  const shell = {
    comment: String.raw`(?<!\S)#[^\n]*`,
    string: String.raw`"[^"\n]*"|'[^'\n]*'`,
    type: String.raw`^[ \t]*(?:[.&][ \t]+)?[A-Za-z_./\\][\w./\\:-]*(?![\w./\\:-]|[ \t]*=)`,
    keyword: String.raw`(?<=\s)--?[A-Za-z][\w-]*`,
    meta: String.raw`\$[A-Za-z_][\w:]*`,
    number: String.raw`(?<![\w.-])\d+(?:\.\d+)?\b`,
  };

  // Data files: keys as types, literals as strings and numbers.
  const data = {
    comment: String.raw`//[^\n]*|/\*[\s\S]*?\*/|(?<!\S)#[^\n]*`,
    type: String.raw`"(?:[^"\\\n]|\\.)*"(?=\s*:)`,
    string: String.raw`"(?:[^"\\\n]|\\.)*"|'[^'\n]*'`,
    number: String.raw`(?<![\w.-])-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?\b`,
    keyword: String.raw`\b(?:true|false|null)\b`,
  };

  const compile = (rules) => new RegExp(Object.entries(rules).map(([name, source]) => `(?<${name}>${source})`).join("|"), "gm");
  const patterns = { csharp: compile(csharp), powershell: compile(shell), bat: compile(shell), shell: compile(shell), json: compile(data), yaml: compile(data) };
  const languages = { cs: "csharp", ps1: "powershell", psd1: "powershell", cmd: "bat", bat: "bat", sh: "shell", json: "json", yml: "yaml", yaml: "yaml" };

  function highlight(source, language) {
    const parts = document.createDocumentFragment();
    const pattern = patterns[language];
    if (!pattern) {
      parts.append(source);
      return parts;
    }
    let position = 0;
    for (const match of source.matchAll(pattern)) {
      parts.append(source.slice(position, match.index));
      const token = document.createElement("span");
      token.className = `tok-${Object.keys(match.groups).find((name) => match.groups[name] !== undefined)}`;
      token.textContent = match[0];
      parts.append(token);
      position = match.index + match[0].length;
    }
    parts.append(source.slice(position));
    return parts;
  }

  function languageFor(path) {
    const extension = String(path).toLowerCase().match(/\.([a-z0-9]+)$/)?.[1] ?? "";
    return languages[extension] ?? (extension || "text");
  }

  window.CodeColors = Object.freeze({ highlight, languageFor });

  function colourPage() {
    for (const code of document.querySelectorAll('pre code[class*="language-"]')) {
      const language = code.className.match(/language-([\w-]+)/)?.[1];
      if (!patterns[language] || code.children.length > 0) continue;
      code.replaceChildren(highlight(code.textContent, language));
    }
  }

  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", colourPage);
  else colourPage();
})();
