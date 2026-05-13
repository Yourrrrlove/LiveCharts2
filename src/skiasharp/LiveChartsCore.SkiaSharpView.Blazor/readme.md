# Consumer prerequisite: wasm-tools workload

SkiaSharp.Views.Blazor must be linked into the consumer's WebAssembly
bundle so its emscripten `Module` is exposed as `globalThis.SkiaSharpModule`
via `SkiaSharpInterop.js`. That link step requires the .NET WebAssembly
build tools workload — without it, the first chart render throws
`ReferenceError: Module is not defined` (see #2229).

Install it once per machine:

```bash
dotnet workload install wasm-tools
```

The package emits build error `LVC0001` if the workload is missing, so
consumers see the requirement at build time instead of debugging the
runtime crash. The check lives in
`build/LiveChartsCore.SkiaSharpView.Blazor.targets` and is packed into
`buildTransitive/` so direct and transitive consumers both get it.

# About *.ts

If the domInterop.ts changes, the *.js files must be manually updated, this due a possible bug where the
*.js files were not consistently generated on the MSBuild process, by the Microsoft.TypeScript.MSBuild package.

I am not completely sure if this is a bug on the MSBuild, the Microsoft.TypeScript.MSBuild the or
if it is a bug on the way I am using it.

Since the domInterop.ts barely changes, let's keep it simple and update the *.js files manually.

Calling `tsc` on the terminal will generate the *.js files, typescript must be installed globally,
`npm install -g typescript`.
