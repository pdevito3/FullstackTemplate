# Example code

The template has example code that shows the patterns of the app. An app that you make from the template does not need it. `scripts/remove-examples.cs` deletes all of it in one run:

```bash
dotnet run scripts/remove-examples.cs
dotnet build FullstackTemplate.slnx
```

Then commit the result. The script changes only a clean git tree. A second run changes nothing. If the script stops before the end, correct the cause and run it again.

The script does these steps:

1. It deletes each tracked folder named `Examples`, `examples`, or `(examples)`.
2. It deletes each section between an `example:start` comment and an `example:end` comment, in all other tracked files.
3. It writes the generated files again: the OpenAPI document of the Server, the generated API client of the SPA, and `routeTree.gen.ts`.

A marker with no partner stops the script before it changes a file.

<!-- example:start -->
## The examples

| Example | Where it is |
|---------|-------------|
| The weather API and its page at `/weather` | `FullstackTemplate.Server/Examples/Weather/`, `frontend/src/examples/weather/`, `frontend/src/routes/(examples)/weather.tsx` |
| The component library at `/components` and the filter builder demo at `/filter-demo` | `frontend/src/routes/(examples)/` |
| The `Examples` menu of the sidebar | `frontend/src/examples/example-nav.tsx` |
| The `get_tenant`, `list_tenants`, and `update_tenant` MCP tools | `FullstackTemplate.Server/Examples/Tenants/` |
| The tests of the examples | the `Examples/` and `examples/` folders of the test projects and of `frontend/tests/` |
| The recipes of the example pages in the verify skill | `.agents/skills/verify-fullstack-template/features/examples/` and `recipes/examples/` |
<!-- example:end -->

## Add example code

Put example code where the script finds it:

- Put a file that is only an example in a folder named `Examples` (.NET), `examples` (the SPA and the tests), or `(examples)` (a TanStack Router route group, which adds no segment to the URL).
- Put example lines in a shared file between two comments. Use the comment form of the file: `// example:start` and `// example:end` in C# and TypeScript, `# example:start` in YAML and shell, `<!-- example:start -->` in Markdown, and `{/* example:start */}` in JSX.
- Do not let code outside the examples use example code. After the script runs, that code does not compile.

The `without-examples` job of CI runs the script, runs it again to prove that the second run changes nothing, and fails if an example name stays in a tracked file. Then it builds the app and runs all of the tests. The other CI jobs test the app with the examples.
