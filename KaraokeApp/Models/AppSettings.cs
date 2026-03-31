namespace KaraokeApp.Models
{
    public class AppSettings
    {
        public string YoutubeApiKey     { get; set; } = string.Empty;
        public int    Volume            { get; set; } = 100;
        public int    PitchSemitones    { get; set; } = 0;
        public bool   VocalOn           { get; set; } = false;
        public bool   Repeat            { get; set; } = false;
        public int    LastCategoryId    { get; set; } = 0;
        public int    LastTab           { get; set; } = 0;

        /// <summary>
        /// Separator character(s) used when auto-parsing song filenames.
        /// Default is "#". Example: PERFECT#ED SHEERAN#BARAT#LEFT.dat
        /// Parts: [0]=Title, [1]=Artist, [2]=Category reference, [3]=Vocal hint (optional)
        /// </summary>
        public string FilenameSeparator { get; set; } = "#";
    }
}
