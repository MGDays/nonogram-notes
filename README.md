# Nonogram Notes

A simple AI-generated nonogram notes application, since I couldn't find one elsewhere.

> Work in progress: puzzles aren't saved yet, so your work is lost when you close the window.

## Requirements

- Windows
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

## Build and run

```
dotnet build NonogramNotes.sln
dotnet run --project NonogramNotes.csproj
```

## Controls

| Action | How |
| --- | --- |
| Fill a cell | Left-click |
| Place a cross | Right-click |
| Clear a cell | Click it again with the same button |
| Mark a line | Drag (locks to the row or column you move along; the status bar shows its length) |
| Enter a clue | Click a box beside a row or above a column and type numbers, separated by Space or a comma |
| Next row clue | Enter |
| Undo / Redo | Ctrl+Z / Ctrl+Y (click the grid first, so a clue box doesn't take the shortcut) |
| Zoom | Zoom slider or Ctrl+mouse wheel |
| Resize the grid | Enter rows and columns (1 to 30) and click **Resize** |

A drag only changes cells in the same state as the one you started on, so it won't wipe out your other marks. A clue turns grey once its line matches it.

## License

Public domain ([Unlicense](LICENSE)): do whatever you like with it.
