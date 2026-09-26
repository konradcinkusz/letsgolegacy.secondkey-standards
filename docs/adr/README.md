# Architecture decision records

Decisions taken while building the standards pack, with the reasons and the alternatives that
lost (P14: a document that says "we considered X and rejected it because Y" is worth more than
one that lists commands).

| ADR | Decision |
|---|---|
| [0001](0001-standards-source-format.md) | The standards source format: one Markdown file per rule, front-matter metadata, one severity vocabulary for agent and gate |
| [0002](0002-generator-as-a-dotnet-tool.md) | The generator is a .NET 10 tool, and it validates the standards from the first pull request |
| [0003](0003-generated-tree-and-versioning.md) | The generated tree, `generate --check`, the version gate, and the configuration-only NuGet package |
