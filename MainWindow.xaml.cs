using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NonogramNotes;

public partial class MainWindow : Window
{
    private const int MaxSize = 30;

    private static readonly Brush ClueBrush = Brushes.Black;
    private static readonly Brush SatisfiedClueBrush = new SolidColorBrush(Color.FromRgb(0xA8, 0xA8, 0xA8));
    private static readonly Brush ClueBackground = Brushes.White;
    private static readonly Brush HoverClueBackground = new SolidColorBrush(Color.FromRgb(0xDC, 0xE8, 0xF8));
    private static readonly Brush ClueBorderBrush = new SolidColorBrush(Color.FromRgb(0xD8, 0xD8, 0xD8));
    private static readonly Brush ClueGroupBorderBrush = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));

    private readonly List<TextBox> _rowClues = new();
    private readonly List<TextBox> _columnClues = new();

    public MainWindow()
    {
        InitializeComponent();
        Board.CellsChanged += (_, _) => UpdateClueStatus();
        Board.PointerChanged += (_, _) => UpdatePointer();
        ZoomSlider.ValueChanged += (_, e) => ApplyCellSize(e.NewValue);
        BuildClues(Board.Rows, Board.Columns);
        ApplyCellSize(ZoomSlider.Value);
    }

    private void BuildClues(int rows, int cols)
    {
        RebuildClueBoxes(_rowClues, RowCluePanel, rows, isColumn: false);
        RebuildClueBoxes(_columnClues, ColumnCluePanel, cols, isColumn: true);
        ApplyCellSize(ZoomSlider.Value);
        UpdateClueStatus();
    }

    /// <summary>Recreates the clue boxes for one edge, keeping existing clue text where it still fits.</summary>
    private void RebuildClueBoxes(List<TextBox> boxes, Panel panel, int count, bool isColumn)
    {
        var oldText = boxes.Select(b => b.Text).ToList();
        boxes.Clear();
        panel.Children.Clear();
        for (int i = 0; i < count; i++)
        {
            var box = CreateClueBox(isColumn, i);
            box.Text = i < oldText.Count ? oldText[i] : "";
            boxes.Add(box);
            panel.Children.Add(box);
        }
    }

    private TextBox CreateClueBox(bool isColumn, int index)
    {
        var box = new TextBox
        {
            Padding = new Thickness(0),
            // Divider after each clue, darker every five to match the grid's guide lines.
            BorderThickness = isColumn ? new Thickness(0, 0, 1, 0) : new Thickness(0, 0, 0, 1),
            BorderBrush = (index + 1) % 5 == 0 ? ClueGroupBorderBrush : ClueBorderBrush,
            Background = ClueBackground,
            FontWeight = FontWeights.SemiBold,
        };

        if (isColumn)
        {
            // Column clues stack vertically, one number per line, aligned to the grid.
            box.AcceptsReturn = true;
            box.TextAlignment = TextAlignment.Center;
            box.VerticalContentAlignment = VerticalAlignment.Bottom;
            box.ToolTip = $"Column {index + 1} clue";
        }
        else
        {
            box.TextAlignment = TextAlignment.Right;
            box.VerticalContentAlignment = VerticalAlignment.Center;
            box.ToolTip = $"Row {index + 1} clue";
        }

        box.PreviewKeyDown += (_, e) => ClueBox_PreviewKeyDown(box, isColumn, index, e);
        box.PreviewTextInput += (_, e) => ClueBox_PreviewTextInput(box, isColumn, e);
        box.LostFocus += (_, _) => box.Text = FormatClue(ParseClue(box.Text), isColumn, box.Text);
        box.TextChanged += (_, _) => UpdateClueStatus();
        return box;
    }

    private void ClueBox_PreviewKeyDown(TextBox box, bool isColumn, int index, KeyEventArgs e)
    {
        // WPF text boxes don't raise PreviewTextInput for space, so separators are handled here.
        if (e.Key == Key.Space && isColumn)
        {
            InsertText(box, Environment.NewLine);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && !isColumn)
        {
            _rowClues[(index + 1) % _rowClues.Count].Focus();
            e.Handled = true;
        }
    }

    private static void ClueBox_PreviewTextInput(TextBox box, bool isColumn, TextCompositionEventArgs e)
    {
        if (e.Text.All(char.IsAsciiDigit))
            return;

        // Treat a comma as a separator; reject anything else that isn't a digit.
        if (e.Text == ",")
            InsertText(box, isColumn ? Environment.NewLine : " ");
        e.Handled = true;
    }

    private static void InsertText(TextBox box, string text)
    {
        box.SelectedText = text;
        box.Select(box.SelectionStart + box.SelectionLength, 0);
    }

    private static List<int> ParseClue(string text) =>
        text.Split(new[] { ' ', ',', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(part => int.TryParse(part, out int n) ? n : 0)
            .Where(n => n > 0)
            .ToList();

    private static string FormatClue(List<int> numbers, bool isColumn, string original)
    {
        // A lone "0" marks a deliberately empty line, so keep it rather than blanking the box.
        if (numbers.Count == 0)
            return original.Any(char.IsAsciiDigit) ? "0" : "";
        return string.Join(isColumn ? Environment.NewLine : " ", numbers);
    }

    private void UpdateClueStatus()
    {
        // During a resize the grid changes size before the clue boxes are rebuilt,
        // so only check lines that exist in both.
        for (int r = 0; r < Math.Min(_rowClues.Count, Board.Rows); r++)
            SetClueStatus(_rowClues[r], Board.GetRowRuns(r));
        for (int c = 0; c < Math.Min(_columnClues.Count, Board.Columns); c++)
            SetClueStatus(_columnClues[c], Board.GetColumnRuns(c));
    }

    /// <summary>Greys out a clue once the filled runs on its line match it exactly.</summary>
    private static void SetClueStatus(TextBox box, List<int> runs)
    {
        bool satisfied = box.Text.Any(char.IsAsciiDigit) && runs.SequenceEqual(ParseClue(box.Text));
        box.Foreground = satisfied ? SatisfiedClueBrush : ClueBrush;
    }

    private void UpdatePointer()
    {
        var hover = Board.HoverCell;
        for (int r = 0; r < _rowClues.Count; r++)
            _rowClues[r].Background = hover?.Row == r ? HoverClueBackground : ClueBackground;
        for (int c = 0; c < _columnClues.Count; c++)
            _columnClues[c].Background = hover?.Col == c ? HoverClueBackground : ClueBackground;

        string status = hover is { } cell ? $"Row {cell.Row + 1}, Column {cell.Col + 1}" : "";
        if (Board.StrokeLength > 0)
            status += $"    Length: {Board.StrokeLength}";
        StatusText.Text = status;
    }

    private void ApplyCellSize(double size)
    {
        Board.CellSize = size;
        double fontSize = Math.Max(8, Math.Round(size * 0.55));
        foreach (var box in _rowClues)
        {
            box.Height = size;
            box.MinWidth = size * 3;
            box.FontSize = fontSize;
        }
        foreach (var box in _columnClues)
        {
            box.Width = size;
            box.MinHeight = size * 3;
            box.FontSize = fontSize;
        }
    }

    private void ResizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadSize(RowsBox, out int rows) || !TryReadSize(ColumnsBox, out int cols))
        {
            MessageBox.Show(this, $"Rows and columns must be whole numbers from 1 to {MaxSize}.",
                "Nonogram Notes", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool losesData = Board.HasMarksOutside(rows, cols)
            || _rowClues.Skip(rows).Any(b => b.Text.Length > 0)
            || _columnClues.Skip(cols).Any(b => b.Text.Length > 0);
        if (losesData && MessageBox.Show(this,
                "Shrinking the grid will discard marks and clues outside the new size. Continue?",
                "Nonogram Notes", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
            return;

        ApplyResize(rows, cols);
    }

    /// <summary>Resizes the grid and its clue boxes together.</summary>
    internal void ApplyResize(int rows, int cols)
    {
        Board.Resize(rows, cols);
        BuildClues(rows, cols);
    }

    private static bool TryReadSize(TextBox box, out int value) =>
        int.TryParse(box.Text.Trim(), out value) && value >= 1 && value <= MaxSize;

    private void UndoButton_Click(object sender, RoutedEventArgs e) => Board.Undo();

    private void RedoButton_Click(object sender, RoutedEventArgs e) => Board.Redo();

    private void ClearMarksButton_Click(object sender, RoutedEventArgs e) => Board.ClearMarks();

    private void ClearCluesButton_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, "Clear all row and column clues?", "Nonogram Notes",
                MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
            return;
        foreach (var box in _rowClues.Concat(_columnClues))
            box.Clear();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        // Text boxes handle their own Ctrl+Z, so this only fires when the grid has focus.
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z)
            Board.Undo();
        else if ((Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y)
                 || (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.Z))
            Board.Redo();
        else
            return;
        e.Handled = true;
    }

    private void Scroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control)
            return;
        ZoomSlider.Value += e.Delta > 0 ? ZoomSlider.TickFrequency : -ZoomSlider.TickFrequency;
        e.Handled = true;
    }
}
