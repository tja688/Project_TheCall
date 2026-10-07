using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TheCall
{
    internal abstract class JsonNode
    {
    }

    internal sealed class JsonObject : JsonNode
    {
        public readonly Dictionary<string, JsonNode> Map = new Dictionary<string, JsonNode>();
    }

    internal sealed class JsonArray : JsonNode
    {
        public readonly List<JsonNode> Items = new List<JsonNode>();
    }

    internal sealed class JsonString : JsonNode
    {
        public JsonString(string text) => Text = text;

        public string Text { get; }
    }

    internal sealed class JsonNumber : JsonNode
    {
        public JsonNumber(int value) => Value = value;

        public int Value { get; }
    }

    internal sealed class JsonBool : JsonNode
    {
        public JsonBool(bool value) => Value = value;

        public bool Value { get; }
    }

    internal sealed class JsonNull : JsonNode
    {
    }

    internal static class BookJson
    {
        public static JsonNode Read(string text)
        {
            if (text == null)
                throw new ContentBookException("内容是空的。");

            var parser = new Parser(text);
            var value = parser.ParseValue();
            parser.Skip();
            if (!parser.End)
                throw new ContentBookException("JSON 在结束后还有字符，位置 " + parser.Index);

            return value;
        }

        sealed class Parser
        {
            readonly string _text;

            public Parser(string text) => _text = text;

            public int Index { get; private set; }

            public bool End => Index >= _text.Length;

            public void Skip()
            {
                while (!End)
                {
                    var ch = _text[Index];
                    if (ch != ' ' && ch != '\n' && ch != '\r' && ch != '\t')
                        return;

                    Index++;
                }
            }

            public JsonNode ParseValue()
            {
                Skip();
                if (End)
                    throw Fail("缺少值");

                var ch = _text[Index];
                if (ch == '{')
                    return ParseObject();
                if (ch == '[')
                    return ParseArray();
                if (ch == '"')
                    return new JsonString(ParseString());
                if (ch == 't' || ch == 'f')
                    return ParseBool();
                if (ch == 'n')
                    return ParseNull();
                if (ch == '-' || (ch >= '0' && ch <= '9'))
                    return new JsonNumber(ParseInt());

                throw Fail("不能出现的字符");
            }

            JsonObject ParseObject()
            {
                Expect('{');
                var obj = new JsonObject();
                Skip();
                if (Peek('}'))
                {
                    Index++;
                    return obj;
                }

                while (true)
                {
                    Skip();
                    var key = ParseString();
                    if (obj.Map.ContainsKey(key))
                        throw Fail("重复的字段 " + key);

                    Skip();
                    Expect(':');
                    obj.Map.Add(key, ParseValue());
                    Skip();
                    if (Peek('}'))
                    {
                        Index++;
                        return obj;
                    }

                    Expect(',');
                    Skip();
                    if (Peek('}'))
                        throw Fail("对象不能以逗号结尾");
                }
            }

            JsonArray ParseArray()
            {
                Expect('[');
                var array = new JsonArray();
                Skip();
                if (Peek(']'))
                {
                    Index++;
                    return array;
                }

                while (true)
                {
                    array.Items.Add(ParseValue());
                    Skip();
                    if (Peek(']'))
                    {
                        Index++;
                        return array;
                    }

                    Expect(',');
                    Skip();
                    if (Peek(']'))
                        throw Fail("数组不能以逗号结尾");
                }
            }

            string ParseString()
            {
                Expect('"');
                var buffer = new StringBuilder();
                while (!End)
                {
                    var ch = _text[Index++];
                    if (ch == '"')
                        return buffer.ToString();
                    if (ch == '\\')
                    {
                        if (End)
                            throw Fail("字符串没有结束");

                        var escaped = _text[Index++];
                        if (escaped == '"' || escaped == '\\' || escaped == '/')
                            buffer.Append(escaped);
                        else if (escaped == 'b')
                            buffer.Append('\b');
                        else if (escaped == 'f')
                            buffer.Append('\f');
                        else if (escaped == 'n')
                            buffer.Append('\n');
                        else if (escaped == 'r')
                            buffer.Append('\r');
                        else if (escaped == 't')
                            buffer.Append('\t');
                        else if (escaped == 'u')
                            buffer.Append(ParseHex());
                        else
                            throw Fail("不能转义的字符");
                    }
                    else if (ch < ' ')
                    {
                        throw Fail("字符串里有控制字符");
                    }
                    else
                    {
                        buffer.Append(ch);
                    }
                }

                throw Fail("字符串没有结束");
            }

            char ParseHex()
            {
                if (Index + 4 > _text.Length)
                    throw Fail("\\u 不完整");

                var hex = _text.Substring(Index, 4);
                Index += 4;
                int code;
                if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                    throw Fail("\\u 不是十六进制");

                return (char)code;
            }

            int ParseInt()
            {
                var start = Index;
                if (Peek('-'))
                    Index++;

                if (End || _text[Index] < '0' || _text[Index] > '9')
                    throw Fail("缺少数字");

                if (_text[Index] == '0')
                {
                    Index++;
                    if (!End && _text[Index] >= '0' && _text[Index] <= '9')
                        throw Fail("数字不能有前导零");
                }
                else
                {
                    while (!End && _text[Index] >= '0' && _text[Index] <= '9')
                        Index++;
                }

                if (!End && (_text[Index] == '.' || _text[Index] == 'e' || _text[Index] == 'E'))
                    throw Fail("只接受整数");

                int value;
                if (!int.TryParse(_text.Substring(start, Index - start), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                    throw Fail("整数超出范围");

                return value;
            }

            JsonBool ParseBool()
            {
                if (Match("true"))
                    return new JsonBool(true);
                if (Match("false"))
                    return new JsonBool(false);

                throw Fail("布尔值不完整");
            }

            JsonNull ParseNull()
            {
                if (!Match("null"))
                    throw Fail("null 不完整");

                return new JsonNull();
            }

            bool Match(string word)
            {
                if (Index + word.Length > _text.Length)
                    return false;

                for (var i = 0; i < word.Length; i++)
                {
                    if (_text[Index + i] != word[i])
                        return false;
                }

                Index += word.Length;
                return true;
            }

            void Expect(char ch)
            {
                Skip();
                if (End || _text[Index] != ch)
                    throw Fail("期望 " + ch);

                Index++;
            }

            bool Peek(char ch) => !End && _text[Index] == ch;

            ContentBookException Fail(string message) =>
                new ContentBookException(message + "，位置 " + Index);
        }
    }
}
