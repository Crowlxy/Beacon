using Beacon.Core;
using Windows.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace Beacon.WinUI;

public static class HighlightedText
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text", typeof(string), typeof(HighlightedText), new PropertyMetadata(string.Empty, OnChanged));

    public static readonly DependencyProperty QueryProperty = DependencyProperty.RegisterAttached(
        "Query", typeof(string), typeof(HighlightedText), new PropertyMetadata(string.Empty, OnChanged));

    public static void SetText(DependencyObject target, string value) => target.SetValue(TextProperty, value);
    public static string GetText(DependencyObject target) => (string)target.GetValue(TextProperty);
    public static void SetQuery(DependencyObject target, string value) => target.SetValue(QueryProperty, value);
    public static string GetQuery(DependencyObject target) => (string)target.GetValue(QueryProperty);

    private static void OnChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not TextBlock textBlock) return;
        var text = GetText(textBlock) ?? string.Empty;
        var match = FuzzyMatcher.Match(GetQuery(textBlock) ?? string.Empty, text);
        textBlock.Inlines.Clear();
        for (var index = 0; index < text.Length;)
        {
            var highlighted = match.Success && match.MatchedIndices.Contains(index);
            var end = index + 1;
            while (end < text.Length && match.Success && match.MatchedIndices.Contains(end) == highlighted) end++;
            textBlock.Inlines.Add(new Run
            {
                Text = text[index..end],
                FontWeight = highlighted ? new FontWeight { Weight = (ushort)(int)Application.Current.Resources["ResultMatchFontWeight"] } : textBlock.FontWeight,
            });
            index = end;
        }
    }
}
