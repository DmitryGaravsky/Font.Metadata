# Current API

Package: Font.Metadata. Namespace: Fonts.Metadata.

This version exposes these public sealed type declarations:

```text
FontMetadata
├── Face
├── NameRecord
└── EotHeader
```

These are type shells with internal constructors. Metadata properties, parsing operations, diagnostics and license analysis are not implemented in this version. There is no callable FontMetadataReader or FontLicenseAnalyzer yet.

Consumers can reference the established type names:

```csharp
using System;
using Fonts.Metadata;

Type faceType = typeof(FontMetadata.Face);
Console.WriteLine(faceType.FullName);
```

The library package provides a net10.0 asset. A namespace import does not conflict with the FontMetadata class name.

The nested types group data concepts at one level. They do not capture an outer FontMetadata instance. No font data is read or allocated by referencing these types.
