using ALLINONE;

var cases = new (string Input, bool Expected)[]
{
    ("how to hurt someone", true),
    ("self harm", true),
    ("suicide", true),
    ("how do I build a game", false),
    ("calculate 2 + 2", false),
    ("search for today's weather", false),
};

var failed = 0;

foreach (var test in cases)
{
    var actual = SafeModePolicy.IsBlocked(test.Input);
    if (actual != test.Expected)
    {
        Console.Error.WriteLine($"FAIL: '{test.Input}' expected {test.Expected}, got {actual}");
        failed++;
    }
}

Console.WriteLine($"Checked {cases.Length} Safe Mode cases.");

if (failed > 0)
{
    Console.Error.WriteLine($"{failed} test(s) failed.");
    return 1;
}

return 0;