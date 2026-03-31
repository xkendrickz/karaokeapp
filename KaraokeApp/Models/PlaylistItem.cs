using System.ComponentModel;

namespace KaraokeApp.Models
{
    public class PlaylistItem : INotifyPropertyChanged
    {
        private bool _isPlaying;

        public Song Song { get; set; }

        public string Title  => Song?.Title  ?? string.Empty;
        public string Artist => Song?.Artist ?? string.Empty;

        /// <summary>True while this item is the currently playing track.
        /// Bound to the yellow highlight row style in the playlist.</summary>
        public bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                if (_isPlaying == value) return;
                _isPlaying = value;
                OnPropertyChanged("IsPlaying");
            }
        }

        public PlaylistItem(Song song)
        {
            Song = song;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
