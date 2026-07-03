# gh-pr-rules

A small POC repo used to verify that GitHub branch-protection "required status checks"
actually block a PR merge when checks fail, rather than just showing a warning.

## What's here

- `src/GhPrRules.Calculator` — a trivial C# class library (`Add`, `Subtract`, `Multiply`, `Divide`)
  packed and published to GitHub Packages on every merge to `main`.
- `tests/GhPrRules.Calculator.Tests` — xUnit tests for the library.
- `.github/workflows/build-and-deploy.yml` — builds, tests, and (on push to `main` only)
  packs and publishes the NuGet package to GitHub Packages. Runs on every PR targeting
  `main` as well, so it can be required to pass before merge. Check name: **`build`**.
- `.github/workflows/pr-title-check.yml` — fails unless the PR title or description
  contains a ticket-style tag matching `[ABC-123]` (regex `\[[A-Z]{2,10}-[0-9]+\]`).
  Check name: **`PR Title Check`**.

## Running locally

```bash
dotnet restore GhPrRules.sln
dotnet build GhPrRules.sln --configuration Release
dotnet test GhPrRules.sln --configuration Release --no-build
```

## Required checks

Branch protection on `main` requires both `build` and `PR Title Check` to pass, blocks
direct pushes (PRs are mandatory), and is enforced even for repo admins.

To satisfy the PR title check, include a tag like `[GHPR-123]` in the PR title or
description.

<!-- test/pr-checks: trigger workflows to discover exact check-run names -->
