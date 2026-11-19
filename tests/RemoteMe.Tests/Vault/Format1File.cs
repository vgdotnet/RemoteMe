namespace RemoteMe.Tests.Vault;

// A settings file written by RemoteMe 0.8.1, the last version that wrote format 1, with 1,000 iterations.
// It holds one connection, "Format 1" at format1.example.test, with the password "format 1 p@ss ✓".
internal static class Format1File {
    public const string Password = "format 1 master password";

    public static byte[] Bytes => Convert.FromBase64String("""
        UmVtb3RlTWUBAAEA6AMAADsgilUR9OCL2wcMAIF0oxPukzSHzQYJdw+KVSQpe2QaUb3dla+505WaxpppdI2nKc9V9pINFBr7
        YUqQ5vYTJaABAAAAAAAAAGscMgldMUXwf4ocoHyeHBPL9FcL+adSFRpTI66QrOnwNl9rsPmfqsPZnIWVSNvWwiexQ90Oh2nW
        RalCgjESdW48PqJTKaeS8ooJsFHenLmTLnQH2hTEmycSjSI56fESGjXnblYKV1fXY/qn68iYztdhfIcXgzyBg4xRhh75fT0R
        SbfwqThhyos0NwNVGjDLvofuTbMPEH68zi+vAvFmVI+yCMxlFyxSB9FIf880rOwTcz3tpxe0JGUgPcEi8Mn8cJ4OUz+EBHiz
        HPbw6H+8vpNrgt5g4fGggXLsUV7/q+PEDUKDW9EAFLsyDoO3E2h3GwSQlM1P94HYPFXl3/X05wX39I4rqwUxvIY9j063LZaD
        J+cwgS7i14DSwTSn0e5MX6XpNnkANDEqQQIcZ54ZuUkWJCjVi83ct20Jvkx7ToJIp8lNQ1NyP65y/iB8dP85bdc7jNGswPIM
        /lqABQPfxHRkD1PZ2Zd0FLHgKg==
        """);
}
