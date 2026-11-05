# Nonogram Notes

A simple AI-generated nonogram notes application, since I couldn't find one elsewhere.

> Work in progress: puzzles aren't saved yet, so your work is lost when you close the window.

## Download

Get the latest zip from [Releases](https://github.com/MGDays/nonogram-notes/releases), extract it and run `NonogramNotes.exe`.

It needs 64-bit Windows 10 or 11 and the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (if it's missing, Windows offers to download it when you first run the app). The exe isn't code-signed, so Windows SmartScreen may warn you the first time: choose **More info**, then **Run anyway**.

## Build from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

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
