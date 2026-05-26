using OtpNet;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: dotnet run -- \"<base32 secret with optional spaces>\"");
    Environment.Exit(2);
}

var raw = string.Concat(args[0].Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();
var bytes = Base32Encoding.ToBytes(raw);
var totp = new Totp(bytes);
Console.WriteLine(totp.ComputeTotp());
