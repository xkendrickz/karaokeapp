namespace KaraokeApp.Models
{
    public class Category
    {
        public int    Id       { get; set; }
        public string Name     { get; set; }

        /// <summary>
        /// Comma-separated keywords that map to this category during filename parsing.
        /// e.g. "english,inggris,barat,western" all resolve to category "English".
        /// </summary>
        public string Keywords { get; set; } = string.Empty;

        public override string ToString() => Name ?? string.Empty;
    }
}
