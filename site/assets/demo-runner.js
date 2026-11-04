// Runs one pattern demo in the browser on the playground's Roslyn + .NET runtime.
// The demo file is the submission; the checks file supplies the interface and an entry that prints Run().
(() => {
  "use strict";

  const workerUrl = new URL("compiler-worker.js", document.currentScript.src);
  const stages = { load: [60000, "加载运行时"], compile: [30000, "编译"], execute: [5000, "执行"] };
  const usings = ["System", "System.Collections.Generic", "System.Globalization", "System.IO", "System.Linq", "System.Text", "System.Threading", "System.Threading.Tasks"]
    .map((name) => `global using ${name};`).join("\n");

  function checksFor(typeName) {
    return `${usings}
namespace DesignPatterns
{
    public interface IPatternDemo
    {
        string Key { get; }
        string Name { get; }
        string Category { get; }
        string Intent { get; }
        IReadOnlyList<string> Run();
    }
}
public static class ExerciseChecks
{
    public static string[] Run()
    {
        var demo = new ${typeName}();
        foreach (var line in demo.Run()) Console.WriteLine(line);
        return new[] { "+" + demo.Name };
    }
}`;
  }

  function run({ source, typeName, onStage }) {
    let worker = null;
    let timer = 0;
    let settle = null;
    const done = new Promise((resolve) => { settle = resolve; });

    function finish(result) {
      clearTimeout(timer);
      worker?.terminate();
      worker = null;
      settle(result);
    }

    function stage(name) {
      clearTimeout(timer);
      const [timeout, label] = stages[name];
      onStage?.(name, label);
      timer = setTimeout(() => finish({ ok: false, timedOut: true, stage: name, label, output: "", diagnostics: [], error: `${label}超时（${timeout / 1000} 秒），已停止。` }), timeout);
    }

    if (new TextEncoder().encode(source).length > 100 * 1024) {
      finish({ ok: false, stage: "compile", output: "", diagnostics: [], error: "源码超过 100 KB，已拒绝运行。" });
      return { done, stop: () => {} };
    }

    stage("load");
    try {
      const active = new Worker(workerUrl, { type: "module" });
      worker = active;
      active.onerror = (event) => { event.preventDefault(); finish({ ok: false, stage: "load", output: "", diagnostics: [], error: `运行失败：${event.message}` }); };
      active.onmessage = ({ data }) => {
        if (worker !== active) return;
        if (data.stage === "compile" || data.stage === "execute") { stage(data.stage); return; }
        if (data.stage === "error") { finish({ ok: false, stage: "load", output: "", diagnostics: [], error: `运行失败：${data.error}` }); return; }
        if (data.stage !== "done") return;
        const diagnostics = (data.compilation?.diagnostics ?? []).map((item) => `${item.severity} (${item.line},${item.column}): ${item.message}`);
        const result = data.result;
        if (!data.compilation?.success) { finish({ ok: false, stage: "compile", output: "", diagnostics, error: "编译失败。" }); return; }
        const output = (result?.output ?? "") + (result?.outputTruncated ? "\n[输出已达到 64 KB，后续输出已截断]" : "");
        finish({ ok: Boolean(result && !result.error), stage: "execute", output, diagnostics, error: result?.error ? `运行异常：${result.error}` : null });
      };
      active.postMessage({ source, checks: checksFor(typeName) });
    } catch (error) {
      finish({ ok: false, stage: "load", output: "", diagnostics: [], error: `无法启动运行环境：${error.message}` });
    }

    return { done, stop: () => finish({ ok: false, stopped: true, stage: "stopped", output: "", diagnostics: [], error: "已停止。" }) };
  }

  window.DemoRunner = Object.freeze({ run, checksFor, stages });
})();
