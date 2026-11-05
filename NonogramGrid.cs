using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace NonogramNotes;

public enum CellState : byte { Empty, Filled, Crossed }

public readonly record struct CellChange(int Row, int Column, CellState Before, CellState After);

/// <summary>
/// Draws the puzzle grid and turns mouse strokes into fills and crosses.
/// Left button toggles fill, right button toggles cross; dragging applies the
/// same change along the row or column the stroke started in.
/// </summary>
public sealed class NonogramGrid : FrameworkElement
{
    public static readonly DependencyProperty CellSizeProperty = DependencyProperty.Register(
        nameof(CellSize), typeof(double), typeof(NonogramGrid),
        new FrameworkPropertyMetadata(24.0,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    // Leaves room for half of the thick outer border.
    private const double Origin = 1;

    private static readonly Brush FillBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22)));
    private static readonly Brush CrossBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x8A, 0x8F, 0x98)));
    private static readonly Brush HoverBrush = Frozen(new SolidColorBrush(Color.FromArgb(0x28, 0x3A, 0x7B, 0xD5)));
    private static readonly Pen ThinPen = Frozen(new Pen(Frozen(new SolidColorBrush(Color.FromRgb(0xB8, 0xB8, 0xB8))), 1));
    private static readonly Pen ThickPen = Frozen(new Pen(Brushes.Black, 2));

    private CellState[,] _cells = new CellState[10, 10];
    private readonly Stack<List<CellChange>> _undo = new();
    private readonly Stack<List<CellChange>> _redo = new();

    // Active stroke; _strokeOriginals is null when no stroke is in progress.
    private Dictionary<(int Row, int Col), CellState>? _strokeOriginals;
    private MouseButton _strokeButton;
    private CellState _strokeFrom, _strokeTo;
    private (int Row, int Col) _strokeStart, _strokeEnd;

    private (int Row, int Col)? _hover;

    public NonogramGrid()
    {
        // Focusable so that clicking the grid moves keyboard focus (and Ctrl+Z) away from the clue boxes.
        Focusable = true;
        FocusVisualStyle = null;
    }

    public double CellSize
    {
        get => (double)GetValue(CellSizeProperty);
        set => SetValue(CellSizeProperty, value);
    }

    public int Rows => _cells.GetLength(0);
    public int Columns => _cells.GetLength(1);
    public (int Row, int Col)? HoverCell => _hover;
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Number of cells covered by the stroke in progress, or 0.</summary>
    public int StrokeLength => _strokeOriginals is null
        ? 0
        : Math.Abs(_strokeEnd.Row - _strokeStart.Row) + Math.Abs(_strokeEnd.Col - _strokeStart.Col) + 1;

    public CellState this[int row, int col] => _cells[row, col];

    /// <summary>Raised whenever any cell changes state.</summary>
    public event EventHandler? CellsChanged;

    /// <summary>Raised when the hovered cell or stroke length changes.</summary>
    public event EventHandler? PointerChanged;

    public void Resize(int rows, int cols)
    {
        CancelStroke();
        var resized = new CellState[rows, cols];
        for (int r = 0; r < Math.Min(rows, Rows); r++)
            for (int c = 0; c < Math.Min(cols, Columns); c++)
                resized[r, c] = _cells[r, c];

        _cells = resized;
        _undo.Clear();
        _redo.Clear();
        _hover = null;
        InvalidateMeasure();
        InvalidateVisual();
        CellsChanged?.Invoke(this, EventArgs.Empty);
        PointerChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool HasMarksOutside(int rows, int cols)
    {
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Columns; c++)
                if ((r >= rows || c >= cols) && _cells[r, c] != CellState.Empty)
                    return true;
        return false;
    }

    public void ClearMarks()
    {
        CancelStroke();
        var changes = new List<CellChange>();
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Columns; c++)
                if (_cells[r, c] != CellState.Empty)
                    changes.Add(new CellChange(r, c, _cells[r, c], CellState.Empty));

        if (changes.Count == 0)
            return;
        Apply(changes, undo: false);
        _undo.Push(changes);
        _redo.Clear();
    }

    public void Undo()
    {
        if (_strokeOriginals is not null || !_undo.TryPop(out var changes))
            return;
        Apply(changes, undo: true);
        _redo.Push(changes);
    }

    public void Redo()
    {
        if (_strokeOriginals is not null || !_redo.TryPop(out var changes))
            return;
        Apply(changes, undo: false);
        _undo.Push(changes);
    }

    /// <summary>Lengths of the consecutive filled runs in a row, left to right.</summary>
    public List<int> GetRowRuns(int row) => GetRuns(Columns, c => _cells[row, c]);

    /// <summary>Lengths of the consecutive filled runs in a column, top to bottom.</summary>
    public List<int> GetColumnRuns(int col) => GetRuns(Rows, r => _cells[r, col]);

    private static List<int> GetRuns(int length, Func<int, CellState> at)
    {
        var runs = new List<int>();
        int run = 0;
        for (int i = 0; i < length; i++)
        {
            if (at(i) == CellState.Filled)
            {
                run++;
            }
            else if (run > 0)
            {
                runs.Add(run);
                run = 0;
            }
        }
        if (run > 0)
            runs.Add(run);
        return runs;
    }

    private void Apply(List<CellChange> changes, bool undo)
    {
        foreach (var change in changes)
            _cells[change.Row, change.Column] = undo ? change.Before : change.After;
        InvalidateVisual();
        CellsChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(Columns * CellSize + 2 * Origin, Rows * CellSize + 2 * Origin);

    protected override void OnRender(DrawingContext dc)
    {
        double s = CellSize;
        double width = Columns * s, height = Rows * s;

        dc.DrawRectangle(Brushes.White, null, new Rect(Origin, Origin, width, height));

        if (_hover is { } hover)
        {
            dc.DrawRectangle(HoverBrush, null, new Rect(Origin, Origin + hover.Row * s, width, s));
            dc.DrawRectangle(HoverBrush, null, new Rect(Origin + hover.Col * s, Origin, s, height));
        }

        var crossPen = new Pen(CrossBrush, Math.Max(1.5, s / 12)) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        double inset = s * 0.25;
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Columns; c++)
            {
                double x = Origin + c * s, y = Origin + r * s;
                switch (_cells[r, c])
                {
                    case CellState.Filled:
                        dc.DrawRectangle(FillBrush, null, new Rect(x, y, s, s));
                        break;
                    case CellState.Crossed:
                        dc.DrawLine(crossPen, new Point(x + inset, y + inset), new Point(x + s - inset, y + s - inset));
                        dc.DrawLine(crossPen, new Point(x + s - inset, y + inset), new Point(x + inset, y + s - inset));
                        break;
                }
            }
        }

        // Thin lines first, then a thick line every five cells and around the edge.
        for (int c = 1; c < Columns; c++)
            if (c % 5 != 0)
                dc.DrawLine(ThinPen, new Point(Origin + c * s + 0.5, Origin), new Point(Origin + c * s + 0.5, Origin + height));
        for (int r = 1; r < Rows; r++)
            if (r % 5 != 0)
                dc.DrawLine(ThinPen, new Point(Origin, Origin + r * s + 0.5), new Point(Origin + width, Origin + r * s + 0.5));
        for (int c = 0; c <= Columns; c++)
            if (c % 5 == 0 || c == Columns)
                dc.DrawLine(ThickPen, new Point(Origin + c * s, Origin - 1), new Point(Origin + c * s, Origin + height + 1));
        for (int r = 0; r <= Rows; r++)
            if (r % 5 == 0 || r == Rows)
                dc.DrawLine(ThickPen, new Point(Origin - 1, Origin + r * s), new Point(Origin + width + 1, Origin + r * s));
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.ChangedButton is MouseButton.Left or MouseButton.Right)
            Focus();
        if (BeginStroke(e.GetPosition(this), e.ChangedButton))
        {
            CaptureMouse();
            e.Handled = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        MovePointer(e.GetPosition(this));
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (EndStroke(e.ChangedButton))
            e.Handled = true;
    }

    // The pointer logic takes positions directly so tests can drive it without real mouse input.

    /// <summary>Starts a fill (left) or cross (right) stroke at a point in grid coordinates.</summary>
    internal bool BeginStroke(Point position, MouseButton button)
    {
        if (_strokeOriginals is not null || button is not (MouseButton.Left or MouseButton.Right))
            return false;
        if (!TryGetCell(position, out var cell))
            return false;

        _strokeFrom = _cells[cell.Row, cell.Col];
        _strokeTo = button == MouseButton.Left
            ? (_strokeFrom == CellState.Filled ? CellState.Empty : CellState.Filled)
            : (_strokeFrom == CellState.Crossed ? CellState.Empty : CellState.Crossed);
        _strokeButton = button;
        _strokeStart = _strokeEnd = cell;
        _strokeOriginals = new();
        UpdateStroke();
        return true;
    }

    internal void MovePointer(Point position)
    {
        if (_strokeOriginals is null)
        {
            SetHover(TryGetCell(position, out var cell) ? cell : null);
            return;
        }

        // Lock the stroke to whichever axis the pointer has moved further along.
        var target = ClampToCell(position);
        int dr = target.Row - _strokeStart.Row, dc = target.Col - _strokeStart.Col;
        var end = Math.Abs(dc) >= Math.Abs(dr) ? (_strokeStart.Row, target.Col) : (target.Row, _strokeStart.Col);
        SetHover(target);
        if (end != _strokeEnd)
        {
            _strokeEnd = end;
            UpdateStroke();
        }
    }

    internal bool EndStroke(MouseButton button)
    {
        if (_strokeOriginals is null || button != _strokeButton)
            return false;
        CommitStroke();
        return true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (_strokeOriginals is not null)
            CommitStroke();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_strokeOriginals is null)
            SetHover(null);
    }

    private void UpdateStroke()
    {
        var originals = _strokeOriginals!;
        var covered = new HashSet<(int Row, int Col)>(StrokeCells());

        // Restore cells the stroke no longer covers (the pointer moved back).
        foreach (var (cell, original) in originals.ToList())
        {
            if (!covered.Contains(cell))
            {
                _cells[cell.Row, cell.Col] = original;
                originals.Remove(cell);
            }
        }

        // Only change cells in the same state as the one the stroke started on,
        // so a fill drag doesn't wipe out crosses and vice versa.
        foreach (var cell in covered)
        {
            if (!originals.ContainsKey(cell) && _cells[cell.Row, cell.Col] == _strokeFrom)
            {
                originals[cell] = _strokeFrom;
                _cells[cell.Row, cell.Col] = _strokeTo;
            }
        }

        InvalidateVisual();
        CellsChanged?.Invoke(this, EventArgs.Empty);
        PointerChanged?.Invoke(this, EventArgs.Empty);
    }

    private IEnumerable<(int Row, int Col)> StrokeCells()
    {
        int rowStep = Math.Sign(_strokeEnd.Row - _strokeStart.Row);
        int colStep = Math.Sign(_strokeEnd.Col - _strokeStart.Col);
        var cell = _strokeStart;
        while (true)
        {
            yield return cell;
            if (cell == _strokeEnd)
                yield break;
            cell = (cell.Row + rowStep, cell.Col + colStep);
        }
    }

    private void CommitStroke()
    {
        var originals = _strokeOriginals!;
        _strokeOriginals = null;
        if (IsMouseCaptured)
            ReleaseMouseCapture();

        if (originals.Count > 0)
        {
            _undo.Push(originals.Select(kv => new CellChange(kv.Key.Row, kv.Key.Col, kv.Value, _strokeTo)).ToList());
            _redo.Clear();
        }
        PointerChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CancelStroke()
    {
        if (_strokeOriginals is null)
            return;
        foreach (var (cell, original) in _strokeOriginals)
            _cells[cell.Row, cell.Col] = original;
        _strokeOriginals = null;
        if (IsMouseCaptured)
            ReleaseMouseCapture();
    }

    private void SetHover((int Row, int Col)? cell)
    {
        if (_hover == cell)
            return;
        _hover = cell;
        InvalidateVisual();
        PointerChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool TryGetCell(Point p, out (int Row, int Col) cell)
    {
        int col = (int)Math.Floor((p.X - Origin) / CellSize);
        int row = (int)Math.Floor((p.Y - Origin) / CellSize);
        cell = (row, col);
        return row >= 0 && row < Rows && col >= 0 && col < Columns;
    }

    private (int Row, int Col) ClampToCell(Point p)
    {
        TryGetCell(p, out var cell);
        return (Math.Clamp(cell.Row, 0, Rows - 1), Math.Clamp(cell.Col, 0, Columns - 1));
    }

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
