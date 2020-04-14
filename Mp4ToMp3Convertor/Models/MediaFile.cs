namespace Mp4ToMp3Convertor.Models
{
    public class MediaFile
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string Action { get; set; }
        public int Progress { get; set; }

        // Equals override, used in listview contains check
        public override bool Equals(object obj)
        {
            if (obj != null && obj is MediaFile mediaFile)
            {
                return FilePath.Equals(mediaFile.FilePath);
            }
            else
            {
                return false;
            }
        }

        // Return hashcode while using equals
        public override int GetHashCode()
        {
            return FilePath.GetHashCode();
        }
    }
}
