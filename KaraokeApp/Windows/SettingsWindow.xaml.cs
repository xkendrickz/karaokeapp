using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using KaraokeApp.Data;
using KaraokeApp.Models;
using KaraokeApp.Services;

namespace KaraokeApp.Windows
{
    public partial class SettingsWindow : Window
    {
        private readonly DatabaseService _db = DatabaseService.Instance;
        private readonly YoutubeService  _yt;

        private static readonly string[] SupportedExtensions =
        {
            ".mp4",".mkv",".mpg",".mpeg",".avi",".dat",
            ".m4a",".mp3",".flac",".ogg",".wav",".wma",
            ".cdg",".zip",".flv",".wmv",".mov",".ts",
            ".vob",".m2ts",".3gp",".webm"
        };

        public SettingsWindow(YoutubeService youtubeService)
        {
            InitializeComponent();
            _yt = youtubeService;
            Loaded += SettingsWindow_Loaded;
        }

        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSongs();
            LoadCategories();
            LoadYoutubeSettings();
            LoadImportSettings();
        }

        // ── SONGS ────────────────────────────────────────────────────

        private void LoadSongs(string filter = "")
        {
            var songs = _db.GetAllSongs(0, filter, true, "local");
            songs.AddRange(_db.GetYoutubeSongs(0, filter, true));
            songs = songs.OrderBy(s => s.Title).ToList();
            SongGrid.ItemsSource = songs;
            TxtSongCount.Text    = songs.Count + " song(s)";
        }

        private void TxtSongSearch_TextChanged(object sender, TextChangedEventArgs e)
            => LoadSongs(TxtSongSearch.Text.Trim());

        private void BtnAddSong_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SongEditDialog(_db.GetAllCategories(),
                separator: LoadSeparator()) { Owner = this };
            if (dlg.ShowDialog() == true) { _db.AddSong(dlg.Song); LoadSongs(); }
        }

        private void BtnEditSong_Click(object sender, RoutedEventArgs e)
        {
            if (SongGrid.SelectedItem is Song sel)
            {
                var dlg = new SongEditDialog(_db.GetAllCategories(),
                    sel, separator: LoadSeparator()) { Owner = this };
                if (dlg.ShowDialog() == true) { _db.UpdateSong(dlg.Song); LoadSongs(); }
            }
        }

        private void BtnDeleteSong_Click(object sender, RoutedEventArgs e)
        {
            if (SongGrid.SelectedItem is Song sel)
            {
                var r = System.Windows.MessageBox.Show(
                    "Delete \"" + sel.Title + "\" from the database?",
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (r == MessageBoxResult.Yes) { _db.DeleteSong(sel.Id); LoadSongs(); }
            }
        }

        private void BtnImportFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var fbd = new System.Windows.Forms.FolderBrowserDialog
            { Description = "Select folder containing karaoke files" })
            {
                if (fbd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                var categories = _db.GetAllCategories();
                var catPicker  = new CategoryPickerDialog(categories) { Owner = this };
                int defaultCatId = 0;
                if (catPicker.ShowDialog() == true)
                    defaultCatId = catPicker.SelectedCategoryId;

                string sep = LoadSeparator();
                int added = 0;

                foreach (string file in Directory.EnumerateFiles(
                    fbd.SelectedPath, "*.*", SearchOption.AllDirectories))
                {
                    string ext = Path.GetExtension(file).ToLower();
                    if (!SupportedExtensions.Contains(ext)) continue;

                    string nameNoExt = Path.GetFileNameWithoutExtension(file).ToLower();
                    if (nameNoExt.EndsWith("_vocal") || nameNoExt.EndsWith("-vocal")) continue;

                    var song = ParseFilename(file, sep, defaultCatId);

                    // Auto-detect companion vocal file
                    string baseName = Path.GetFileNameWithoutExtension(file);
                    foreach (var ve in new[] { "_vocal", "-vocal" })
                        foreach (var ae in new[] { ".mp3", ".m4a", ".wav", ".flac" })
                        {
                            string vf = Path.Combine(
                                Path.GetDirectoryName(file) ?? "", baseName + ve + ae);
                            if (File.Exists(vf)) { song.VocalFilePath = vf; break; }
                        }

                    _db.AddSong(song);
                    added++;
                }

                LoadSongs();
                System.Windows.MessageBox.Show(
                    "Import complete: " + added + " song(s) added.",
                    "Import Done", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Parse a filename into a Song using the separator.
        /// Format: TITLE # ARTIST # CATEGORY_REF # VOCAL_HINT
        /// Example: PERFECT#ED SHEERAN#BARAT#LEFT.dat
        /// </summary>
        private Song ParseFilename(string filePath, string separator, int defaultCategoryId)
        {
            string nameNoExt = Path.GetFileNameWithoutExtension(filePath);
            string[] parts   = nameNoExt.Split(
                new[] { separator }, StringSplitOptions.None);

            string title  = parts.Length > 0 ? parts[0].Trim() : nameNoExt;
            string artist = parts.Length > 1 ? parts[1].Trim() : string.Empty;
            int    catId  = defaultCategoryId;

            if (parts.Length > 2)
            {
                var cat = _db.FindCategoryByReference(parts[2].Trim());
                if (cat != null) catId = cat.Id;
            }

            // parts[3] is a vocal hint (LEFT / RIGHT) — stored in VocalFilePath if needed
            // For now we just note it; actual separate-track support requires a paired file.

            return new Song
            {
                Title      = title,
                Artist     = artist,
                FilePath   = filePath,
                CategoryId = catId,
                SourceType = "local",
                DateAdded  = DateTime.Now
            };
        }

        // ── CATEGORIES ───────────────────────────────────────────────

        private void LoadCategories()
        {
            CategoryGrid.ItemsSource      = _db.GetAllCategories();
            CmbManualCategory.ItemsSource = _db.GetAllCategories();
        }

        private void BtnAddCategory_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new CategoryEditDialog() { Owner = this };
            if (dlg.ShowDialog() == true)
            { _db.AddCategory(dlg.CategoryName, dlg.CategoryKeywords); LoadCategories(); }
        }

        private void BtnEditCategory_Click(object sender, RoutedEventArgs e)
        {
            if (CategoryGrid.SelectedItem is Category cat)
            {
                var dlg = new CategoryEditDialog(cat) { Owner = this };
                if (dlg.ShowDialog() == true)
                { _db.UpdateCategory(cat.Id, dlg.CategoryName, dlg.CategoryKeywords); LoadCategories(); }
            }
        }

        private void BtnDeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            if (CategoryGrid.SelectedItem is Category cat)
            {
                var r = System.Windows.MessageBox.Show(
                    "Delete category \"" + cat.Name + "\"?\nSongs in this category will be uncategorized.",
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (r == MessageBoxResult.Yes) { _db.DeleteCategory(cat.Id); LoadCategories(); }
            }
        }

        // ── IMPORT SETTINGS ──────────────────────────────────────────

        private void LoadImportSettings()
        {
            var settings = _db.LoadSettings();
            TxtSeparator.Text = settings.FilenameSeparator;
        }

        private string LoadSeparator()
        {
            string s = TxtSeparator?.Text?.Trim();
            return string.IsNullOrEmpty(s) ? "#" : s;
        }

        private void BtnSaveSeparator_Click(object sender, RoutedEventArgs e)
        {
            _db.SaveSetting("FilenameSeparator", LoadSeparator());
            System.Windows.MessageBox.Show("Separator saved: " + LoadSeparator(),
                "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ── YOUTUBE ──────────────────────────────────────────────────

        private void LoadYoutubeSettings()
        {
            TxtYoutubeApiKey.Text = _yt.ApiKey;
        }

        private void BtnSaveApiKey_Click(object sender, RoutedEventArgs e)
        {
            _yt.ApiKey = TxtYoutubeApiKey.Text.Trim();
            _db.SaveSetting("YoutubeApiKey", _yt.ApiKey);
            System.Windows.MessageBox.Show("API key saved.",
                "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnAddManualYoutube_Click(object sender, RoutedEventArgs e)
        {
            string url    = TxtManualUrl.Text.Trim();
            string title  = TxtManualTitle.Text.Trim();
            string artist = TxtManualArtist.Text.Trim();
            int    catId  = CmbManualCategory.SelectedValue is int c ? c : 0;

            if (string.IsNullOrEmpty(url))
            {
                System.Windows.MessageBox.Show("Please enter a YouTube URL.",
                    "Missing URL", MessageBoxButton.OK, MessageBoxImage.Warning); return;
            }
            string videoId = YoutubeService.ExtractVideoId(url);
            if (string.IsNullOrEmpty(videoId))
            {
                System.Windows.MessageBox.Show("Could not parse a video ID from that URL.",
                    "Invalid URL", MessageBoxButton.OK, MessageBoxImage.Warning); return;
            }
            if (string.IsNullOrEmpty(title)) title = url;

            _db.AddSong(new Song
            {
                Title      = title, Artist = artist, SourceType = "youtube",
                YoutubeId  = videoId,
                YoutubeUrl = "https://www.youtube.com/watch?v=" + videoId,
                CategoryId = catId, DateAdded = DateTime.Now
            });
            TxtManualUrl.Clear(); TxtManualTitle.Clear(); TxtManualArtist.Clear();
            System.Windows.MessageBox.Show("YouTube song added to library.",
                "Added", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ── BOTTOM ───────────────────────────────────────────────────

        private void BtnClose_Click(object sender, RoutedEventArgs e)  => Close();

        private void BtnExitApp_Click(object sender, RoutedEventArgs e)
        {
            var r = System.Windows.MessageBox.Show("Exit Karaoke Player?", "Exit",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r == MessageBoxResult.Yes)
                System.Windows.Application.Current.Shutdown();
        }
    }
}
