using System;

namespace Fonts.Metadata.Tests {

    // Independently encoded by Windows TTEmbedFontFromFileA(TTEMBED_TTCOMPRESSED).
    // The source TTF was generated with fontTools 4.59.2: empty .notdef/space and one triangle glyph.
    // No third-party font bytes are included. The second fixture adds a synthetic ZZZZ table of
    // 256 runs of 200 identical bytes to exercise the optional RLE layer as well as LZCOMP.
    internal static class EotMtxFixtures {
        internal static byte[] Create(bool runLength) {
            return Convert.FromBase64String(runLength ? RunLength : Plain);
        }
        const string Plain =
            "PQMAAGMCAAACAAIABAAAAAAAAAAAAAAAAAAAAJABAAAAAExQAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAblu6gAAAAAAAAAAAAAA" +
            "AAAAAAAAAA4AUwB5AHMAdABlAG0AAAAAAAIAAAAAABoAVgBlAHIAcwBpAG8AbgAgADEALgAyADMANAAAADgASQBMAFMAYwBhAG4A" +
            "IABTAHkAbgB0AGgAZQB0AGkAYwAgAEUATwBUACAAUgBlAGcAdQBsAGEAcgAAAAAAQlNHUOQEAAAAAAAAAAAAAAAAAAADACFYAAJb" +
            "AAJfAAKECMSDs84lnozkQ5TcnZSVXwlPf0LCM8kMHDSEJJkWXM/nO+DsjMleKj5yJ8OKyMTAE1rcjXFxCXDoW5jkuCaI5JLrzUQk" +
            "nTgNs0s5I4niUnGtp/y9gijXi6gpspJlCU3q7IjYDpwTE2HPGP8cXPXbW8X/RlpChiQDfKQxDRc15OtE/W3ZgjkWwenk8LjQTPRg" +
            "sgoCCocTEwUgYIXP6Xrp6IcFQim07r6imMjFGfIXQp2Ezr//e8frHtuOw7ySqLa3uBmPXNxU4sGkRUw9LLPQvlspxpNBMBFdgGHZ" +
            "JpMosgSKC1uuuQebKM7nX3rrzWIGFTxbGqzZtUSKFkuDAYZS+MV41cjp1OIRJr0fHUEEjQdih/defPOTroC7NX5miBLj4P697WO/" +
            "2PbaSzn4/0cpd56dWcHOGAAAHfGiKIYIkmQO4BgnCLi1vLLsnyK70nSr5mSp7FQa8UoeRcuME5opRy/sSFikmQMCdNTLF7maX4qE" +
            "KLmgwrGrcbmZA3cDNQv9Dy93jxpLScSDgxyGqKGQAAyiqyVpAR14lQGSZaVjLDCARpkdq42VWgro/IQw/YLumdZDFVDdNeOWxMd1" +
            "x8YHiolUYuOoBt3IQCk0xMu/nypa1M4U6j9X5HLLgpMoVwWBUeUfI6DQURHOmY7zWlJg4yInYyKTe8dLvkG1npGZicko5S6am5XJ" +
            "XNYiNkeRltNxU6UUU0I42hTwJu09JipVS+fVTj7OOGQ2oOUlOHbTIsrUttS1mm2rQpxjVKEX9G2Z21MGqP12UCQCOrdVGlAAAAAA" +
            "AAAAAA==";
        const string RunLength =
            "BgUAACwEAAACAAIABAAAAAAAAAAAAAAAAAAAAJABAAAAAExQAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAz8ISkAAAAAAAAAAAAAAA" +
            "AAAAAAAAAA4AUwB5AHMAdABlAG0AAAAAAAIAAAAAABoAVgBlAHIAcwBpAG8AbgAgADEALgAyADMANAAAADgASQBMAFMAYwBhAG4A" +
            "IABTAHkAbgB0AGgAZQB0AGkAYwAgAEUATwBUACAAUgBlAGcAdQBsAGEAcgAAAAAAQlNHUOQEAAAAAAAAAAAAAAAAAAADAOloAAQk" +
            "AAQogAPDE0RiQdnkFw9GciHU3J2UlV8JT39CwjfJDBl5EfRXOClMckkj/WaGkISTOJdze7jsRz3J8VHzkT4cVkdsx0ViFGGs0Khn" +
            "Q9zHI78mZ+JJkuMCM2yA22nDi5dcKas23P/WNE355uSF1ZKUMSnE3IYr96Vapk45QecrrNY4RGf0DaSi5ME42kMQ1oAlXao7XnaT" +
            "fRslMPXQXwtkWLBekPX336nRaBXC3MtU7OiIaOvQ6o/jS9ZeG3/+7+stm2cBKDAQYJAgdgnfhKdHZAbICjDEIUoi2EMoqILmxXUK" +
            "bAS38Z/InYV0l9wXWP1QOSzs4vELIF5Zc2WyFGiVpDGTpEEyPwjOJxiSkXEExxIIu4VtEhCkxP8CTA5Ac4GsB7BEwdMC2wHNhWVT" +
            "cdrFpMwSMA6V6RtIWgepw04qBGkPSGpHUd0CtIigRI0o2JBEdqOxGVGhHtAMTnSFJEEhKM6G0eSHaHqNid6diPEtqdNMIjaj1JFH" +
            "JJiBIUgKoINLOlgyk5MhRxSxc4o/o1gHwTYEYh8G+BvAXgXwC7uwnp5YDsCOBbA1mshMnXCcmqgWTdwhIG82MDSGJNxNrAbhiTfQ" +
            "D4aIA8CaGHCVAVhlwEYZsB2cacmG7PHPbDdnchCQmJ7ITc+kIeGTCBhlhAT6zznshPw2wgJ3YYMMeEye6GbAdgcMkC8MOAmByBxA" +
            "0gfhKhLz8QN4CMF+C2BrCcgewXoC4QMJmA5Cdguwk4B0AyEvAMhIhBwX4QMJyBLA+grwKYDYDsFaCnA1BWhBwgYJ4L8DWBpA0QvI" +
            "hoSCFF9giCgsWwnsokb/aLtkI2aZScwHt4DsxuhVerzqx56OIX8kcjwyJ+LRBSJYsKtWySbJa4aDP7FZgjhf+Gd0Sb8qvNE53Olu" +
            "Fgceg4KbBbXfwvPtZ2eV4fbF7lke0itXVnC8tZAX3Y0Ndlwkvl5LB/LYGLLGf5JrtvngrKA/pvljPnDOmZxYfUjtbDZ4V8gWPqT/" +
            "pHPR6TEbWaOb7cQrs3pUyaJ1PzTXD5Y8NDXyFZ/MLfQOWsADrMZqQInmbSJ8YAJnmdia9UksECZGS55FHCbiN4Ry5Q8kZ7c9mYCC" +
            "8kKgOOIRTqB+uqgsVEXszCsStRrgmX37+OxCr/sQ/aPHwSRNh67mmhAQg9NuU5BwU9o4PBPQ3gRDYrYDvUlNSg4YKU80HjEWOBgZ" +
            "umYw0jNh3wTG0J+lciwgMLsbRxoUW5qiogxhYXwYgo1F7QPGEBrA0ELARF0tEZGlsFATa0+nRUS7F9Lr2ItsHQKiqvZ3+XgyyJ40" +
            "SMNDDYxECnYxbu8W2MIW+hJHdRSbEhffCQ/QmUhgfIoPoHv1c7PD4NPXPW0EIUGLSWBIU8CarotP1eicPFdzFJy7DuU9luV6DweW" +
            "fbx8AAAAAAAAAAA=";
    }
}
