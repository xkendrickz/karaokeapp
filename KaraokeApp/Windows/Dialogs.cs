using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using KaraokeApp.Data;
using KaraokeApp.Models;
using WpfOpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace KaraokeApp.Windows
{
    // ═══════════════════════════════════════════════════════════════
    //  INPUT DIALOG
    // ═══════════════════════════════════════════════════════════════

    public class InputDialog : Window
    {
        public string InputText { get; private set; }
        private readonly TextBox _tb;

        public InputDialog(string label, string title, string defaultValue = "")
        {
            Title  = title; Width = 340; Height = 148;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;

            var sp = new StackPanel { Margin = new Thickness(12) };
            sp.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0,0,0,6) });
            _tb = new TextBox { Text = defaultValue };
            sp.Children.Add(_tb);

            var bp = new StackPanel { Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,10,0,0) };
            var ok = new Button { Content = "OK", Width = 70, Margin = new Thickness(0,0,6,0) };
            ok.Click += (s, e) => { InputText = _tb.Text; DialogResult = true; };
            var cancel = new Button { Content = "Cancel", Width = 70 };
            cancel.Click += (s, e) => DialogResult = false;
            bp.Children.Add(ok); bp.Children.Add(cancel);
            sp.Children.Add(bp);
            Content = sp;
            Loaded += (s, e) => _tb.Focus();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  CATEGORY EDIT DIALOG  (Name + References)
    // ═══════════════════════════════════════════════════════════════

    public class CategoryEditDialog : Window
    {
        public string CategoryName       { get; private set; }
        public string CategoryKeywords { get; private set; }

        private readonly TextBox _tbName, _tbRefs;

        public CategoryEditDialog(Category edit = null)
        {
            Title  = edit == null ? "Add Category" : "Edit Category";
            Width  = 480; Height = 210;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;

            var grid = new Grid { Margin = new Thickness(12) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            void AddRow(int row, string lbl, TextBox tb, string tip = "")
            {
                var l = new TextBlock { Text = lbl, VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 6, 8, 6) };
                Grid.SetRow(l, row); Grid.SetColumn(l, 0); grid.Children.Add(l);
                tb.Margin = new Thickness(0, 6, 0, 6);
                if (!string.IsNullOrEmpty(tip)) tb.ToolTip = tip;
                Grid.SetRow(tb, row); Grid.SetColumn(tb, 1); grid.Children.Add(tb);
            }

            _tbName = new TextBox { Text = edit?.Name ?? "" };
            _tbRefs = new TextBox
            {
                Text    = edit?.Keywords ?? "",
                ToolTip = "Comma-separated keywords, e.g: english,inggris,barat,western"
            };

            AddRow(0, "Name:", _tbName);
            AddRow(1, "References:", _tbRefs, "Comma-separated keywords, e.g: english,inggris,barat");

            var hint = new TextBlock
            {
                Text         = "References are comma-separated keywords matched during filename import.",
                FontSize     = 10,
                Foreground   = System.Windows.Media.Brushes.Gray,
                TextWrapping = TextWrapping.Wrap,
                Margin       = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(hint, 2); Grid.SetColumn(hint, 0); Grid.SetColumnSpan(hint, 2);
            grid.Children.Add(hint);

            var bp = new StackPanel { Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right };
            var ok = new Button { Content = "Save", Width = 80, Margin = new Thickness(0,0,6,0) };
            ok.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_tbName.Text))
                {
                    MessageBox.Show("Name is required.", "Validation",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                CategoryName       = _tbName.Text.Trim();
                CategoryKeywords = _tbRefs.Text.Trim();
                DialogResult = true;
            };
            var cancel = new Button { Content = "Cancel", Width = 80 };
            cancel.Click += (s, e) => DialogResult = false;
            bp.Children.Add(ok); bp.Children.Add(cancel);
            Grid.SetRow(bp, 3); Grid.SetColumn(bp, 0); Grid.SetColumnSpan(bp, 2);
            grid.Children.Add(bp);

            Content = grid;
            Loaded += (s, e) => _tbName.Focus();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  CATEGORY PICKER  (used during folder import)
    // ═══════════════════════════════════════════════════════════════

    public class CategoryPickerDialog : Window
    {
        public int SelectedCategoryId { get; private set; }
        private readonly ComboBox _combo;

        public CategoryPickerDialog(List<Category> categories)
        {
            Title  = "Default Category for Import"; Width = 320; Height = 148;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;

            var sp = new StackPanel { Margin = new Thickness(12) };
            sp.Children.Add(new TextBlock
            {
                Text       = "Assign songs to category if filename has no match:",
                Margin     = new Thickness(0, 0, 0, 6),
                TextWrapping = TextWrapping.Wrap
            });
            _combo = new ComboBox { DisplayMemberPath = "Name", SelectedValuePath = "Id" };
            var opts = new List<Category> { new Category { Id = 0, Name = "(Auto-detect from filename)" } };
            opts.AddRange(categories);
            _combo.ItemsSource   = opts;
            _combo.SelectedIndex = 0;
            sp.Children.Add(_combo);

            var bp = new StackPanel { Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,10,0,0) };
            var ok = new Button { Content = "OK", Width = 70, Margin = new Thickness(0,0,6,0) };
            ok.Click += (s, e) =>
            {
                SelectedCategoryId = _combo.SelectedValue is int id ? id : 0;
                DialogResult = true;
            };
            var cancel = new Button { Content = "Cancel", Width = 70 };
            cancel.Click += (s, e) => DialogResult = false;
            bp.Children.Add(ok); bp.Children.Add(cancel);
            sp.Children.Add(bp);
            Content = sp;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  SONG EDIT DIALOG  (with auto-parse from filename)
    // ═══════════════════════════════════════════════════════════════

    public class SongEditDialog : Window
    {
        public Song Song { get; private set; }

        private TextBox  _txtTitle, _txtArtist, _txtFilePath, _txtVocalPath;
        private ComboBox _cmbCategory;

        private readonly List<Category> _categories;
        private readonly Song           _editSong;
        private readonly string         _separator;

        public SongEditDialog(List<Category> categories,
                              Song editSong   = null,
                              string separator = "#")
        {
            _categories = categories;
            _editSong   = editSong;
            _separator  = separator;

            Title  = editSong == null ? "Add Song" : "Edit Song";
            Width  = 560; Height = 420;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            BuildUI();
            if (editSong != null) PopulateFields(editSong);
        }

        private void BuildUI()
        {
            var grid = new Grid { Margin = new Thickness(12) };
            for (int i = 0; i < 7; i++)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });

            _txtTitle     = new TextBox();
            _txtArtist    = new TextBox();
            _txtFilePath  = new TextBox();
            _txtVocalPath = new TextBox();
            _cmbCategory  = BuildCategoryCombo();

            AddRow(grid, 0, "Title:",        _txtTitle);
            AddRow(grid, 1, "Artist:",       _txtArtist);
            AddRow(grid, 2, "Category:",     _cmbCategory);
            AddRow(grid, 3, "File Path:",    _txtFilePath,  browseMain:  true);
            AddRow(grid, 4, "Vocal Track:",  _txtVocalPath, browseVocal: true);

            // Auto-parse hint
            var hint = new TextBlock
            {
                Text         = "Tip: click \"Auto-parse\" after selecting a file to extract title/artist/category from the filename.",
                FontSize     = 10,
                Foreground   = System.Windows.Media.Brushes.Gray,
                TextWrapping = TextWrapping.Wrap,
                Margin       = new Thickness(0, 4, 0, 4)
            };
            Grid.SetRow(hint, 5); Grid.SetColumn(hint, 0); Grid.SetColumnSpan(hint, 3);
            grid.Children.Add(hint);

            // Buttons row
            var bp = new StackPanel
            {
                Orientation         = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin              = new Thickness(0, 8, 0, 0)
            };
            var btnParse = new Button { Content = "Auto-parse", Width = 90, Margin = new Thickness(0,0,10,0) };
            btnParse.Click += BtnAutoParse_Click;
            var btnOk     = new Button { Content = "Save",   Width = 80, Margin = new Thickness(0,0,6,0) };
            btnOk.Click += BtnOk_Click;
            var btnCancel = new Button { Content = "Cancel", Width = 80 };
            btnCancel.Click += (s, e) => DialogResult = false;
            bp.Children.Add(btnParse);
            bp.Children.Add(btnOk);
            bp.Children.Add(btnCancel);
            Grid.SetRow(bp, 6); Grid.SetColumn(bp, 0); Grid.SetColumnSpan(bp, 3);
            grid.Children.Add(bp);

            Content = grid;
        }

        private void AddRow(Grid grid, int row, string label,
                            Control ctrl, bool browseMain = false, bool browseVocal = false)
        {
            var lbl = new TextBlock
            {
                Text = label, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 6, 8, 6)
            };
            Grid.SetRow(lbl, row); Grid.SetColumn(lbl, 0); grid.Children.Add(lbl);

            ctrl.Margin = new Thickness(0, 6, 6, 6);
            Grid.SetRow(ctrl, row); Grid.SetColumn(ctrl, 1);
            if (!browseMain && !browseVocal) Grid.SetColumnSpan(ctrl, 2);
            grid.Children.Add(ctrl);

            if (browseMain || browseVocal)
            {
                var btn = new Button { Content = "Browse", Width = 72, Margin = new Thickness(0, 6, 0, 6) };
                var target = ctrl as TextBox;
                bool isVocal = browseVocal;
                btn.Click += (s, e) => BrowseFile(target, isVocal);
                Grid.SetRow(btn, row); Grid.SetColumn(btn, 2);
                grid.Children.Add(btn);
            }
        }

        private ComboBox BuildCategoryCombo()
        {
            var cb = new ComboBox { DisplayMemberPath = "Name", SelectedValuePath = "Id" };
            var opts = new List<Category> { new Category { Id = 0, Name = "(Uncategorized)" } };
            opts.AddRange(_categories);
            cb.ItemsSource   = opts;
            cb.SelectedIndex = 0;
            return cb;
        }

        private void BrowseFile(TextBox target, bool isVocal)
        {
            var ofd = new WpfOpenFileDialog
            {
                Title  = isVocal ? "Select Vocal Track" : "Select Karaoke File",
                Filter = "All Supported|*.mp4;*.mkv;*.mpg;*.mpeg;*.avi;*.dat;*.m4a;*.mp3;" +
                         "*.flac;*.ogg;*.wav;*.wma;*.cdg;*.zip;*.flv;*.wmv;*.mov;*.ts;*.vob;*.webm" +
                         "|All Files|*.*"
            };
            if (ofd.ShowDialog() == true && target != null)
            {
                target.Text = ofd.FileName;
                // Auto-parse main file immediately
                if (!isVocal) AutoParseFilename(ofd.FileName);
            }
        }

        /// <summary>
        /// Parse filename using separator → fill Title, Artist, Category fields.
        /// Format: TITLE # ARTIST # CATEGORY_REF # VOCAL_HINT (ext ignored)
        /// </summary>
        private void AutoParseFilename(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;
            string nameNoExt = Path.GetFileNameWithoutExtension(filePath);
            string[] parts   = nameNoExt.Split(
                new[] { _separator }, StringSplitOptions.None);

            if (parts.Length >= 1 && string.IsNullOrEmpty(_txtTitle.Text))
                _txtTitle.Text = parts[0].Trim();

            if (parts.Length >= 2 && string.IsNullOrEmpty(_txtArtist.Text))
                _txtArtist.Text = parts[1].Trim();

            if (parts.Length >= 3)
            {
                var cat = DatabaseService.Instance.FindCategoryByReference(parts[2].Trim());
                if (cat != null) _cmbCategory.SelectedValue = cat.Id;
            }
        }

        private void BtnAutoParse_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_txtFilePath.Text))
            {
                // Force-overwrite fields
                string nameNoExt = Path.GetFileNameWithoutExtension(_txtFilePath.Text);
                string[] parts   = nameNoExt.Split(
                    new[] { _separator }, StringSplitOptions.None);

                if (parts.Length >= 1) _txtTitle.Text  = parts[0].Trim();
                if (parts.Length >= 2) _txtArtist.Text = parts[1].Trim();
                if (parts.Length >= 3)
                {
                    var cat = DatabaseService.Instance.FindCategoryByReference(parts[2].Trim());
                    if (cat != null) _cmbCategory.SelectedValue = cat.Id;
                }
            }
        }

        private void PopulateFields(Song s)
        {
            _txtTitle.Text     = s.Title;
            _txtArtist.Text    = s.Artist;
            _txtFilePath.Text  = s.FilePath;
            _txtVocalPath.Text = s.VocalFilePath;
            if (s.CategoryId > 0) _cmbCategory.SelectedValue = s.CategoryId;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_txtTitle.Text))
            {
                MessageBox.Show("Title is required.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Song = new Song
            {
                Id            = _editSong?.Id ?? 0,
                Title         = _txtTitle.Text.Trim(),
                Artist        = _txtArtist.Text.Trim(),
                FilePath      = _txtFilePath.Text.Trim(),
                VocalFilePath = _txtVocalPath.Text.Trim(),
                CategoryId    = _cmbCategory.SelectedValue is int id ? id : 0,
                SourceType    = _editSong?.SourceType ?? "local",
                DateAdded     = _editSong?.DateAdded  ?? DateTime.Now
            };
            DialogResult = true;
        }
    }
}
