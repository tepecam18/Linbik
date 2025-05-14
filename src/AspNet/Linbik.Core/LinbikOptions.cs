namespace Linbik.Core;

public class LinbikOptions
{
    public string version { get; set; } = LinbikVersion.dev2025;
    public string[] appIds { get; set; }
    /// <summary>
    /// If true, all apps are allowed to use the token. use for testing only.
    /// </summary>
    public bool allowAllApp { get; set; } = false;

    public string publicKey
    {
        get
        {
            switch (version)
            {
                case LinbikVersion.dev2025:
                    return "MIICITANBgkqhkiG9w0BAQEFAAOCAg4AMIICCQKCAgBp6ADswC7ZoDYMOyoFRC/cGTx4naAQ+gih2PQTVHVrC3YXRUzxYJwVkdCfuWQkZ3P/FTmpITWENa8OryAJUGAwuFqwgbhH0T0m+lUBLBa49/bJ2KVxjooN3FTQCp1fHS+wmpjIw/yCqDtSih3ylcdLcuhRJCKyj8aSYuY1Y9y1oKmhJvDNc6TrfS/i02Q5oFWu8PF96tQV4MAqKzdKSaytg19AEqv3SV+sPYLoacj3oCnAb7J05W7poBmdFuTXcmbfVc+MZYFoSiyzWk35PyqbPGLKYsq0RYzfoGeLFWVsZ8QOS0tf/P7z1J15rb58PMIyRrcbTqiNJ3MR3EGvCW3dgli0yS8mI2ynD9OKi5xgu6txlAZIKENegmwji9e87uj7SnNxGD7JPSqAW0VaLq4rDKZwnu7WMa6uL/Y83hVRAew19ouTpH50lW7XRaGJu6H8siKRfSa+di/erzev6ppmnpmFBeCzaaSBMLS8gpN/l4rQoI49lbSjzYiipjSusynxVlPvoabx1iH3Ngep7cYR994W7/MhHvbcaziMN4NoeiLbfmiFVDKqEEmRZkId18fyLSkmONBRdwkoovrEr0bNSOjobLrSiXSVZn7eeSZUH7aDRkD/oUVKH0KiSkxnFZr6CiaJep9hykCJyLLzQIzKaCFNcfWTxfguD9lMDEBniQIDAQAB";
                case LinbikVersion.prod2025:
                    return "MIICIjANBgkqhkiG9w0BAQEFAAOCAg8AMIICCgKCAgEAq+tbwd6rTFOHWKBsFVBUIuleAwdazrmpMLd+WYjWmWns/Z0oIwlDO+9Y5fcm+HAYBFVqm+8UUxKKJvUZLuVZvO2oDKxx0pdhaLkQQ4gYuOg06DR8YaAysyp/KlkHGrcn4qV7kmkgXXxT8QSB+6MAjw+idep8K0e7y1NMdgzRX77d0CEAPPXaLQx2CTENhjDRG4IBKtN9fmG3M2c9pm44pC4oNF3fBaq0bclJw8kjfDakiUSdtKHH+1eUDI98a2yykxRwGXsPjLkexrYiXKDNqSGFQ0tJakv7PwtxElCOz9vICR9F8KBbO2S/AUXqxsgp73dywztz+s91Rh+Sn3d+VLLxue2+x8Nnr53hs4DLrZlfgEkAl8iRMWYr18C2ZGbCpXmZfRk0n5Y1RbI4FCUz/GgHbySZ1QcYzBi2JN+/YNv/FwNnMjLwDiUPp/tLgty9L+8AxEIjXS426vhoiPWnbmNPTifKH8Jrg3HBN5VUC23aZhZbfn2XXPB9+B6R+vpJsUP0s9CRN5YcmLqAE3RS+jfsp5wR7Bw2QHegigYyoAW+0ssaGJnLbwAicFXMBUAr36e2ZmXp2piWer15y/k0Kd8tleTVDxQx+ekYXcFor42w2Prh3EUAHEGk3T5DtJy5GwLuqSviSwBVtDX7NMz5FR1Ah9sv5tT/oW9hI8TEqHECAwEAAQ==";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}

public static class LinbikVersion
{
    public const string dev2025 = "dev2025";
    public const string prod2025 = "prod2025";
}
