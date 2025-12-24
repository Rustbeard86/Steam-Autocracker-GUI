# Phase 3: Extend Service Layer to EnhancedShareWindow

## PR Title
Phase 3: Extend service layer to EnhancedShareWindow and eliminate duplicate formatting methods

## Description
This PR continues the SOLID refactoring of the Steam Auto-Cracker GUI by extending the service architecture established in Phase 2 to `EnhancedShareWindow.cs` and eliminating duplicate formatting utility methods across the codebase.

## Background
Phases 1 and 2 (documented in `REFACTORING-BATCHGAMESELECTIONFORM.md`) successfully:
- Extracted helper classes from BatchGameSelectionForm
- Created service interfaces (IAppIdDetectionService, IBatchGameDataService, IFormattingService)
- Reduced BatchGameSelectionForm by 26.1% (797 lines)

## Goals
1. Eliminate duplicate `FormatFileSize` methods - Replace instances in EnhancedShareWindow.cs with `IBatchGameDataService.FormatFileSize()`
2. Eliminate duplicate `FormatSize` method - Same as above
3. Inject services into EnhancedShareWindow - Add constructor parameters for IBatchGameDataService and IFormattingService
4. Update service usage - Replace all local formatting calls with service calls

## Files to Modify

### EnhancedShareWindow.cs
- Add service dependencies (IBatchGameDataService, IFormattingService) via constructor injection
- Remove duplicate `FormatFileSize(long bytes)` method
- Remove duplicate `FormatSize(long bytes)` method
- Replace all calls to these removed methods with `_gameData.FormatFileSize()`
- Replace ETA formatting with `_formatting.FormatEta()` where applicable

### Form1.cs
- Update `EnhancedShareWindow` instantiation to pass service dependencies
- Consider extracting remaining duplicate utility methods

## Implementation Notes

### Service Injection Pattern
Follow existing BatchGameSelectionForm pattern:

```csharp
// In EnhancedShareWindow.cs
private readonly IBatchGameDataService _gameData;
private readonly IFormattingService _formatting;

public EnhancedShareWindow(
    Form parent, 
    IBatchGameDataService? gameData = null, 
    IFormattingService? formatting = null)
{
    parentForm = parent;
    
    // Initialize services with defaults if not provided
    var fileSystem = new APPID.Services.FileSystemService();
    _gameData = gameData ?? new APPID.Services.BatchGameDataService(fileSystem);
    _formatting = formatting ?? new APPID.Services.FormattingService();
    
    // ... rest of constructor
}
```

### Methods to Remove from EnhancedShareWindow.cs

| Method | Location | Replacement |
|--------|----------|-------------|
| `FormatFileSize(long bytes)` | ~line 387 | `_gameData.FormatFileSize(bytes)` |
| `FormatSize(long bytes)` | ~line 2060 | `_gameData.FormatFileSize(bytes)` |
| `FormatBytesForUpload(long bytes)` | If duplicate | `_gameData.FormatFileSize(bytes)` |
| `FormatUploadEta(double seconds)` | If exists | `_formatting.FormatEta(seconds)` |

## Acceptance Criteria
- [ ] No duplicate `FormatFileSize` or `FormatSize` methods remain in EnhancedShareWindow.cs
- [ ] EnhancedShareWindow uses IBatchGameDataService for all file size formatting
- [ ] All existing functionality preserved (backward compatible)
- [ ] Build succeeds with no errors or warnings related to formatting
- [ ] Optional service parameters maintain backward compatibility

## Testing
- Verify file size displays correctly in EnhancedShareWindow grid
- Verify upload progress shows correct size formatting
- Verify batch processing works end-to-end

## Related Files
- `REFACTORING-BATCHGAMESELECTIONFORM.md` - Refactoring documentation
- `Services/Interfaces/IBatchGameDataService.cs` - Service interface
- `Services/BatchGameDataService.cs` - Service implementation
- `BatchGameSelectionForm.cs` - Reference implementation

## Labels
refactoring, code-quality, SOLID-principles

---

## Next Steps Summary

| Priority | Task | Impact |
|----------|------|--------|
| 1 | Inject services into EnhancedShareWindow | Consistency with BatchGameSelectionForm |
| 2 | Remove duplicate FormatFileSize in EnhancedShareWindow | Code deduplication |
| 3 | Remove duplicate FormatSize in EnhancedShareWindow | Code deduplication |
| 4 | Update Form1.cs to pass services | Proper dependency injection |
| 5 | Update REFACTORING-BATCHGAMESELECTIONFORM.md with Phase 3 | Documentation |

## Notes
- The refactoring maintains backward compatibility through optional constructor parameters with default implementations
- This follows the pattern already established in BatchGameSelectionForm
- No breaking changes to public APIs
