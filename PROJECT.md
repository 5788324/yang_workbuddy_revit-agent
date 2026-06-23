# Project: YangTools Revit Plugin Optimization

## Architecture
- Code resides in `src/YangTools.Revit/`.
- UI resides in `src/YangTools.Revit/UI/`.
- Commands reside in `src/YangTools.Revit/Commands/`.
- Core logic in `src/YangTools.Revit/Core/`.
- Icons in `src/YangTools.Revit/Icons/`.

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| 1 | Dead Code & Asset Cleanup | Identify and remove unused C# files, XAML resources, images, and dead logic. Output: `cleanup_report.md` | none | DONE |
| 2 | Deep Optimization | Code review covering WPF/XAML, Revit API safety, and performance. Apply fixes. Output: `review_and_optimization_log.md` | M1 | DONE |
| 3 | Documentation | Generate clear markdown manuals in `Docs/` detailing plugin features. | M2 | DONE |
| 4 | Final Build Verification | Verify compilation with 0 errors via `dotnet build -c "2024" ...`. | M3 | DONE |

## Interface Contracts
- N/A for this refactoring and documentation project. Focus on Revit API safety, MVVM best practices, and performance.

## Code Layout
- Root directory: `E:\Antigravity\YANG TOOLS_REVIT`
- Source code: `E:\Antigravity\YANG TOOLS_REVIT\src\YangTools.Revit`
- Documentation output: `E:\Antigravity\YANG TOOLS_REVIT\Docs`
