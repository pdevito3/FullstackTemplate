# Example code

The template has example code that shows the patterns of the app. An app that you make from the template does not need it. `scripts/remove-examples.cs` deletes all of it in one run:

```bash
dotnet run scripts/remove-examples.cs
dotnet build FullstackTemplate.slnx
```

Then commit the result. The script changes only a clean git tree. A second run changes nothing. If the script stops before the end, it prints the command that undoes its changes. Correct the cause, undo the changes, and run it again.

The script does these steps:

1. It deletes each tracked folder named `Examples`, `examples`, or `(examples)`.
2. It deletes each section between an `example:start` comment and an `example:end` comment, in all other tracked files.
3. It writes the generated files again: the OpenAPI document of the Server, the generated API client of the SPA, and `routeTree.gen.ts`.
4. It writes `scripts/examples-removed.txt`. Keep this file. When the app has it, `scripts/template-merge.cs` deletes the examples from each template update, so that a template update adds no example code again. See [Take updates from the template](template-updates.md#an-app-that-deleted-the-examples).

A marker with no partner stops the script before it changes a file. Do not put a marker at the start of a line in a code block of a doc, because the script reads it as a marker.

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

<!-- example:start -->
The `without-examples` job of CI runs the script, runs it again to prove that the second run changes nothing, and fails if an example name stays in a tracked file. Then it builds the app and runs all of the tests. The other CI jobs test the app with the examples.

When you add an example, add each name that only the example uses to the `git grep` of the step `Fail on example code that the script left` in `.github/workflows/ci.yml`. Use a word that the app does not use for its own code, for example the name of the entity or of the route. The check cannot find the names by itself: a marker gives no name, and the files of an example also use shared names, such as `Route` and `Tenants`.

The `init` job of CI makes an app from the parent commit, deletes its examples, and merges the commit with `scripts/template-merge.cs`. It fails if the merge has a conflict or adds an example file again.
<!-- example:end -->
