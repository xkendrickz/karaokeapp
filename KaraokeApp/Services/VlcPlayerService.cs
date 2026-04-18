using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using LibVLCSharp.Shared;
using LibVLCSharp.WPF;

namespace KaraokeApp.Services
{
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
        private int _pitchSemitones = 0;

        public MediaPlayer MediaPlayer => _mediaPlayer;

        public event EventHandler EndReached;
        public event EventHandler TimeChanged;
        public event EventHandler Playing;
        public event EventHandler Paused;
        public event EventHandler Stopped;

        private VlcPlayerService() { }

        // ─────────────────────────────────────────────
        // INIT
        // ─────────────────────────────────────────────
        public void Initialize(VideoView videoView)
        {
            _tempExtractDir = Path.Combine(Path.GetTempPath(), "KaraokeApp_vlc");
            Directory.CreateDirectory(_tempExtractDir);

            _libVlc = new LibVLC(
                "--audio-filter=scaletempo",
                "--no-video-title-show",
                "--quiet"
            );

            _mediaPlayer = new MediaPlayer(_libVlc);
            videoView.MediaPlayer = _mediaPlayer;

            _mediaPlayer.EndReached += (s, e) => EndReached?.Invoke(this, EventArgs.Empty);
            _mediaPlayer.TimeChanged += (s, e) => TimeChanged?.Invoke(this, EventArgs.Empty);
            _mediaPlayer.Playing += (s, e) => Playing?.Invoke(this, EventArgs.Empty);
            _mediaPlayer.Paused += (s, e) => Paused?.Invoke(this, EventArgs.Empty);
            _mediaPlayer.Stopped += (s, e) => Stopped?.Invoke(this, EventArgs.Empty);
        }

        // ─────────────────────────────────────────────
        // PLAYBACK
        // ─────────────────────────────────────────────
        public void PlayFile(string filePath, bool vocalOn, bool hasVocalTrack)
        {
            if (_mediaPlayer == null) return;

            _vocalOn = vocalOn;
            _hasVocalTrack = hasVocalTrack;

            string playPath = filePath;

            if (Path.GetExtension(filePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                playPath = ExtractZip(filePath);
                if (playPath == null) return;
            }

            using (var media = new Media(_libVlc, playPath, FromType.FromPath))
            {
                _mediaPlayer.Play(media);
            }

            Task.Delay(800).ContinueWith(_ =>
            {
                ApplyVocalSetting(_vocalOn, _hasVocalTrack);
                ApplyVolume(_volume);
            });
        }

        public void Stop() => _mediaPlayer?.Stop();

        public void Pause()
        {
            if (_mediaPlayer?.CanPause == true)
                _mediaPlayer.Pause();
        }

        public void Resume() => _mediaPlayer?.Play();

        public bool IsPlaying => _mediaPlayer?.IsPlaying ?? false;
        public bool IsPaused => _mediaPlayer?.State == VLCState.Paused;

        public long Time => _mediaPlayer?.Time ?? 0;
        public long Length => _mediaPlayer?.Length ?? 0;

        public void SeekTo(long timeMs)
        {
            if (_mediaPlayer?.IsSeekable == true)
                _mediaPlayer.Time = timeMs;
        }

        // ─────────────────────────────────────────────
        // VOLUME
        // ─────────────────────────────────────────────
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

        // ─────────────────────────────────────────────
        // PITCH
        // ─────────────────────────────────────────────
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

            float rate = (float)Math.Pow(2.0, semitones / 12.0);
            _mediaPlayer.SetRate(rate);
        }

        // ─────────────────────────────────────────────
        // VOCAL
        // ─────────────────────────────────────────────
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
                    var tracks = _mediaPlayer.AudioTrackDescription;

                    if (tracks != null && tracks.Length > 1)
                    {
                        int trackId = vocalOn
                            ? (tracks.Length > 2 ? tracks[2].Id : tracks[1].Id)
                            : tracks[1].Id;

                        _mediaPlayer.SetAudioTrack(trackId);
                    }
                }
                else
                {
                    int channel = vocalOn ? 1 : 3;
                    NativeMethods.libvlc_audio_set_channel(_mediaPlayer.NativeReference, channel);
                }
            }
            catch { }
        }

        // ─────────────────────────────────────────────
        // ZIP
        // ─────────────────────────────────────────────
        private string ExtractZip(string zipPath)
        {
            try
            {
                string destDir = Path.Combine(_tempExtractDir, Path.GetFileNameWithoutExtension(zipPath));
                Directory.CreateDirectory(destDir);

                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name)) continue;

                        string destFile = Path.Combine(destDir, entry.Name);
                        entry.ExtractToFile(destFile, true);
                    }
                }

                foreach (var f in Directory.GetFiles(destDir))
                    if (Path.GetExtension(f).ToLower() == ".cdg")
                        return f;

                return Directory.GetFiles(destDir)[0];
            }
            catch
            {
                return null;
            }
        }

        // ─────────────────────────────────────────────
        // CLEANUP
        // ─────────────────────────────────────────────
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

    // ─────────────────────────────────────────────
    // NATIVE
    // ─────────────────────────────────────────────
    internal static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("libvlc", CallingConvention =
            System.Runtime.InteropServices.CallingConvention.Cdecl)]
        internal static extern int libvlc_audio_set_channel(IntPtr p_mi, int channel);
    }

    // ─────────────────────────────────────────────
    // YOUTUBE HELPER (DIPISAH BIAR CLEAN)
    // ─────────────────────────────────────────────
    public static class YoutubeHelper
    {
        public static async Task<string> GetYoutubeStreamUrlAsync(string youtubeUrl)
        {
            string ytdlpPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "yt-dlp.exe");

            if (!File.Exists(ytdlpPath))
                throw new FileNotFoundException(
                    "yt-dlp.exe not found in app folder:\n" + ytdlpPath);

            // Use a unique base name WITHOUT extension — yt-dlp will add its own
            string tempDir = Path.GetTempPath();
            string baseName = "KaraokeApp_yt_" + Guid.NewGuid().ToString("N");
            string tempBase = Path.Combine(tempDir, baseName);

            return await Task.Run(() =>
            {
                string appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
                string ffmpegExe = Path.Combine(appDir, "ffmpeg.exe");
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = ytdlpPath,
                    // %(ext)s lets yt-dlp choose the right extension
                    Arguments = "-f \"best[height<=720][ext=mp4]/best[height<=720]/best\"" +
                                " --no-playlist" +
                                " --no-part" +
                                (File.Exists(ffmpegExe) ? " --ffmpeg-location \"" + ffmpegExe + "\"" : "") +
                                " --output \"" + tempBase + ".%(ext)s\"" +
                                " \"" + youtubeUrl + "\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = System.Diagnostics.Process.Start(psi))
                {
                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                        throw new Exception(
                            "yt-dlp failed (code " + process.ExitCode + "):\n" + stderr);

                    // Find whichever file yt-dlp actually created
                    string[] candidates = Directory.GetFiles(tempDir, baseName + ".*");
                    if (candidates.Length == 0)
                        throw new Exception(
                            "yt-dlp ran but no output file found.\n\nyt-dlp output:\n" + stdout + "\n" + stderr);

                    // If multiple (e.g. video+audio before merge) pick the largest
                    string result = candidates[0];
                    long maxSize = 0;
                    foreach (var f in candidates)
                    {
                        long size = new FileInfo(f).Length;
                        if (size > maxSize) { maxSize = size; result = f; }
                    }

                    return result;
                }
            });
        }

        public static void CleanupTempFiles()
        {
            try
            {
                foreach (var f in Directory.GetFiles(
                    Path.GetTempPath(), "KaraokeApp_yt_*"))
                {
                    try { File.Delete(f); } catch { }
                }
            }
            catch { }
        }
    }
}