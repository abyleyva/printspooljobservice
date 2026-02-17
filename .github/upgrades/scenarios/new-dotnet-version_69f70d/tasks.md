# PrintSpoolJobService .NET net10.0 Upgrade Tasks

## Overview

This document tracks the execution of the upgrade for `PrintSpoolJobService` to validate project and package compatibility with `net10.0`. The workflow includes prerequisites verification, a single consolidated upgrade pass with build/fix, automated testing, and a final commit.

**Progress**: 2/4 tasks complete (50%) ![0%](https://progress-bar.xyz/50)

---

## Tasks

### [✓] TASK-001: Verify prerequisites *(Completed: 2026-02-17 18:35)*
**References**: Plan §Project-by-Project Plans, Plan §Migration Strategy

- [✓] (1) Verify required .NET SDK is installed per Plan §Project-by-Project Plans (preconditions)
- [✓] (2) Runtime/SDK version meets minimum requirements (**Verify**)
- [✓] (3) Check for presence of `global.json` and verify SDK version compatibility per Plan §Project-by-Project Plans
- [✓] (4) Update `global.json` if required to match the required SDK per Plan §Project-by-Project Plans
- [✓] (5) Configuration files compatible with target version (**Verify**)

### [✓] TASK-002: Atomic framework and dependency upgrade with compilation fixes *(Completed: 2026-02-17 12:37)*
**References**: Plan §Project-by-Project Plans, Plan §Package Update Reference, Plan §Breaking Changes Catalog, Plan §Migration Strategy

- [✓] (1) Update `<TargetFramework>` in `PrintSpoolJobService.csproj` if a target change is required (per Plan §Project-by-Project Plans)
- [✓] (2) Update `PackageReference` versions per Plan §Package Update Reference (apply suggested updates listed in the plan)
- [✓] (3) Check and update shared MSBuild/import files (e.g., `Directory.Build.props`, `Directory.Packages.props`) if present per Plan §Project-by-Project Plans
- [✓] (4) Restore dependencies (`dotnet restore`) and ensure all packages restore successfully (**Verify**)
- [✓] (5) Build the solution to identify compilation errors (first pass)
- [✓] (6) Fix all compilation errors caused by framework or package changes per Plan §Breaking Changes Catalog
- [✓] (7) Rebuild the solution to verify fixes
- [✓] (8) Solution builds with 0 errors (**Verify**)

### [⊘] TASK-003: Run full test suite and validate upgrade
**References**: Plan §Testing & Validation Strategy, Plan §Breaking Changes Catalog

- [⊘] (1) Run tests for all test projects discovered per Plan §Testing & Validation Strategy (`dotnet test`) — if no test projects exist, mark as not applicable
- [⊘] (2) Fix any test failures (reference Plan §Breaking Changes Catalog for likely causes)
- [⊘] (3) Re-run tests after fixes
- [⊘] (4) All tests pass with 0 failures (**Verify**)

### [ ] TASK-004: Final commit
**References**: Plan §Source Control Strategy

- [ ] (1) Commit all remaining changes with message: "TASK-004: Complete upgrade to `net10.0` for PrintSpoolJobService"



