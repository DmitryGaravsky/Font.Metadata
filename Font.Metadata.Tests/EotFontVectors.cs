namespace Fonts.Metadata.Tests {
    // Independent vectors produced with Google sfntly LzcompCompress (Apache-2.0), revision a56f5782f209771aa226063757d57e6b5c948478.
    // https://github.com/googlefonts/sfntly/tree/a56f5782f209771aa226063757d57e6b5c948478/java/src/com/google/typography/font/tools/conversion/eot
    // Inputs are synthetic metadata/byte sequences; no third-party font or compressor code is embedded.
    // RLE cases compress an explicit escape/count/value sequence, then set its leading RLE flag.
    internal static class EotFontVectors {
        internal const string Sfnt =
            "AAEAAAACACAAAQAAT1MvMgAAAAAAAAAsAAAAPm5hbWUAAAAAAAAAbAAAAaYAAAAAAAAAAAAIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAVEVTVAAAAAAACQByAAMAAQQJAAEAGAAAAAMAAQQJAAIADgAYAAMAAQQJAAQAKAAmAAMAAQQJAAUAGABOAAMAAQQJ" +
            "AAYAJgBmAAMAAQQJAAgAHgCMAAMAAQQJAAkAIACqAAMAAQQJAA0AMgDKAAMAAQQJAA4AOAD8AEUAeABhAG0AcABsAGUAIABTAGEAbgBzAFIAZQBn" +
            "AHUAbABhAHIARQB4AGEAbQBwAGwAZQAgAFMAYQBuAHMAIABSAGUAZwB1AGwAYQByAFYAZQByAHMAaQBvAG4AIAAxAC4AMgA1AEUAeABhAG0AcABs" +
            "AGUAUwBhAG4AcwAtAFIAZQBnAHUAbABhAHIARQB4AGEAbQBwAGwAZQAgAEYAbwB1AG4AZAByAHkARQB4AGEAbQBwAGwAZQAgAEQAZQBzAGkAZwBu" +
            "AGUAcgBFAHgAYQBtAHAAbABlACAATwBwAGUAbgAgAEYAbwBuAHQAIABMAGkAYwBlAG4AcwBlAGgAdAB0AHAAcwA6AC8ALwBlAHgAYQBtAHAAbABl" +
            "AC4AdABlAHMAdAAvAGwAaQBjAGUAbgBzAGU=";
        internal const string Metadata =
            "AwAAAAAA7QAA8QABCQjEg7POIZ0MSxydlJVHm5ZJU9GWGRhJCIGBHHkeYZIoAqoworlhoqKKQu7QbLImglVKqIimIjR9dxQLHJBpk48YxW42Fy65" +
            "dbICSBFGtHjtsAqlryVAmivnWTGcZs2EuDiO2aWSEXAfdOdpufugOUxoAAg2XyrET7E7J2NFtZoRiFSQFyEIutjJ6f+Os8jpFi1ySXmFlbdZhpMu" +
            "bwa+44NRybno2Yz9Ic1bGNNkJ07P1rcY2c1LcagJp3BBpgIVSoCHG8kgFwoJhWHcH3Jt95c3xETHjNqAWSKLPkKqHlkgAAAAAAAAAAA=";
        internal const string Commands =
            "AwAAAAACWAACXAADYgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhD" +
            "IgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjE" +
            "hDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIgjEhDIg" +
            "jEhDIgjEhDIgjEhDIgjEhDIgjEhDIhTKhDIhTKhDIhbFhDIhbJhDIhdJhDIgvJRDHXCBKASCI4OJWCmBeUsf+EUSmUZHSDaSYYMBaLBWKhSKBOJh" +
            "KJBGIhCIA+Hg6HA2GgyGAuFgqFAmEgiEAeDgaDAWCgSCAOBgKBAGAgCAP/+/fz7+vn49/b19PPy8fDv7u3s6+rp6Ofm5eTj4uHg397d3Nva2djX1" +
            "tXU09LR0M/OzczLysnIx8bFxMPCwcC/vr28u7q5uLe2tbSzsrGwr66trKsZDKtGVeMrEZWYytRlbjK5GV2Mr0ZX4ywRlhjLFGWOMskZZYyzRlnjL" +
            "RGWmMtUZa4y2RltjLdGW+MuEZcZGRcgz5hn0DPqGfYM+4Z+Az8hn6DP2GfwM/oZ/gz/hmABmAhmBBmBhmCBmChmDBmDhmEBmEhmFBmFhmGBmGhmH" +
            "BmHhmIBmIhmJBd8AAAAAAAAAAAA=";
        internal const string RunLength =
            "AwAAAAAAFAAAGIAAA4XAomEAtNAAAAAAAAAAAA==";
        internal const string InvalidRunLength =
            "AwAAAAAADwAAE4AAAQXAAAAAAAAAAAA=";
        internal const string Expansion =
            "AwAAAAAAJgAAKoAGAJI2Q8xQCKUDKkGfCgTChRk0v03TiSGIgUAAAAAAAAAAAA==";
        // Generation recipe: TestData/mtx-copy-loop.md. Keep this vector in the assembly string data.
        internal const string LongCopy =
            "AwAAAAAj8gAj9iAQAG3Co1ab9Z7qFOpjCUcySgpnkUpazss5jDAYDSFwKPCxRwJEQS7QJqicS5UfxFgDoTAhHTCD1LFupjIh/EbKdhNfs5IpyEAQ" +
            "M36hYcmOYTvw6RT0Mt4hw8L7OeQBVuvI01cN65ocIUgxfyy3vWfSrAMU0kw3PyI4K6ESA6YtSswaAQo/RnVgFiwAMCdMIujO+QYBWo5iyKhUULji" +
            "xKj0bphk94o2fFekhL3VX8b9PGS3pyWnCmxd8ige6UaY5PGb/RtWU/GjsWS/p/JWSPulkVQS2snk4KZNXR5o+G7uRR3jAj26n74Xdcuq4mcLswKh" +
            "/VYst1vK78zkeTgqaE7YAFZBNNIvW6OL6cBKSmrXHqjbg0QBcw8Qu48kYUNPQVA/7Wa8TS8eaIwC/Sc7Cg+GwhO22K7YKQFBAmnEA+nPcwPrVgkY" +
            "ELkhL6bUb3NMxKilqeHV6w9OTWSrS7nlKjIwqsv8M8mGQ06yf1hZkljLWHaEIrUnAlRYkX+ATCd1zJChsO7CkvDUlHHQJF1PF4G7F/xI7x8IZk/O" +
            "cFGx54sH64BvEJZbLQWA13z3RqOzAksfSuZR17aNr4Q7lUDwnolgw0ZhbCbyccO3VoMeXy5rgT/k7nq8pljodwlBDkwVIEjRUANk12kYCozey4EX" +
            "MoP5PWOdgEShQAx88EuQ48wK91CAWsED9f1fK3gucw4OGtMHgPMugP7YZkoXqNaWR33RzWBlQuuqdFRawLX7PdAAuLNdLum1j15ZKavAQu50Zzst" +
            "jhXa2PpcAWI4LzIBi+yFMiYj0poEagJd5cjzOYedA08LpbG8UXQZqhXzE50KNz/WaZS2gi9qW7EOWEoFckaq6Dyu2z4VNiCAcW0yxrFxeF2xrZC8" +
            "x5vjieVLe9QKYrMmsVH07eCyWQkHx4V6KlILcBZdnsBpz3HuDYHNfdxrZIzR0u4MKa/kEcMoswxys5fCP25g1BOgdwjYjTgDW82C5IhUe4zL25+x" +
            "BbHZiROCVTdJnwCxWAYMOPiK6VYF6Acc5ArH2V6SXqkiXKX0YUO7/yjCkl5ximHYT/IGP/f5BURYWgk6URRaN9+ipaq2TcBQ4pGGlMFo7SQHoYhr" +
            "o0UNhGNRXOloQLQQx5JGH0B44o7VcVc1JBY41YYsxAXy+J+uaVKhheaproxF7WAzyDriT07eaocJhwoE1UfF69V9z+R3xsRsROReIvSOBtnP6ekq" +
            "Mcm2Es6Spvp73YUuILv8OHEg9MJsoigvH6ujdHCw3Q7lglXh+dtiGXQWvS5lyKJKWGVXixeLQ4Dm6sOdIBTuKi4oZtYYGJKjCSgP0D9M6PbSZ+Lq" +
            "Q2Dy90fiZ9jUjxlL/nPTHSdWChSXT945ab+TmZ3APnl3Fw2BQQMo/JZpMg7YRfYmfCQG5UQtuKhm7PU3I8Tf8oZQrraR32n7iKS0Vii5LFWIgB3a" +
            "t8H1II5fPdvecthZ41bj1R32FiL4n4FXApHA5MOT20Wo2KDqujmmfIgEwwScXwmNr5SdgpwJ3z+Wdavgt6OZgm8i1pyKcIZoUmSYkY3MEDRIFm9o" +
            "iYaR6wzRLtCE9nbbY6tXp6W3Fo4ZC0VAId9RJ5HzINMBUiANbgodMb4MRM2bPwwjTb4S2VTsjmLIhzkAh5vuMt8tS0BogDcLb/8hqIuihVusoi85" +
            "z2JoFxFsdGGOnx1Ei7lZrwiBGDbfQ68Jj7l2gDXfWeknfJbEaskcGBrBf6enagFgBjLliW7mSF4yiL4meEAgJOhBL0xVmqiTUmQpPRL8GyZRGPK8" +
            "YMqIhZ46/t/OdoXOBLo9jOS8YDhe2FFkkbyHbiXmD++1sEl3Ft4JtjReUYKTUNi1MdDNQci5FEkr7E8MZ03PDU/iUDG8oRF9E0E9fuqXT/isoQea" +
            "6ORZIgVFxIJDJVVH6MyB6XAhybvhpBrGQRUE+T06e1SPenZFAnyTt0e4H6T3uue2AXtRxb3rdIIUkeAB2lJ0iFW2WOfACWBvaxmHPiMwFBlFZAbt" +
            "eMoyxtnkS/irX9DboNRWoxmTI6KKJi4z9jGXvegaos26RqJJ8tgNZQpbDSDWszql/9K4tUQ80I3IBGRlWYGWuoNHv9QiIwA1GVhdYDcD/r8DQFdi" +
            "6jyYg3gyyI7YAQh8zF95Ee0OjpjmF9KKrApeCQCvqMpWebrnCYJMwIHtMkeOozXzStQLFoYXaNh2L/d1dYUB+EWQj5GDWZcmQFFRgveXvvcxlDjd" +
            "teb4JTCGxe+8j6mWvgnDr6offrlNd2WYBPKEGJkJ+LHJLLLvReMkdTusNIaKolZXOgQiE8Ke3akCxFFOgEBNSLp6noI6UdlihyuQNhxBJ4si/FP1" +
            "oPxg7m4pqIacgTjR5ZvgeD+1iMenXCZNfHVNx5VWhwXUMl3xn9BWAUFO7dDY9nRUeyS8M5s+aZCirkaCyia8Id8aiNEIOBP+xlDB6ZC79nwI9jUT" +
            "5kQf48X7Zzh+enNTKIaUbcIAkDGwkf9V+hELv5SyZZO53G9Z0BW/LI8U9kPuNSamVT9/dXU67MiS+yQFBlYkY0oSEIEDMIGOCIUeTiuvIwLIowuw" +
            "SVqCg7ItBggaQvh+6XnbFDnb6gzN8oLxdoYX6Z8QQoc5AzRYc0YzGU1FxqaM6vJaB4g8uBoxR+ReOUInR+hfdWrGZ7xzbEv26/sMkRecbg+ywd/f" +
            "1vWmlxnjrjGAHrsg+uwEJvMVFMMlAmPS6OcYhdh0YgQXroBHUvjbC+agBNhRwqZaE5OssagHbhNJhfQ7RHmPbetw4RGKrs3QT+YCk4avXZXEZMBe" +
            "hyCKIptNmlpuvkj9Eco2Jpd7kSSQNECx7HC/5dQcW34xHZOHjAhHcYXfl7F9YkBUi5uqUtW6SVcTW11L7CI45P1MSYvwjBYoBZ1kCRUNcyBS3TUt" +
            "e2RGspLJblND6BTyQOTlzKwgroGMt4/bkMI8T75xyuIUOtO5aFuzbXqCW9zWrjbdv7a4Q3FrvLQIRuW+PUPEKwAGXn1CC6Z157fWR963VBQPyYjF" +
            "4aRrpEGBT8Bo+1ImiyEZCafu2kZjcl6Q3ojaQtsYfxQo1TPnNzZS7umVn3JaqIBUAHGJ4NbS2xXHwFLgDgtrqj3eJqULEG12Kp7sJ9saQrwO2EA1" +
            "BWiNZsD7vs+iTRztleL1A5AOoKWwY1d6VPtCQkwAoTz8QD/T9i4DD4Z8roP9msIE8Ya++IkqgJCw/E7gWx29N5hxuV2Z28WCJcm4gNBBmG4IzJt5" +
            "34W4uHbb7W1GPyCjTYaQd8njeOjYlLz6LvAF1xK7gAMzBgV650A5OojAYRDFbD9XLYYK2awE/ULMMqE7YgBh6KrC7zgK0i0Fm2OuOJHxHGClZ0ik" +
            "GaSejVQ3433CfUvxDJzQ8b4ZyzmsGQI9oIliFfEizoa7Kd724yrilBQIYYgwDmMAV3GUjibB1FzdM+Bgt4nmtI7IPCTG69lXilXwUSnhQJJhIXkP" +
            "DgcwjT8TygEKS2eGuDkyC2XWBPUPxpwKIR1Ai5NMWjPX8ZgJUlMvEYlVcNA2dcwbm2y+o2g0GMuLoQxAnZgU/lMgrkWsyuOC0K8YQjJmH58BCa+1" +
            "Cr2CVQHhXBhgAiMs/LE6GSw1hsGbYLmF8qEbE7LWWpQEX5mmtXelUqaZWcnGaG2mV0gum+Ob4OGwJtmAVjjlCyLN2/aA0U/s7CzewzjIDFP8t1UB" +
            "Nmh6BoggdYpJpAhN3490tX06G0yFU4UGS5FQcdz7Pyeo++rlBTRoj+V/KeXrpc6D6H424VW7q9QCSXkTQXAEIFsqWSNrl2mcOGhJzAj2g/EAPP4o" +
            "Ct6KeP3po8w8OOz4bNbMErPjHBAPmLwXuLGo2GzXxRek8WJcwI8aQxoaPS6pLfXDpA/grfUPMIjwGyuoXiyV7UhJU19AMvs1mICVdKwEimY1HCbZ" +
            "G2epKUV5Fz9sAA1GiNaWFBSqdBy0IvI2tPin20qeU8+MxAssB5BMf+zGNz7DTbFkdcP9EKKJpFgSFQkTkQtIRZNuih9hTGQYNyAs9LdgRAkFh7Dt" +
            "K2MWFzHTNamM+VshEAom4/aJcm6uQNezE6UoM3vBIQoYmpC1Y5fnJ/NLCH9o2hV/9ge+6wlz2bZPJXPTgVLoTqQZGsAqFAwPTwwpSUNtJrJJTYBU" +
            "Gi0TBvZJkNKcxhEnyj2TUG1rsEWJWV1bAux0ptxlvO24+2LMbTZlZQpsN0EFQ3qM02QqDHnVZKQapGSYJGBW40fDINXi8Ez5A6jSvCBBkNk3kVoN" +
            "fSJSzGtQYJOQz2rMg14GEQAnHsmI0F8yQwHwrmSggDasRqgLot343BfuHpOqYTkuXGmExzGLV5DJ9jlTD2vvt2nUH7AcfTXvZ+c5FQ0JRytYIdjl" +
            "8fcacFQQrO+Rn0Yoo71ncYwxibNzeDhleAvXg3qdWcsFQlr6a2A4lytOy2F4LAq07FCsHHVT9UMIgFNFxq20IDiQAF9lsdFm/tLga0A70zRiA7jB" +
            "IqFvbr7xDwGVmAcX/fma6fHRCSaXUYZ44LAFPn7vC1HnIIHjkjFr2fV0zGCNRrTiPi9TyIKQZUcQG/LHktGkavJX8KqF5PBB7m3wKTAZPlMQB4z1" +
            "sKJHd0u1E+ETxzcxlFgAXbpwZIcObsx5ogWkXw3Cq0H3wMDDPOs4dWA0WLa2xsaHU8t5zPpo9AHJZMgZblLXUYexwH/iItDZx5A6TF4r9f6S0YXf" +
            "mBzI+Z+erBbBYZWFOMYfEDzKD6CAw7Qth2YntgviJLbfociiq77wO5hhZJwBSPl+lJNYzvw3BiAOUf1Yhjl5vvv7Amx+AlD334QrB/WjAGkPOxdZ" +
            "njtoNqHK9MuWrfOLi5E+YX68FNWVNoKirdCrbvROodcHkFPeCIpP4MAbMAtMcnHVFY2GNVnfyD4bzdyZ5JtQk2AxK+LVx/XTW44iLoIOQXa7bfg+" +
            "SrBOU8JkbYysK3kS9jAbKyleNYNW+kzFkLcMPv48Nx5I8zyxffg8Lx81yI2ZB5sD6vHJwl3n+SfeN968SI/QHHXpHRcTZw9hUtv8OjZpAPVHMieP" +
            "2hlAfgB1o5ZIWwuDqtbpeTOonqUPOMCNz2cPdAg4bqInu1+8CB1vCiJUwJXDws7SBBRYR0tncpSO5mHNbUNCjeztrt4QzGup1+ehlzpOUTlMAT84" +
            "WsCCMsCwZ1aELIfqqD6qPBmd0p5ThDxX3sKI3alrQVKCxR4VjdI33woNYiG8VFUoBZGMc6/Byrv61sE0eYAS1w+oCrmE24yF44hukwxYysWvhDX2" +
            "pw0IY7OtaV8TCY34Txq8oX9X2E+TILYzIP+R/9PHaZ+rTrA9TKfxPieqI2hiIKVyBCIV8+K/gQ9XLVJZojkTYoRewPmhIAtEqryyytaqgnua0b9K" +
            "iy3peFlEwSgmloWVPUZo9BuKu4P51PfMw4g9So7ZHl4LarKprTdArDhBiom+JL9vTeoIQlTQ0M5MwD4Y+upPvAif4P2s8qCxsJxW5JueCNqXGo7Y" +
            "T1uJR+BsHYdC4Fc+qC4YNo2dIn8BGL1KdvpWeTWoCRmj2E+sv/AgOxbxQuvSfqOepGzdFaGnzV1B8EPToS9OqIRKMtpOUXZ0qKDQvF9ho90OjkPI" +
            "ZgT0xAONzeYJfE8KPOEBFh9VW99hBrncwIT4citim+W8tVmGugifIqjLfiCRkPqvr9dr61CaxvbK0ls8/b4VmQXZeU/0c/SfdDKPFxUij5hS/lKN" +
            "iWOu8IK+lSOYBhffd9Vu3jEvPCElbkqKb7cZiqDuG1oYX7kfqY8sJIHax7KXkDHlKFkwvb67lF/C3qujdmPbnkbmEBz7JV/5LbpGHAS+hZcdyl2E" +
            "kxAU7zewWJWLTEs6LK1tRxVGuqa9e4zeg7Oved65hlCovM1rh4dt7GxN6cPcdn+lyLUl5mP8XwETOUzhFqEiU9+qZ21HDXubi8nv4XaqWilVBTDY" +
            "SfK1uJBKWCejawjxIZc18XMoU1BCids3kMF2HBIXKFfMiqDFytarFQifOFyiW+B+d9r0A6OvrRM0kTESx602+HnXS8I0baMMrA8G0gJWjTVJE+ta" +
            "AQGwYsMw9hSdxa2weLqFwWK8PvFAs/Wf1GECAEv6fqiSBexoGlsk3Gn0yRrCh8LmHeHxihYybAmAN0o6opc75byxdB56m0QGVIKUGt5/6YoVyNJz" +
            "obqD7f90BQVjbYpzpqVGR/ueFBvmKS1Fg7ojBxxvsG83j+OOSe+I4P4zV7Ui7kDqYlpRvycCGTgsaBb6vCFy3TeqJBzBDvC65A/hjEVh9CPrgM3t" +
            "3jK95+3DwywmnyeyyT28RDqAH6xAET/WyNrbcw+wLYA+cXW6On10eFqmfWezDZE0LZSAI6MlWGPBUi6K87d4cxyfy5ttE4A9HG4rtQTQtYObHAlw" +
            "QXpc8H/oN4hXSXlGUZ1uls3oI2P9MfWwQ1Az9hWi0xBTWVwBHFFM06nAYIbm3zoVUqrXk3hJGwd/6+ueq/HGFOfMqsFnK2HctsfnchhPRfuxTYBM" +
            "TF9lhwR9z8dZZHnUG22fHckE96D4uTiMcRjjOFRDA73PUTTpJSIYZ63pl6PrJLc2MO/2M4LAKIcdwoLrWgNFmoU0ynZN5Ghdxv7aDDEQ9LYsGvTt" +
            "Y8i8agQJxXCItEoaObAgXzAOCItQlg6lTzP0W4cAIavUmXH7YyGSQSMIOFW4/SEX+5pQHIO52M0I9Z7Nc3790qayOqN4EJhG9MdwQKTFLOj0GvEz" +
            "kU/QBn+8Ih6TmwTDb8yI9GRyPfutwQr53AgeDiDL5uUR5iAN66Yv8pEymxN/bv2VhumE1t0YWzoqm5/zqYAC0qjYAAmIuaXIq/LwanpcIhjkak3E" +
            "oFeYaMTWLwVOUle+BlgHl6cYotqG3Fne4tXOFhp295Kmm2fM5S3CVHUn8yIOSIy23RL3cHktPBtFTw+SRqgIYYx638Gb3cQdaGI6s8PkjJBoZPvJ" +
            "aXVWTuhdpeWsdaNNIooV95Z0z6YXznBy+e7qPC9Nx5UGJ9SL2vJAoiaEH27dZjB+zvNDcWtE6rdJrMzWUKxao2Puk5g3nnuX1376CU4R2dIWgJvP" +
            "j+0Vavw+TF/4EoLVJsV9Af1USFpktRlyjIBcXkGRHvzSS5aww4RiqBat6zgAaiZIed7MHLm4ACXtc5f4Ti2F01em41ZRC6a/wCruiuOsSJDQ/M3N" +
            "w0gRCl3bVBGVib20NSt7FV3wVEJjCy8MuXZidYkx1Y7cPNjTWNiylsBbxqL0pBz41WJj314XozBITEAOyUqUFHOcvUMoC3LeSoHVuNs3ikCxSgXm" +
            "HjqBrx53hDXwD6ZnOvNFJjbRG+zt0zWATPWNhYT7IhP9ow/1939NC3rPtogBJkagmghSaQHeTRfTwWQnkqDEsO88mnvBC5pEIGw7fZy7VVpPF3Po" +
            "e0G/uZU2IQx3Qz3SAjfeL+9S3KWqSh+S1ZscW5uSTsi0nwMSEAwCvwqvCfeHf2xDsRbvbdapAWUWITTJ7dvlEOpFo8s8Ab9ZPnjMuJzuYpxtbnZt" +
            "l6Dn7XijTtTb3xiUECwbAFWtsJJ+wggyUMhTbfRyxHB7++czQvQfyfWUIiH6ZcCCQgD6f9rCey/oi46eBHNRBAGJWlc3woyBInoGq9wwnhYSRIWd" +
            "XEF9UzDacn1IeiJCc1+5vT6qndfKARc7Bvt7H+JQkJUJ8qZhXQYCee8dvqQDnp4ZKJb341Gu0vqNdalTE29u8sKa2xxEDeAEwwlye8ve3VrHPBTL" +
            "Ym+VsZgVDCDlCpAgp0L5qy+nR55ETSGms06dfHw3LBCqUCucdw6YopeBuqd5XMNb86PI43kxZIUsOVYGWXfPFZqz3T33CThWEdhqxoXxJMmhlgkt" +
            "bhf69/yEKtmeoPsf5z9vFC7u/iOaXp2kZj7+u5nmD9MC+QtkzHa/tA7ZIZjXArReym3nkI9tBuOjLedEM66iSaunsXcpHfxDw4lJL+gdAd0m9JLf" +
            "LVXsz/pvJch2Dj/38q0Le4iok8GFt3+qt7nooaZOGsWJjwygdMORA1ACBZorDQzOS1SWc3AG0RdKmHEiEbpvdpFW2Jd7m08exzcKcQCGq2WfL8oY" +
            "1xQtwlQBJy8m4wCCd7A8tMITrrM5KkDuiWsB0UZ5EJonWfcrua84GH0NdOJAkazlyiVk06B/d4NNVTEkEV8VkSTYX7Qp3lAWQaSZscP94UctKq8e" +
            "vYOYkCN+8Xc5zexNJqQu3q5ed642bBa3uXCE336mlk26J78Bx0UgxE9eBIWAql5AbcDdPP3gatjutAFuwvochw8tWAq7poISEZ/zMq7KPtAOK/t0" +
            "vahsN3/FTv/ZVyEjqVWqBdV/gdvlSojHpqf3D+W6XMSudYZnzTDtRbmcmqU2WeAQrM66BthqHdumOrhnmVSstGPBBJAoQMao12iWtxUB17w21DCm" +
            "7vEzivbDY+GaT7r18I9Zv6kEe9o/zY1xMFHY+SHU82bNt0Q+44IQcTxwvcZAiSrG98rz7mhhTw5ZkXN1hoEMl7HaXvzAgj9FgFtlNsE9uoaYadCV" +
            "CJ5vjCgxMu9cu8uiNpnorOdk3Jc2N/+5EXG9lf3KXBJ/4BsWwQC5OaAnMRRTkUUr4ndlioMmmhxNJ6QtPocvi/XuF5dmVz6C9YyrgtmT8ocezmpN" +
            "1PeJqOn5uDpRBOzrV8uNWWm8yogMflGDLSpVVKseUo5KJZFUCr2LWIZkmfsbcR1D4qDj8E29nfnfnq72/uAdacqdgOu2iDguWOhe203ghX6cSPSV" +
            "i/gpS4m8Er8dGp6Le3WLR0W8q4b2OxheNRVpI4aYFQtyqVsA8pp2SWpyehfH0GZQzDl4xWpGd2Xr2XlEtlWSJ3mmTM/1+MamCvrZnhtk0tqd++Bx" +
            "cg0jE6Wo6meLkjKkTIdeK5jQ8EPkbX0OzM7Rukjk9pdLfNJV51JbZdVwq2lSgwh4ZQxMR7qi8Z1bNQPoyEq5obBUsXtZ9JiUIob4LKUQBOfeXwJH" +
            "mDQi3fWMRJiuugmRaWSc6T6YAscoF4fcYSMzIz7QMlN/EBO/QyekcJuL9rufO5CAz8gPy4BfbFuTHXAr8fvsEaq6kmSyE3y1ZfpxfLQxj2Gh0AkS" +
            "Dlh44+UaKrIPzwAUGDZlbTPhb+2C+Xjv0/07tmp07Y+xnPLRUm9C6f1ciKuzWocvlMOxrQDlKnDKTTSetuakJarM5VpxE6hDHh4DLWkgjLKhgC/l" +
            "8fr5YxWH5F1DHELabwwroMI9Z1q6zbhn5j2DIO2BoO5GscN93UEg/tuJdR+LbW8PIjkkdQOxl5lNzXLyxrnjHWNne2NPwK9KystZDg5XYjmL66vC" +
            "wIx894L5+YWzVhg7k2MrZWZhptKTBZKibmzjt5No6MLdGDAFMPKqN2JBSL1oDr5whClvYOdtcZuebJUhg02z5g8F6bU0llZguFehjrk4bD7dx2ll" +
            "W0pBLp36rWDg9j2xXtsHKsMhYtxpWUvbvI6L6OVPLhSyMK7O4Jvhibetz1ZQ7EQWr9jLoERWGR3WXuHUG0z9f9R9W7QsIM56j11Dz176ZUTLMbvt" +
            "YfWIBlILOtMEqdnN9OM2vyEMRnhgarZ1uQpboBGtE21PEot+LhMwckLrRR1kxo1ccorXM84G1giwci3JmmkhEFqFrkgnrMhyaWbn9QqR9ExVTuOl" +
            "c3pgRq9FLn547tXejclD+mC1xh9sk0mWJxbP3NEukZXl6fSnFrSihnm9OakCsJNf9d9KcyLOGWj/GNIf0B3FzVNthntkb/mMafwghACzFrzYUBcF" +
            "dLhRjRqqUwP58JgGfwyEZKiSUtes7RmnqIkbfRETDWpiMEdjVZa2Y72XiPyvFgzUTD4KgARH1S/xEpyHXE5ZPSsxAGfBm+gqqg/BIZ8QG/8jeXne" +
            "QHboViAzbFeNsfNaaZAmLBli5dz2fLYOBcql4EyaOhoTGiAij6hfxYFwM3NiUsoiBePjKVHgORMU/2hLgNT42FD77Oef8EIfoohBgCC7MDYRD31f" +
            "Elrv5AVp9eVEEk9pZbuRokD03OFkJB+wfWmmctPGljvU6NxBn7ygO9gshLHnGvXzH6jMbFKwH5+4sviQZndSiSFYpMcDpa4OIEzvxu2lg8oc90+U" +
            "PKodp7WwiBOUxIvNcb9wNEp9mVhmlDG5PHNy1rF+ysGfagb710EB12N2ZtfQ+G1M1dDj9x4PTsUzWPBjovU7gsO5AZPEhSdXrxKdlf+3hSZMeRa7" +
            "jePxj5LAO7DEJfA6Aenrbl1/CUH9Y1rQhgACb5X0nbDh70NBny4VAyeaqPIeBLqfSFLdfTH0BE84PfGAUbvpQLClDtlrImr1v+n+O8+xTto0HFfi" +
            "1mdVBh0d0/6D1r/OKMRnqrRsZZKjc+Z021LPl8Hwm3vC0oZjLveCqLQB6FdNjgPnj1YLzAJwycGtsaUDzWkoxvCDBuZv5yt/BQYOIV9Xs9TBKzW3" +
            "KsHnWpF1QJ3FoTmTqJwiayYsGU9B2uBhpf2pb8VWmESGVQ9GQumziZx+LE8L/rrSl4rLmvexsOlfKj5PxGB3IVQUqCDBoyPAU81ltfg372KpfHgb" +
            "DUeeqpXSIy3Ax6yUyobgDAGgCAxtOhXC0kSStZ4xzd+0yh0j5gh11eP50R4J93dvfm4TLU49Et3D9G1SJecWmlGeQ/bTpqldB2/iBnQFkC5518uW" +
            "DsCqDz8FuMlzuwUoU/hGj5CUZQkZOQ+WzQpneNcHnt9B8ki76p8AkDjdINli2DWRApVSv3OZQF9dbTN/cEcK84jj17Ug4DVzLZOIQpshOPhrAqvE" +
            "AbLpCk/C0gihFFycLE87vAEKn7B46xhspRerfEkh6nAIwBHgQtoBKCdnoluu+Qc0j/+7A/Jxkki6Ty213+JsAka+O1QNFIe6cy9BrUbsK9P6Ho5V" +
            "CfH2NqW6eJLTFKJl2b6juqOo7fH5wxx3hElIM5MW7uWWKfHTglUa30ekRbo9viFbL56t7XaHlEEnNvntCrAfbHAOR1ynG/nuSmjcAyAMWBeoinN5" +
            "BrBsvWaXtXLympRNZ0agzr/Nm+f+QAvqURCPTu36U3GYBLkpZJZ+dlJ5AkuhawLOz0hETwEFKJ/O158t+t4ZvoQsEv9jcYu4GdMXTtMxE9NaSY1e" +
            "TxfCRoqaZ/wVc8fPNyFgbOEpHxnyjhX7hCscgJ2DkuM/W9wxh7hpRjGjRXb/HYD/3jEq4qWeCG+n2ek9IiW4TPV5zpSG1i0EddrabPEIcfjHrdOg" +
            "B1sCKGMVEiYyAfN+HfWoOMc3qYUI13aoBq3qG7LtYn5pZFvANIFis3M2F/PsuqRYCkTajZC93kNSPrkt4nAJk0g22CQRuA0rdr2f8dJx+dLevv8q" +
            "yZJM9Kt3koe5mGOeouG9AGSN00ofUt3ehJAC3iaoUV9GqnGPYDWpzkXyJm6ORlI5KBx+HVSX6cu6Cc8H5vkbDsqbydU/sMvdt8kbB4Az2PPA5VIE" +
            "xRxFX+VYlNU/JVVOThIf+uWZGTyKIA5uwF1uPnLS9eFpeoCd9uqX6UuV2YuBvwuA5QuOX/Li0LriVirivNWikJREQqiIZFGVeoyXFGBSowFUGAOg" +
            "z8oM9SD9pB+Ag8DQdzIOdUHKaDjpBxYg4RQcEoOBEH/UH9kH7kH4UH0UGvUGrEGmEGgkGdEGZkGV0GSUGO0GKkGHkGF0GEUGDUGC0GCUGBkGBUGA" +
            "0GAkG/oN/Qb4g3tBvCDd0G4oNxQbSg2lBr6DX0GroNWQaag0xBoSDQUGdoM6QZogzNBliDLEGSoMlQY8gx5BiyDFUGIIMPQYYgwxBhKDCUGDoMHQ" +
            "YMgwZBgqDBUGCIMDQYGgwNBgSDAkGBIMBQYAgwBBgCDAEG/Qb5BvkG+Qb1BvEG8QbxBuUG5QblBuUG1QbVBtUG1QbBBsEGwQbBBrEGsQaxBq0GnQ" +
            "adBp0GmQaJBoUGhQaFBnkGeQZ5BnUGbQZpBmkGaQZdBl0GWQZZBk0GTQZNBk0GQQZBBj0GPQYxBi0GLQYtBiUGIQYhBiEGHQYdBhkGGQYVBhUGFQ" +
            "YVBhEGEQYRBhEGEQYRBg0GDQYNBg0GDQYNBg0GCQYJBgkGCQYJBgkGCQYJBgkGCQYFBgUGBQYFBgUGBQYFBgUGBQYBBgEGAQYBBgEGAQYBBgEGAQ" +
            "bgsWG2LFRm2xUZeEVGxOLGs+LGUGLGGOLGFOLGEiLGDSKGCaKGBiKGAqKGn0UNEooZCxQxjihiVFDDyKGEMUMGYoYKRQwNihv2qBvWqBuiqBnuqB" +
            "m6qBl8qBlEqBhmqBtnUDYuoGtNQNQKgaKVAyHqBjLUDEyoGFdQMJqgYOQYChgAAAAAAAAAAA";
    }
}
