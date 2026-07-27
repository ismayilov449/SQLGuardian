namespace SQLGuardian.Ssms;

public sealed class LaunchOptions
{
    public List<string> Paths { get; } = [];

    public string? ConfigPath { get; set; }

    public string? ConnectionString { get; set; }

    public bool Quiet { get; set; }

    public static LaunchOptions Parse(string[] args)
    {
        var options = new LaunchOptions();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg is "-c" or "--config")
            {
                if (i + 1 < args.Length)
                {
                    options.ConfigPath = args[++i];
                }

                continue;
            }

            if (arg is "--connection")
            {
                if (i + 1 < args.Length)
                {
                    options.ConnectionString = args[++i];
                }

                continue;
            }

            if (arg is "-q" or "--quiet")
            {
                options.Quiet = true;
                continue;
            }

            if (arg is "-f" or "--file" or "--folder" or "--path")
            {
                if (i + 1 < args.Length)
                {
                    options.Paths.Add(args[++i]);
                }

                continue;
            }

            if (arg.StartsWith('-'))
            {
                continue;
            }

            // SSMS External Tools often pass the raw path as the first argument.
            options.Paths.Add(arg);
        }

        return options;
    }
}
