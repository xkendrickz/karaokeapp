using System;

namespace KaraokeApp.Models
{
    public class Song
    {
        public int      Id            { get; set; }
        public string   Title         { get; set; }
        public string   Artist        { get; set; }
        public string   FilePath      { get; set; }
        public string   VocalFilePath { get; set; }
        public int      CategoryId    { get; set; }
        public string   CategoryName  { get; set; }
        public string   SourceType    { get; set; } = "local";
        public string   YoutubeId     { get; set; }
        public string   YoutubeUrl    { get; set; }
        public string   ThumbnailUrl  { get; set; }
        public DateTime DateAdded     { get; set; } = DateTime.Now;

        public bool IsYoutube => SourceType == "youtube";
        public bool IsLocal   => SourceType == "local";

        public override string ToString() => Title + " - " + Artist;
    }
}
