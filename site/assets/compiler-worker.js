import { dotnet } from "./compiler/_framework/dotnet.js";

self.onmessage = async ({ data }) => {
  try {
    const runtime = await dotnet.create();
    const exports = await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
    const compiler = exports.BrowserCompiler;
    const base = new URL("./compiler/refs/", import.meta.url);
    const manifest = await fetch(new URL("manifest.json", base)).then((response) => {
      if (!response.ok) throw new Error("无法加载编译引用清单。");
      return response.json();
    });
    const references = await Promise.all(manifest.map(async (name) => {
      const response = await fetch(new URL(name, base));
      if (!response.ok) throw new Error(`无法加载 ${name}`);
      return new Uint8Array(await response.arrayBuffer());
    }));
    for (const reference of references) compiler.AddReference(reference);
    self.postMessage({ stage: "compile" });
    const compilation = JSON.parse(compiler.Compile(data.source, data.checks));
    if (!compilation.success) { self.postMessage({ stage: "done", compilation }); return; }
    self.postMessage({ stage: "execute", compilation });
    const result = JSON.parse(compiler.Execute());
    self.postMessage({ stage: "done", compilation, result });
  } catch (error) {
    self.postMessage({ stage: "error", error: error.message ?? String(error) });
  }
};
