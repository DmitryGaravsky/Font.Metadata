# MTX copy-loop vector

`EotFontVectors.LongCopy` is a Base64-encoded synthetic MTX container generated using Google sfntly `LzcompCompress`,
`HuffmanEncoder`, and `BitIOWriter` at revision `a56f5782f209771aa226063757d57e6b5c948478`:
[upstream Apache-2.0 sources](https://github.com/googlefonts/sfntly/tree/a56f5782f209771aa226063757d57e6b5c948478/java/src/com/google/typography/font/tools/conversion/eot).

The first stream input is Python 3 `random.Random(1729).randbytes(8192) + bytes(range(256)) * 16384`.
The second and third streams encode empty inputs. Each stream was compressed with the unchanged upstream
`LzcompCompress.compress(byte[])`; the three outputs were prefixed with the MTX version-3 header,
zero copy-limit hint and big-endian UInt24 offsets. The random prefix keeps expansion below the inspector safeguard.

This vector checks independent decompression and cancellation during copy work. It is not a renderable font.
The vector is compiled into the test assembly as string data; no adjacent data file is required.
It adds no manifest resource. Tests need neither Java, the compressor, nor a network connection.
No third-party font or upstream source code is redistributed in this fixture.
