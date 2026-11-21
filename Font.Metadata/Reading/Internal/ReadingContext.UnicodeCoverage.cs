namespace Fonts.Metadata.Internal {
    internal sealed partial class ReadingContext {
        int cmapWork;
        int unicodeRanges;
        internal bool TryConsumeCmapWork() {
            CheckCancellation();
            if(cmapWork == Limits.MaxTotalCmapWork)
                return false;
            cmapWork++;
            return true;
        }
        internal bool TryRetainUnicodeRange() {
            if(unicodeRanges == Limits.MaxTotalUnicodeRanges)
                return false;
            unicodeRanges++;
            return true;
        }
    }
}
