Console.OutputEncoding = Encoding.UTF8;
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

try
{
    return CliApp.Run(args);
}
catch (VtbfParseException ex)
{
    Console.Error.WriteLine($"Parse error: {ex.Message}");
    return 2;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}