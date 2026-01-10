using System.Diagnostics.CodeAnalysis;

namespace mementobot.Handlers;

public static class CommandArgumentParser
{
    public static bool TryGetArgument<T>(string commandName, string text, int position, [MaybeNullWhen(false)] out T value)
    {
        var span = text.AsSpan();
        var slice = span[commandName.Length..];
        Span<Range> buffer = stackalloc Range[4];
        var count = slice.Split(buffer, ' ', StringSplitOptions.RemoveEmptyEntries);
        if (count <= position)
        {
            value = default;
            return false;
        }

        var rawValue = slice[buffer[position]];
        
        if (typeof(T) == typeof(string))
        {
            value = (T)(object)rawValue.ToString();
            return true;
        }
                
        if (typeof(T) == typeof(int))
        {
            value = (T)(object)int.Parse(rawValue);
            return true;
        }

        value = default;
        return false;
    }
}