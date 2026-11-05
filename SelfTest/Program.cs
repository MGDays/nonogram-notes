// Drives the app in-process and offscreen: no real mouse or keyboard input, no focus stealing.
// Usage: NonogramNotes.SelfTest [screenshot.png]
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NonogramNotes;

int failures = 0;
void Check(bool ok, string what)
{
    Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {what}");
    if (!ok) failures++;
}

var thread = new Thread(() =>
{
    _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
    var w = new MainWindow
    {
        WindowStartupLocation = WindowStartupLocation.Manual,
        Left = -20000, Top = -20000,
        ShowActivated = false, ShowInTaskbar = false,
    };
    w.Show();
    void Pump() => w.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    Pump();

    var board = w.Board;
    double size = board.CellSize;
    Point Cell(int r, int c) => new(1 + c * size + size / 2, 1 + r * size + size / 2);
    void Drag(Point a, Point b, MouseButton button)
    {
        board.BeginStroke(a, button);
        board.MovePointer(new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2));
        board.MovePointer(b);
        board.EndStroke(button);
    }
    string Row(int r) => string.Concat(Enumerable.Range(0, board.Columns).Select(c => board[r, c] switch
    {
        CellState.Filled => '#', CellState.Crossed => 'x', _ => '.',
    }));
    TextBox RowClue(int i) => (TextBox)w.RowCluePanel.Children[i];
    TextBox ColClue(int i) => (TextBox)w.ColumnCluePanel.Children[i];

    RowClue(0).Text = "3 2";
    RowClue(1).Text = "4";
    ColClue(0).Text = "2" + Environment.NewLine + "1";

    Drag(Cell(0, 0), Cell(0, 2), MouseButton.Left);
    Drag(Cell(0, 5), Cell(0, 6), MouseButton.Left);
    Drag(Cell(1, 0), Cell(1, 1), MouseButton.Left);
    Check(Row(0) == "###..##...", $"fill drags on row 1 -> {Row(0)}");
    Check(board.GetRowRuns(0).SequenceEqual(new[] { 3, 2 }), "row 1 runs are 3 2");
    Check(RowClue(0).Foreground != Brushes.Black, "satisfied row clue greys out");
    Check(RowClue(1).Foreground == Brushes.Black, "unsatisfied row clue stays black");

    // Diagonal drift should stay locked to the column.
    Drag(Cell(0, 9), new Point(Cell(5, 9).X - size * 0.8, Cell(5, 9).Y), MouseButton.Right);
    Check(Enumerable.Range(0, 6).All(r => board[r, 9] == CellState.Crossed) && board[6, 9] == CellState.Empty,
        "right drag crosses column 10 rows 1-6");
    Check(Enumerable.Range(0, 10).All(r => board[r, 8] == CellState.Empty), "column drag doesn't spill into column 9");

    // A cross drag from an empty cell only touches empty cells.
    Drag(Cell(0, 3), Cell(0, 8), MouseButton.Right);
    Check(Row(0) == "###xx##xxx", $"cross drag skips filled cells -> {Row(0)}");

    // Drag out and back: cells the pointer leaves are restored.
    board.BeginStroke(Cell(3, 0), MouseButton.Left);
    board.MovePointer(Cell(3, 7));
    board.MovePointer(Cell(3, 2));
    board.EndStroke(MouseButton.Left);
    Check(Row(3) == "###......x", $"dragging back shortens the stroke -> {Row(3)}");

    Drag(Cell(7, 0), Cell(7, 8), MouseButton.Left);
    Check(Row(7) == "#########.", "fill row 8");
    board.Undo();
    Check(Row(7) == "..........", "undo removes the whole stroke");
    board.Redo();
    Check(Row(7) == "#########.", "redo restores it");
    board.Undo();

    // Clicking a filled cell clears it.
    Drag(Cell(1, 0), Cell(1, 0), MouseButton.Left);
    Check(board[1, 0] == CellState.Empty, "click toggles a filled cell off");
    board.Undo();

    // Resize through the window so the clue boxes are rebuilt too: grow one axis at a time, then shrink each.
    bool Resize(int rows, int cols, string label)
    {
        try
        {
            w.ApplyResize(rows, cols);
            Pump();
            bool ok = board.Rows == rows && board.Columns == cols
                && w.RowCluePanel.Children.Count == rows && w.ColumnCluePanel.Children.Count == cols;
            Check(ok, $"{label} -> {rows}x{cols}");
            return ok;
        }
        catch (Exception ex)
        {
            Check(false, $"{label} -> {rows}x{cols} threw {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }
    Resize(10, 30, "grow columns");
    Resize(30, 30, "grow rows");
    Check(Row(0).StartsWith("###xx##xxx") && RowClue(0).Text == "3 2", "growing keeps marks and clues");
    Resize(30, 8, "shrink columns");
    Resize(6, 8, "shrink rows");
    Check(Row(0) == "###xx##x" && RowClue(0).Text == "3 2", "shrinking keeps marks and clues that still fit");
    Resize(6, 1, "shrink to one column");
    Resize(1, 1, "shrink to 1x1");
    Resize(10, 10, "back to 10x10");

    if (args.Length > 0)
    {
        board.MovePointer(Cell(4, 4));
        Pump();
        var root = (FrameworkElement)w.Content;
        var dpi = VisualTreeHelper.GetDpi(root);
        var bmp = new RenderTargetBitmap((int)(root.ActualWidth * dpi.DpiScaleX), (int)(root.ActualHeight * dpi.DpiScaleY),
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        var bg = new DrawingVisual();
        using (var dc = bg.RenderOpen())
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, root.ActualWidth, root.ActualHeight));
        bmp.Render(bg);
        bmp.Render(root);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var f = File.Create(args[0]);
        enc.Save(f);
        Console.WriteLine($"Screenshot: {args[0]}");
    }
    w.Close();
});
thread.SetApartmentState(ApartmentState.STA);
thread.Start();
thread.Join();
Console.WriteLine(failures == 0 ? "All checks passed." : $"{failures} check(s) failed.");
return failures == 0 ? 0 : 1;
