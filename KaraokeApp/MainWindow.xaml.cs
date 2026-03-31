using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using KaraokeApp.Data;
using KaraokeApp.Models;
using KaraokeApp.Services;
using KaraokeApp.Windows;

namespace KaraokeApp
{
    public partial class MainWindow : Window
    {
        private readonly DatabaseService  _db  = DatabaseService.Instance;
        private readonly VlcPlayerService _vlc = VlcPlayerService.Instance;
        private readonly YoutubeService   _yt  = new YoutubeService();

        private AppSettings _settings;
        private readonly List<ToggleButton> _categoryButtons = new List<ToggleButton>();
        private int  _activeCategoryId = 0;
        private bool _isYoutubeTab     = false;
        private bool _searchByTitle    = true;

        private readonly ObservableCollection<PlaylistItem> _playlist =
            new ObservableCollection<PlaylistItem>();
        private bool _isAnySongLoaded = false;
        private Song _currentSong     = null;

        // Progress bar timer (updates every 500ms)
        private DispatcherTimer _progressTimer;

        // ════════════════════════════════════════════════════════════
        //  INIT
        // ════════════════════════════════════════════════════════════

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await YoutubeWebView.EnsureCoreWebView2Async();
                YoutubeWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                YoutubeWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            }
            catch { }

            _vlc.Initialize(VideoView);
            _vlc.EndReached += Vlc_EndReached;
            _vlc.Playing    += (s, ev) => Dispatcher.Invoke(UpdateTransportButtons);
            _vlc.Paused     += (s, ev) => Dispatcher.Invoke(UpdateTransportButtons);
            _vlc.Stopped    += (s, ev) => Dispatcher.Invoke(UpdateTransportButtons);

            // Progress bar update timer
            _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _progressTimer.Tick += ProgressTimer_Tick;
            _progressTimer.Start();

            _settings = _db.LoadSettings();
            ApplySettings();
            BuildKeyboard();
            LoadCategoryButtons();
            PlaylistView.ItemsSource = _playlist;
            RefreshSongList();
        }

        private void ProgressTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                long len  = _vlc.Length;
                long time = _vlc.Time;
                if (len > 0)
                {
                    SongProgress.Maximum = len;
                    SongProgress.Value   = time;
                    var cur = TimeSpan.FromMilliseconds(time);
                    var tot = TimeSpan.FromMilliseconds(len);
                    VideoTimeDisplay.Text = string.Format("{0}:{1:D2} / {2}:{3:D2}",
                        (int)cur.TotalMinutes, cur.Seconds,
                        (int)tot.TotalMinutes, tot.Seconds);
                }
                else
                {
                    SongProgress.Value    = 0;
                    VideoTimeDisplay.Text = "";
                }
            }
            catch { }
        }

        // ════════════════════════════════════════════════════════════
        //  SETTINGS
        // ════════════════════════════════════════════════════════════

        private void ApplySettings()
        {
            _yt.ApiKey          = _settings.YoutubeApiKey;
            VolumeSlider.Value  = _settings.Volume;
            VolumeLabel.Text    = _settings.Volume.ToString();
            _vlc.Volume         = _settings.Volume;
            PitchSlider.Value   = _settings.PitchSemitones;
            PitchLabel.Text     = FormatPitch(_settings.PitchSemitones);
            _vlc.PitchSemitones = _settings.PitchSemitones;
            BtnVocal.IsChecked  = _settings.VocalOn;
            BtnRepeat.IsChecked = _settings.Repeat;
            _activeCategoryId   = _settings.LastCategoryId;
        }

        private void SaveCurrentSettings()
        {
            _settings.Volume         = (int)VolumeSlider.Value;
            _settings.PitchSemitones = (int)PitchSlider.Value;
            _settings.VocalOn        = BtnVocal.IsChecked == true;
            _settings.Repeat         = BtnRepeat.IsChecked == true;
            _settings.LastCategoryId = _activeCategoryId;
            _settings.LastTab        = _isYoutubeTab ? 1 : 0;
            _db.SaveSettings(_settings);
        }

        // ════════════════════════════════════════════════════════════
        //  CATEGORY BAR
        // ════════════════════════════════════════════════════════════

        private void LoadCategoryButtons()
        {
            CategoryButtonPanel.Children.Clear();
            _categoryButtons.Clear();
            AddCategoryButton(new Category { Id = 0, Name = "ALL" });
            foreach (var cat in _db.GetAllCategories())
                AddCategoryButton(cat);
            SelectCategoryButton(_activeCategoryId);
        }

        private void AddCategoryButton(Category cat)
        {
            var btn = new ToggleButton
            {
                Content = cat.Name.ToUpper(),
                Style   = (Style)FindResource("CatBtn"),
                Tag     = cat.Id
            };
            btn.Click += CategoryButton_Click;
            _categoryButtons.Add(btn);
            CategoryButtonPanel.Children.Add(btn);
        }

        private void SelectCategoryButton(int id)
        {
            foreach (var btn in _categoryButtons)
                btn.IsChecked = (btn.Tag is int t && t == id);
        }

        private void CategoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton btn && btn.Tag is int id)
            {
                _activeCategoryId = id;
                SelectCategoryButton(id);
                if (_isYoutubeTab) LoadYoutubeLibrary();
                else               RefreshSongList();
            }
        }

        private void BtnCategoryScrollLeft_Click(object sender, RoutedEventArgs e)
            => CategoryScrollViewer.ScrollToHorizontalOffset(
                CategoryScrollViewer.HorizontalOffset - 150);

        private void BtnCategoryScrollRight_Click(object sender, RoutedEventArgs e)
            => CategoryScrollViewer.ScrollToHorizontalOffset(
                CategoryScrollViewer.HorizontalOffset + 150);

        // ════════════════════════════════════════════════════════════
        //  TABS
        // ════════════════════════════════════════════════════════════

        private void BtnTabMyComputer_Click(object sender, RoutedEventArgs e)
        {
            _isYoutubeTab = false;
            BtnTabMyComputer.IsChecked = true;
            BtnTabYoutube.IsChecked    = false;
            SongListView.Visibility     = Visibility.Visible;
            YoutubePanelGrid.Visibility = Visibility.Collapsed;
            RefreshSongList();
        }

        private void BtnTabYoutube_Click(object sender, RoutedEventArgs e)
        {
            _isYoutubeTab = true;
            BtnTabMyComputer.IsChecked = false;
            BtnTabYoutube.IsChecked    = true;
            SongListView.Visibility     = Visibility.Collapsed;
            YoutubePanelGrid.Visibility = Visibility.Visible;
            LoadYoutubeLibrary();
        }

        // ════════════════════════════════════════════════════════════
        //  SONG LIST
        // ════════════════════════════════════════════════════════════

        private void RefreshSongList()
        {
            SongListView.ItemsSource = _db.GetAllSongs(
                _activeCategoryId, SearchBox.Text.Trim(), _searchByTitle, "local");
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isYoutubeTab) RefreshSongList();
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e) { }

        private void SearchMode_Click(object sender, RoutedEventArgs e)
        {
            _searchByTitle = BtnSearchByTitle.IsChecked == true;
            BtnSearchByTitle.IsChecked  = _searchByTitle;
            BtnSearchByArtist.IsChecked = !_searchByTitle;
            if (!_isYoutubeTab) RefreshSongList();
        }

        // ════════════════════════════════════════════════════════════
        //  ON-SCREEN KEYBOARD
        // ════════════════════════════════════════════════════════════

        private void BuildKeyboard()
        {
            string[][] rows =
            {
                new[] { "Q","W","E","R","T","Y","U","I","O","P","-","'","<" },
                new[] { "A","S","D","F","G","H","J","K","L","1","2","3","4","5" },
                new[] { "Z","X","C","V","B","N","M","SPACE","6","7","8","9","0" }
            };
            WrapPanel[] panels = { KbRow1, KbRow2, KbRow3 };
            for (int r = 0; r < rows.Length; r++)
                foreach (string key in rows[r])
                {
                    Style s = key == "SPACE" ? (Style)FindResource("KbKeySpace")
                            : key == "<"     ? (Style)FindResource("KbKeyWide")
                            :                  (Style)FindResource("KbKey");
                    var btn = new Button { Content = key, Style = s, Tag = key };
                    btn.Click += KeyboardKey_Click;
                    panels[r].Children.Add(btn);
                }
        }

        private void KeyboardKey_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn)) return;
            string key = btn.Tag?.ToString() ?? "";
            string cur = SearchBox.Text;
            int caret  = SearchBox.CaretIndex;
            switch (key)
            {
                case "<":
                    if (caret > 0) { SearchBox.Text = cur.Remove(caret-1,1); SearchBox.CaretIndex = caret-1; }
                    break;
                case "SPACE":
                    SearchBox.Text = cur.Insert(caret," "); SearchBox.CaretIndex = caret+1;
                    break;
                default:
                    string ins = key.ToLower();
                    SearchBox.Text = cur.Insert(caret,ins); SearchBox.CaretIndex = caret+ins.Length;
                    break;
            }
        }

        // ════════════════════════════════════════════════════════════
        //  YOUTUBE
        // ════════════════════════════════════════════════════════════

        private void LoadYoutubeLibrary()
        {
            YoutubeListView.ItemsSource = _db.GetYoutubeSongs(
                _activeCategoryId, SearchBox.Text.Trim(), _searchByTitle);
        }

        private async void BtnYoutubeSearch_Click(object sender, RoutedEventArgs e)
        {
            string query = YoutubeSearchBox.Text.Trim();
            if (string.IsNullOrEmpty(query)) return;
            if (!_yt.HasApiKey)
            {
                MessageBox.Show("No YouTube API key. Go to SETTING → YouTube tab.",
                    "Missing API Key", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            YoutubeLoadingOverlay.Visibility = Visibility.Visible;
            YoutubeListView.ItemsSource = null;
            try   { YoutubeListView.ItemsSource = await _yt.SearchAsync(query); }
            catch (Exception ex)
            {
                MessageBox.Show("YouTube search failed:\n" + ex.Message,
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { YoutubeLoadingOverlay.Visibility = Visibility.Collapsed; }
        }

        private void BtnYoutubeLibrary_Click(object sender, RoutedEventArgs e)
        { YoutubeSearchBox.Clear(); LoadYoutubeLibrary(); }

        private void YoutubeSearchBox_KeyDown(object sender, KeyEventArgs e)
        { if (e.Key == Key.Enter) BtnYoutubeSearch_Click(sender, e); }

        private void YoutubeList_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        { if (YoutubeListView.SelectedItem is Song s) EnqueueAndMaybePlay(s); }

        private void YoutubeList_KeyDown(object sender, KeyEventArgs e)
        { if ((e.Key == Key.Enter || e.Key == Key.Insert) && YoutubeListView.SelectedItem is Song s) EnqueueAndMaybePlay(s); }

        // ════════════════════════════════════════════════════════════
        //  PLAYLIST
        // ════════════════════════════════════════════════════════════

        private void EnqueueAndMaybePlay(Song song)
        {
            _playlist.Add(new PlaylistItem(song));
            if (!_isAnySongLoaded) DequeueAndPlay();
        }

        private void DequeueAndPlay()
        {
            if (_playlist.Count == 0) { ShowIdle(); return; }
            var item     = _playlist[0];
            _currentSong = item.Song;
            _playlist.RemoveAt(0);
            _isAnySongLoaded = true;

            if (_currentSong.IsYoutube) PlayYoutubeVideo(_currentSong);
            else                         PlayLocalFile(_currentSong);

            ShowNowPlaying(_currentSong.Title, _currentSong.Artist);
            UpdateTransportButtons();
        }

        private void BtnPlaylistInsert_Click(object sender, RoutedEventArgs e)
        {
            Song sel = _isYoutubeTab
                ? YoutubeListView.SelectedItem as Song
                : SongListView.SelectedItem as Song;
            if (sel != null) EnqueueAndMaybePlay(sel);
        }

        private void BtnPlaylistDelete_Click(object sender, RoutedEventArgs e)
        {
            int idx = PlaylistView.SelectedIndex;
            if (idx >= 0) _playlist.RemoveAt(idx);
        }

        private void BtnPlaylistUp_Click(object sender, RoutedEventArgs e)
        {
            int idx = PlaylistView.SelectedIndex;
            if (idx > 0) { _playlist.Move(idx, idx-1); PlaylistView.SelectedIndex = idx-1; }
        }

        private void BtnPlaylistDown_Click(object sender, RoutedEventArgs e)
        {
            int idx = PlaylistView.SelectedIndex;
            if (idx >= 0 && idx < _playlist.Count-1) { _playlist.Move(idx, idx+1); PlaylistView.SelectedIndex = idx+1; }
        }

        private void Playlist_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            int idx = PlaylistView.SelectedIndex;
            if (idx < 0) return;
            if (idx > 0) _playlist.Move(idx, 0);
            _vlc.Stop(); _isAnySongLoaded = false;
            DequeueAndPlay();
        }

        private void Playlist_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)  Playlist_DoubleClick(sender, null);
            if (e.Key == Key.Delete) BtnPlaylistDelete_Click(sender, e);
        }

        private void SongList_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        { if (SongListView.SelectedItem is Song s) EnqueueAndMaybePlay(s); }

        private void SongList_KeyDown(object sender, KeyEventArgs e)
        { if ((e.Key == Key.Enter || e.Key == Key.Insert) && SongListView.SelectedItem is Song s) EnqueueAndMaybePlay(s); }

        // ════════════════════════════════════════════════════════════
        //  PLAYBACK
        // ════════════════════════════════════════════════════════════

        private void PlayLocalFile(Song song)
        {
            VideoView.Visibility        = Visibility.Visible;
            YoutubeWebView.Visibility   = Visibility.Collapsed;
            VideoPlaceholder.Visibility = Visibility.Collapsed;
            _vlc.PlayFile(song.FilePath, BtnVocal.IsChecked == true,
                          !string.IsNullOrEmpty(song.VocalFilePath));
            _vlc.Volume         = (int)VolumeSlider.Value;
            _vlc.PitchSemitones = (int)PitchSlider.Value;
        }

        private void PlayYoutubeVideo(Song song)
        {
            _vlc.Stop();
            VideoView.Visibility        = Visibility.Collapsed;
            YoutubeWebView.Visibility   = Visibility.Visible;
            VideoPlaceholder.Visibility = Visibility.Collapsed;
            string url = YoutubeService.GetEmbedUrl(song.YoutubeId);
            if (!string.IsNullOrEmpty(url)) YoutubeWebView.Source = new Uri(url);
        }

        private void ShowNowPlaying(string title, string artist)
        {
            NowPlayingText.Text    = title;
            NowPlayingArtist.Text  = string.IsNullOrEmpty(artist) ? "" : artist;
            NowPlayingText.Foreground    = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xF8, 0xFA, 0xFC));
            NowPlayingLabel.Visibility = Visibility.Visible;
            PlayingDot.Visibility      = Visibility.Visible;

            // Pulse the banner border cyan for 1 second on song change
            var anim = new System.Windows.Media.Animation.ColorAnimation(
                System.Windows.Media.Color.FromRgb(0x22, 0xD3, 0xEE),
                System.Windows.Media.Color.FromRgb(0x1E, 0x3A, 0x5F),
                TimeSpan.FromSeconds(1.2));
            var brush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x22, 0xD3, 0xEE));
            NowPlayingBorder.BorderBrush = brush;
            brush.BeginAnimation(System.Windows.Media.SolidColorBrush.ColorProperty, anim);
        }

        private void ShowIdle()
        {
            _isAnySongLoaded            = false;
            _currentSong                = null;
            NowPlayingText.Text         = "No song playing";
            NowPlayingText.Foreground   = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x94, 0xA3, 0xB8));
            NowPlayingArtist.Text       = "";
            NowPlayingLabel.Visibility  = Visibility.Collapsed;
            PlayingDot.Visibility       = Visibility.Collapsed;
            NowPlayingBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x1E, 0x3A, 0x5F));
            VideoPlaceholder.Visibility = Visibility.Visible;
            SongProgress.Value          = 0;
            VideoTimeDisplay.Text       = "";
            UpdateTransportButtons();
        }

        private void Vlc_EndReached(object sender, EventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        if (BtnRepeat.IsChecked == true && _currentSong != null)
                        {
                            if (_currentSong.IsYoutube) PlayYoutubeVideo(_currentSong);
                            else                         PlayLocalFile(_currentSong);
                            return;
                        }
                        _isAnySongLoaded = false;
                        if (_playlist.Count > 0) DequeueAndPlay();
                        else                      ShowIdle();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("EndReached error: " + ex.Message);
                        ShowIdle();
                    }
                });
            }
            catch { }
        }

        // ════════════════════════════════════════════════════════════
        //  TRANSPORT CONTROLS
        // ════════════════════════════════════════════════════════════

        private void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            if (_vlc.IsPaused)
                _vlc.Resume();
            else if (!_isAnySongLoaded && _playlist.Count > 0)
                DequeueAndPlay();
        }

        private void BtnPause_Click(object sender, RoutedEventArgs e)
        { if (_vlc.IsPlaying) _vlc.Pause(); }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            _vlc.Stop(); _isAnySongLoaded = false;
            if (_playlist.Count > 0) DequeueAndPlay();
            else                      ShowIdle();
        }

        private void BtnRepeat_Changed(object sender, RoutedEventArgs e)
            => _settings.Repeat = BtnRepeat.IsChecked == true;

        private void BtnVocal_Changed(object sender, RoutedEventArgs e)
        {
            bool on = BtnVocal.IsChecked == true;
            _vlc.SetVocal(on);
            BtnVocal.Content = on ? "🎤  VOCAL ON" : "🎤  VOCAL";
        }

        private void UpdateTransportButtons()
        {
            bool playing = _vlc.IsPlaying;
            bool paused  = _vlc.IsPaused;
            BtnPlay.IsEnabled  = paused || (!playing && !_isAnySongLoaded);
            BtnPause.IsEnabled = playing;
        }

        // ════════════════════════════════════════════════════════════
        //  VOLUME / PITCH
        // ════════════════════════════════════════════════════════════

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int vol = (int)e.NewValue;
            if (VolumeLabel != null) VolumeLabel.Text = vol.ToString();
            _vlc.Volume = vol;
        }

        private void BtnVolumeDown_Click(object sender, RoutedEventArgs e)
            => VolumeSlider.Value = Math.Max(0, VolumeSlider.Value - 5);
        private void BtnVolumeUp_Click(object sender, RoutedEventArgs e)
            => VolumeSlider.Value = Math.Min(200, VolumeSlider.Value + 5);

        private void PitchSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int sem = (int)e.NewValue;
            if (PitchLabel != null) PitchLabel.Text = FormatPitch(sem);
            _vlc.PitchSemitones = sem;
        }

        private void BtnPitchDown_Click(object sender, RoutedEventArgs e)
            => PitchSlider.Value = Math.Max(-12, PitchSlider.Value - 1);
        private void BtnPitchUp_Click(object sender, RoutedEventArgs e)
            => PitchSlider.Value = Math.Min(12, PitchSlider.Value + 1);

        private static string FormatPitch(int s) => s > 0 ? "+" + s : s.ToString();

        // ════════════════════════════════════════════════════════════
        //  SETTINGS
        // ════════════════════════════════════════════════════════════

        private void BtnSetting_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow(_yt) { Owner = this };
            win.ShowDialog();
            _settings  = _db.LoadSettings();
            _yt.ApiKey = _settings.YoutubeApiKey;
            LoadCategoryButtons();
            RefreshSongList();
            if (_isYoutubeTab) LoadYoutubeLibrary();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _progressTimer?.Stop();
            SaveCurrentSettings();
            try { _vlc.Stop(); }  catch { }
            try { _vlc.Dispose(); } catch { }
        }
    }
}
