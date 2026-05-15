using System.Windows;
using System.Windows.Controls;

namespace SquadDash;

/// <summary>
/// Reusable markdown formatting toolbar. Attach to either a
/// <see cref="RichTextBox"/> via <see cref="TargetRichTextBox"/> or a plain
/// <see cref="TextBox"/> via <see cref="TargetTextBox"/>.
/// </summary>
public partial class MarkdownEditorToolbar : UserControl
{
    // ── Routed event ─────────────────────────────────────────────────────────

    public static readonly RoutedEvent ImageInsertRequestedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(ImageInsertRequested),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(MarkdownEditorToolbar));

    public event RoutedEventHandler ImageInsertRequested
    {
        add    => AddHandler(ImageInsertRequestedEvent, value);
        remove => RemoveHandler(ImageInsertRequestedEvent, value);
    }

    // ── Dependency properties ─────────────────────────────────────────────────

    public static readonly DependencyProperty ShowImageButtonProperty =
        DependencyProperty.Register(
            nameof(ShowImageButton), typeof(bool), typeof(MarkdownEditorToolbar),
            new PropertyMetadata(true, OnShowImageButtonChanged));

    public bool ShowImageButton
    {
        get => (bool)GetValue(ShowImageButtonProperty);
        set => SetValue(ShowImageButtonProperty, value);
    }

    private static void OnShowImageButtonChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownEditorToolbar tb)
            tb.ImageButton.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public static readonly DependencyProperty ShowHrButtonProperty =
        DependencyProperty.Register(
            nameof(ShowHrButton), typeof(bool), typeof(MarkdownEditorToolbar),
            new PropertyMetadata(true, OnShowHrButtonChanged));

    public bool ShowHrButton
    {
        get => (bool)GetValue(ShowHrButtonProperty);
        set => SetValue(ShowHrButtonProperty, value);
    }

    private static void OnShowHrButtonChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownEditorToolbar tb)
        {
            var vis = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
            tb.HrButton.Visibility    = vis;
            tb.HrSeparator.Visibility = vis;
        }
    }

    public static readonly DependencyProperty TargetRichTextBoxProperty =
        DependencyProperty.Register(
            nameof(TargetRichTextBox), typeof(RichTextBox), typeof(MarkdownEditorToolbar),
            new PropertyMetadata(null, OnTargetRichTextBoxChanged));

    public RichTextBox? TargetRichTextBox
    {
        get => (RichTextBox?)GetValue(TargetRichTextBoxProperty);
        set => SetValue(TargetRichTextBoxProperty, value);
    }

    private static void OnTargetRichTextBoxChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not MarkdownEditorToolbar tb) return;
        if (e.OldValue is RichTextBox old) old.SelectionChanged -= tb.RichTextBox_SelectionChanged;
        if (e.NewValue is RichTextBox next) next.SelectionChanged += tb.RichTextBox_SelectionChanged;
        tb.UpdateSelectionGatedButtons();
    }

    public static readonly DependencyProperty TargetTextBoxProperty =
        DependencyProperty.Register(
            nameof(TargetTextBox), typeof(TextBox), typeof(MarkdownEditorToolbar),
            new PropertyMetadata(null, OnTargetTextBoxChanged));

    public TextBox? TargetTextBox
    {
        get => (TextBox?)GetValue(TargetTextBoxProperty);
        set => SetValue(TargetTextBoxProperty, value);
    }

    private static void OnTargetTextBoxChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not MarkdownEditorToolbar tb) return;
        if (e.OldValue is TextBox old) old.SelectionChanged -= tb.TextBox_SelectionChanged;
        if (e.NewValue is TextBox next) next.SelectionChanged += tb.TextBox_SelectionChanged;
        tb.UpdateSelectionGatedButtons();
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    public MarkdownEditorToolbar()
    {
        InitializeComponent();
    }

    // ── Selection-gating ─────────────────────────────────────────────────────

    private void RichTextBox_SelectionChanged(object sender, RoutedEventArgs e) =>
        UpdateSelectionGatedButtons();

    private void TextBox_SelectionChanged(object sender, RoutedEventArgs e) =>
        UpdateSelectionGatedButtons();

    private void UpdateSelectionGatedButtons()
    {
        bool hasSelection = false;
        if (TargetRichTextBox is { } rtb)
            hasSelection = rtb.Selection is { IsEmpty: false };
        else if (TargetTextBox is { } tb)
            hasSelection = tb.SelectionLength > 0;

        BoldButton.IsEnabled         = hasSelection;
        ItalicButton.IsEnabled       = hasSelection;
        BulletListButton.IsEnabled   = hasSelection;
        NumberedListButton.IsEnabled = hasSelection;
    }

    // ── Command dispatch ──────────────────────────────────────────────────────

    private void Dispatch(Action<RichTextBox> richAction, Action<TextBox> plainAction)
    {
        if (TargetRichTextBox is { } rtb) { richAction(rtb); rtb.Focus(); }
        else if (TargetTextBox is { } tb) { plainAction(tb); tb.Focus(); }
    }

    private void BoldButton_Click(object sender, RoutedEventArgs e) =>
        Dispatch(MarkdownEditorCommands.ApplyBold, MarkdownEditorCommands.ApplyBold);

    private void ItalicButton_Click(object sender, RoutedEventArgs e) =>
        Dispatch(MarkdownEditorCommands.ApplyItalic, MarkdownEditorCommands.ApplyItalic);

    private void LinkButton_Click(object sender, RoutedEventArgs e) =>
        Dispatch(MarkdownEditorCommands.InsertLink, MarkdownEditorCommands.InsertLink);

    private void ImageButton_Click(object sender, RoutedEventArgs e) =>
        RaiseEvent(new RoutedEventArgs(ImageInsertRequestedEvent, this));

    private void TableButton_Click(object sender, RoutedEventArgs e) =>
        Dispatch(MarkdownEditorCommands.InsertTable, MarkdownEditorCommands.InsertTable);

    private void InlineCodeButton_Click(object sender, RoutedEventArgs e) =>
        Dispatch(MarkdownEditorCommands.InsertInlineCode, MarkdownEditorCommands.InsertInlineCode);

    private void CodeBlockButton_Click(object sender, RoutedEventArgs e) =>
        Dispatch(MarkdownEditorCommands.InsertCodeBlock, MarkdownEditorCommands.InsertCodeBlock);

    private void HrButton_Click(object sender, RoutedEventArgs e) =>
        Dispatch(MarkdownEditorCommands.InsertHorizontalRule, MarkdownEditorCommands.InsertHorizontalRule);

    private void BulletListButton_Click(object sender, RoutedEventArgs e) =>
        Dispatch(MarkdownEditorCommands.ApplyBulletList, MarkdownEditorCommands.ApplyBulletList);

    private void NumberedListButton_Click(object sender, RoutedEventArgs e) =>
        Dispatch(MarkdownEditorCommands.ApplyNumberedList, MarkdownEditorCommands.ApplyNumberedList);
}
