# Plan: .NET Version Upgrade — PrintSpoolJobService

## Table of contents

- Executive Summary
- Migration Strategy
- Detailed Dependency Analysis
- Project-by-Project Plans
- Package Update Reference
- Breaking Changes Catalog
- Testing & Validation Strategy
- Risk Management
- Complexity & Effort Assessment
- Source Control Strategy
- Success Criteria
- Appendix: Assessment Reference


## Executive Summary

### Selected Strategy
**All-At-Once Strategy** — All projects upgraded simultaneously in a single atomic operation.

Rationale:
- Solution size: 1 project (small solution)
- Current target framework: `net10.0` (already at proposed target)
- All NuGet packages present in the assessment are reported compatible with `net10.0`
- Low dependency complexity (no project-to-project dependencies)

Scope:
- Project in scope: `PrintSpoolJobService.csproj` (root project)
- Primary goal: validate that the solution is fully compatible with `net10.0` and provide an atomic upgrade checklist for future target changes

Notes:
- Assessment found no compatibility issues; this plan documents the All-At-Once approach and the exact steps to perform an atomic framework upgrade if desired in the future.


## Migration Strategy

Approach: All-At-Once (atomic operation)

Summary of operations (single coordinated batch):
- Verify SDKs and `global.json` (prerequisite)
- Update TargetFramework values across all project files (if a change is needed)
- Update package references (if suggested updates exist)
- Restore dependencies
- Build solution and fix compilation issues (one bounded pass)
- Run test projects and address failing tests

Deliverable for atomic upgrade: solution builds with zero compilation errors, all tests pass, and no remaining security vulnerabilities.


## Detailed Dependency Analysis

- Total projects: 1
- Project list:
  - `PrintSpoolJobService.csproj` (AspNetCore, SDK-style)
- Dependency graph: no project-to-project references
- Critical path: none (single-project solution)
- Circular dependencies: none detected

Implication: With no inter-project dependencies the All-At-Once approach is straightforward — the atomic operation affects only the single project and its package graph.


## Project-by-Project Plans

### Project: `PrintSpoolJobService.csproj`

Current state:
- Target framework: `net10.0`
- SDK-style: True
- Project kind: AspNetCore
- Files: 7
- LOC: 937

Target state:
- Target framework: `net10.0` (no change required per assessment)
- All package references compatible

Migration steps (what the execution agent should perform if an upgrade to a newer target is desired):
1. Preconditions
   - Ensure the correct .NET SDK is installed for the chosen target (use `upgrade_validate_dotnet_sdk_installation` during execution stage).
   - If `global.json` exists, validate SDK version compatibility and update if required.
   - Ensure working tree is clean or follow the pending-changes action defined by repository policy (commit/stash/undo).
2. Atomic changes (single commit)
   - Update `<TargetFramework>` in `PrintSpoolJobService.csproj` (if changing target).
   - Update `PackageReference` versions where assessment suggests updates.
   - Check for Directory.Build.props/targets or `Directory.Packages.props` files and update the shared settings if present.
3. Restore & build
   - Restore NuGet packages to resolve updated versions.
   - Build the entire solution and capture compilation errors.
   - Fix all compilation errors caused by framework or package changes in the same atomic operation.
4. Tests
   - Run all discovered test projects and address failures.
5. Final verification
   - Confirm solution builds with 0 errors and that tests pass where applicable.

Validation checklist (per-project)
- [ ] `PrintSpoolJobService.csproj` TargetFramework set to desired value
- [ ] All package updates applied as specified in §Package Update Reference
- [ ] Solution builds with 0 compilation errors
- [ ] Unit and integration tests pass (if present)
- [ ] No remaining security vulnerabilities reported for packages


## Package Update Reference

Assessment shows all packages compatible with `net10.0`. Include the following matrix to maintain precision for future updates.

### Current NuGet package state (from assessment)

| Package | Current Version | Suggested Version | Projects Affected | Notes |
|---|---:|---:|---|---|
| FreeSpire.PDF | 8.6.0 | (none) | `PrintSpoolJobService.csproj` | Compatible with `net10.0` |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.0 | (none) | `PrintSpoolJobService.csproj` | Compatible |
| Microsoft.Extensions.Hosting.WindowsServices | 10.0.0 | (none) | `PrintSpoolJobService.csproj` | Compatible |
| Microsoft.IdentityModel.Tokens | 8.14.0 | (none) | `PrintSpoolJobService.csproj` | Compatible |
| Swashbuckle.AspNetCore | 9.0.6 | (none) | `PrintSpoolJobService.csproj` | Compatible |
| System.Drawing.Common | 10.0.0 | (none) | `PrintSpoolJobService.csproj` | Note: `System.Drawing.Common` is supported on Windows; non-Windows runtimes require cross-platform alternatives |
| System.IdentityModel.Tokens.Jwt | 8.14.0 | (none) | `PrintSpoolJobService.csproj` | Compatible |

Guidance:
- Include all package updates from assessment in the atomic upgrade when a target change is required.
- Address security vulnerabilities immediately if discovered in the future; this plan assumes none exist now.


## Breaking Changes Catalog

Assessment reported 0 breaking changes. The following are generic areas to watch if frameworks or packages are changed in the future:

- `System.Drawing.Common` behavior and platform support differences on non-Windows environments — consider cross-platform libraries for image rendering.
- Third-party libraries (e.g., `FreeSpire.PDF`) may have license or behavior changes between major versions — verify rendering/printing behavior after package upgrades.
- ASP.NET Core hosting and configuration changes (minimal host vs older patterns) — check `Program.cs` startup patterns if moving between major runtime lines.
- Authentication/Token packages (`Microsoft.IdentityModel.Tokens`, `System.IdentityModel.Tokens.Jwt`) can introduce validation behavior changes; run auth-related flows.

⚠️ If a package has known breaking changes, list them here with exact versions and remediation. (None detected by assessment.)


## Testing & Validation Strategy

Levels of testing:
- Per-project/build validation: solution restores and builds with 0 errors.
- Unit tests: run and pass for test projects (none discovered in assessment). If tests exist, include them in the atomic task.
- Integration tests / End-to-end: run if available.

Validation steps for the atomic upgrade (single pass):
1. `dotnet restore` — ensure dependencies resolved.
2. `dotnet build` — expect 0 errors.
3. `dotnet test` — run tests and ensure they pass (if present).

Note: Manual smoke testing and UI checks are out-of-scope for automated tasks and should be listed as post-upgrade manual validations.


## Risk Management

Risk summary for this solution: Low

Risk factors:
- Small codebase (937 LOC)
- Single project reduces coordination risk
- No packages with reported security vulnerabilities
- `System.Drawing.Common` platform concerns if the service runs on non-Windows hosts

Mitigations:
- Run builds and tests in CI targeting the production runtime OS.
- For `System.Drawing.Common`, validate behavior on the target OS; if non-Windows, consider `SixLabors.ImageSharp` or other cross-platform options.
- Keep third-party binaries (FreeSpire) under test for printing quality changes.

Rollback/Contingency:
- Use single atomic commit for upgrade so rollback is a single revert.
- If breaking issues appear, revert the commit and open a focused follow-up to address the specific package or API change.


## Complexity & Effort Assessment

- Overall complexity: Low
- Per-project complexity:
  - `PrintSpoolJobService.csproj`: Low — no API/package incompatibilities detected

Notes: No time estimates are provided; use relative complexity to plan resource allocation.


## Source Control Strategy

- Create a single upgrade branch for the atomic change (e.g., `upgrade/netX-atomic-<date>`).
- Apply all changes in a single commit that updates project file(s) and package versions.
- Open a pull request with the single commit. PR checklist should include:
  - CI build success
  - All tests passing
  - Reviewers assigned from runtime ownership and security

Rationale: All-At-Once strategy relies on single-commit atomicity to simplify rollback and review.


## Success Criteria

The upgrade is complete when:
- All projects target the proposed framework (or remain intentionally unchanged) — in this case `net10.0`.
- All package updates from the assessment are applied (if any) and resolved.
- Solution builds with 0 errors and 0 warnings caused by the upgrade.
- All automated tests pass.
- No known security vulnerabilities remain in NuGet packages.


## Appendix: Assessment Reference

See `C:\Projects\printspooljobservice\.github\upgrades\scenarios\new-dotnet-version_69f70d\assessment.md` for the full assessment output used to generate this plan.
