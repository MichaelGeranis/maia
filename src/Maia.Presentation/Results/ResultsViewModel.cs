using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maia.Application.Abstractions;
using Maia.Application.Plans;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Presentation.Collections;
using Maia.Presentation.Common;
using Maia.Presentation.Formatting;
using Maia.Presentation.Localization;
using Maia.Presentation.Navigation;

namespace Maia.Presentation.Results;

/// <summary>
/// "Results" (design screen 08, as the owner changed it): every report and scan photo in one list, newest first, ready
/// to show her doctor. They stand on their own, apart from the entries on her journey. One search box finds a result by
/// its name, what it is ("photo", "report") or when it was added ("oct", "3 oct"), in English or Greek, with or without
/// accents. Removing a result happens where it opens (the viewer). "Select" (or holding a tile) lets her tick several
/// and share them at once through the phone's share sheet (email, WhatsApp, Viber…). Tiles are kept across reloads and
/// searches, so only what changed is redrawn.
/// </summary>
public sealed partial class ResultsViewModel : ObservableObject
{
    private static readonly CultureInfo[] DateCultures = [CultureInfo.GetCultureInfo("en-GB"), CultureInfo.GetCultureInfo("el-GR")];

    private readonly PlanQueries _queries;
    private readonly ResultFiling _filing;
    private readonly ShareIntake _shares;
    private readonly INavigator _navigator;
    private readonly IErrorReporter _errors;
    private readonly ResultThumbnails _thumbnails;
    private readonly Dictionary<AttachmentId, ResultCard> _cards = [];
    private IReadOnlyList<ResultView> _results = [];
    private Task _drawing = Task.CompletedTask;

    public ResultsViewModel(
        PlanQueries queries,
        ResultFiling filing,
        ShareIntake shares,
        INavigator navigator,
        IErrorReporter errors,
        ResultThumbnails thumbnails)
    {
        _queries = queries;
        _filing = filing;
        _shares = shares;
        _navigator = navigator;
        _errors = errors;
        _thumbnails = thumbnails;
    }

    /// <summary>Done once every tile shown so far has its picture, or keeps its icon (for tests).</summary>
    internal Task ThumbnailsDrawn => _drawing;

    /// <summary>The results the search lets through, newest first, as tiles.</summary>
    public ObservableCollection<ResultCard> Results { get; } = [];

    /// <summary>What she types in the search box; empty shows every result.</summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    /// <summary>The search box shows once she has a result to search.</summary>
    [ObservableProperty]
    public partial bool CanSearch { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartSelecting))]
    public partial bool HasResults { get; private set; }

    /// <summary>Why the list is empty: nothing kept yet, or nothing matches the search.</summary>
    [ObservableProperty]
    public partial string EmptyText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string? Notice { get; private set; }

    /// <summary>"Take a photo" is offered when the phone has a camera app.</summary>
    public bool CanTakePhoto => _filing.CanTakePhoto;

    /// <summary>Whether she is choosing results to share: a tap ticks a tile rather than opening it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartSelecting))]
    public partial bool IsSelecting { get; private set; }

    /// <summary>"Select" shows while she has results and is not choosing already.</summary>
    public bool CanStartSelecting => HasResults && !IsSelecting;

    /// <summary>How many she has ticked.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(ShareSelectedCommand))]
    public partial int SelectedCount { get; private set; }

    public bool HasSelection => SelectedCount > 0;

    /// <summary>"Choose results to share", "1 selected", "3 selected".</summary>
    [ObservableProperty]
    public partial string SelectionText { get; private set; } = string.Empty;

    public async Task LoadAsync()
    {
        var overview = await _queries.GetResultsOverviewAsync();
        if (overview is null)
        {
            await _navigator.GoToSetupAsync();
            return;
        }

        Show(overview);
        await KeepSharedAsync();
    }

    /// <summary>
    /// What another app shared with Maia: kept in her results at once. Called as Results loads and again when more
    /// arrives while it shows.
    /// </summary>
    public async Task KeepSharedAsync()
    {
        if (_shares.Pending.Count == 0)
        {
            return;
        }

        Notice = null;
        await AfterAddingAsync(await _filing.KeepAsync(_shares.Take()));
    }

    /// <summary>
    /// Whether a result matches what she typed: every word she typed is found in its name, what it is or the day it was
    /// added, ignoring case and accents. Nothing typed matches everything.
    /// </summary>
    internal static bool Matches(string searchable, string query)
    {
        ArgumentNullException.ThrowIfNull(searchable);
        ArgumentNullException.ThrowIfNull(query);
        return Fold(query)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .All(word => searchable.Contains(word, StringComparison.Ordinal));
    }

    /// <summary>Lower case without accents, so "Υπερηχογράφημα", "υπερηχογραφημα" and "ΥΠΕΡΗΧΟΓΡΑΦΗΜΑ" all match.</summary>
    internal static string Fold(string text)
    {
        var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var folded = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                // A final sigma reads as any other, so "σκαν" finds "σκάνς" mid-word and at its end alike.
                folded.Append(c == 'ς' ? 'σ' : c);
            }
        }

        return folded.ToString().Normalize(NormalizationForm.FormC);
    }

    partial void OnSearchTextChanged(string value) => ShowResults();

    /// <summary>After a change: the list shows it, and a reload that fails says so instead of ending Maia.</summary>
    private async Task ReloadAsync() => Notice = await ScreenLoad.ReloadAsync(LoadAsync, "results.load", _errors) ?? Notice;

    /// <summary>"Upload results": keeps what she picks in her results.</summary>
    [RelayCommand]
    private async Task UploadAsync()
    {
        Notice = null;
        await AfterAddingAsync(await _filing.UploadToResultsAsync());
    }

    /// <summary>"Take a photo": photographs a paper report and keeps it in her results.</summary>
    [RelayCommand]
    private async Task TakePhotoAsync()
    {
        Notice = null;
        await AfterAddingAsync(await _filing.TakePhotoToResultsAsync());
    }

    /// <summary>
    /// A tap on a tile: opens the result in Maia's own viewer, where it can also be renamed or removed; while she is
    /// choosing results to share, ticks it or unticks it instead.
    /// </summary>
    [RelayCommand]
    private async Task OpenAsync(ResultCard card)
    {
        if (card is null)
        {
            return;
        }

        if (IsSelecting)
        {
            Toggle(card);
            return;
        }

        Notice = null;
        Notice = await _filing.OpenAsync(card.Item);
    }

    /// <summary>"Select": she starts choosing results to share, with none ticked yet.</summary>
    [RelayCommand]
    private void StartSelecting()
    {
        IsSelecting = true;
        Count();
    }

    /// <summary>She holds a tile: choosing starts with that result ticked (or, already choosing, it is ticked or unticked).</summary>
    [RelayCommand]
    private void SelectFrom(ResultCard card)
    {
        if (card is null)
        {
            return;
        }

        if (!IsSelecting)
        {
            IsSelecting = true;
            card.IsSelected = true;
            Count();
            return;
        }

        Toggle(card);
    }

    /// <summary>"Select all": every result the search shows is ticked; with all of them ticked already, none are.</summary>
    [RelayCommand]
    private void SelectAll()
    {
        var all = Results.Count > 0 && Results.All(c => c.IsSelected);
        foreach (var card in Results)
        {
            card.IsSelected = !all;
        }

        Count();
    }

    /// <summary>× or the phone's Back: choosing ends and every tick goes.</summary>
    [RelayCommand]
    private void StopSelecting()
    {
        foreach (var card in _cards.Values)
        {
            card.IsSelected = false;
        }

        IsSelecting = false;
        Count();
    }

    /// <summary>
    /// "Share": the ticked results go to the phone's share sheet, where she picks the app (email, WhatsApp, Viber…).
    /// Choosing ends once the share sheet opens; if it can't, the ticks stay and a calm line says so.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task ShareSelectedAsync()
    {
        var chosen = _results.Select(r => _cards[r.Result.Id]).Where(c => c.IsSelected).Select(c => c.Item).ToList();
        if (chosen.Count == 0)
        {
            return;
        }

        Notice = await _filing.ShareAsync(chosen);
        if (Notice is null)
        {
            StopSelecting();
        }
    }

    /// <summary>The phone's Back while choosing ends the choosing rather than leaving Results.</summary>
    /// <returns>Whether Back was used for that.</returns>
    public bool HandleBack()
    {
        if (!IsSelecting)
        {
            return false;
        }

        StopSelecting();
        return true;
    }

    /// <summary>Leaving Results ends any choosing, so she comes back to the plain list.</summary>
    public void Leave()
    {
        if (IsSelecting)
        {
            StopSelecting();
        }
    }

    private void Toggle(ResultCard card)
    {
        card.IsSelected = !card.IsSelected;
        Count();
    }

    private void Count()
    {
        SelectedCount = _cards.Values.Count(c => c.IsSelected);
        SelectionText = SelectedCount == 0 ? Text.Get("Results_SelectHint") : Text.Format("Results_Selected", SelectedCount);
    }

    /// <summary>After adding results here: the list shows them, with the search cleared so they are in view.</summary>
    private async Task AfterAddingAsync(AddedResults added)
    {
        Notice = added.Message;
        if (added.Ids.Count == 0)
        {
            return;
        }

        SearchText = string.Empty;
        await ReloadAsync();
    }

    private void Show(ResultsOverview overview)
    {
        var language = Text.Language;
        _results = overview.Results;
        foreach (var gone in _cards.Keys.Except(_results.Select(r => r.Result.Id)).ToList())
        {
            _cards.Remove(gone);
        }

        if (IsSelecting)
        {
            Count();
        }

        foreach (var view in _results)
        {
            var item = ResultItem.From(view.Result);
            if (!_cards.TryGetValue(item.Id, out var card))
            {
                _cards[item.Id] = card = new ResultCard(item);
            }

            card.Show(item, DisplayFormat.Date(DateOnly.FromDateTime(view.Result.AddedAt.LocalDateTime)), Searchable(view));
        }

        CanSearch = _results.Count > 0;
        ShowResults();
        if (_drawing.IsCompleted)
        {
            _drawing = DrawThumbnailsAsync();
        }
    }

    /// <summary>
    /// Gives each tile its small picture, newest first and one at a time, after the list is on screen; until then (and
    /// for a file the phone can't draw) the tile shows its icon. A tile keeps its picture across reloads, so each is
    /// drawn once, and a result added while drawing is drawn in the same pass.
    /// </summary>
    private async Task DrawThumbnailsAsync()
    {
        while (_results.Select(r => r.Result).FirstOrDefault(r => _cards.TryGetValue(r.Id, out var card) && !card.HasTriedThumbnail) is { } next)
        {
            var card = _cards[next.Id];
            card.HasTriedThumbnail = true;
            card.Thumbnail = await _thumbnails.MakeAsync(next);
        }
    }

    /// <summary>Everything a search can find a result by, folded once: name, what it is, and the day it was added.</summary>
    private static string Searchable(ResultView view)
    {
        var words = new List<string> { view.Result.DisplayName };

        foreach (var language in new[] { Text.English, Text.Greek })
        {
            var photo = view.Kind == AttachmentKind.Photo;
            words.Add(Text.In(photo ? "Results_KindPhoto" : "Results_KindPdf", language));
            words.Add(Text.In(photo ? "Results_SearchPhotoWords" : "Results_SearchReportWords", language));
        }

        var added = view.Result.AddedAt.LocalDateTime;
        foreach (var culture in DateCultures)
        {
            words.Add(added.ToString("dddd d MMMM MMM yyyy", culture));
        }

        return Fold(string.Join(' ', words));
    }

    private void ShowResults()
    {
        var wanted = _results.Select(r => _cards[r.Result.Id]).Where(c => Matches(c.Searchable, SearchText)).ToList();
        CollectionSync.Apply(Results, wanted, (a, b) => a.Id == b.Id);
        HasResults = Results.Count > 0;
        EmptyText = Text.Get(_results.Count > 0 ? "Results_EmptySearch" : "Results_EmptyAll");
    }
}

/// <summary>
/// A result's tile on Results: its name, what it is and the day it was added. Kept for as long as the result is in her
/// plan, so it is updated in place.
/// </summary>
public sealed partial class ResultCard : ObservableObject
{
    internal ResultCard(ResultItem item)
    {
        Item = item;
    }

    /// <summary>The result, to open it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Name), nameof(KindLabel), nameof(Kind), nameof(IsPhoto))]
    public partial ResultItem Item { get; private set; }

    public AttachmentId Id => Item.Id;

    public string Name => Item.Name;

    public string KindLabel => Item.KindLabel;

    public AttachmentKind Kind => Item.Kind;

    public bool IsPhoto => Item.IsPhoto;

    /// <summary>"PDF · Sat 3 Oct": what it is and the day it was added.</summary>
    [ObservableProperty]
    public partial string Meta { get; private set; } = string.Empty;

    /// <summary>Whether she has ticked it to share.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; internal set; }

    /// <summary>A small picture of the photo or of the PDF's first page; null while it is drawn, or when it can't be.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasThumbnail))]
    public partial byte[]? Thumbnail { get; internal set; }

    public bool HasThumbnail => Thumbnail is not null;

    /// <summary>Whether its picture has been drawn, or tried: each tile's is drawn once.</summary>
    internal bool HasTriedThumbnail { get; set; }

    /// <summary>What a search looks through, folded (see <see cref="ResultsViewModel.Fold"/>).</summary>
    internal string Searchable { get; private set; } = string.Empty;

    internal void Show(ResultItem item, string addedOn, string searchable)
    {
        Item = item;
        Meta = $"{item.KindLabel} · {addedOn}";
        Searchable = searchable;
    }
}
