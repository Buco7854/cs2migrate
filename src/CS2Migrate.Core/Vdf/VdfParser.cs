using System.Text;

namespace CS2Migrate.Core.Vdf;

public static class VdfParser
{
    public static VdfObject Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var tokenizer = new Tokenizer(content);
        return ParseObject(tokenizer, isRoot: true);
    }

    private static VdfObject ParseObject(Tokenizer tokenizer, bool isRoot)
    {
        var result = new VdfObject();

        while (tokenizer.TryRead(out var key))
        {
            if (key == "}")
            {
                if (isRoot)
                {
                    throw new FormatException("Unexpected closing brace in VDF document.");
                }

                return result;
            }

            if (key == "{")
            {
                throw new FormatException("Unexpected opening brace in VDF document.");
            }

            if (!tokenizer.TryRead(out var value))
            {
                throw new FormatException($"Missing value for VDF key '{key}'.");
            }

            if (value == "{")
            {
                result.Add(key, VdfValue.FromObject(ParseObject(tokenizer, isRoot: false)));
            }
            else if (value == "}")
            {
                throw new FormatException($"Missing value for VDF key '{key}'.");
            }
            else
            {
                result.Add(key, VdfValue.FromScalar(value));
            }
        }

        if (!isRoot)
        {
            throw new FormatException("Unclosed object in VDF document.");
        }

        return result;
    }

    private sealed class Tokenizer(string content)
    {
        private int _position;

        public bool TryRead(out string token)
        {
            SkipTrivia();
            if (_position >= content.Length)
            {
                token = string.Empty;
                return false;
            }

            var current = content[_position];
            if (current is '{' or '}')
            {
                _position++;
                token = current.ToString();
                return true;
            }

            token = current == '"' ? ReadQuoted() : ReadBare();
            return true;
        }

        private void SkipTrivia()
        {
            while (_position < content.Length)
            {
                if (char.IsWhiteSpace(content[_position]))
                {
                    _position++;
                    continue;
                }

                if (content[_position] == '/' && _position + 1 < content.Length && content[_position + 1] == '/')
                {
                    _position += 2;
                    while (_position < content.Length && content[_position] is not '\r' and not '\n')
                    {
                        _position++;
                    }

                    continue;
                }

                break;
            }
        }

        private string ReadQuoted()
        {
            _position++;
            var value = new StringBuilder();
            while (_position < content.Length)
            {
                var current = content[_position++];
                if (current == '"')
                {
                    return value.ToString();
                }

                if (current == '\\' && _position < content.Length)
                {
                    var escaped = content[_position];
                    if (escaped is '"' or '\\')
                    {
                        value.Append(escaped);
                        _position++;
                        continue;
                    }
                }

                value.Append(current);
            }

            throw new FormatException("Unterminated quoted string in VDF document.");
        }

        private string ReadBare()
        {
            var start = _position;
            while (_position < content.Length &&
                   !char.IsWhiteSpace(content[_position]) &&
                   content[_position] is not '{' and not '}')
            {
                _position++;
            }

            return content[start.._position];
        }
    }
}
