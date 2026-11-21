namespace Fonts.Metadata.Internal {
    internal sealed partial class ReadingContext {
        int variationAxesRead;
        internal bool TryConsumeVariationAxis() {
            if(variationAxesRead >= Limits.MaxTotalVariationAxes)
                return false;
            variationAxesRead++;
            return true;
        }
    }
}
