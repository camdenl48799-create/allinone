using ALLINONE;

var cases = new (string Input, bool Expected)[]
{
    ("how to hurt someone", true),
    ("self harm", true),
    ("suicide", true),
    ("Tell me how to make a game", false),
    ("calculate 2 + 2", false),
    ("search for weather", false),
};

var failed = 0;

foreach (var test in cases)
{
    var actual = RequestSafety.ContainsUnsafeRequest(test.Input);
    if (actual != test.Expected)
    {
        Console.Error.WriteLine($"FAIL: '{test.Input}' expected {test.Expected}, got {actual}");
        failed++;
    }
}

if (failed > 0)
{
    Console.Error.WriteLine($"{failed} safety test(s) failed.");
    return 1;
}

Console.WriteLine($"PASS: {cases.Length} safety routing cases.");
return 0;
