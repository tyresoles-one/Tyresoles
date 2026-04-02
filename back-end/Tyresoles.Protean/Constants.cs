namespace Tyresoles.Protean;

/// <summary>
/// Hardcoded Protean GSP configuration (E-Invoice / E-Waybill).
/// Mirrors Tyresoles.Live backend Tyresoles.One.Data.Services.Protean.Constants.
/// </summary>
public static class Constants
{
    private static ProteanEnvironment _environment = GetInitialEnvironment();

    /// <summary>Use Sandbox for test endpoints; Production for live NIC IRP. Can be set via env PROTEAN_SANDBOX=true or Protean__Sandbox=true.</summary>
    public static ProteanEnvironment Environment { get => _environment; set => _environment = value; }

    private static ProteanEnvironment GetInitialEnvironment()
    {
        var v = System.Environment.GetEnvironmentVariable("PROTEAN_SANDBOX") ?? System.Environment.GetEnvironmentVariable("Protean__Sandbox");
        if (string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) || v == "1") return ProteanEnvironment.Sandbox;
        return ProteanEnvironment.Production;
    }

    public static string AspId { get; } = "29AAACT2744B000356";
    public static string AuthTokenFormat { get; } = "v2.0:<asp_id>:<client_id>:<txn_id>:<timestamp>:<gstin>:<api_action>";
    public static string DefaultGstin { get; } = "29AAACT2744B1ZM";
    public static string DefaultEInvUsername { get; } = "API_tyresolesindia";
    public static string DefaultEInvPassword { get; } = "Tyres@123";

    public static bool Sandbox => Environment == ProteanEnvironment.Sandbox;

    public static string PrivateKeyXml =>
        Environment == ProteanEnvironment.Sandbox
            ? @"<RSAKeyValue><Modulus>wKDcAoC86zLp9BnhGTim+fWwhonI+obg4vQgK5GP2Go69WJbRhZn8d5160omUMSzgKx2S6p2/SIh/fGbdvEOrUaRdjrlmSlloVKK5sjaW9JVzsRzcZc7JWJXlu27ZEZEeWMFUw3C1JG6gerZJogXV98e1o7aWri6Ysc2rUj59OG1HmcFxLA9o/4Pzb1acL9q4TXljRYmnLdgDpN265ZCL/9pOeJMoZF5R3gmAW5zQP5nr7DHjcsNgynAySBCzn43cfeWJuqSMyEPypVXlq4Pk52O+Erzt4aT57E8xi9nr4rWCOUeCCI0JoVJIuT+yTuW496rCuznoKfXdXpoE8519Q==</Modulus><Exponent>AQAB</Exponent><P>3/6vid4PrBiKqpyK0KFfNmwuDpP9TqnmGOx0Lxecz8TfEp8nanZxRaYSxnPzrBfjmmDh4CDJO6yUDR5F6n7+obxWWHB9S4nx6+aBUcNW9kj1IRN0ce/xgVF9XVaA0BvX/GCAwI2w06iRIGans96y5jM4RbcHYPqqHDNTjju3j2E=</P><Q>3CbYaBazY+6XWQ8O7UTiMZdYsQ1EWCB5tC3Zyx0zqI1vuChlH8dhYPWsYFJyYhxmlz76ayDYRlKw5WOV75ZIQ2ea3WBfVHE4vTijaT9VcskrgBaBE1P5s/zoFI0j9m1d6QYiH+ZgHDScRGCabPLzIES1oDjaesB4FovrquVLkxU=</Q><DP>OTDQ5vWQsNMPTKJtevlI0x9p3wUADv6oJkLxIzj3K8FLZ3jFWpRlNXBCqPLgC1eKIFeONGiKmLbmkjbhiQ7luqvYq/oEs63D3ARdjl++q2FTXR+XgqPN2MPSq2sztm71hZWqKf/DXzdR3qD3PqCkssvvGTWzJ5q4pjygn9c1deE=</DP><DQ>CQvblqNgKboTJAi3X06WUN1cOqJF7a0f8yRzL6J6jfGJ5rJaPW0O9lICDE82/gsWuB5RYfeOi404UaS+84JkY8itF22vXSPt5fCe3mNI2pejnvbNxQKNXzUEQFvwX17tHfcvjcoEpwQOa8zga5usG+ZW5EeRyrt/88U6IOKdMWU=</DQ><InverseQ>N4BMbj8b91UiqYtsebbyqin2XulVf9ruotaCvCYx10edb/to0zSYyAsjZRbuZQ7F/615nm6ZyAihO/64yn9H+gVvGsMxw71Sumi63vB27XMEC22TBMkIFLl2O7e0v+afcA3bl/taLBLxpNsP6iww80mI7xEYqsdoU4+elFxQNsU=</InverseQ><D>AIdghemRuzPmVFskKk7PbIpqC+XXGqhMopWI0hMh+eDex7uCTyNdcS/1vb80HY7QrwF/iKPPlFB9QeZ20WEw3l++I1I0PP2nWlrw59h00FbikUD/pSKaZmGfpm8sJMa+8OCz9sn9TIVC5buTPDRtm0+Q6jC1tOqNaRstZqqM3x559JWeMbkFDJ7GXMChcSLj7J+1YwaavDqCfC+wUOB4S6bCD0PLgb0YHyG7TPpFVr8sPSe4Q02Y5QmM2cFnEofDmX8R0Xf+f9H/bv3jPCfIq9HVOvayk9grxBAjHt3tD9R2CFKrZcY/XFPaBri1fcoYEv0epBKdcNRIrbm6CQTEYQ==</D></RSAKeyValue>"
            : @"<RSAKeyValue><Modulus>rV55wC4DSfB21yOscN/I5vKAfr9aRWDng4mjhE3aREgHm3tS52eDc2el/lpDz6P3ozliEkIPmVpqUnMTg1PMTJdzn6yh6f6jq2J1m1qCqZcIVHWHzgcrOkTWyaNLeKGwd0n4zcaITw9Rj0DioGQnKnPpBPmiB270mKFksjvWt5y1Obp0tMCrOBnB7JwzikffiOQPdtqJZ/lztBOgOLTUUZ6AqEUisZdQUKSIpFN7hcoFJwa9Buai8ys5C9oULjuBxtceLYSm7UAtqvb/3Xkcv2LW8RL5+MkyuckkLU01emLzCz9Fkq4eD8TV54iAv+M/cSXz5KVcg1RuN4v7MYzpiQ==</Modulus><Exponent>AQAB</Exponent><P>9LYjxBdTbFyuPcSHozaJG7cZbzPlvgIkNPenibRpCDVAizRwcdFVNj7QD2txgjMstuLZhrTE/pBqiSoLmeRuRy6JugA0WM3FxflBQVcqV5f6zCLQ+JmNuLofoavrLNd24RnfOwA/X4ixJuTP3vjZXGWwWY2qTROgpqppv70/EdM=</P><Q>tV3VuB3u3x+qND3FnMyg+J+mNvGB1PCV2X1q+AoEeDZDoPEaeLYKpPh6XCx1DYDlubR59Ie4JGISmdJQOPueX+9lcWmmHpQOwh5n07ZXh1/5GO3c+5Ujz9FvB5NkQTCBk9ToXnc5rcEazZaIvDf4/yioB7yt1T1UAW4kKxpz4bM=</Q><DP>bWDgGazHZ/8BDrDCY6xjmlS79J2Aye4RGu+7HezaqYWL5Oy6edU0PjvTKaE/qg+Po+s/iVtRVetdAxQc/VtCA2UEwHOI8kx/Yt7nUPpR/bb7rbS+LPANjYz7yHqccn2YMEMtKSUSLnRdomHRm/skxIzbRxvwC+WDABvZio9fGms=</DP><DQ>K/OjeUz3uGaqDqLxosAMOnQmCaaZOgtbpSs5owZ6cs9jsDr9mUc0aSo2LOQxMaaNBWXxMAs72sVRkKlg+44uoT0YrlVWBo8GzJAlxS6pI4tkiGAMb4OC/Dfa2lEjuJCStkqgTVRDTBJB9pSGamZ+Fwe5QLa+wbBtDTOt3Cw67PU=</DQ><InverseQ>UsteMZXDCBdQ/XjtVJilo5JQGZnz7FNDbEWh2iRou4rtzB0x2IFA6/j5kv9PkiI+JRx7QAYAmf3hnT6RFTGCp/EXaAI72sD0g5QOloDE13FHqy7zHKFzjrRLC5+d7BeIy/G/X7iQOS+XM8GO0gAJuKfTpk0b1QIr9bX4IMhBHVU=</InverseQ><D>KaFLCQvDfGjCkKyfRsdl3ZcTpdvbGFfJn/tYCXgMVmMZeItnf+uKVBbqdCZGrECKQWaRFyNHzaI5b6z0XY+suiOP/yKcCq07rfbJmNFmGMUMmgNS5fUwuyA2l1qfz8q7rhlUJk1rA9kA065X/P2vrfEBXlF3R+Or1k7C+E2IJLSVO66XvT2iTVqBDxmAPsy3s0WtCILbjZH4hOWG5QPlsJZX/sAqpnCErN7HpOkuw0Aa61q3DJzCwOcMoxHFKrnzhaWU1Mkz+32Q18tY/DE5Y39Pk3uOpVWDZvBh/m33C5Fo0Psnl6zpZzB5KNpjfDQpFJgagPTUaa43y89kK7875Q==</D></RSAKeyValue>";

    /// <summary>NIC portal to verify signed invoice JSON (upload JSON, export PDF). Mirrors Live <c>Services.Protean.Constants.URL_Verify_Signed_Inv</c>.</summary>
    public static string UrlVerifySignedInvoice { get; } = "https://einvoice1.gst.gov.in/Others/VSignedInvoice";

    public static string PublicKeyNicIrp =>
        Environment == ProteanEnvironment.Sandbox
            ? "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEArxd93uLDs8HTPqcSPpxZrf0Dc29r3iPp0a8filjAyeX4RAH6lWm9qFt26CcE8ESYtmo1sVtswvs7VH4Bjg/FDlRpd+MnAlXuxChij8/vjyAwE71ucMrmZhxM8rOSfPML8fniZ8trr3I4R2o4xWh6no/xTUtZ02/yUEXbphw3DEuefzHEQnEF+quGji9pvGnPO6Krmnri9H4WPY0ysPQQQd82bUZCk9XdhSZcW/am8wBulYokITRMVHlbRXqu1pOFmQMO5oSpyZU3pXbsx+OxIOc4EDX0WMa9aH4+snt18WAXVGwF2B4fmBk7AtmkFzrTmbpmyVqA3KO2IjzMZPw0hQIDAQAB"
            : @"MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAjo1FvyiKcQ9hDR2+vH0+
O2XazuLbo2bPfRiiUnpaPhE3ly+Pwh05gvEuzo2UhUIDg98cX4E0vbfWOF1po2wW
TBxb8jMY1nAJ8fz1xyHc1Wa7KZ0CeTvAGeifkMux7c22pMu6pBGJN8f3q7MnIW/u
SJloJF6+x4DZcgvnDUlgZD3Pcoi3GJF1THbWQi5pDQ8U9hZsSJfpsuGKnz41QRsK
s7Dz7qmcKT2WwN3ULWikgCzywfuuREWb4TVE2p3e9WuoDNPUziLZFeUfMP0NqYsi
GVYHs1tVI25G42AwIVJoIxOWys8Zym9AMaIBV6EMVOtQUBbNIZufix/TwqTlxNPQ
VwIDAQAB";
}

public enum ProteanEnvironment
{
    Production,
    Sandbox
}
