# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Critical Rules

**These rules override all other instructions:**

1. **NEVER commit directly to main** - Always create a feature branch and submit a pull request
2. **Conventional commits** - Format: `type(scope): description`
3. **GitHub Issues for TODOs** - Use `gh` CLI to manage issues, no local TODO files. Use conventional commit format for issue titles
4. **Pull Request titles** - Use conventional commit format (same as commits)
5. **Branch naming** - Use format: `type/scope/short-description` (e.g., `feat/ui/settings-dialog`)
6. **Working an issue** - Always create a new branch from an updated main branch
7. **Check branch status before pushing** - Verify the remote tracking branch still exists. If a PR was merged/deleted, create a new branch from main instead
8. **Microsoft coding guidelines** - Follow [Microsoft C# coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions) and [.NET library design guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/)
9. **WPF for all UI** - All UI must be implemented using WPF (XAML/C#). No web-based technologies (HTML, JavaScript, WebView)

---

### GitHub CLI Commands

```bash
gh issue list                    # List open issues
gh issue view <number>           # View details
gh issue create --title "type(scope): description" --body "..."
gh issue close <number>
```

### Conventional Commit Types

| Type | Description |
|------|-------------|
| `feat` | New feature |
| `fix` | Bug fix |
| `docs` | Documentation only |
| `refactor` | Code change that neither fixes a bug nor adds a feature |
| `test` | Adding or updating tests |
| `chore` | Maintenance tasks |
| `perf` | Performance improvement |
| `ci` | CI/CD changes |

### VSIX Development Rules

**Solution & Project Structure:**
- SLNX solution files only (no legacy .sln)
- Solution naming: `CodingWithCalvin.<ProjectFolder>`
- Primary project naming: `CodingWithCalvin.<ProjectFolder>`
- Additional project naming: `CodingWithCalvin.<ProjectFolder>.<Classifier>`

**Build Configuration:**
- Configurations: Debug and Release
- Platform: AnyCPU (or x64 where required)
- Build Tools: Latest 17.* release
- VSSDK: Latest 17.* release

**Target Frameworks:**
- Main VSIX project: .NET Framework 4.8
- Library projects: .NET Standard 2.0 (may use SDK-style project format)

**VSIX Manifest:**
- Version range: `[17.0,19.0)` — supports VS 2022 through VS 2026
- Architectures: AMD64 and ARM64
- Prerequisites: List Community edition only (captures Pro/Enterprise)

**CI/CD:**
- Build workflow: Automated build on push/PR
- Publish workflow: Automated marketplace publishing
- Marketplace config: `publish.manifest.json` for automated publishing

**Development Environment:**
- Required extension: Extensibility Essentials 2022
- Helper library: Community.VisualStudio.Toolkit (where applicable)

**Documentation:**
- README should be exciting and use emojis

---

## Project Overview

VS-SuperClean is a Visual Studio 2022 extension that adds a "Super Clean" context menu option to Solution and Project nodes in Solution Explorer. It clears out bin and obj folders for all projects (when invoked on solution) or selected projects.

## Build Commands

```bash
# Build the solution
dotnet build src/CodingWithCalvin.SuperClean/CodingWithCalvin.SuperClean.csproj

# Build Release
dotnet build src/CodingWithCalvin.SuperClean/CodingWithCalvin.SuperClean.csproj -c Release
```

## Architecture

The extension has a simple architecture:

- **SuperCleanPackage.cs** - Main VS Package class extending `AsyncPackage`. Initializes on load and sets up the command handler.

- **Commands/SuperCleanCommand.cs** - Command handler that detects whether invoked on Solution or Project, then recursively deletes bin/obj folders for the appropriate scope.

- **VSCommandTable.vsct** - Defines the context menu command placement for both Solution (`IDG_VS_CTXT_SOLUTION_EXPLORE`) and Project (`IDG_VS_CTXT_PROJECT_EXPLORE`) context menus.

## Technology Stack

- C# / .NET Framework 4.8
- CodingWithCalvin.VsixSdk/0.3.0
- Community.VisualStudio.Toolkit.17
- Visual Studio SDK (v17.0+)
- VSIX v3 package format

## CI/CD

GitHub Actions workflows in `.github/workflows/`:

- **build.yml** - Triggered on push to main or PR. Builds and uploads VSIX artifact.
- **publish.yml** - Manual trigger to publish to VS Marketplace.

## Development Setup

- Requires Visual Studio 2022 with "Visual Studio extension development" workload
- Install "Extensibility Essentials 2022" extension
- Open `src/CodingWithCalvin.SuperClean.slnx` in Visual Studio
- Test by running in experimental VS instance (F5 from VS)
