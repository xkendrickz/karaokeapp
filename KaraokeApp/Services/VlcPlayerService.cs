using System;
using System.IO;
using System.IO.Compression;
using LibVLCSharp.Shared;
using LibVLCSharp.WPF;

namespace KaraokeApp.Services
{
    /// <summary>
    /// Wraps LibVLC for karaoke playback.
    /// Supported formats: MP4, MKV, MPG, DAT, M4A, MP3, CDG, ZIP (CDG+MP3), AVI, FLV, WMV, etc.
    ///
    /// VOCAL ON/OFF strategy:
    ///   1. If song has a separate VocalFilePath → switch audio tracks (track 0=main, track 1=vocal).
    ///      Vocal OFF = track 0 only; Vocal ON = track 1 (or mix – requires custom LibVLC filter).
    ///   2. If no separate track → use audio channel switching:
    ///      Vocal OFF = Right channel only (AudioChannel=2) → assumes Right=instrumental
    ///      Vocal ON  = Stereo (AudioChannel=1)
    ///
    /// PITCH:
    ///   LibVLC does not expose a direct pitch-shift API without custom filter graphs.
    ///   We simulate pitch via playback rate (changes both speed + pitch together).
    ///   For true pitch-shift without tempo change, install the VLC pitch plugin and
    ///   pass "--audio-filter=scaletempo_pitch" at LibVLC init.
    /// </summary>
    public class VlcPlayerService : IDisposable
    {
        private static VlcPlayerService _instance;
        public static VlcPlayerService Instance => _instance ?? (_instance = new VlcPlayerService());

        private LibVLC _libVlc;
        private MediaPlayer _mediaPlayer;

        private string _tempExtractDir;
        private bool _vocalOn = false;
        private bool _hasVocalTrack = false;
        private int _volume = 100;

        public MediaPlayer MediaPlayer => _mediaPlayer;

        public event EventHandler EndReached;
        public event EventHandler TimeChanged;
        public event EventHandler Playing;
        public event EventHandler Paused;
        public event EventHandler Stopped;

        private VlcPlayerService() { }

        public void Initialize(VideoView videoView)
        {
            _tempExtractDir = Path.Combine(Path.GetTempPath(), "KaraokeApp_vlc");
            Directory.CreateDirectory(_tempExtractDir);

            // "--audio-filter=scaletempo_pitch" enables independent pitch control
            // if VLC's scaletempo_pitch module is installed.
            _libVlc = new LibVLC(
                "--audio-filter=scaletempo",   // time-stretch for rate changes
                "--no-video-title-show",
                "--no-snapshot-preview",
                "--quiet");

            _mediaPlayer = new MediaPlayer(_libVlc);
            videoView.MediaPlayer = _mediaPlayer;

            // Wire events
            _mediaPlayer.EndReached   += (s, e) => EndReached?.Invoke(this, EventArgs.Empty);
            _mediaPlayer.TimeChanged  += (s, e) => TimeChanged?.Invoke(this, EventArgs.Empty);
            _mediaPlayer.Playing      += (s, e) => Playing?.Invoke(this, EventArgs.Empty);
            _mediaPlayer.Paused       += (s, e) => Paused?.Invoke(this, EventArgs.Empty);
            _mediaPlayer.Stopped      += (s, e) => Stopped?.Invoke(this, EventArgs.Empty);
        }

        // ─────────────────────────────────────────────────────────────────
        //  PLAYBACK
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Play a local file. Handles ZIP extraction for CDG+MP3 archives.</summary>
        public void PlayFile(string filePath, bool vocalOn, bool hasVocalTrack)
        {
            if (_mediaPlayer == null) return;

            _vocalOn = vocalOn;
            _hasVocalTrack = hasVocalTrack;

            string playPath = filePath;
            if (string.Equals(Path.GetExtension(filePath), ".zip",
                StringComparison.OrdinalIgnoreCase))
            {
                playPath = ExtractZip(filePath);
                if (playPath == null) return;
            }

            using (var media = new Media(_libVlc, playPath, FromType.FromPath))
            {
                _mediaPlayer.Play(media);
            }

            // Delay gives VLC time to load the audio tracks before we switch
            System.Threading.Tasks.Task.Delay(800).ContinueWith(_ =>
            {
                ApplyVocalSetting(_vocalOn, _hasVocalTrack);
                ApplyVolume(_volume);
            });
        }

        public void Stop()
        {
            _mediaPlayer?.Stop();
        }

        public void Pause()
        {
            if (_mediaPlayer?.CanPause == true)
                _mediaPlayer.Pause();
        }

        public void Resume()
        {
            _mediaPlayer?.Play();
        }

        public bool IsPlaying => _mediaPlayer?.IsPlaying ?? false;
        public bool IsPaused  => _mediaPlayer?.State == VLCState.Paused;

        public long Time  => _mediaPlayer?.Time ?? 0;
        public long Length => _mediaPlayer?.Length ?? 0;

        public void SeekTo(long timeMs)
        {
            if (_mediaPlayer != null && _mediaPlayer.IsSeekable)
                _mediaPlayer.Time = timeMs;
        }

        // ─────────────────────────────────────────────────────────────────
        //  VOLUME  (0–200; 100 = normal)
        // ─────────────────────────────────────────────────────────────────

        public int Volume
        {
            get => _volume;
            set
            {
                _volume = Math.Max(0, Math.Min(200, value));
                ApplyVolume(_volume);
            }
        }

        private void ApplyVolume(int vol)
        {
            if (_mediaPlayer != null)
                _mediaPlayer.Volume = vol;
        }

        // ─────────────────────────────────────────────────────────────────
        //  PITCH  (semitone offset; -12 to +12)
        //  LibVLC does not expose true pitch shift via its C API in v3.
        //  We map semitones to playback rate as an approximation.
        //  Each semitone ≈ rate * 2^(1/12).
        //  For true pitch-shift install scaletempo_pitch plugin.
        // ─────────────────────────────────────────────────────────────────

        private int _pitchSemitones = 0;

        public int PitchSemitones
        {
            get => _pitchSemitones;
            set
            {
                _pitchSemitones = Math.Max(-12, Math.Min(12, value));
                ApplyPitch(_pitchSemitones);
            }
        }

        private void ApplyPitch(int semitones)
        {
            if (_mediaPlayer == null) return;
            // Rate-based approximation: rate = 2^(semitones/12)
            float rate = (float)Math.Pow(2.0, semitones / 12.0);
            _mediaPlayer.SetRate(rate);
        }

        // ─────────────────────────────────────────────────────────────────
        //  VOCAL ON/OFF
        // ─────────────────────────────────────────────────────────────────

        public void SetVocal(bool on)
        {
            _vocalOn = on;
            if (_mediaPlayer != null &&
                (_mediaPlayer.IsPlaying || _mediaPlayer.State == VLCState.Paused))
            {
                ApplyVocalSetting(on, _hasVocalTrack);
            }
        }

        private void ApplyVocalSetting(bool vocalOn, bool hasVocalTrack)
        {
            if (_mediaPlayer == null) return;
            try
            {
                if (hasVocalTrack)
                {
                    // Song has a separate vocal file — switch audio tracks
                    // Track index 1 = first real track (disable = index 0 in LibVLC)
                    var tracks = _mediaPlayer.AudioTrackDescription;
                    if (tracks != null && tracks.Length > 1)
                    {
                        // tracks[0] is usually "Disable", tracks[1] = instrumental, tracks[2] = vocal
                        int trackId = vocalOn
                            ? (tracks.Length > 2 ? tracks[2].Id : tracks[1].Id)
                            : tracks[1].Id;
                        _mediaPlayer.SetAudioTrack(trackId);
                    }
                }
                else
                {
                    // No separate track — use channel switching via P/Invoke
                    // Channel: 1=Stereo, 2=RStereo, 3=Left, 4=Right
                    // Vocal OFF (default) = Right channel only = instrumental on most karaoke files
                    // Vocal ON            = Stereo = both channels (vocal + instrumental)
                    int channel = vocalOn ? 1 : 3;
                    try
                    {
                        NativeMethods.libvlc_audio_set_channel(
                            _mediaPlayer.NativeReference, channel);
                    }
                    catch
                    {
                        // P/Invoke failed — media may not be loaded yet, will retry on next call
                    }
                }
            }
            catch { /* media not ready, ignore */ }
        }

        /// <summary>
        /// Sets the VLC audio channel mode via the media player's audio output.
        /// channel: 1=Stereo, 2=RStereo, 3=Left, 4=Right, 5=Mono
        /// LibVLCSharp 3.x does not expose AudioChannel directly on MediaPlayer,
        /// so we use the VLC native interop via audio output device approach.
        /// </summary>
        private void SetVlcAudioChannel(int channel)
        {
            // LibVLCSharp 3.8 exposes this through the underlying native handle.
            // The safest approach in .NET 4.8 / LibVLCSharp 3.x is to toggle
            // the track mix by enabling/disabling specific channels via:
            //   MediaPlayer.AudioOutputDevice + audio-channels option.
            // Since direct AudioChannel property was removed, we use the internal
            // VLC audio filter option approach available in the VLC option string.
            // For broad compatibility we use the libvlc_audio_set_channel P/Invoke:
            try
            {
                NativeMethods.libvlc_audio_set_channel(_mediaPlayer.NativeReference, channel);
            }
            catch
            {
                // Fallback: if native call fails (e.g. no audio output yet), ignore silently.
                // The channel will be applied when PlayFile is called with this setting.
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  ZIP EXTRACTION (for CDG+MP3 or MP3+G zip archives)
        // ─────────────────────────────────────────────────────────────────

        private string ExtractZip(string zipPath)
        {
            try
            {
                string destDir = Path.Combine(_tempExtractDir,
                    Path.GetFileNameWithoutExtension(zipPath));
                Directory.CreateDirectory(destDir);

                // .NET 4.8: ZipFile.ExtractToDirectory does not support overwrite param.
                // Manually extract, overwriting existing files.
                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name)) continue; // directory entry
                        string destFile = Path.Combine(destDir, entry.Name);
                        // Overwrite if exists
                        entry.ExtractToFile(destFile, overwrite: true);
                    }
                }

                // Prefer .cdg file (karaoke); LibVLC will auto-pair it with .mp3
                foreach (var f in Directory.GetFiles(destDir))
                {
                    string ext = Path.GetExtension(f).ToLower();
                    if (ext == ".cdg") return f;
                }
                // Fall back to any video/audio file
                string[] preferred = { ".mp4", ".mkv", ".avi", ".mpg", ".mp3", ".m4a" };
                foreach (var p in preferred)
                    foreach (var f in Directory.GetFiles(destDir))
                        if (Path.GetExtension(f).ToLower() == p) return f;

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ZIP extract failed: " + ex.Message);
                return null;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  CLEANUP
        // ─────────────────────────────────────────────────────────────────

        public void Dispose()
        {
            _mediaPlayer?.Stop();
            _mediaPlayer?.Dispose();
            _libVlc?.Dispose();
            try
            {
                if (Directory.Exists(_tempExtractDir))
                    Directory.Delete(_tempExtractDir, true);
            }
            catch { }
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  NATIVE INTEROP  (libvlc functions not yet wrapped by LibVLCSharp 3.x)
    // ─────────────────────────────────────────────────────────────────

    internal static class NativeMethods
    {
        // Sets the audio channel (mix mode) on a media player.
        // channel: 0=Error, 1=Stereo, 2=RStereo, 3=Left, 4=Right, 5=Dolby
        // libvlc_audio_set_channel is available in libvlc 1.1.0+
        [System.Runtime.InteropServices.DllImport("libvlc", CallingConvention =
            System.Runtime.InteropServices.CallingConvention.Cdecl)]
        internal static extern int libvlc_audio_set_channel(
            System.IntPtr p_mi, int channel);
    }
}
