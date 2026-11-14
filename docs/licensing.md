# License analysis

`FontLicenseAnalyzer.Analyze(FontMetadata.Face)` examines already decoded name IDs 13 and 14. It performs no file access, network requests or catalog lookups, and changes neither metadata nor reading diagnostics. The result contains indications with source evidence; it does not establish ownership, approval, permission to use or redistribute a font, or a final license conclusion.

## From source text to a recognized hint

The raw declaration is the decoded `NameRecord.Value`. A recognized hint adds an identifier and rule while retaining that original record as its evidence. The hint does not replace the source text.

| Source value in name ID 13 | Current result | Meaning |
| --- | --- | --- |
| `MIT` | `Spdx / MIT / FLA001` with the original source record | A supported individual identifier was recognized |
| `MIT OR Apache-2.0` | No hints from this declaration; the value remains in `NameRecords` | Compound expressions are outside the current analyzer contract |
| `Not licensed under MIT` | No hint from this statement | An incidental or negative mention is not a positive declaration |

A complete name table can therefore produce no hints even when it contains a license declaration. Independent accepted declarations in other records are still reported.

Structured [SPDX expression analysis](spdx.md) is implemented separately in the optional Font.Metadata.Spdx package. It returns native expression results and never converts operands into legacy hints. The rules below describe FontLicenseAnalyzer only.

## Result and evidence

- `IsSourceComplete` is true for a Complete or Missing name table, and false for Partial, Invalid, NotRead or Unsupported. Partial records are still analyzed.
- Source completeness is independent of OS/2, the overall inspection status and recognition coverage. A completely read but unrecognized expression has an empty result with `IsSourceComplete=true`.
- `Hints` and their contents are immutable. `SourceRecord` is the original record object, preserving its index, language and decoded Value.
- All available ID 13/14 records are considered, including nonpreferred languages and unreadable language tags. Other name IDs and raw fsType flags are not licensing identifiers.
- Results follow source-record order, then the rule order below, then the first occurrence of an indication within that rule.
- The same identifier/kind/rule appears once per record. Identical indications in different records remain separate. A name and a URL in one record can supply separate rules for the same identifier.
- Conflicts remain visible. A more specific indication does not delete a family indication from another record. Empty hints never prove that a font has no license.

See [API](api.md#license-analysis) for a complete usage example.

## Stable rules

| RuleId | Evidence | IdentifierKind |
| --- | --- | --- |
| FLA001 | A complete supported individual SPDX identifier | Spdx |
| FLA002 | A complete supported license name with sufficient specificity | Spdx |
| FLA003 | A supported family name without sufficient version/variant information | Family |
| FLA004 | An allowed URL with verified URI structure and exact path | Spdx or Family |

An exact indication does not additionally emit Family for the same text. Rule IDs identify matching evidence, not confidence or legal status.

### Supported identifiers

The FLA001 vocabulary is deliberately finite:

`MIT`, `Apache-2.0`, `OFL-1.0`, `OFL-1.1`, `BSD-2-Clause`, `BSD-3-Clause`, `MPL-2.0`, `BSL-1.0`, `GPL-2.0-only`, `GPL-2.0-or-later`, `GPL-3.0-only`, `GPL-3.0-or-later`, `LGPL-2.1-only`, `LGPL-2.1-or-later`, `LGPL-3.0-only`, `LGPL-3.0-or-later`.

An identifier-shaped string is not automatically a recognized SPDX license. Other identifiers, including valid SPDX IDs outside this list, are not inferred. Identifier matching is ordinal and case-insensitive; output uses the canonical spelling. The [SPDX license list](https://spdx.org/licenses/) defines those identifiers; [SPDX expression rules](https://spdx.github.io/spdx-spec/v2.3.1/SPDX-license-expressions/) distinguish identifiers, operators and exceptions. No runtime dependency or downloaded license database is used.

### Supported names and families

Each spelling below is matched as a complete name, independently of culture. Line breaks in versioned headers are described below.

| Names | Identifier | Rule |
| --- | --- | --- |
| MIT License; The MIT License | MIT | FLA002 |
| Apache License, Version 2.0; Apache License Version 2.0; Apache License 2.0; Apache 2.0 | Apache-2.0 | FLA002 |
| SIL Open Font License, Version 1.0 | OFL-1.0 | FLA002 |
| SIL Open Font License, Version 1.1 | OFL-1.1 | FLA002 |
| BSD 2-Clause License | BSD-2-Clause | FLA002 |
| BSD 3-Clause License | BSD-3-Clause | FLA002 |
| Mozilla Public License 2.0; Mozilla Public License Version 2.0 | MPL-2.0 | FLA002 |
| Boost Software License 1.0; Boost Software License - Version 1.0 | BSL-1.0 | FLA002 |
| Apache License | Apache | FLA003 |
| SIL Open Font License; Open Font License | OFL | FLA003 |
| BSD License | BSD | FLA003 |
| GNU General Public License; GNU General Public License version 2; GPL; GPL version 3 | GPL | FLA003 |
| GNU Lesser General Public License; GNU Lesser General Public License version 2.1; LGPL | LGPL | FLA003 |
| Mozilla Public License | MPL | FLA003 |
| Boost Software License | Boost | FLA003 |

Only these incomplete forms produce Family. Unsupported explicit versions such as Apache License 9.0 or Mozilla Public License 1.1 are not silently reduced to a family. GPL/LGPL prose containing a version alone does not determine only/or-later; use a supported exact ID to recognize that distinction.

## Accepted text contexts

A name or ID can occupy the whole record, or occur in a complete declaration with one of these prefixes:

- `Licensed under [the] ...`
- `This font is licensed under [the] ...`
- `This Font Software is licensed under [the] ...`

A complete `SPDX-License-Identifier: <supported-id>` declaration also yields FLA001. At the beginning of an independent statement it may occupy a separate line before other text. Following expression operators, qualifiers or sentence continuations prevent that line from being accepted on its own; an ordinary prose newline does not create a new statement. A terminal full stop is allowed. Independent declarations can be separated by semicolons or sentence endings. Decimal points and dots inside URLs are not sentence boundaries. Bare fragments after arbitrary prose are not extracted as names.

Versioned titles at the beginning of a record may occupy a complete first line, followed by license body text. A title can also occupy the first line with a supported Version line immediately following it, for example:

```text
SIL OPEN FONT LICENSE
Version 1.1
```

A continuation with an expression operator, unsupported version or qualifying text such as `is not applicable` or `for comparison` is not accepted as a separate title. Operator boundaries include punctuation, so malformed continuations such as `WITH:` or `OR;` cannot turn the preceding title into evidence. Unversioned title/body recognition is not supported. A newline inside ordinary prose does not itself establish a positive declaration.

Leading/trailing whitespace and NUL characters are ignored only for matching. Inter-word whitespace, including TAB, CR/LF and NBSP, is normalized locally. Internal NULs are not removed. Original values remain unchanged. Unicode lookalikes are not substituted.

The bounded positive grammar rejects negations and incidental mentions such as `Not licensed under MIT`, `Compatible with the MIT License` and quoted examples. Straight quotes, backticks, curly quotes, low opening quotes and guillemets protect their contents from statement splitting. It is not a general natural-language negation parser. All record languages are visited, but free-text recognition supports the English forms above, without translation. Short generic grant phrases alone do not identify MIT or BSD.

## URL recognition

A complete URL can appear in ID 13 or 14, either on its own or after `License: ` or `Licensed under `. Arbitrary links in prose, quotes or negative statements are not scanned. Text rules do not search inside URI paths or parameters.

Only absolute HTTP/HTTPS URLs with the following host/path combinations are recognized:

| Exact host (case-insensitive) | Exact path (case-sensitive) | Result |
| --- | --- | --- |
| apache.org; www.apache.org | /licenses/LICENSE-2.0; /licenses/LICENSE-2.0.txt | Apache-2.0 / Spdx |
| opensource.org; www.opensource.org | /licenses/MIT; /licenses/mit-license.php; /license/mit | MIT / Spdx |
| scripts.sil.org | /OFL; /OFL/ | OFL / Family |
| spdx.org | /licenses/`<supported-id>`.html, using canonical identifier case | That ID / Spdx |

The historical OFL URL does not establish a version. A scheme's explicit default port is accepted; nondefault ports, user information, query strings and fragments are rejected. Percent escapes, backslashes, dot segments and implicit URI normalization do not introduce additional accepted aliases. No URL is fetched or redirected.

A URL from one record does not replace another record's declaration. For example, a rejected compound declaration in ID 13 and an allowed MIT URL in ID 14 yield only the URL indication, while the compound source remains available in NameRecords.

## Deliberately unrecognized forms

The current result model represents individual indications, not expression structure. It does not return operands from:

- `MIT OR Apache-2.0`, `MIT AND Apache-2.0` or parenthesized groups, including `(MIT)`.
- `GPL-2.0-only WITH Classpath-exception-2.0` or any other exception pair.
- Damaged expressions such as `MIT OR` and `MIT WITH`.
- Lowercase operators or guessed separators such as `MIT / Apache-2.0`.
- `LicenseRef-*`, `AdditionRef-*`, `DocumentRef-*` and unknown identifiers.
- Deprecated spellings such as `GPL-2.0`, `GPL-2.0+` and identifiers with an embedded exception.
- File references such as `SEE LICENSE IN LICENSE.txt`.

The entire unsupported declaration remains in source metadata. Independent declarations and records are still analyzed. Hints are never combined into AND/OR, and no preferred branch is selected. General license-text matching, arbitrary languages, expression output and licensing policy are outside this API's current scope.

## CLI evidence

Use `font-metadata inspect Example.ttf --analyze-licenses` to include per-face indications with complete source records. Raw preferred license strings remain available without that option; `--include-name-records` independently includes every decoded record. Hints never affect exit codes. See [CLI reports](cli.md#license-evidence).
