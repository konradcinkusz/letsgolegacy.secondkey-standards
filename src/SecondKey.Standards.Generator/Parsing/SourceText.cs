namespace SecondKey.Standards.Generator.Parsing;

internal static class SourceText
{
    /// <summary>
    /// Strips a UTF-8 byte-order mark and normalises line endings to LF. Git checks the files out
    /// with LF (.gitattributes), but an editor can still save CRLF, and the rules must mean the
    /// same thing either way.
    /// </summary>
    public static string Normalise(string text)
    {
        if (text.Length > 0 && text[0] == '﻿')
        {
            text = text[1..];
        }

        return text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }
}
