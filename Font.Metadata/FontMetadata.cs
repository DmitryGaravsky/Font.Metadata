namespace Fonts.Metadata {
    /// <summary>
    /// Names the font metadata model. Reading and metadata properties will be added in a later implementation stage.
    /// </summary>
    public sealed class FontMetadata {
        internal FontMetadata() {
        }
        /// <summary>Names the metadata of one face within a font container.</summary>
        public sealed class Face {
            internal Face() {
            }
        }
        /// <summary>Names a decoded record from an OpenType name table.</summary>
        public sealed class NameRecord {
            internal NameRecord() {
            }
        }
        /// <summary>Names metadata from an Embedded OpenType container header.</summary>
        public sealed class EotHeader {
            internal EotHeader() {
            }
        }
    }
}
